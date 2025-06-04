using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Orleans;
using PsiOrleans.Grains;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// Manages callbacks for agent function calls and tracks pending calls
/// </summary>
public class AgentCallbackManager
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<AgentCallbackManager> _logger;
    
    // Thread-safe storage for pending calls
    private readonly ConcurrentDictionary<string, PendingAgentCall> _pendingCalls = new();
    
    // Event handlers for call completion
    private readonly ConcurrentDictionary<string, List<Func<PendingAgentCall, Task>>> _callbackHandlers = new();

    public AgentCallbackManager(
        IClusterClient clusterClient,
        ILogger<AgentCallbackManager> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    /// <summary>
    /// Register a pending agent call
    /// </summary>
    public void RegisterPendingCall(PendingAgentCall pendingCall)
    {
        _pendingCalls.TryAdd(pendingCall.CallId, pendingCall);
        _logger.LogDebug("Registered pending call {CallId} from {CallingAgent} to {TargetAgent}",
            pendingCall.CallId, pendingCall.CallingAgentId, pendingCall.TargetAgentId);
    }

    /// <summary>
    /// Get a pending call by ID
    /// </summary>
    public PendingAgentCall? GetPendingCall(string callId)
    {
        _pendingCalls.TryGetValue(callId, out var call);
        return call;
    }

    /// <summary>
    /// Get all pending calls for a specific calling agent
    /// </summary>
    public List<PendingAgentCall> GetPendingCallsForAgent(string agentId)
    {
        return _pendingCalls.Values
            .Where(call => call.CallingAgentId == agentId && call.Status == PendingCallStatus.Pending)
            .ToList();
    }

    /// <summary>
    /// Register a callback handler for when calls complete
    /// </summary>
    public void RegisterCallbackHandler(string agentId, Func<PendingAgentCall, Task> handler)
    {
        _callbackHandlers.AddOrUpdate(agentId,
            new List<Func<PendingAgentCall, Task>> { handler },
            (key, existing) =>
            {
                existing.Add(handler);
                return existing;
            });
        
        _logger.LogDebug("Registered callback handler for agent {AgentId}", agentId);
    }

    /// <summary>
    /// Remove a callback handler for an agent
    /// </summary>
    public void RemoveCallbackHandlers(string agentId)
    {
        _callbackHandlers.TryRemove(agentId, out _);
        _logger.LogDebug("Removed callback handlers for agent {AgentId}", agentId);
    }

    /// <summary>
    /// Notify that an agent call has completed
    /// </summary>
    public async Task NotifyCallCompletedAsync(PendingAgentCall completedCall)
    {
        try
        {
            _logger.LogInformation("Processing callback for completed call {CallId} with status {Status}",
                completedCall.CallId, completedCall.Status);

            // Update the call in our registry
            _pendingCalls.TryUpdate(completedCall.CallId, completedCall, _pendingCalls[completedCall.CallId]);

            // Notify the calling agent about the completion
            await NotifyCallingAgentAsync(completedCall);

            // Execute any registered callback handlers
            if (_callbackHandlers.TryGetValue(completedCall.CallingAgentId, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        await handler(completedCall);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing callback handler for agent {AgentId}",
                            completedCall.CallingAgentId);
                    }
                }
            }

            // Clean up completed/failed calls after a delay
            _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ => CleanupCall(completedCall.CallId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing callback for call {CallId}", completedCall.CallId);
        }
    }

    /// <summary>
    /// Notify the calling agent that an agent call has completed
    /// </summary>
    private async Task NotifyCallingAgentAsync(PendingAgentCall completedCall)
    {
        try
        {
            var callingAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>(completedCall.CallingAgentId);
            
            // Check if the calling agent is still available
            var isInitialized = await callingAgent.IsInitializedAsync();
            if (!isInitialized)
            {
                _logger.LogWarning("Calling agent {AgentId} is no longer initialized, cannot deliver callback",
                    completedCall.CallingAgentId);
                return;
            }

            // Prepare the callback message
            var callbackMessage = FormatCallbackMessage(completedCall);
            
            // Deliver the callback as a continuation message
            await callingAgent.ReceiveCallbackAsync(completedCall.CallId, callbackMessage, completedCall.Status == PendingCallStatus.Completed);
            
            _logger.LogInformation("Successfully delivered callback to agent {AgentId} for call {CallId}",
                completedCall.CallingAgentId, completedCall.CallId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify calling agent {AgentId} about completed call {CallId}",
                completedCall.CallingAgentId, completedCall.CallId);
        }
    }

    /// <summary>
    /// Format the callback message for the calling agent
    /// </summary>
    private static string FormatCallbackMessage(PendingAgentCall completedCall)
    {
        var status = completedCall.Status == PendingCallStatus.Completed ? "completed" : "failed";
        var duration = completedCall.CompletedAt?.Subtract(completedCall.CreatedAt).TotalSeconds ?? 0;
        
        var message = $"Agent call {completedCall.CallId} to {completedCall.TargetAgentId} has {status} " +
                     $"(duration: {duration:F1}s).";

        if (completedCall.Status == PendingCallStatus.Completed && !string.IsNullOrEmpty(completedCall.Result))
        {
            message += $"\n\nResult from {completedCall.TargetAgentId}:\n{completedCall.Result}";
        }
        else if (completedCall.Status == PendingCallStatus.Failed && !string.IsNullOrEmpty(completedCall.ErrorMessage))
        {
            message += $"\n\nError: {completedCall.ErrorMessage}";
        }

        return message;
    }

    /// <summary>
    /// Clean up a completed call from memory
    /// </summary>
    private void CleanupCall(string callId)
    {
        if (_pendingCalls.TryRemove(callId, out var removedCall))
        {
            _logger.LogDebug("Cleaned up completed call {CallId}", callId);
        }
    }

    /// <summary>
    /// Get statistics about pending calls
    /// </summary>
    public (int Total, int Pending, int InProgress, int Completed, int Failed) GetStatistics()
    {
        var calls = _pendingCalls.Values.ToList();
        
        return (
            Total: calls.Count,
            Pending: calls.Count(c => c.Status == PendingCallStatus.Pending),
            InProgress: calls.Count(c => c.Status == PendingCallStatus.InProgress),
            Completed: calls.Count(c => c.Status == PendingCallStatus.Completed),
            Failed: calls.Count(c => c.Status == PendingCallStatus.Failed)
        );
    }

    /// <summary>
    /// Get all pending calls (for debugging/monitoring)
    /// </summary>
    public List<PendingAgentCall> GetAllPendingCalls()
    {
        return _pendingCalls.Values.ToList();
    }

    /// <summary>
    /// Cleanup old calls that might have been orphaned
    /// </summary>
    public void CleanupOldCalls(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var oldCalls = _pendingCalls.Values
            .Where(call => call.CreatedAt < cutoff && 
                          (call.Status == PendingCallStatus.Completed || call.Status == PendingCallStatus.Failed))
            .ToList();

        foreach (var oldCall in oldCalls)
        {
            _pendingCalls.TryRemove(oldCall.CallId, out _);
        }

        if (oldCalls.Any())
        {
            _logger.LogInformation("Cleaned up {Count} old calls", oldCalls.Count);
        }
    }
} 
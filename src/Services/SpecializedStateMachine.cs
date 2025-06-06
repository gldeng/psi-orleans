using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Orleans;
using PsiOrleans.Grains;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// State machine implementation for Specialized agents that use sync direct execution pattern.
/// 
/// Execution Pattern:
/// - Executes tasks directly using available tools
/// - Awaits tool results synchronously (linear execution flow)
/// - Sends completion callbacks to parent agent when finished
/// - Does not create or manage child agents
/// </summary>
public class SpecializedStateMachine : IAgentStateMachine
{
    private readonly IConfigurableKernelService _kernelService;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SpecializedStateMachine> _logger;

    public SpecializedStateMachine(
        IConfigurableKernelService kernelService,
        IGrainFactory grainFactory,
        ILogger<SpecializedStateMachine> logger)
    {
        _kernelService = kernelService;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    /// <summary>
    /// Execute task using sync direct execution pattern.
    /// Specialized agents use their configured tools directly and send completion callbacks.
    /// </summary>
    public async Task<string> ExecuteTaskAsync(string task, Kernel kernel, ConfigurableAgentState state, AgentConfiguration config)
    {
        _logger.LogInformation("SpecializedStateMachine executing task for agent {AgentId}: {Task}", 
            state.AgentId, task);

        if (kernel == null)
        {
            throw new InvalidOperationException("Kernel is not configured for specialized execution");
        }

        try
        {
            // Sync Direct Execution Pattern:
            // Execute with specialized tools (normal blocking tools) and await results immediately
            var result = await ExecuteWithDirectTools(task, kernel, state, config);
            
            // Send completion callback to parent if exists
            if (!string.IsNullOrEmpty(state.ParentAgentId))
            {
                await SendCompletionCallback(state.ParentAgentId, result, true);
            }
            
            _logger.LogInformation("SpecializedStateMachine completed task for agent {AgentId}", state.AgentId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SpecializedStateMachine execution failed for agent {AgentId}", state.AgentId);
            
            // Send failure callback to parent if exists
            if (!string.IsNullOrEmpty(state.ParentAgentId))
            {
                var errorMessage = $"Specialized task failed: {ex.Message}";
                await SendCompletionCallback(state.ParentAgentId, errorMessage, false);
            }
            
            throw;
        }
    }

    /// <summary>
    /// Process callbacks from child agents.
    /// Specialized agents typically don't receive callbacks since they don't create children.
    /// </summary>
    public Task ProcessCallbackAsync(string callId, string message, bool isSuccess, ConfigurableAgentState state, Kernel kernel)
    {
        // Specialized agents don't typically receive callbacks from children
        // This method can be empty or log unexpected callbacks
        _logger.LogWarning("SpecializedStateMachine received unexpected callback {CallId} for agent {AgentId}: {Message}", 
            callId, state.AgentId, message);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Execute task with direct tool usage - the core sync execution pattern.
    /// Uses existing kernel service with waitForCompletion = false for linear execution.
    /// </summary>
    private async Task<string> ExecuteWithDirectTools(string task, Kernel kernel, ConfigurableAgentState state, AgentConfiguration config)
    {
        _logger.LogDebug("Executing task with direct tools for agent {AgentId}", state.AgentId);
        
        // Use existing kernel service execution with specialized agent configuration
        // waitForCompletion = false ensures linear, synchronous execution pattern
        var result = await _kernelService.ExecuteTaskAsync(kernel, task, state, config.SystemPrompt, waitForCompletion: false);
        
        return result;
    }

    /// <summary>
    /// Send completion callback to parent agent using Orleans grain communication.
    /// This maintains compatibility with existing callback infrastructure.
    /// </summary>
    private async Task<bool> SendCompletionCallback(string parentId, string result, bool isSuccess)
    {
        try
        {
            var parentAgent = _grainFactory.GetGrain<IConfigurableAgentGrain>(parentId);
            var callId = Guid.NewGuid().ToString();
            
            await parentAgent.ReceiveCallbackAsync(callId, result, isSuccess);
            
            _logger.LogInformation("Sent completion callback to parent {ParentId}: Success={Success}", 
                parentId, isSuccess);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send completion callback to parent {ParentId}", parentId);
            return false;
        }
    }
} 
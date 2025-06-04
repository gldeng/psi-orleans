using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Orleans;
using PsiOrleans.Grains;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// Manual function call processor based on Microsoft's FunctionCallsProcessor
/// Handles agent calls asynchronously (non-blocking) and regular tools synchronously
/// </summary>
public class ManualFunctionCallProcessor : IManualFunctionCallProcessor
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<ManualFunctionCallProcessor> _logger;
    private readonly AgentCallbackManager _callbackManager;

    public ManualFunctionCallProcessor(
        IClusterClient clusterClient,
        ILogger<ManualFunctionCallProcessor> logger,
        AgentCallbackManager callbackManager)
    {
        _clusterClient = clusterClient;
        _logger = logger;
        _callbackManager = callbackManager;
    }

    public async Task<ManualFunctionCallResult> ProcessFunctionCallsAsync(
        ChatMessageContent chatResult,
        Kernel kernel,
        string agentId,
        ChatHistory chatHistory,
        CancellationToken cancellationToken = default)
    {
        var result = new ManualFunctionCallResult();
        
        try
        {
            // Add the AI response to chat history first
            chatHistory.Add(chatResult);
            
            // Extract function calls from the chat result
            var functionCalls = FunctionCallContent.GetFunctionCalls(chatResult).ToArray();
            
            if (functionCalls.Length == 0)
            {
                _logger.LogDebug("No function calls found in chat result");
                return result;
            }

            _logger.LogInformation("Processing {Count} function calls for agent {AgentId}", 
                functionCalls.Length, agentId);

            // Process each function call
            foreach (var functionCall in functionCalls)
            {
                try
                {
                    await ProcessSingleFunctionCallAsync(
                        functionCall, 
                        kernel, 
                        agentId, 
                        chatHistory, 
                        result, 
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing function call {FunctionName}", 
                        functionCall.FunctionName);
                    
                    var errorMessage = $"Error processing function {functionCall.FunctionName}: {ex.Message}";
                    result.ErrorMessages.Add(errorMessage);
                    
                    // Add error to chat history
                    AddErrorToChatHistory(chatHistory, functionCall, errorMessage);
                }
            }

            result.ChatHistoryUpdated = true;
            _logger.LogInformation("Completed processing function calls for agent {AgentId}. " +
                "Pending agent calls: {PendingCount}, Errors: {ErrorCount}", 
                agentId, result.PendingAgentCalls.Count, result.ErrorMessages.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in ProcessFunctionCallsAsync for agent {AgentId}", agentId);
            result.Success = false;
            result.ErrorMessages.Add($"Critical processing error: {ex.Message}");
            return result;
        }
    }

    private async Task ProcessSingleFunctionCallAsync(
        FunctionCallContent functionCall,
        Kernel kernel,
        string agentId,
        ChatHistory chatHistory,
        ManualFunctionCallResult result,
        CancellationToken cancellationToken)
    {
        // Validate function call
        if (!TryValidateFunctionCall(functionCall, kernel, out var function, out var errorMessage))
        {
            result.ErrorMessages.Add(errorMessage!);
            AddErrorToChatHistory(chatHistory, functionCall, errorMessage!);
            return;
        }

        // Check if this is an agent communication function
        if (IsAgentFunction(functionCall.FunctionName, functionCall.PluginName))
        {
            await ProcessAgentFunctionCallAsync(functionCall, agentId, chatHistory, result, cancellationToken);
        }
        else
        {
            await ProcessRegularFunctionCallAsync(functionCall, function!, kernel, chatHistory, cancellationToken);
        }
    }

    private async Task ProcessAgentFunctionCallAsync(
        FunctionCallContent functionCall,
        string callingAgentId,
        ChatHistory chatHistory,
        ManualFunctionCallResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var targetAgentId = ExtractTargetAgentId(functionCall.FunctionName);
            
            // For call_agent function, extract the real target agent ID from arguments
            if (string.Equals(functionCall.FunctionName, "call_agent", StringComparison.OrdinalIgnoreCase))
            {
                targetAgentId = ExtractAgentIdFromArguments(functionCall.Arguments);
                if (string.IsNullOrEmpty(targetAgentId))
                {
                    var error = $"No agent ID found in arguments for call_agent function";
                    result.ErrorMessages.Add(error);
                    AddErrorToChatHistory(chatHistory, functionCall, error);
                    return;
                }
            }
            else if (string.IsNullOrEmpty(targetAgentId))
            {
                var error = $"Could not extract target agent ID from function {functionCall.FunctionName}";
                result.ErrorMessages.Add(error);
                AddErrorToChatHistory(chatHistory, functionCall, error);
                return;
            }

            // Extract query from arguments
            var query = ExtractQueryFromArguments(functionCall.Arguments);
            if (string.IsNullOrEmpty(query))
            {
                var error = $"No query found in arguments for agent call {functionCall.FunctionName}";
                result.ErrorMessages.Add(error);
                AddErrorToChatHistory(chatHistory, functionCall, error);
                return;
            }

            // Create pending call record
            var pendingCall = new PendingAgentCall
            {
                CallId = functionCall.Id ?? Guid.NewGuid().ToString(),
                FunctionName = functionCall.FunctionName,
                PluginName = functionCall.PluginName,
                Arguments = functionCall.Arguments?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new(),
                CallingAgentId = callingAgentId,
                TargetAgentId = targetAgentId,
                Query = query,
                Status = PendingCallStatus.Pending
            };

            // Register the pending call
            _callbackManager.RegisterPendingCall(pendingCall);
            result.PendingAgentCalls.Add(pendingCall);

            // Add immediate response to chat history indicating call was initiated
            var immediateResponse = $"Agent call to {targetAgentId} has been initiated. " +
                $"CallId: {pendingCall.CallId}. The agent will callback when complete.";
            
            AddFunctionResultToChatHistory(chatHistory, functionCall, immediateResponse);

            // Initiate agent call asynchronously (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteAgentCallAsync(pendingCall);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background agent call execution for CallId {CallId}", 
                        pendingCall.CallId);
                    
                    pendingCall.Status = PendingCallStatus.Failed;
                    pendingCall.ErrorMessage = ex.Message;
                    pendingCall.CompletedAt = DateTime.UtcNow;
                    
                    await _callbackManager.NotifyCallCompletedAsync(pendingCall);
                }
            }, cancellationToken);

            _logger.LogInformation("Initiated non-blocking agent call {CallId} from {CallingAgent} to {TargetAgent}", 
                pendingCall.CallId, callingAgentId, targetAgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing agent function call {FunctionName}", functionCall.FunctionName);
            var error = $"Error initiating agent call: {ex.Message}";
            result.ErrorMessages.Add(error);
            AddErrorToChatHistory(chatHistory, functionCall, error);
        }
    }

    private async Task ExecuteAgentCallAsync(PendingAgentCall pendingCall)
    {
        try
        {
            _logger.LogInformation("Executing agent call {CallId} to {TargetAgentId}", pendingCall.CallId, pendingCall.TargetAgentId);
            
            pendingCall.Status = PendingCallStatus.InProgress;

            // Get target agent grain
            var targetAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>(pendingCall.TargetAgentId);

            // Check if target agent is initialized with retry logic
            var isInitialized = await WaitForAgentInitializationAsync(targetAgent, pendingCall.TargetAgentId);
            if (!isInitialized)
            {
                throw new InvalidOperationException($"Target agent {pendingCall.TargetAgentId} failed to initialize after waiting period");
            }

            // Execute task on target agent
            var result = await targetAgent.ExecuteTaskAsync(pendingCall.Query);

            // Update pending call with result
            pendingCall.Result = result;
            pendingCall.Status = PendingCallStatus.Completed;
            pendingCall.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Agent call {CallId} completed successfully", pendingCall.CallId);

            // TODO: this notification is wrong, the returned result doesn't mean the task is done. We need to track the state of the sent task
            // And only notify the caller when call back is received and the LLM deems the task is completed or failed
            await _callbackManager.NotifyCallCompletedAsync(pendingCall);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent call {CallId} failed", pendingCall.CallId);
            
            pendingCall.Status = PendingCallStatus.Failed;
            pendingCall.ErrorMessage = ex.Message;
            pendingCall.CompletedAt = DateTime.UtcNow;

            await _callbackManager.NotifyCallCompletedAsync(pendingCall);
            throw;
        }
    }

    /// <summary>
    /// Wait for agent to be initialized with retry logic
    /// </summary>
    private async Task<bool> WaitForAgentInitializationAsync(IConfigurableAgentGrain targetAgent, string agentId)
    {
        const int maxRetries = 10;
        const int retryDelayMs = 500;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var isInitialized = await targetAgent.IsInitializedAsync();
                if (isInitialized)
                {
                    _logger.LogDebug("Agent {AgentId} initialized successfully on attempt {Attempt}", agentId, attempt);
                    return true;
                }

                if (attempt < maxRetries)
                {
                    _logger.LogDebug("Agent {AgentId} not yet initialized, attempt {Attempt}/{MaxRetries}, waiting {Delay}ms...", 
                        agentId, attempt, maxRetries, retryDelayMs);
                    await Task.Delay(retryDelayMs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking initialization status for agent {AgentId} on attempt {Attempt}", agentId, attempt);
                
                if (attempt < maxRetries)
                {
                    await Task.Delay(retryDelayMs);
                }
            }
        }

        _logger.LogError("Agent {AgentId} failed to initialize after {MaxRetries} attempts", agentId, maxRetries);
        return false;
    }

    private async Task ProcessRegularFunctionCallAsync(
        FunctionCallContent functionCall,
        KernelFunction function,
        Kernel kernel,
        ChatHistory chatHistory,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Executing regular function {FunctionName}", functionCall.FunctionName);

            // Execute function synchronously (blocking)
            var functionResult = await function.InvokeAsync(kernel, functionCall.Arguments, cancellationToken);
            
            var resultContent = ProcessFunctionResult(functionResult.GetValue<object>());
            
            AddFunctionResultToChatHistory(chatHistory, functionCall, resultContent);

            _logger.LogDebug("Regular function {FunctionName} executed successfully", functionCall.FunctionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing regular function {FunctionName}", functionCall.FunctionName);
            var errorMessage = $"Function execution failed: {ex.Message}";
            AddErrorToChatHistory(chatHistory, functionCall, errorMessage);
            throw;
        }
    }

    public bool IsAgentFunction(string functionName, string? pluginName = null)
    {
        // Check if function name starts with "Call_" (agent communication pattern)
        if (functionName.StartsWith("Call_", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if plugin is "AgentCommunication"
        if (string.Equals(pluginName, "AgentCommunication", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for specific agent function names
        if (string.Equals(functionName, "call_agent", StringComparison.OrdinalIgnoreCase))
            return true;

        // Additional agent function patterns can be added here
        return false;
    }

    public string? ExtractTargetAgentId(string functionName)
    {
        // Handle "Call_AgentId" format
        if (functionName.StartsWith("Call_", StringComparison.OrdinalIgnoreCase))
        {
            // Extract agent ID from "Call_AgentId" format
            var agentId = functionName.Substring(5); // Remove "Call_" prefix
            
            // Convert back from function-safe format to agent ID
            agentId = agentId.Replace("_", "-").Replace(" ", "_");
            
            return string.IsNullOrEmpty(agentId) ? null : agentId;
        }

        // For call_agent function, the target agent ID should be in the arguments
        // This will be extracted from arguments in ProcessAgentFunctionCallAsync
        if (string.Equals(functionName, "call_agent", StringComparison.OrdinalIgnoreCase))
        {
            return "call_agent_target"; // Placeholder - actual ID extracted from arguments
        }

        return null;
    }

    private static bool TryValidateFunctionCall(
        FunctionCallContent functionCall,
        Kernel kernel,
        out KernelFunction? function,
        out string? errorMessage)
    {
        function = null;
        errorMessage = null;

        // Check if the function call has an exception
        if (functionCall.Exception is not null)
        {
            errorMessage = $"Error: Function call processing failed. {functionCall.Exception.Message}";
            return false;
        }

        // Look up the function in the kernel
        if (kernel.Plugins.TryGetFunction(functionCall.PluginName, functionCall.FunctionName, out function))
        {
            return true;
        }

        errorMessage = $"Error: Function {functionCall.FunctionName} not found in kernel.";
        return false;
    }

    private static string ExtractQueryFromArguments(KernelArguments? arguments)
    {
        if (arguments == null)
            return string.Empty;

        // Look for common parameter names that contain the query
        var queryParameterNames = new[] { "query", "task", "message", "input", "text", "prompt" };
        
        foreach (var paramName in queryParameterNames)
        {
            if (arguments.TryGetValue(paramName, out var value) && value != null)
            {
                return value.ToString() ?? string.Empty;
            }
        }

        // If no standard parameter found, use the first string argument
        var firstStringArg = arguments.Values.FirstOrDefault(v => v is string);
        return firstStringArg?.ToString() ?? string.Empty;
    }

    private static string? ExtractAgentIdFromArguments(KernelArguments? arguments)
    {
        if (arguments == null)
            return null;

        // Look for common parameter names that contain the agent ID
        var agentIdParameterNames = new[] { "agentId", "agent_id", "id", "targetAgent", "target_agent" };
        
        foreach (var paramName in agentIdParameterNames)
        {
            if (arguments.TryGetValue(paramName, out var value) && value != null)
            {
                var agentId = value.ToString();
                if (!string.IsNullOrEmpty(agentId))
                {
                    return agentId;
                }
            }
        }

        return null;
    }

    private static void AddFunctionResultToChatHistory(
        ChatHistory chatHistory,
        FunctionCallContent functionCall,
        string result)
    {
        var message = new ChatMessageContent(AuthorRole.Tool, result);
        message.Items.Add(new FunctionResultContent(
            functionCall.FunctionName,
            functionCall.PluginName,
            functionCall.Id,
            result));
        
        chatHistory.Add(message);
    }

    private static void AddErrorToChatHistory(
        ChatHistory chatHistory,
        FunctionCallContent functionCall,
        string errorMessage)
    {
        var message = new ChatMessageContent(AuthorRole.Tool, errorMessage);
        message.Items.Add(new FunctionResultContent(
            functionCall.FunctionName,
            functionCall.PluginName,
            functionCall.Id,
            errorMessage));
        
        chatHistory.Add(message);
    }

    /// <summary>
    /// Process function result to string representation
    /// Based on Microsoft's FunctionCallsProcessor.ProcessFunctionResult
    /// </summary>
    private static string ProcessFunctionResult(object? functionResult)
    {
        if (functionResult is string stringResult)
        {
            return stringResult;
        }

        // Optimization for ChatMessageContent
        if (functionResult is ChatMessageContent chatMessageContent)
        {
            return chatMessageContent.ToString();
        }

        // Optimization for enumerable of ChatMessageContent
        if (functionResult is IEnumerable<ChatMessageContent> chatMessageContents)
        {
            return string.Join(",", chatMessageContents.Select(c => c.ToString()));
        }

        return functionResult != null 
            ? JsonSerializer.Serialize(functionResult, JsonSerializerOptions.Default)
            : string.Empty;
    }
} 
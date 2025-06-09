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
        // Start function call processing tracing
        using var functionCallProcessingActivity = AgentTracingService.StartAgentActivity("ManualFunctionCallProcessor.ProcessFunctionCalls", agentId);
        functionCallProcessingActivity?.SetTag("function_call.agent_id", agentId);
        functionCallProcessingActivity?.SetTag("function_call.chat_result_content", !string.IsNullOrEmpty(chatResult.Content));
        
        var result = new ManualFunctionCallResult();
        
        // 📊 TASK_INFO: Log detailed function call processing start
        _logger.LogInformation("📊 TASK_INFO: Function Call Processing Start - Agent: {AgentId}, Chat Result Length: {Length}, Content Preview: '{Content}'", 
            agentId, chatResult.Content?.Length ?? 0, 
            chatResult.Content?.Length > 200 ? chatResult.Content.Substring(0, 200) + "..." : chatResult.Content ?? "null");
        
        try
        {
            // Add the AI response to chat history first
            chatHistory.Add(chatResult);
            
            // Extract function calls from the chat result
            var functionCalls = FunctionCallContent.GetFunctionCalls(chatResult).ToArray();
            
            functionCallProcessingActivity?.SetTag("function_call.total_calls", functionCalls.Length);
            
            // 📊 TASK_INFO: Log function call details
            if (functionCalls.Length > 0)
            {
                var functionCallDetails = functionCalls.Select(fc => new
                {
                    Function = fc.FunctionName,
                    Plugin = fc.PluginName ?? "none",
                    Id = fc.Id ?? "none",
                    Arguments = fc.Arguments?.Select(kv => $"{kv.Key}={kv.Value}").ToList() ?? new List<string>()
                }).ToList();

                _logger.LogInformation("📊 TASK_INFO: Function Calls Detected - Agent: {AgentId}, Count: {Count}, Calls: {Calls}", 
                    agentId, functionCalls.Length, 
                    string.Join("; ", functionCallDetails.Select(fc => $"{fc.Plugin}.{fc.Function}({string.Join(", ", fc.Arguments.Take(2))})")));
            }
            
            if (functionCalls.Length == 0)
            {
                _logger.LogDebug("No function calls found in chat result");
                
                // 📊 TASK_INFO: Log no function calls detected
                _logger.LogInformation("📊 TASK_INFO: No Function Calls - Agent: {AgentId}, Chat Result Content Available: {HasContent}", 
                    agentId, !string.IsNullOrEmpty(chatResult.Content));
                
                AgentTracingService.SetSuccess(functionCallProcessingActivity, "No function calls to process");
                return result;
            }

            _logger.LogInformation("Processing {Count} function calls for agent {AgentId}", 
                functionCalls.Length, agentId);

            // Categorize function calls for tracing
            var agentCalls = functionCalls.Where(fc => IsAgentFunction(fc.FunctionName, fc.PluginName)).ToArray();
            var regularCalls = functionCalls.Where(fc => !IsAgentFunction(fc.FunctionName, fc.PluginName)).ToArray();
            
            functionCallProcessingActivity?.SetTag("function_call.agent_calls", agentCalls.Length);
            functionCallProcessingActivity?.SetTag("function_call.regular_calls", regularCalls.Length);
            functionCallProcessingActivity?.SetTag("function_call.agent_call_names", string.Join(", ", agentCalls.Select(fc => fc.FunctionName).Take(3)));
            functionCallProcessingActivity?.SetTag("function_call.regular_call_names", string.Join(", ", regularCalls.Select(fc => fc.FunctionName).Take(3)));

            // 📊 TASK_INFO: Log function call categorization
            _logger.LogInformation("📊 TASK_INFO: Function Call Categorization - Agent: {AgentId}, Agent Calls: {AgentCalls} ({AgentCallNames}), Regular Calls: {RegularCalls} ({RegularCallNames})", 
                agentId, agentCalls.Length, string.Join(", ", agentCalls.Select(fc => fc.FunctionName)), 
                regularCalls.Length, string.Join(", ", regularCalls.Select(fc => fc.FunctionName)));

            // Process each function call
            foreach (var functionCall in functionCalls)
            {
                try
                {
                    // 📊 TASK_INFO: Log individual function call processing
                    _logger.LogInformation("📊 TASK_INFO: Processing Function Call - Agent: {AgentId}, Function: {Function}, Plugin: {Plugin}, Arguments: {Arguments}", 
                        agentId, functionCall.FunctionName, functionCall.PluginName ?? "none", 
                        functionCall.Arguments?.Count > 0 ? string.Join(", ", functionCall.Arguments.Select(kv => $"{kv.Key}={kv.Value}").Take(3)) : "none");

                    var callStartTime = DateTime.UtcNow;
                    await ProcessSingleFunctionCallAsync(
                        functionCall, 
                        kernel, 
                        agentId, 
                        chatHistory, 
                        result, 
                        cancellationToken);
                    var callDuration = DateTime.UtcNow - callStartTime;

                    // 📊 TASK_INFO: Log function call completion
                    _logger.LogInformation("📊 TASK_INFO: Function Call Completed - Agent: {AgentId}, Function: {Function}, Duration: {Duration}ms, Success: True", 
                        agentId, functionCall.FunctionName, callDuration.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing function call {FunctionName}", 
                        functionCall.FunctionName);
                    
                    var errorMessage = $"Error processing function {functionCall.FunctionName}: {ex.Message}";
                    result.ErrorMessages.Add(errorMessage);
                    
                    // 📊 TASK_INFO: Log function call error
                    _logger.LogError("📊 TASK_INFO: Function Call Error - Agent: {AgentId}, Function: {Function}, Error: {Error}, Exception Type: {ExceptionType}", 
                        agentId, functionCall.FunctionName, ex.Message, ex.GetType().Name);
                    
                    // Add error to chat history
                    AddErrorToChatHistory(chatHistory, functionCall, errorMessage);
                }
            }

            result.ChatHistoryUpdated = true;
            
            // 📊 TASK_INFO: Log function call processing summary
            _logger.LogInformation("📊 TASK_INFO: Function Call Processing Complete - Agent: {AgentId}, Total Calls: {Total}, Pending Agent Calls: {Pending}, Errors: {Errors}, Should Pause LLM: {ShouldPause}", 
                agentId, functionCalls.Length, result.PendingAgentCalls.Count, result.ErrorMessages.Count, result.ShouldPauseLLMExecution);
            
            _logger.LogInformation("Completed processing function calls for agent {AgentId}. " +
                "Pending agent calls: {PendingCount}, Errors: {ErrorCount}", 
                agentId, result.PendingAgentCalls.Count, result.ErrorMessages.Count);

            functionCallProcessingActivity?.SetTag("function_call.pending_agent_calls", result.PendingAgentCalls.Count);
            functionCallProcessingActivity?.SetTag("function_call.error_count", result.ErrorMessages.Count);
            functionCallProcessingActivity?.SetTag("function_call.should_pause_llm", result.ShouldPauseLLMExecution);
            AgentTracingService.SetSuccess(functionCallProcessingActivity, $"Function call processing completed: {result.PendingAgentCalls.Count} pending, {result.ErrorMessages.Count} errors");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in ProcessFunctionCallsAsync for agent {AgentId}", agentId);
            
            // 📊 TASK_INFO: Log critical processing error
            _logger.LogError("📊 TASK_INFO: Critical Function Call Processing Error - Agent: {AgentId}, Error: {Error}, Exception Type: {ExceptionType}, Stack Trace: {StackTrace}", 
                agentId, ex.Message, ex.GetType().Name, ex.StackTrace);
            
            result.Success = false;
            result.ErrorMessages.Add($"Critical processing error: {ex.Message}");
            
            AgentTracingService.SetError(functionCallProcessingActivity, ex);
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
        // Start single function call tracing
        using var singleCallActivity = AgentTracingService.StartAgentActivity("ProcessSingleFunctionCall", agentId);
        singleCallActivity?.SetTag("single_call.function_name", functionCall.FunctionName);
        singleCallActivity?.SetTag("single_call.plugin_name", functionCall.PluginName ?? "none");
        singleCallActivity?.SetTag("single_call.function_id", functionCall.Id ?? "none");
        singleCallActivity?.SetTag("single_call.is_agent_function", IsAgentFunction(functionCall.FunctionName, functionCall.PluginName));
        
        try
        {
            // Phase 1: Validate function call
            using var validationActivity = AgentTracingService.StartAgentActivity("ValidateFunctionCall", agentId);
            validationActivity?.SetTag("validation.function_name", functionCall.FunctionName);
            validationActivity?.SetTag("validation.has_exception", functionCall.Exception != null);
            
            if (!TryValidateFunctionCall(functionCall, kernel, out var function, out var errorMessage))
            {
                result.ErrorMessages.Add(errorMessage!);
                AddErrorToChatHistory(chatHistory, functionCall, errorMessage!);
                
                validationActivity?.SetTag("validation.result", "failed");
                validationActivity?.SetTag("validation.error", errorMessage);
                AgentTracingService.SetSuccess(validationActivity, $"Function validation failed: {errorMessage}");
                
                singleCallActivity?.SetTag("single_call.result", "validation_failed");
                AgentTracingService.SetSuccess(singleCallActivity, $"Function call validation failed: {errorMessage}");
                return;
            }
            
            validationActivity?.SetTag("validation.result", "success");
            AgentTracingService.SetSuccess(validationActivity, "Function call validation passed");

            // Phase 2: Route to appropriate processor
            if (IsAgentFunction(functionCall.FunctionName, functionCall.PluginName))
            {
                singleCallActivity?.SetTag("single_call.type", "agent_function");
                await ProcessAgentFunctionCallAsync(functionCall, agentId, chatHistory, result, cancellationToken);
            }
            else
            {
                singleCallActivity?.SetTag("single_call.type", "regular_function");
                await ProcessRegularFunctionCallAsync(functionCall, function!, kernel, chatHistory, cancellationToken);
            }
            
            singleCallActivity?.SetTag("single_call.result", "success");
            AgentTracingService.SetSuccess(singleCallActivity, $"Function call processed successfully: {functionCall.FunctionName}");
        }
        catch (Exception ex)
        {
            singleCallActivity?.SetTag("single_call.result", "error");
            AgentTracingService.SetError(singleCallActivity, ex);
            throw;
        }
    }

    private async Task ProcessAgentFunctionCallAsync(
        FunctionCallContent functionCall,
        string callingAgentId,
        ChatHistory chatHistory,
        ManualFunctionCallResult result,
        CancellationToken cancellationToken)
    {
        // Start agent function call processing tracing
        using var agentCallActivity = AgentTracingService.StartAgentActivity("ProcessAgentFunctionCall", callingAgentId);
        agentCallActivity?.SetTag("agent_call.function_name", functionCall.FunctionName);
        agentCallActivity?.SetTag("agent_call.calling_agent", callingAgentId);
        agentCallActivity?.SetTag("agent_call.function_id", functionCall.Id ?? "none");
        agentCallActivity?.SetTag("agent_call.pattern", "NonBlocking");
        
        try
        {
            // Phase 1: Extract target agent ID
            using var targetExtractionActivity = AgentTracingService.StartAgentActivity("ExtractTargetAgentId", callingAgentId);
            
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
                    
                    targetExtractionActivity?.SetTag("extraction.result", "failed");
                    targetExtractionActivity?.SetTag("extraction.error", "no_agent_id_in_arguments");
                    AgentTracingService.SetSuccess(targetExtractionActivity, "Target extraction failed: no agent ID in arguments");
                    
                    agentCallActivity?.SetTag("agent_call.result", "target_extraction_failed");
                    AgentTracingService.SetSuccess(agentCallActivity, "Agent call failed: no target agent ID");
                    return;
                }
            }
            else if (string.IsNullOrEmpty(targetAgentId))
            {
                var error = $"Could not extract target agent ID from function {functionCall.FunctionName}";
                result.ErrorMessages.Add(error);
                AddErrorToChatHistory(chatHistory, functionCall, error);
                
                targetExtractionActivity?.SetTag("extraction.result", "failed");
                targetExtractionActivity?.SetTag("extraction.error", "could_not_extract");
                AgentTracingService.SetSuccess(targetExtractionActivity, "Target extraction failed: could not extract from function name");
                
                agentCallActivity?.SetTag("agent_call.result", "target_extraction_failed");
                AgentTracingService.SetSuccess(agentCallActivity, "Agent call failed: could not extract target");
                return;
            }
            
            targetExtractionActivity?.SetTag("extraction.result", "success");
            targetExtractionActivity?.SetTag("extraction.target_agent", targetAgentId);
            AgentTracingService.SetSuccess(targetExtractionActivity, $"Target agent extracted: {targetAgentId}");

            // Phase 2: Extract query from arguments
            using var queryExtractionActivity = AgentTracingService.StartAgentActivity("ExtractQueryFromArguments", callingAgentId);
            
            var query = ExtractQueryFromArguments(functionCall.Arguments);
            if (string.IsNullOrEmpty(query))
            {
                var error = $"No query found in arguments for agent call {functionCall.FunctionName}";
                result.ErrorMessages.Add(error);
                AddErrorToChatHistory(chatHistory, functionCall, error);
                
                queryExtractionActivity?.SetTag("query_extraction.result", "failed");
                queryExtractionActivity?.SetTag("query_extraction.error", "no_query_found");
                AgentTracingService.SetSuccess(queryExtractionActivity, "Query extraction failed: no query in arguments");
                
                agentCallActivity?.SetTag("agent_call.result", "query_extraction_failed");
                AgentTracingService.SetSuccess(agentCallActivity, "Agent call failed: no query found");
                return;
            }
            
            queryExtractionActivity?.SetTag("query_extraction.result", "success");
            queryExtractionActivity?.SetTag("query_extraction.query_length", query.Length);
            AgentTracingService.SetSuccess(queryExtractionActivity, $"Query extracted: {query.Length} chars");

            // Phase 3: Create pending call record
            using var pendingCallCreationActivity = AgentTracingService.StartAgentActivity("CreatePendingAgentCall", callingAgentId);
            
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
            
            pendingCallCreationActivity?.SetTag("pending_call.call_id", pendingCall.CallId);
            pendingCallCreationActivity?.SetTag("pending_call.target_agent", targetAgentId);
            pendingCallCreationActivity?.SetTag("pending_call.query_length", query.Length);
            AgentTracingService.SetSuccess(pendingCallCreationActivity, $"Pending call created: {pendingCall.CallId}");

            // Register the pending call
            _callbackManager.RegisterPendingCall(pendingCall);
            result.PendingAgentCalls.Add(pendingCall);

            // Add immediate response to chat history indicating call was initiated
            var immediateResponse = $"Agent call to {targetAgentId} has been initiated. " +
                $"CallId: {pendingCall.CallId}. The agent will callback when complete.";
            
            AddFunctionResultToChatHistory(chatHistory, functionCall, immediateResponse);

            // Phase 4: Initiate agent call asynchronously (fire and forget)
            using var asyncExecutionActivity = AgentTracingService.StartAgentActivity("InitiateAsyncAgentExecution", callingAgentId);
            asyncExecutionActivity?.SetTag("async_execution.call_id", pendingCall.CallId);
            asyncExecutionActivity?.SetTag("async_execution.target_agent", targetAgentId);
            asyncExecutionActivity?.SetTag("async_execution.pattern", "FireAndForget");
            
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
            
            AgentTracingService.SetSuccess(asyncExecutionActivity, "Async agent execution initiated");

            _logger.LogInformation("Initiated non-blocking agent call {CallId} from {CallingAgent} to {TargetAgent}", 
                pendingCall.CallId, callingAgentId, targetAgentId);
            
            agentCallActivity?.SetTag("agent_call.result", "initiated");
            agentCallActivity?.SetTag("agent_call.target_agent", targetAgentId);
            agentCallActivity?.SetTag("agent_call.call_id", pendingCall.CallId);
            AgentTracingService.SetSuccess(agentCallActivity, $"Agent call initiated: {callingAgentId} → {targetAgentId} (CallId: {pendingCall.CallId})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing agent function call {FunctionName}", functionCall.FunctionName);
            var error = $"Error initiating agent call: {ex.Message}";
            result.ErrorMessages.Add(error);
            AddErrorToChatHistory(chatHistory, functionCall, error);
            
            agentCallActivity?.SetTag("agent_call.result", "error");
            AgentTracingService.SetError(agentCallActivity, ex);
        }
    }

    private async Task ExecuteAgentCallAsync(PendingAgentCall pendingCall)
    {
        // Start agent call execution tracing
        using var agentExecutionActivity = AgentTracingService.StartAgentActivity("ExecuteAgentCall", pendingCall.CallingAgentId);
        agentExecutionActivity?.SetTag("execution.call_id", pendingCall.CallId);
        agentExecutionActivity?.SetTag("execution.target_agent", pendingCall.TargetAgentId);
        agentExecutionActivity?.SetTag("execution.calling_agent", pendingCall.CallingAgentId);
        agentExecutionActivity?.SetTag("execution.query_length", pendingCall.Query?.Length ?? 0);
        
        try
        {
            _logger.LogInformation("Executing agent call {CallId} to {TargetAgentId}", pendingCall.CallId, pendingCall.TargetAgentId);
            
            pendingCall.Status = PendingCallStatus.InProgress;
            agentExecutionActivity?.SetTag("execution.status", "InProgress");

            // Phase 1: Get target agent grain
            using var grainLookupActivity = AgentTracingService.StartAgentActivity("GetTargetAgentGrain", pendingCall.CallingAgentId);
            grainLookupActivity?.SetTag("grain_lookup.target_agent", pendingCall.TargetAgentId);
            
            var targetAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>(pendingCall.TargetAgentId);
            
            AgentTracingService.SetSuccess(grainLookupActivity, "Target agent grain obtained");

            // Phase 2: Check if target agent is initialized with retry logic
            using var initializationCheckActivity = AgentTracingService.StartAgentActivity("WaitForAgentInitialization", pendingCall.CallingAgentId);
            initializationCheckActivity?.SetTag("initialization.target_agent", pendingCall.TargetAgentId);
            
            var isInitialized = await WaitForAgentInitializationAsync(targetAgent, pendingCall.TargetAgentId);
            if (!isInitialized)
            {
                var error = new InvalidOperationException($"Target agent {pendingCall.TargetAgentId} failed to initialize after waiting period");
                
                initializationCheckActivity?.SetTag("initialization.result", "failed");
                AgentTracingService.SetError(initializationCheckActivity, error);
                throw error;
            }
            
            initializationCheckActivity?.SetTag("initialization.result", "success");
            AgentTracingService.SetSuccess(initializationCheckActivity, "Target agent initialization confirmed");

            // Phase 3: Execute task on target agent
            using var taskExecutionActivity = AgentTracingService.StartAgentActivity("ExecuteTaskOnTargetAgent", pendingCall.CallingAgentId);
            taskExecutionActivity?.SetTag("task_execution.target_agent", pendingCall.TargetAgentId);
            taskExecutionActivity?.SetTag("task_execution.query", pendingCall.Query?.Length > 100 ? pendingCall.Query.Substring(0, 100) + "..." : pendingCall.Query);
            
            var result = await targetAgent.ExecuteTaskAsync(pendingCall.Query);
            
            taskExecutionActivity?.SetTag("task_execution.result_length", result?.Length ?? 0);
            AgentTracingService.SetSuccess(taskExecutionActivity, $"Task executed on target agent: {result?.Length ?? 0} chars result");

            // Update pending call with result
            pendingCall.Result = result;
            pendingCall.Status = PendingCallStatus.Completed;
            pendingCall.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Agent call {CallId} completed successfully", pendingCall.CallId);

            // TODO: this notification is wrong, the returned result doesn't mean the task is done. We need to track the state of the sent task
            // And only notify the caller when call back is received and the LLM deems the task is completed or failed
            await _callbackManager.NotifyCallCompletedAsync(pendingCall);
            
            agentExecutionActivity?.SetTag("execution.status", "Completed");
            agentExecutionActivity?.SetTag("execution.result_length", result?.Length ?? 0);
            AgentTracingService.SetSuccess(agentExecutionActivity, $"Agent call completed successfully: {pendingCall.CallId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent call {CallId} failed", pendingCall.CallId);
            
            pendingCall.Status = PendingCallStatus.Failed;
            pendingCall.ErrorMessage = ex.Message;
            pendingCall.CompletedAt = DateTime.UtcNow;

            await _callbackManager.NotifyCallCompletedAsync(pendingCall);
            
            agentExecutionActivity?.SetTag("execution.status", "Failed");
            AgentTracingService.SetError(agentExecutionActivity, ex);
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
        catch (HttpRequestException httpEx) when (httpEx.Message.Contains("432"))
        {
            _logger.LogWarning("Tavily API usage limit exceeded for function {FunctionName}: {Error}", 
                functionCall.FunctionName, httpEx.Message);
            
            var usageLimitMessage = $"Tavily search service is temporarily unavailable (usage limit reached). " +
                $"The search request could not be completed at this time. " +
                $"Consider upgrading your Tavily plan or trying again later.";
            
            AddFunctionResultToChatHistory(chatHistory, functionCall, usageLimitMessage);
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogWarning("HTTP error executing function {FunctionName}: {Error}", 
                functionCall.FunctionName, httpEx.Message);
            
            var httpErrorMessage = $"Web service error: {httpEx.Message}. " +
                $"The external service is currently unavailable. Please try again later.";
            
            AddFunctionResultToChatHistory(chatHistory, functionCall, httpErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing regular function {FunctionName}", functionCall.FunctionName);
            
            // Provide more specific error messages for known function types
            string errorMessage;
            if (functionCall.PluginName?.Equals("Tavily", StringComparison.OrdinalIgnoreCase) == true)
            {
                errorMessage = $"Search function error: {ex.Message}. Web search is temporarily unavailable.";
            }
            else if (functionCall.PluginName?.Equals("Math", StringComparison.OrdinalIgnoreCase) == true ||
                     functionCall.PluginName?.Equals("MathematicalOperations", StringComparison.OrdinalIgnoreCase) == true)
            {
                errorMessage = $"Mathematical calculation error: {ex.Message}. Please check your input parameters.";
            }
            else
            {
                errorMessage = $"Function execution failed: {ex.Message}";
            }
            
            AddErrorToChatHistory(chatHistory, functionCall, errorMessage);
            
            // Don't re-throw for service errors - we want to continue with graceful degradation
            // Only re-throw for critical system errors
            if (ex is not HttpRequestException && ex is not TimeoutException)
            {
                throw;
            }
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
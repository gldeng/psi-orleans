using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;
using PsiOrleans.Models;
using System.Text.Json;

namespace PsiOrleans.Services;

/// <summary>
/// Service for creating and managing configurable Semantic Kernel instances
/// </summary>
public class ConfigurableKernelService : IConfigurableKernelService
{
    private readonly IKernelFunctionRegistry _functionRegistry;
    private readonly ILogger<ConfigurableKernelService> _logger;
    private readonly IManualFunctionCallProcessor? _manualFunctionCallProcessor;

    public ConfigurableKernelService(
        IKernelFunctionRegistry functionRegistry, 
        ILogger<ConfigurableKernelService> logger,
        IManualFunctionCallProcessor? manualFunctionCallProcessor = null)
    {
        _functionRegistry = functionRegistry;
        _logger = logger;
        _manualFunctionCallProcessor = manualFunctionCallProcessor;
    }

    public async Task<Kernel> CreateKernelAsync(
        AgentConfiguration configuration, 
        IEnumerable<string>? functionNames = null,
        IEnumerable<string>? pluginNames = null)
    {
        try
        {
            _logger.LogInformation("Creating kernel for agent: {AgentName}", configuration.AgentName);

            var builder = Kernel.CreateBuilder();

            // Get API key (from config or environment)
            var apiKey = configuration.Model.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "OpenAI API key is required. Set OPENAI_API_KEY environment variable or provide in configuration.");
            }

            // Add OpenAI chat completion service
            if (!string.IsNullOrEmpty(configuration.Model.BaseUrl))
            {
                builder.AddOpenAIChatCompletion(
                    modelId: configuration.Model.ModelId,
                    apiKey: apiKey,
                    httpClient: new HttpClient { BaseAddress = new Uri(configuration.Model.BaseUrl) });
            }
            else
            {
                builder.AddOpenAIChatCompletion(
                    modelId: configuration.Model.ModelId,
                    apiKey: apiKey);
            }

            // Register kernel plugins by name
            if (pluginNames != null && pluginNames.Any())
            {
                var plugins = _functionRegistry.GetPlugins(pluginNames);
                foreach (var plugin in plugins)
                {
                    builder.Plugins.Add(plugin);
                    _logger.LogInformation("Registered plugin: {PluginName}", plugin.Name);
                }
            }

            // Register individual kernel functions by name
            if (functionNames != null && functionNames.Any())
            {
                var functions = _functionRegistry.GetFunctions(functionNames);
                var functionsList = functions.ToList();
                
                if (functionsList.Any())
                {
                    var functionsPlugin = KernelPluginFactory.CreateFromFunctions(
                        "CustomFunctions",
                        "Custom functions for this agent",
                        functionsList);
                    
                    builder.Plugins.Add(functionsPlugin);
                    _logger.LogInformation("Registered {Count} custom functions", functionsList.Count);
                }
            }

            var kernel = builder.Build();
            
            var totalFunctions = kernel.Plugins.SelectMany(p => p).Count();
            _logger.LogInformation("Kernel created successfully for agent: {AgentName} with {FunctionCount} total functions", 
                configuration.AgentName, totalFunctions);
            
            return kernel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating kernel for agent: {AgentName}", configuration.AgentName);
            throw;
        }
    }

    public async Task<Kernel> CreateKernelAsync(
        AgentConfiguration configuration,
        IEnumerable<string>? toolNames)
    {
        try
        {
            _logger.LogInformation("Creating kernel for agent: {AgentName} with unified tools", configuration.AgentName);

            var builder = Kernel.CreateBuilder();

            // Get API key (from config or environment)
            var apiKey = configuration.Model.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "OpenAI API key is required. Set OPENAI_API_KEY environment variable or provide in configuration.");
            }

            // Add OpenAI chat completion service
            if (!string.IsNullOrEmpty(configuration.Model.BaseUrl))
            {
                builder.AddOpenAIChatCompletion(
                    modelId: configuration.Model.ModelId,
                    apiKey: apiKey,
                    httpClient: new HttpClient { BaseAddress = new Uri(configuration.Model.BaseUrl) });
            }
            else
            {
                builder.AddOpenAIChatCompletion(
                    modelId: configuration.Model.ModelId,
                    apiKey: apiKey);
            }

            if (toolNames != null && toolNames.Any())
            {
                // Get all tools by qualified names
                var tools = _functionRegistry.GetToolsByQualifiedNames(toolNames);
                
                // Group tools by their source (individual functions vs plugin functions)
                var individualFunctions = new List<KernelFunction>();
                var pluginFunctions = new Dictionary<string, List<KernelFunction>>();

                foreach (var (qualifiedName, function) in tools)
                {
                    // Check if this is a plugin.function pattern
                    var parts = qualifiedName.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length == 2 && _functionRegistry.HasPlugin(parts[0]))
                    {
                        // This is a plugin function
                        var pluginName = parts[0];
                        if (!pluginFunctions.ContainsKey(pluginName))
                        {
                            pluginFunctions[pluginName] = new List<KernelFunction>();
                        }
                        pluginFunctions[pluginName].Add(function);
                    }
                    else
                    {
                        // This is an individual function
                        individualFunctions.Add(function);
                    }
                }

                // Register individual functions as a custom plugin
                if (individualFunctions.Any())
                {
                    var customFunctionsPlugin = KernelPluginFactory.CreateFromFunctions(
                        "CustomFunctions",
                        "Custom functions for this agent",
                        individualFunctions);
                    
                    builder.Plugins.Add(customFunctionsPlugin);
                    _logger.LogInformation("Registered {Count} individual functions", individualFunctions.Count);
                }

                // Register plugin functions as custom plugins (one per original plugin to maintain context)
                foreach (var (pluginName, functions) in pluginFunctions)
                {
                    var pluginFunctionsPlugin = KernelPluginFactory.CreateFromFunctions(
                        $"{pluginName}Selected",
                        $"Selected functions from {pluginName} plugin",
                        functions);
                    
                    builder.Plugins.Add(pluginFunctionsPlugin);
                    _logger.LogInformation("Registered {Count} functions from plugin {PluginName}", functions.Count, pluginName);
                }

                _logger.LogInformation("Registered {TotalCount} tools from {SourceCount} sources", 
                    tools.Count, 
                    (individualFunctions.Any() ? 1 : 0) + pluginFunctions.Count);
            }

            var kernel = builder.Build();
            
            var totalFunctions = kernel.Plugins.SelectMany(p => p).Count();
            _logger.LogInformation("Kernel created successfully for agent: {AgentName} with {FunctionCount} total functions", 
                configuration.AgentName, totalFunctions);
            
            return kernel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating kernel for agent: {AgentName}", configuration.AgentName);
            throw;
        }
    }

    public async Task<string> ExecuteTaskAsync(Kernel kernel, string task, ConfigurableAgentState state, string systemPrompt, bool waitForCompletion = true)
    {
        // Start kernel service execution tracing
        using var kernelExecutionActivity = AgentTracingService.StartKernelActivity("ConfigurableKernelService.ExecuteTask", state.AgentId);
        kernelExecutionActivity?.SetTag("kernel_execution.agent_id", state.AgentId);
        kernelExecutionActivity?.SetTag("kernel_execution.task_length", task.Length);
        kernelExecutionActivity?.SetTag("kernel_execution.wait_for_completion", waitForCompletion);
        kernelExecutionActivity?.SetTag("kernel_execution.system_prompt_length", systemPrompt.Length);
        
        // 🟢 FLOW_START: Kernel service task execution flow
        _logger.LogInformation("🟢 FLOW_START: ConfigurableKernelService.ExecuteTask - AgentId: {AgentId}, Task: '{Task}', WaitForCompletion: {WaitForCompletion}", 
            state.AgentId, task.Length > 100 ? task.Substring(0, 100) + "..." : task, waitForCompletion);
        
        try
        {
            // Phase 1: Prepare chat service and history
            using var chatPreparationActivity = AgentTracingService.StartKernelActivity("PrepareChatServiceAndHistory", state.AgentId);
            
            // 🔄 FLOW_STEP: Preparing chat service and history
            _logger.LogInformation("🔄 FLOW_STEP: Preparing chat service and history - AgentId: {AgentId}", state.AgentId);
            
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = state.ToSemanticKernelChatHistory();
            
            chatPreparationActivity?.SetTag("chat_prep.existing_history_count", chatHistory.Count);

            // Add system message if this is a fresh conversation
            if (chatHistory.Count == 0)
            {
                chatHistory.AddSystemMessage(systemPrompt);
                state.AddChatMessage("system", systemPrompt);
                chatPreparationActivity?.SetTag("chat_prep.system_message_added", true);
                
                // 🔄 FLOW_STEP: Added system message for fresh conversation
                _logger.LogInformation("🔄 FLOW_STEP: Added system message for fresh conversation - AgentId: {AgentId}", state.AgentId);
            }
            else
            {
                chatPreparationActivity?.SetTag("chat_prep.system_message_added", false);
                
                // 🔄 FLOW_STEP: Using existing conversation history
                _logger.LogInformation("🔄 FLOW_STEP: Using existing conversation history ({Count} messages) - AgentId: {AgentId}", 
                    chatHistory.Count, state.AgentId);
            }

            // Add the user task
            chatHistory.AddUserMessage(task);
            state.AddChatMessage("user", task);
            
            chatPreparationActivity?.SetTag("chat_prep.final_history_count", chatHistory.Count);
            
            // ✅ FLOW_SUCCESS: Chat preparation completed
            _logger.LogInformation("✅ FLOW_SUCCESS: Chat preparation completed - AgentId: {AgentId}, History count: {Count}", 
                state.AgentId, chatHistory.Count);
            
            AgentTracingService.SetSuccess(chatPreparationActivity, $"Chat prepared with {chatHistory.Count} messages");

            // Phase 2: Configure execution settings
            using var executionSettingsActivity = AgentTracingService.StartKernelActivity("ConfigureExecutionSettings", state.AgentId);
            
            // 🔄 FLOW_STEP: Configuring LLM execution settings
            _logger.LogInformation("🔄 FLOW_STEP: Configuring LLM execution settings - AgentId: {AgentId}", state.AgentId);
            
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                // Enable function calling but don't auto-invoke - we'll handle manually
                ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions,
                MaxTokens = state.Configuration?.MaxTokens ?? 4000,
                Temperature = state.Configuration?.Temperature ?? 0.1
            };
            
            executionSettingsActivity?.SetTag("execution_settings.max_tokens", executionSettings.MaxTokens);
            executionSettingsActivity?.SetTag("execution_settings.temperature", executionSettings.Temperature);
            executionSettingsActivity?.SetTag("execution_settings.tool_call_behavior", "EnableKernelFunctions");
            
            // ✅ FLOW_SUCCESS: Execution settings configured
            _logger.LogInformation("✅ FLOW_SUCCESS: Execution settings configured - AgentId: {AgentId}, MaxTokens: {MaxTokens}, Temperature: {Temperature}", 
                state.AgentId, executionSettings.MaxTokens, executionSettings.Temperature);
            
            AgentTracingService.SetSuccess(executionSettingsActivity, "Execution settings configured for LLM");

            // Phase 3: Get initial response from LLM
            using var initialLLMActivity = AgentTracingService.StartKernelActivity("InitialLLMCall", state.AgentId);
            initialLLMActivity?.SetTag("llm_call.type", "initial");
            initialLLMActivity?.SetTag("llm_call.history_count", chatHistory.Count);
            
            // 🔄 FLOW_STEP: Making initial LLM call
            _logger.LogInformation("🔄 FLOW_STEP: Making initial LLM call - AgentId: {AgentId}, History count: {Count}", 
                state.AgentId, chatHistory.Count);
            
            var result = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                kernel);
            
            var hasFunctionCalls = FunctionCallContent.GetFunctionCalls(result).Any();
            initialLLMActivity?.SetTag("llm_call.has_function_calls", hasFunctionCalls);
            initialLLMActivity?.SetTag("llm_call.response_length", result.Content?.Length ?? 0);
            
            // ✅ FLOW_SUCCESS: Initial LLM response received
            _logger.LogInformation("✅ FLOW_SUCCESS: Initial LLM response received - AgentId: {AgentId}, Response length: {Length}, Function calls: {HasCalls}", 
                state.AgentId, result.Content?.Length ?? 0, hasFunctionCalls);
            
            AgentTracingService.SetSuccess(initialLLMActivity, $"Initial LLM response received: {result.Content?.Length ?? 0} chars, {(hasFunctionCalls ? "with" : "no")} function calls");

            // Phase 4: Process function calls if manual processor is available
            if (_manualFunctionCallProcessor != null && state.Configuration != null)
            {
                // Start function call processing loop
                using var functionCallLoopActivity = AgentTracingService.StartKernelActivity("FunctionCallProcessingLoop", state.AgentId);
                functionCallLoopActivity?.SetTag("function_loop.max_iterations", 10);
                
                // 🔄 FLOW_STEP: Starting function call processing loop
                _logger.LogInformation("🔄 FLOW_STEP: Starting function call processing loop - AgentId: {AgentId}, Max iterations: 10", state.AgentId);
                
                const int maxIterations = 10; // Prevent infinite loops
                int iteration = 0;
                
                while (iteration < maxIterations)
                {
                    iteration++;
                    
                    // Phase 4.1: Process function calls for this iteration
                    using var iterationActivity = AgentTracingService.StartKernelActivity($"FunctionCallIteration_{iteration}", state.AgentId);
                    iterationActivity?.SetTag("iteration.number", iteration);
                    iterationActivity?.SetTag("iteration.has_function_calls", FunctionCallContent.GetFunctionCalls(result).Any());
                    
                    // 🔄 FLOW_STEP: Processing function calls iteration
                    _logger.LogInformation("🔄 FLOW_STEP: Processing function calls iteration {Iteration} - AgentId: {AgentId}, Has function calls: {HasCalls}", 
                        iteration, state.AgentId, FunctionCallContent.GetFunctionCalls(result).Any());
                    
                    var functionCallResult = await _manualFunctionCallProcessor.ProcessFunctionCallsAsync(
                        result,
                        kernel,
                        state.AgentId,
                        chatHistory);

                    iterationActivity?.SetTag("iteration.processing_success", functionCallResult.Success);
                    iterationActivity?.SetTag("iteration.pending_agent_calls", functionCallResult.PendingAgentCalls.Count);
                    iterationActivity?.SetTag("iteration.errors", functionCallResult.ErrorMessages.Count);

                    if (!functionCallResult.Success)
                    {
                        var errorMessage = $"Function call processing failed: {string.Join(", ", functionCallResult.ErrorMessages)}";
                        
                        // ❌ FLOW_ERROR: Function call processing failed
                        _logger.LogError("❌ FLOW_ERROR: Function call processing failed - AgentId: {AgentId}, Iteration: {Iteration}, Error: {Error}", 
                            state.AgentId, iteration, errorMessage);
                        
                        iterationActivity?.SetTag("iteration.result", "processing_failed");
                        AgentTracingService.SetSuccess(iterationActivity, $"Iteration {iteration} failed: function call processing error");
                        
                        kernelExecutionActivity?.SetTag("kernel_execution.result", "function_call_error");
                        AgentTracingService.SetError(kernelExecutionActivity, new InvalidOperationException(errorMessage));
                        return errorMessage;
                    }

                    // If there were pending agent calls, inform the user
                    if (functionCallResult.PendingAgentCalls.Any())
                    {
                        // 🔄 FLOW_STEP: Non-blocking agent calls initiated
                        _logger.LogInformation("🔄 FLOW_STEP: Initiated {Count} non-blocking agent calls - AgentId: {AgentId}, Iteration: {Iteration}", 
                            functionCallResult.PendingAgentCalls.Count, state.AgentId, iteration);
                    }

                    // Phase 4.2: Check if we should pause LLM execution for pending agent calls
                    if (functionCallResult.ShouldPauseLLMExecution)
                    {
                        // Handle execution pause
                        using var pauseHandlingActivity = AgentTracingService.StartKernelActivity("HandleExecutionPause", state.AgentId);
                        pauseHandlingActivity?.SetTag("pause.pending_calls", functionCallResult.PendingCallsRequiringWait);
                        pauseHandlingActivity?.SetTag("pause.wait_for_completion", waitForCompletion);
                        
                        // 🔀 FLOW_DECISION: Should pause LLM execution for pending agent calls
                        _logger.LogInformation("🔀 FLOW_DECISION: Pause LLM execution required - AgentId: {AgentId}, Pending calls: {PendingCalls}, WaitForCompletion: {WaitForCompletion}", 
                            state.AgentId, functionCallResult.PendingCallsRequiringWait, waitForCompletion);
                        
                        if (!waitForCompletion)
                        {
                            // Store pause state for later resumption
                            state.WorkingMemory["execution_paused"] = "true";
                            state.WorkingMemory["pending_agent_calls"] = functionCallResult.PendingCallsRequiringWait.ToString();
                            state.WorkingMemory["pause_timestamp"] = DateTime.UtcNow.ToString("O");

                            var pauseMessage = $"LLM execution paused due to {functionCallResult.PendingCallsRequiringWait} pending agent calls. " +
                                             "Execution will resume when callbacks are received.";
                            
                            pauseHandlingActivity?.SetTag("pause.action", "paused_for_callbacks");
                            AgentTracingService.SetSuccess(pauseHandlingActivity, "Execution paused for pending agent callbacks");
                            
                            iterationActivity?.SetTag("iteration.result", "paused");
                            AgentTracingService.SetSuccess(iterationActivity, $"Iteration {iteration} paused for {functionCallResult.PendingCallsRequiringWait} callbacks");
                            
                            functionCallLoopActivity?.SetTag("function_loop.iterations_completed", iteration);
                            functionCallLoopActivity?.SetTag("function_loop.result", "paused");
                            AgentTracingService.SetSuccess(functionCallLoopActivity, $"Function call loop paused after {iteration} iterations");
                            
                            kernelExecutionActivity?.SetTag("kernel_execution.result", "paused");
                            kernelExecutionActivity?.SetTag("kernel_execution.iterations", iteration);
                            
                            // ⏸️ FLOW_PAUSE: Execution paused for pending agent callbacks
                            _logger.LogInformation("⏸️ FLOW_PAUSE: LLM execution paused - AgentId: {AgentId}, Pending calls: {PendingCalls}, Message: '{Message}'", 
                                state.AgentId, functionCallResult.PendingCallsRequiringWait, pauseMessage);
                            
                            // 🏁 FLOW_END: Early exit due to pause
                            _logger.LogInformation("🏁 FLOW_END: ConfigurableKernelService.ExecuteTask - PAUSED for agent {AgentId}", state.AgentId);
                            
                            AgentTracingService.SetSuccess(kernelExecutionActivity, pauseMessage);
                            
                            return pauseMessage;
                        }
                        else
                        {
                            pauseHandlingActivity?.SetTag("pause.action", "wait_for_completion");
                            
                            // 🔄 FLOW_STEP: Wait for completion mode - continuing execution
                            _logger.LogInformation("🔄 FLOW_STEP: Wait for completion mode - continuing execution - AgentId: {AgentId}", state.AgentId);
                            
                            AgentTracingService.SetSuccess(pauseHandlingActivity, "Waiting for completion mode - continuing execution");
                        }
                        
                        // Continue processing in case there are more function calls
                        iterationActivity?.SetTag("iteration.result", "continued");
                        AgentTracingService.SetSuccess(iterationActivity, $"Iteration {iteration} continued with pending calls");
                        continue;
                    }

                    // Phase 4.3: Check if we should continue or break
                    if (!functionCallResult.ChatHistoryUpdated && !string.IsNullOrEmpty(result.Content))
                    {
                        iterationActivity?.SetTag("iteration.result", "completed_with_content");
                        
                        // ✅ FLOW_SUCCESS: Iteration completed with final content
                        _logger.LogInformation("✅ FLOW_SUCCESS: Iteration {Iteration} completed with final content - AgentId: {AgentId}, Content length: {Length}", 
                            iteration, state.AgentId, result.Content.Length);
                        
                        AgentTracingService.SetSuccess(iterationActivity, $"Iteration {iteration} completed with final content");
                        break;
                    }

                    // Phase 4.4: Get next response from LLM after function results
                    if (functionCallResult.ChatHistoryUpdated)
                    {
                        using var nextLLMActivity = AgentTracingService.StartKernelActivity($"LLMCall_Iteration_{iteration}", state.AgentId);
                        nextLLMActivity?.SetTag("llm_call.type", "continuation");
                        nextLLMActivity?.SetTag("llm_call.iteration", iteration);
                        nextLLMActivity?.SetTag("llm_call.history_count", chatHistory.Count);
                        
                        // 🔄 FLOW_STEP: Making continuation LLM call after function execution
                        _logger.LogInformation("🔄 FLOW_STEP: Making continuation LLM call after function execution - AgentId: {AgentId}, Iteration: {Iteration}, History count: {Count}", 
                            state.AgentId, iteration, chatHistory.Count);
                        
                        var nextResult = await chatService.GetChatMessageContentAsync(
                            chatHistory,
                            executionSettings,
                            kernel);
                        
                        result = nextResult;
                        
                        var nextHasFunctionCalls = FunctionCallContent.GetFunctionCalls(result).Any();
                        nextLLMActivity?.SetTag("llm_call.has_function_calls", nextHasFunctionCalls);
                        nextLLMActivity?.SetTag("llm_call.response_length", result.Content?.Length ?? 0);
                        
                        // ✅ FLOW_SUCCESS: Continuation LLM response received
                        _logger.LogInformation("✅ FLOW_SUCCESS: Continuation LLM response received - AgentId: {AgentId}, Iteration: {Iteration}, Response length: {Length}, Function calls: {HasCalls}", 
                            state.AgentId, iteration, result.Content?.Length ?? 0, nextHasFunctionCalls);
                        
                        AgentTracingService.SetSuccess(nextLLMActivity, $"Continuation LLM response: {result.Content?.Length ?? 0} chars, {(nextHasFunctionCalls ? "with" : "no")} function calls");
                        
                        // If this result has content and no function calls, we're done
                        if (!string.IsNullOrEmpty(result.Content) && !nextHasFunctionCalls)
                        {
                            iterationActivity?.SetTag("iteration.result", "completed_final");
                            
                            // ✅ FLOW_SUCCESS: Final response received - iteration complete
                            _logger.LogInformation("✅ FLOW_SUCCESS: Final response received - iteration {Iteration} complete - AgentId: {AgentId}", 
                                iteration, state.AgentId);
                            
                            AgentTracingService.SetSuccess(iterationActivity, $"Iteration {iteration} completed - final response received");
                            break;
                        }
                        
                        iterationActivity?.SetTag("iteration.result", "continued");
                        
                        // 🔄 FLOW_STEP: More function calls to process - continuing
                        _logger.LogInformation("🔄 FLOW_STEP: More function calls to process - continuing iteration {Iteration} - AgentId: {AgentId}", 
                            iteration, state.AgentId);
                        
                        AgentTracingService.SetSuccess(iterationActivity, $"Iteration {iteration} continued - more function calls to process");
                    }
                    else
                    {
                        // No chat history update means no function calls were processed
                        iterationActivity?.SetTag("iteration.result", "no_updates");
                        
                        // 🔄 FLOW_STEP: No function calls processed - ending iteration
                        _logger.LogInformation("🔄 FLOW_STEP: No function calls processed - ending iteration {Iteration} - AgentId: {AgentId}", 
                            iteration, state.AgentId);
                        
                        AgentTracingService.SetSuccess(iterationActivity, $"Iteration {iteration} - no function calls processed");
                        break;
                    }
                }
                
                functionCallLoopActivity?.SetTag("function_loop.iterations_completed", iteration);
                if (iteration >= maxIterations)
                {
                    // ⚠️ FLOW_WARNING: Reached maximum iterations
                    _logger.LogWarning("⚠️ FLOW_WARNING: Reached maximum function call iterations ({MaxIterations}) for task execution - AgentId: {AgentId}", 
                        maxIterations, state.AgentId);
                    
                    functionCallLoopActivity?.SetTag("function_loop.result", "max_iterations_reached");
                    AgentTracingService.SetSuccess(functionCallLoopActivity, $"Function call loop completed: reached max iterations ({maxIterations})");
                }
                else
                {
                    // ✅ FLOW_SUCCESS: Function call loop completed naturally
                    _logger.LogInformation("✅ FLOW_SUCCESS: Function call loop completed naturally after {Iterations} iterations - AgentId: {AgentId}", 
                        iteration, state.AgentId);
                    
                    functionCallLoopActivity?.SetTag("function_loop.result", "completed_naturally");
                    AgentTracingService.SetSuccess(functionCallLoopActivity, $"Function call loop completed naturally after {iteration} iterations");
                }
            }
            else
            {
                // Fallback to adding the result directly if no manual processor
                chatHistory.Add(result);
                
                // ⚠️ FLOW_WARNING: Manual function call processor not available
                _logger.LogWarning("⚠️ FLOW_WARNING: Manual function call processor not available, using basic processing - AgentId: {AgentId}", state.AgentId);
                
                kernelExecutionActivity?.SetTag("kernel_execution.fallback_processing", true);
            }

            // Phase 5: Finalize response
            using var responseFinalizationActivity = AgentTracingService.StartKernelActivity("FinalizeResponse", state.AgentId);
            
            // 🔄 FLOW_STEP: Finalizing response and updating state
            _logger.LogInformation("🔄 FLOW_STEP: Finalizing response and updating state - AgentId: {AgentId}", state.AgentId);
            
            var response = result.Content ?? "No response generated.";

            // Add assistant response to chat history and state
            state.AddChatMessage("assistant", response);

            // Update working memory
            state.WorkingMemory["last_user_message"] = task;
            state.WorkingMemory["last_assistant_response"] = response;
            state.WorkingMemory["execution_timestamp"] = DateTime.UtcNow.ToString("O");
            
            responseFinalizationActivity?.SetTag("response.length", response.Length);
            responseFinalizationActivity?.SetTag("response.working_memory_updated", true);
            
            // ✅ FLOW_SUCCESS: Response finalization completed
            _logger.LogInformation("✅ FLOW_SUCCESS: Response finalization completed - AgentId: {AgentId}, Response length: {Length}", 
                state.AgentId, response.Length);
            
            AgentTracingService.SetSuccess(responseFinalizationActivity, $"Response finalized: {response.Length} chars");

            // ✅ FLOW_SUCCESS: Task execution completed successfully
            _logger.LogInformation("✅ FLOW_SUCCESS: Task execution completed successfully - AgentId: {AgentId}, Final response length: {Length}", 
                state.AgentId, response.Length);
            
            kernelExecutionActivity?.SetTag("kernel_execution.result", "success");
            kernelExecutionActivity?.SetTag("kernel_execution.response_length", response.Length);
            
            // 🏁 FLOW_END: Kernel service task execution flow complete
            _logger.LogInformation("🏁 FLOW_END: ConfigurableKernelService.ExecuteTask - SUCCESS for agent {AgentId}", state.AgentId);
            
            AgentTracingService.SetSuccess(kernelExecutionActivity, $"Task execution completed: {response.Length} chars response");
            
            return response;
        }
        catch (Exception ex)
        {
            // ❌ FLOW_ERROR: Task execution failed
            _logger.LogError(ex, "❌ FLOW_ERROR: ConfigurableKernelService.ExecuteTask failed for agent {AgentId}: {Error}", 
                state.AgentId, ex.Message);
            
            // 🏁 FLOW_END: Kernel service task execution flow complete with error
            _logger.LogInformation("🏁 FLOW_END: ConfigurableKernelService.ExecuteTask - FAILED for agent {AgentId}", state.AgentId);
            
            kernelExecutionActivity?.SetTag("kernel_execution.result", "error");
            AgentTracingService.SetError(kernelExecutionActivity, ex);
            throw;
        }
    }

    /// <summary>
    /// Wait for all agent callbacks to complete by polling the state
    /// </summary>
    private async Task WaitForCallbacksToCompleteAsync(ConfigurableAgentState state, int expectedCallbacks)
    {
        const int maxWaitSeconds = 60; // Maximum wait time
        const int pollIntervalMs = 100; // Poll every 100ms
        
        var startTime = DateTime.UtcNow;
        
        while ((DateTime.UtcNow - startTime).TotalSeconds < maxWaitSeconds)
        {
            // Check if execution is no longer paused
            if (!state.WorkingMemory.ContainsKey("execution_paused") || 
                state.WorkingMemory["execution_paused"]?.ToString() != "true")
            {
                _logger.LogInformation("All agent callbacks completed, execution resumed");
                return;
            }
            
            // Check pending calls count
            if (state.WorkingMemory.ContainsKey("pending_agent_calls"))
            {
                var pendingCallsValue = state.WorkingMemory["pending_agent_calls"]?.ToString();
                if (!string.IsNullOrEmpty(pendingCallsValue) && int.TryParse(pendingCallsValue, out var pendingCount))
                {
                    if (pendingCount <= 0)
                    {
                        _logger.LogInformation("All agent callbacks completed (pending count: {Count})", pendingCount);
                        return;
                    }
                }
            }
            
            await Task.Delay(pollIntervalMs);
        }
        
        _logger.LogWarning("Timed out waiting for agent callbacks to complete after {MaxWait} seconds", maxWaitSeconds);
    }

    public async Task<string> ContinueConversationAsync(Kernel kernel, string userMessage, ConfigurableAgentState state, string systemPrompt)
    {
        // Start conversation continuation tracing
        using var conversationActivity = AgentTracingService.StartKernelActivity("ConfigurableKernelService.ContinueConversation", state.AgentId);
        conversationActivity?.SetTag("conversation.agent_id", state.AgentId);
        conversationActivity?.SetTag("conversation.user_message_length", userMessage.Length);
        conversationActivity?.SetTag("conversation.system_prompt_length", systemPrompt.Length);
        
        try
        {
            _logger.LogInformation("Continuing conversation with configurable kernel: {Message}", userMessage);

            // Phase 1: Prepare chat service and history
            using var chatPreparationActivity = AgentTracingService.StartKernelActivity("PrepareChatForContinuation", state.AgentId);
            
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = state.ToSemanticKernelChatHistory();
            
            chatPreparationActivity?.SetTag("chat_prep.existing_history_count", chatHistory.Count);

            // Add system message if no chat history exists
            if (chatHistory.Count == 0)
            {
                chatHistory.AddSystemMessage(systemPrompt);
                state.AddChatMessage("system", systemPrompt);
                chatPreparationActivity?.SetTag("chat_prep.system_message_added", true);
            }
            else
            {
                chatPreparationActivity?.SetTag("chat_prep.system_message_added", false);
            }

            // Add the user message
            chatHistory.AddUserMessage(userMessage);
            state.AddChatMessage("user", userMessage);
            
            chatPreparationActivity?.SetTag("chat_prep.final_history_count", chatHistory.Count);
            AgentTracingService.SetSuccess(chatPreparationActivity, $"Chat prepared for continuation with {chatHistory.Count} messages");

            // Phase 2: Configure execution settings
            using var executionSettingsActivity = AgentTracingService.StartKernelActivity("ConfigureConversationExecutionSettings", state.AgentId);
            
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                // Enable function calling but don't auto-invoke - we'll handle manually
                ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions,
                MaxTokens = state.Configuration?.MaxTokens ?? 4000,
                Temperature = state.Configuration?.Temperature ?? 0.1
            };
            
            executionSettingsActivity?.SetTag("execution_settings.max_tokens", executionSettings.MaxTokens);
            executionSettingsActivity?.SetTag("execution_settings.temperature", executionSettings.Temperature);
            executionSettingsActivity?.SetTag("execution_settings.tool_call_behavior", "EnableKernelFunctions");
            AgentTracingService.SetSuccess(executionSettingsActivity, "Execution settings configured for conversation");

            // Phase 3: Get initial response from LLM
            using var initialLLMActivity = AgentTracingService.StartKernelActivity("ConversationInitialLLMCall", state.AgentId);
            initialLLMActivity?.SetTag("llm_call.type", "conversation_initial");
            initialLLMActivity?.SetTag("llm_call.history_count", chatHistory.Count);
            
            var result = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                kernel);
            
            var hasFunctionCalls = FunctionCallContent.GetFunctionCalls(result).Any();
            initialLLMActivity?.SetTag("llm_call.has_function_calls", hasFunctionCalls);
            initialLLMActivity?.SetTag("llm_call.response_length", result.Content?.Length ?? 0);
            AgentTracingService.SetSuccess(initialLLMActivity, $"Initial conversation response: {result.Content?.Length ?? 0} chars, {(hasFunctionCalls ? "with" : "no")} function calls");

            // Phase 4: Process function calls if manual processor is available
            if (_manualFunctionCallProcessor != null && state.Configuration != null)
            {
                // Start function call processing loop for conversation
                using var functionCallLoopActivity = AgentTracingService.StartKernelActivity("ConversationFunctionCallLoop", state.AgentId);
                functionCallLoopActivity?.SetTag("function_loop.max_iterations", 10);
                functionCallLoopActivity?.SetTag("function_loop.context", "conversation");
                
                const int maxIterations = 10; // Prevent infinite loops
                int iteration = 0;
                
                while (iteration < maxIterations)
                {
                    iteration++;
                    
                    // Phase 4.1: Process function calls for this iteration
                    using var iterationActivity = AgentTracingService.StartKernelActivity($"ConversationIteration_{iteration}", state.AgentId);
                    iterationActivity?.SetTag("iteration.number", iteration);
                    iterationActivity?.SetTag("iteration.has_function_calls", FunctionCallContent.GetFunctionCalls(result).Any());
                    iterationActivity?.SetTag("iteration.context", "conversation");
                    
                    var functionCallResult = await _manualFunctionCallProcessor.ProcessFunctionCallsAsync(
                        result,
                        kernel,
                        state.AgentId,
                        chatHistory);

                    iterationActivity?.SetTag("iteration.processing_success", functionCallResult.Success);
                    iterationActivity?.SetTag("iteration.pending_agent_calls", functionCallResult.PendingAgentCalls.Count);
                    iterationActivity?.SetTag("iteration.errors", functionCallResult.ErrorMessages.Count);

                    if (!functionCallResult.Success)
                    {
                        var errorMessage = $"Function call processing failed: {string.Join(", ", functionCallResult.ErrorMessages)}";
                        _logger.LogError(errorMessage);
                        
                        iterationActivity?.SetTag("iteration.result", "processing_failed");
                        AgentTracingService.SetSuccess(iterationActivity, $"Conversation iteration {iteration} failed: function call processing error");
                        
                        functionCallLoopActivity?.SetTag("function_loop.result", "error");
                        AgentTracingService.SetSuccess(functionCallLoopActivity, "Function call loop failed in conversation");
                        
                        conversationActivity?.SetTag("conversation.result", "function_call_error");
                        AgentTracingService.SetError(conversationActivity, new InvalidOperationException(errorMessage));
                        return errorMessage;
                    }

                    // If there were pending agent calls, inform the user
                    if (functionCallResult.PendingAgentCalls.Any())
                    {
                        _logger.LogInformation("Initiated {Count} non-blocking agent calls", 
                            functionCallResult.PendingAgentCalls.Count);
                    }

                    // Phase 4.2: Check if we should pause LLM execution for pending agent calls
                    if (functionCallResult.ShouldPauseLLMExecution)
                    {
                        using var pauseHandlingActivity = AgentTracingService.StartKernelActivity("HandleConversationPause", state.AgentId);
                        pauseHandlingActivity?.SetTag("pause.pending_calls", functionCallResult.PendingCallsRequiringWait);
                        pauseHandlingActivity?.SetTag("pause.context", "conversation");
                        
                        _logger.LogInformation("Pausing conversation execution, waiting for {Count} agent callbacks", 
                            functionCallResult.PendingCallsRequiringWait);
                        
                        // For conversations, we always use waitForCompletion=false pattern
                        state.WorkingMemory["execution_paused"] = "true";
                        state.WorkingMemory["pending_agent_calls"] = functionCallResult.PendingCallsRequiringWait.ToString();
                        state.WorkingMemory["pause_timestamp"] = DateTime.UtcNow.ToString("O");

                        var pauseMessage = $"Conversation paused - waiting for {functionCallResult.PendingCallsRequiringWait} agent callback(s) to complete.";
                        
                        pauseHandlingActivity?.SetTag("pause.action", "paused_for_callbacks");
                        AgentTracingService.SetSuccess(pauseHandlingActivity, "Conversation paused for pending agent callbacks");
                        
                        iterationActivity?.SetTag("iteration.result", "paused");
                        AgentTracingService.SetSuccess(iterationActivity, $"Conversation iteration {iteration} paused for {functionCallResult.PendingCallsRequiringWait} callbacks");
                        
                        functionCallLoopActivity?.SetTag("function_loop.iterations_completed", iteration);
                        functionCallLoopActivity?.SetTag("function_loop.result", "paused");
                        AgentTracingService.SetSuccess(functionCallLoopActivity, $"Conversation function call loop paused after {iteration} iterations");
                        
                        conversationActivity?.SetTag("conversation.result", "paused");
                        conversationActivity?.SetTag("conversation.iterations", iteration);
                        AgentTracingService.SetSuccess(conversationActivity, pauseMessage);
                        
                        return pauseMessage;
                    }

                    // Phase 4.3: Check if we should continue or break
                    if (!functionCallResult.ChatHistoryUpdated && !string.IsNullOrEmpty(result.Content))
                    {
                        iterationActivity?.SetTag("iteration.result", "completed_with_content");
                        AgentTracingService.SetSuccess(iterationActivity, $"Conversation iteration {iteration} completed with final content");
                        break;
                    }

                    // Phase 4.4: Get next response from LLM after function results
                    if (functionCallResult.ChatHistoryUpdated)
                    {
                        using var nextLLMActivity = AgentTracingService.StartKernelActivity($"ConversationLLMCall_Iteration_{iteration}", state.AgentId);
                        nextLLMActivity?.SetTag("llm_call.type", "conversation_continuation");
                        nextLLMActivity?.SetTag("llm_call.iteration", iteration);
                        nextLLMActivity?.SetTag("llm_call.history_count", chatHistory.Count);
                        
                        var nextResult = await chatService.GetChatMessageContentAsync(
                            chatHistory,
                            executionSettings,
                            kernel);
                        
                        result = nextResult;
                        
                        var nextHasFunctionCalls = FunctionCallContent.GetFunctionCalls(result).Any();
                        nextLLMActivity?.SetTag("llm_call.has_function_calls", nextHasFunctionCalls);
                        nextLLMActivity?.SetTag("llm_call.response_length", result.Content?.Length ?? 0);
                        AgentTracingService.SetSuccess(nextLLMActivity, $"Conversation continuation response: {result.Content?.Length ?? 0} chars, {(nextHasFunctionCalls ? "with" : "no")} function calls");
                        
                        // If this result has content and no function calls, we're done
                        if (!string.IsNullOrEmpty(result.Content) && !nextHasFunctionCalls)
                        {
                            iterationActivity?.SetTag("iteration.result", "completed_final");
                            AgentTracingService.SetSuccess(iterationActivity, $"Conversation iteration {iteration} completed - final response received");
                            break;
                        }
                        
                        iterationActivity?.SetTag("iteration.result", "continued");
                        AgentTracingService.SetSuccess(iterationActivity, $"Conversation iteration {iteration} continued - more function calls to process");
                    }
                    else
                    {
                        // No chat history update means no function calls were processed
                        iterationActivity?.SetTag("iteration.result", "no_updates");
                        AgentTracingService.SetSuccess(iterationActivity, $"Conversation iteration {iteration} - no function calls processed");
                        break;
                    }
                }
                
                functionCallLoopActivity?.SetTag("function_loop.iterations_completed", iteration);
                if (iteration >= maxIterations)
                {
                    _logger.LogWarning("Reached maximum function call iterations ({MaxIterations}) for conversation", maxIterations);
                    functionCallLoopActivity?.SetTag("function_loop.result", "max_iterations_reached");
                    AgentTracingService.SetSuccess(functionCallLoopActivity, $"Conversation function call loop completed: reached max iterations ({maxIterations})");
                }
                else
                {
                    functionCallLoopActivity?.SetTag("function_loop.result", "completed_naturally");
                    AgentTracingService.SetSuccess(functionCallLoopActivity, $"Conversation function call loop completed naturally after {iteration} iterations");
                }
            }
            else
            {
                // Fallback to adding the result directly if no manual processor
                chatHistory.Add(result);
                _logger.LogWarning("Manual function call processor not available, using basic processing");
                
                conversationActivity?.SetTag("conversation.fallback_processing", true);
            }

            // Phase 5: Finalize conversation response
            using var responseFinalizationActivity = AgentTracingService.StartKernelActivity("FinalizeConversationResponse", state.AgentId);
            
            var response = result.Content ?? "No response generated.";

            // Add assistant response to chat history and state
            state.AddChatMessage("assistant", response);

            // Update working memory
            state.WorkingMemory["last_user_message"] = userMessage;
            state.WorkingMemory["last_assistant_response"] = response;
            state.WorkingMemory["conversation_timestamp"] = DateTime.UtcNow.ToString("O");
            
            responseFinalizationActivity?.SetTag("response.length", response.Length);
            responseFinalizationActivity?.SetTag("response.working_memory_updated", true);
            AgentTracingService.SetSuccess(responseFinalizationActivity, $"Conversation response finalized: {response.Length} chars");

            _logger.LogInformation("Conversation continued successfully");
            
            conversationActivity?.SetTag("conversation.result", "success");
            conversationActivity?.SetTag("conversation.response_length", response.Length);
            AgentTracingService.SetSuccess(conversationActivity, $"Conversation continued successfully: {response.Length} chars response");
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing conversation with configurable kernel");
            
            conversationActivity?.SetTag("conversation.result", "error");
            AgentTracingService.SetError(conversationActivity, ex);
            throw;
        }
    }

    public async Task<List<(string FunctionName, string Description, List<string> Parameters)>> GetKernelFunctionMetadataAsync(Kernel kernel)
    {
        var functionMetadata = new List<(string FunctionName, string Description, List<string> Parameters)>();

        try
        {
            foreach (var plugin in kernel.Plugins)
            {
                foreach (var function in plugin)
                {
                    var parameters = function.Metadata.Parameters
                        .Select(p => $"{p.Name} ({p.ParameterType?.Name ?? "object"}): {p.Description}")
                        .ToList();

                    functionMetadata.Add((
                        FunctionName: $"{plugin.Name}.{function.Name}",
                        Description: function.Description ?? "No description available",
                        Parameters: parameters
                    ));
                }
            }

            _logger.LogInformation("Retrieved metadata for {Count} functions", functionMetadata.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving kernel function metadata");
        }

        return functionMetadata;
    }

    public async Task<(bool IsValid, string ErrorMessage)> ValidateConfigurationAsync(AgentConfiguration configuration)
    {
        try
        {
            // Validate system prompt
            if (string.IsNullOrWhiteSpace(configuration.SystemPrompt))
            {
                return (false, "System prompt cannot be empty");
            }

            // Validate model configuration
            if (string.IsNullOrWhiteSpace(configuration.Model.ModelId))
            {
                return (false, "Model ID cannot be empty");
            }

            // Check for API key
            var apiKey = configuration.Model.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                return (false, "OpenAI API key is required");
            }

            // Validate temperature range
            if (configuration.Temperature < 0.0 || configuration.Temperature > 2.0)
            {
                return (false, "Temperature must be between 0.0 and 2.0");
            }

            // Validate max tokens
            if (configuration.MaxTokens <= 0 || configuration.MaxTokens > 128000)
            {
                return (false, "MaxTokens must be between 1 and 128000");
            }

            // Validate agent name
            if (string.IsNullOrWhiteSpace(configuration.AgentName))
            {
                return (false, "Agent name cannot be empty");
            }

            _logger.LogInformation("Configuration validation passed for agent: {AgentName}", configuration.AgentName);
            return (true, "Configuration is valid");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration");
            return (false, $"Validation error: {ex.Message}");
        }
    }

    public IEnumerable<string> GetAvailableFunctionNames()
    {
        return _functionRegistry.GetAvailableFunctionNames();
    }

    public IEnumerable<string> GetAvailablePluginNames()
    {
        return _functionRegistry.GetAvailablePluginNames();
    }

    public IEnumerable<string> GetAllAvailableToolNames()
    {
        return _functionRegistry.GetAllAvailableToolNames();
    }
} 
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
        try
        {
            _logger.LogInformation("Executing task with configurable kernel: {Task} (waitForCompletion: {WaitForCompletion})", task, waitForCompletion);

            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = state.ToSemanticKernelChatHistory();

            // Add system message if this is a fresh conversation
            if (chatHistory.Count == 0)
            {
                chatHistory.AddSystemMessage(systemPrompt);
                state.AddChatMessage("system", systemPrompt);
            }

            // Add the user task
            chatHistory.AddUserMessage(task);
            state.AddChatMessage("user", task);

            // Configure execution settings for manual function calling
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                // Enable function calling but don't auto-invoke - we'll handle manually
                ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions,
                MaxTokens = state.Configuration?.MaxTokens ?? 4000,
                Temperature = state.Configuration?.Temperature ?? 0.1
            };

            // Get initial response from LLM (may contain function calls)
            var result = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                kernel);

            // Check if manual function call processor is available
            if (_manualFunctionCallProcessor != null && state.Configuration != null)
            {
                // Process function calls in a loop until we get a final response with content
                const int maxIterations = 10; // Prevent infinite loops
                int iteration = 0;
                
                while (iteration < maxIterations)
                {
                    iteration++;
                    
                    // Process any function calls manually
                    var functionCallResult = await _manualFunctionCallProcessor.ProcessFunctionCallsAsync(
                        result,
                        kernel,
                        state.AgentId,
                        chatHistory);

                    if (!functionCallResult.Success)
                    {
                        var errorMessage = $"Function call processing failed: {string.Join(", ", functionCallResult.ErrorMessages)}";
                        _logger.LogError(errorMessage);
                        return errorMessage;
                    }

                    // If there were pending agent calls, inform the user
                    if (functionCallResult.PendingAgentCalls.Any())
                    {
                        _logger.LogInformation("Initiated {Count} non-blocking agent calls", 
                            functionCallResult.PendingAgentCalls.Count);
                    }

                    // Check if we should pause LLM execution for pending agent calls
                    if (functionCallResult.ShouldPauseLLMExecution)
                    {
                        _logger.LogInformation("Pausing LLM execution, waiting for {Count} agent callbacks", 
                            functionCallResult.PendingCallsRequiringWait);
                        
                        // Update state to track pending calls
                        state.WorkingMemory["pending_agent_calls"] = functionCallResult.PendingCallsRequiringWait.ToString();
                        state.WorkingMemory["execution_paused"] = "true";
                        state.WorkingMemory["pause_timestamp"] = DateTime.UtcNow.ToString("O");

                        // If waitForCompletion is false (user-interactive), return pause message immediately
                        if (!waitForCompletion)
                        {
                            var pauseMessage = $"Execution paused - waiting for {functionCallResult.PendingCallsRequiringWait} agent callback(s) to complete.";
                            state.AddChatMessage("system", pauseMessage);
                            return pauseMessage;
                        }
                        
                        // If waitForCompletion is true (agent-to-agent calls), wait for callbacks
                        _logger.LogInformation("Waiting for agent callbacks to complete before returning result");
                        
                        // Wait for all callbacks to complete by checking the WorkingMemory
                        await WaitForCallbacksToCompleteAsync(state, functionCallResult.PendingCallsRequiringWait);
                        
                        // After callbacks complete, get the final result from chat history
                        var finalResult = await chatService.GetChatMessageContentAsync(
                            state.ToSemanticKernelChatHistory(),
                            executionSettings,
                            kernel);
                        
                        result = finalResult;
                        
                        // Continue processing in case there are more function calls
                        continue;
                    }

                    // If no function calls were processed and we have content, we're done
                    if (!functionCallResult.ChatHistoryUpdated && !string.IsNullOrEmpty(result.Content))
                    {
                        break;
                    }

                    // Get next response from LLM after function results
                    if (functionCallResult.ChatHistoryUpdated)
                    {
                        var nextResult = await chatService.GetChatMessageContentAsync(
                            chatHistory,
                            executionSettings,
                            kernel);
                        
                        result = nextResult;
                        
                        // If this result has content and no function calls, we're done
                        if (!string.IsNullOrEmpty(result.Content) && 
                            !FunctionCallContent.GetFunctionCalls(result).Any())
                        {
                            break;
                        }
                    }
                    else
                    {
                        // No chat history update means no function calls were processed
                        break;
                    }
                }
                
                if (iteration >= maxIterations)
                {
                    _logger.LogWarning("Reached maximum function call iterations ({MaxIterations}) for task execution", maxIterations);
                }
            }
            else
            {
                // Fallback to adding the result directly if no manual processor
                chatHistory.Add(result);
                _logger.LogWarning("Manual function call processor not available, using basic processing");
            }

            var response = result.Content ?? "Task completed but no response generated.";

            // Add assistant response to chat history and state
            state.AddChatMessage("assistant", response);

            // Update working memory
            state.WorkingMemory["last_task"] = task;
            state.WorkingMemory["last_response"] = response;
            state.WorkingMemory["execution_timestamp"] = DateTime.UtcNow.ToString("O");

            _logger.LogInformation("Task execution completed successfully");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task with configurable kernel");
            var errorMessage = $"Task execution failed: {ex.Message}";
            return errorMessage;
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
        try
        {
            _logger.LogInformation("Continuing conversation with configurable kernel: {Message}", userMessage);

            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = state.ToSemanticKernelChatHistory();

            // Add system message if no chat history exists
            if (chatHistory.Count == 0)
            {
                chatHistory.AddSystemMessage(systemPrompt);
                state.AddChatMessage("system", systemPrompt);
            }

            // Add the user message
            chatHistory.AddUserMessage(userMessage);
            state.AddChatMessage("user", userMessage);

            // Configure execution settings for manual function calling
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                // Enable function calling but don't auto-invoke - we'll handle manually
                ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions,
                MaxTokens = state.Configuration?.MaxTokens ?? 4000,
                Temperature = state.Configuration?.Temperature ?? 0.1
            };

            // Get initial response from LLM (may contain function calls)
            var result = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                kernel);

            // Check if manual function call processor is available
            if (_manualFunctionCallProcessor != null && state.Configuration != null)
            {
                // Process function calls in a loop until we get a final response with content
                const int maxIterations = 10; // Prevent infinite loops
                int iteration = 0;
                
                while (iteration < maxIterations)
                {
                    iteration++;
                    
                    // Process any function calls manually
                    var functionCallResult = await _manualFunctionCallProcessor.ProcessFunctionCallsAsync(
                        result,
                        kernel,
                        state.AgentId,
                        chatHistory);

                    if (!functionCallResult.Success)
                    {
                        var errorMessage = $"Function call processing failed: {string.Join(", ", functionCallResult.ErrorMessages)}";
                        _logger.LogError(errorMessage);
                        return errorMessage;
                    }

                    // If there were pending agent calls, inform the user
                    if (functionCallResult.PendingAgentCalls.Any())
                    {
                        _logger.LogInformation("Initiated {Count} non-blocking agent calls", 
                            functionCallResult.PendingAgentCalls.Count);
                    }

                    // Check if we should pause LLM execution for pending agent calls
                    if (functionCallResult.ShouldPauseLLMExecution)
                    {
                        _logger.LogInformation("Pausing LLM execution, waiting for {Count} agent callbacks", 
                            functionCallResult.PendingCallsRequiringWait);
                        
                        // Update state to track pending calls
                        state.WorkingMemory["pending_agent_calls"] = functionCallResult.PendingCallsRequiringWait.ToString();
                        state.WorkingMemory["execution_paused"] = "true";
                        state.WorkingMemory["pause_timestamp"] = DateTime.UtcNow.ToString("O");

                        // Return special message indicating execution is paused
                        var pauseMessage = $"Execution paused - waiting for {functionCallResult.PendingCallsRequiringWait} agent callback(s) to complete.";
                        state.AddChatMessage("system", pauseMessage);
                        return pauseMessage;
                    }

                    // If no function calls were processed and we have content, we're done
                    if (!functionCallResult.ChatHistoryUpdated && !string.IsNullOrEmpty(result.Content))
                    {
                        break;
                    }

                    // Get next response from LLM after function results
                    if (functionCallResult.ChatHistoryUpdated)
                    {
                        var nextResult = await chatService.GetChatMessageContentAsync(
                            chatHistory,
                            executionSettings,
                            kernel);
                        
                        result = nextResult;
                        
                        // If this result has content and no function calls, we're done
                        if (!string.IsNullOrEmpty(result.Content) && 
                            !FunctionCallContent.GetFunctionCalls(result).Any())
                        {
                            break;
                        }
                    }
                    else
                    {
                        // No chat history update means no function calls were processed
                        break;
                    }
                }
                
                if (iteration >= maxIterations)
                {
                    _logger.LogWarning("Reached maximum function call iterations ({MaxIterations}) for conversation", maxIterations);
                }
            }
            else
            {
                // Fallback to adding the result directly if no manual processor
                chatHistory.Add(result);
                _logger.LogWarning("Manual function call processor not available, using basic processing");
            }

            var response = result.Content ?? "No response generated.";

            // Add assistant response to chat history and state
            state.AddChatMessage("assistant", response);

            // Update working memory
            state.WorkingMemory["last_user_message"] = userMessage;
            state.WorkingMemory["last_assistant_response"] = response;
            state.WorkingMemory["conversation_timestamp"] = DateTime.UtcNow.ToString("O");

            _logger.LogInformation("Conversation continued successfully");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing conversation with configurable kernel");
            var errorMessage = $"Conversation failed: {ex.Message}";
            return errorMessage;
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
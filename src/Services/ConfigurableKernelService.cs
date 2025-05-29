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

    public ConfigurableKernelService(
        IKernelFunctionRegistry functionRegistry, 
        ILogger<ConfigurableKernelService> logger)
    {
        _functionRegistry = functionRegistry;
        _logger = logger;
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

    public async Task<string> ExecuteTaskAsync(Kernel kernel, string task, ConfigurableAgentState state, string systemPrompt)
    {
        try
        {
            _logger.LogInformation("Executing task with configurable kernel: {Task}", task);

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

            // Configure execution settings
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = state.Configuration?.MaxTokens ?? 4000,
                Temperature = state.Configuration?.Temperature ?? 0.1
            };

            // Execute with automatic function calling
            var result = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                kernel);

            var response = result.Content ?? "Task completed but no response generated.";

            // Add assistant response to chat history
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

            // Configure execution settings
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = state.Configuration?.MaxTokens ?? 4000,
                Temperature = state.Configuration?.Temperature ?? 0.1
            };

            // Execute with automatic function calling
            var result = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                kernel);

            var response = result.Content ?? "No response generated.";

            // Add assistant response to chat history
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
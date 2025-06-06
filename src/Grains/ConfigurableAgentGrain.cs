using Orleans;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using PsiOrleans.Models;
using PsiOrleans.Services;
using System.Threading.Tasks;
using System.Threading;

namespace PsiOrleans.Grains;

/// <summary>
/// Orleans Grain implementation of a configurable agent that can be initialized with custom prompts and tools
/// </summary>
public class ConfigurableAgentGrain : Grain, IConfigurableAgentGrain
{
    private readonly IConfigurableKernelService _kernelService;
    private readonly ILogger<ConfigurableAgentGrain> _logger;
    
    // ====== Phase 2 Refactoring: State Machine Services ======
    private readonly IStateMachineFactory _stateMachineFactory;
    private readonly IAgentRoleConfigurator _roleConfigurator;
    
    private ConfigurableAgentState _state = new();
    private Kernel? _kernel;

    public ConfigurableAgentGrain(
        IConfigurableKernelService kernelService, 
        ILogger<ConfigurableAgentGrain> logger,
        IStateMachineFactory stateMachineFactory,
        IAgentRoleConfigurator roleConfigurator)
    {
        _kernelService = kernelService;
        _logger = logger;
        _stateMachineFactory = stateMachineFactory;
        _roleConfigurator = roleConfigurator;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _state.AgentId = this.GetPrimaryKeyString();
        _logger.LogInformation("ConfigurableAgent {AgentId} activated", _state.AgentId);
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<(bool Success, string Message, string AgentId)> InitializeAsync(
        AgentConfiguration configuration,
        IEnumerable<string>? functionNames = null,
        IEnumerable<string>? pluginNames = null)
    {
        try
        {
            _logger.LogInformation("Initializing ConfigurableAgent {AgentId} with configuration", _state.AgentId);

            // Validate configuration
            var (isValid, errorMessage) = await _kernelService.ValidateConfigurationAsync(configuration);
            if (!isValid)
            {
                _logger.LogError("Configuration validation failed for agent {AgentId}: {Error}", _state.AgentId, errorMessage);
                return (false, $"Configuration validation failed: {errorMessage}", _state.AgentId);
            }

            // Create kernel with the provided configuration and named tools
            _kernel = await _kernelService.CreateKernelAsync(configuration, functionNames, pluginNames);
            
            // Store configuration in state
            _state.Configuration = configuration;
            _state.IsInitialized = true;
            _state.InitializedAt = DateTime.UtcNow;
            _state.LastUpdated = DateTime.UtcNow;

            // Get available functions metadata
            _state.AvailableFunctions = await GetFunctionMetadata(_kernel);

            var functionCount = _state.AvailableFunctions.Count;
            var pluginCount = pluginNames?.Count() ?? 0;
            var individualFunctionCount = functionNames?.Count() ?? 0;

            _logger.LogInformation("ConfigurableAgent {AgentId} initialized successfully with {FunctionCount} total functions ({PluginCount} plugins, {IndividualCount} individual functions)", 
                _state.AgentId, functionCount, pluginCount, individualFunctionCount);

            return (true, $"Agent '{configuration.AgentName}' initialized successfully with {functionCount} functions ({pluginCount} plugins, {individualFunctionCount} individual functions)", _state.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing ConfigurableAgent {AgentId}", _state.AgentId);
            return (false, $"Initialization failed: {ex.Message}", _state.AgentId);
        }
    }

    public async Task<(bool Success, string Message, string AgentId)> InitializeAsync(
        AgentConfiguration configuration,
        IEnumerable<string>? toolNames)
    {
        try
        {
            _logger.LogInformation("Initializing ConfigurableAgent {AgentId} with unified tools", _state.AgentId);

            // Validate configuration
            var (isValid, errorMessage) = await _kernelService.ValidateConfigurationAsync(configuration);
            if (!isValid)
            {
                _logger.LogError("Configuration validation failed for agent {AgentId}: {Error}", _state.AgentId, errorMessage);
                return (false, $"Configuration validation failed: {errorMessage}", _state.AgentId);
            }

            // Store the original tool names before kernel transformation
            _state.OriginalToolNames = toolNames?.ToList() ?? new List<string>();
            
            // Create kernel with the specified tools
            _kernel = await _kernelService.CreateKernelAsync(configuration, toolNames);
            if (_kernel == null)
            {
                return (false, "Failed to create kernel", string.Empty);
            }
            
            // Store configuration in state
            _state.Configuration = configuration;
            _state.IsInitialized = true;
            _state.InitializedAt = DateTime.UtcNow;
            _state.LastUpdated = DateTime.UtcNow;

            // Get available functions metadata
            _state.AvailableFunctions = await GetFunctionMetadata(_kernel);

            var functionCount = _state.AvailableFunctions.Count;
            var toolCount = toolNames?.Count() ?? 0;

            _logger.LogInformation("ConfigurableAgent {AgentId} initialized successfully with {FunctionCount} total functions from {ToolCount} specified tools", 
                _state.AgentId, functionCount, toolCount);

            return (true, $"Agent '{configuration.AgentName}' initialized successfully with {functionCount} functions from {toolCount} tools", _state.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing ConfigurableAgent {AgentId}", _state.AgentId);
            return (false, $"Initialization failed: {ex.Message}", _state.AgentId);
        }
    }

    public async Task<string> ExecuteTaskAsync(string task)
    {
        if (!_state.IsInitialized || _kernel == null || _state.Configuration == null)
        {
            var errorMsg = "Agent is not initialized. Please call InitializeAsync first.";
            _logger.LogError(errorMsg);
            return errorMsg;
        }

        _logger.LogInformation("ConfigurableAgent {AgentId} executing task: {Task}", _state.AgentId, task);

        var startTime = DateTime.UtcNow;

        try
        {
            // For user-interactive calls, use waitForCompletion = false to maintain pause-resume behavior
            var result = await _kernelService.ExecuteTaskAsync(_kernel, task, _state, _state.Configuration.SystemPrompt, waitForCompletion: false);
            
            var executionTime = DateTime.UtcNow - startTime;
            _state.AddExecutionTime(executionTime);
            _state.IncrementSuccessfulTasks();

            _logger.LogInformation("ConfigurableAgent {AgentId} completed task in {Duration}ms", 
                _state.AgentId, executionTime.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - startTime;
            _state.AddExecutionTime(executionTime);
            _state.IncrementFailedTasks();

            _logger.LogError(ex, "Error executing task for ConfigurableAgent {AgentId}", _state.AgentId);
            var errorMessage = $"Task execution failed: {ex.Message}";
            return errorMessage;
        }
    }

    public async Task<string> ContinueConversationAsync(string userMessage)
    {
        if (!_state.IsInitialized || _kernel == null || _state.Configuration == null)
        {
            var errorMsg = "Agent is not initialized. Please call InitializeAsync first.";
            _logger.LogError(errorMsg);
            return errorMsg;
        }

        _logger.LogInformation("ConfigurableAgent {AgentId} continuing conversation: {Message}", _state.AgentId, userMessage);

        try
        {
            var result = await _kernelService.ContinueConversationAsync(_kernel, userMessage, _state, _state.Configuration.SystemPrompt);
            
            _logger.LogInformation("ConfigurableAgent {AgentId} continued conversation successfully", _state.AgentId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing conversation for ConfigurableAgent {AgentId}", _state.AgentId);
            var errorMessage = $"Conversation failed: {ex.Message}";
            return errorMessage;
        }
    }

    public Task<ConfigurableAgentState> GetStateAsync()
    {
        return Task.FromResult(_state);
    }

    public Task<AgentConfiguration?> GetConfigurationAsync()
    {
        return Task.FromResult(_state.Configuration);
    }

    public Task<bool> IsInitializedAsync()
    {
        return Task.FromResult(_state.IsInitialized);
    }

    public Task<bool> ResetAsync()
    {
        try
        {
            _state = new ConfigurableAgentState
            {
                AgentId = this.GetPrimaryKeyString()
            };
            _kernel = null;
            
            _logger.LogInformation("ConfigurableAgent {AgentId} reset successfully", _state.AgentId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting ConfigurableAgent {AgentId}", _state.AgentId);
            return Task.FromResult(false);
        }
    }

    public Task<(int TotalTasks, int SuccessfulTasks, int FailedTasks, TimeSpan TotalExecutionTime)> GetMetricsAsync()
    {
        return Task.FromResult((
            TotalTasks: _state.TotalTasks,
            SuccessfulTasks: _state.SuccessfulTasks,
            FailedTasks: _state.FailedTasks,
            TotalExecutionTime: _state.TotalExecutionTime
        ));
    }

    public async Task<List<(string FunctionName, string Description, List<string> Parameters)>> GetAvailableToolsAsync()
    {
        if (!_state.IsInitialized || _kernel == null)
        {
            return new List<(string FunctionName, string Description, List<string> Parameters)>();
        }

        try
        {
            return await _kernelService.GetKernelFunctionMetadataAsync(_kernel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available tools for ConfigurableAgent {AgentId}", _state.AgentId);
            return new List<(string FunctionName, string Description, List<string> Parameters)>();
        }
    }

    public Task<List<ChatMessage>> GetChatHistoryAsync()
    {
        return Task.FromResult(_state.ChatHistory);
    }

    public Task<bool> ClearChatHistoryAsync()
    {
        try
        {
            _state.ClearChatHistory();
            _logger.LogInformation("ConfigurableAgent {AgentId} cleared chat history", _state.AgentId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing chat history for ConfigurableAgent {AgentId}", _state.AgentId);
            return Task.FromResult(false);
        }
    }

    public Task<List<string>> GetAvailableFunctionNamesAsync()
    {
        try
        {
            var functionNames = _kernelService.GetAvailableFunctionNames().ToList();
            return Task.FromResult(functionNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available function names for ConfigurableAgent {AgentId}", _state.AgentId);
            return Task.FromResult(new List<string>());
        }
    }

    public Task<List<string>> GetAvailablePluginNamesAsync()
    {
        try
        {
            var pluginNames = _kernelService.GetAvailablePluginNames().ToList();
            return Task.FromResult(pluginNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available plugin names for ConfigurableAgent {AgentId}", _state.AgentId);
            return Task.FromResult(new List<string>());
        }
    }

    public Task<List<string>> GetAllAvailableToolNamesAsync()
    {
        try
        {
            var toolNames = _kernelService.GetAllAvailableToolNames().ToList();
            return Task.FromResult(toolNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all available tool names for ConfigurableAgent {AgentId}", _state.AgentId);
            return Task.FromResult(new List<string>());
        }
    }

    private async Task<List<AgentFunctionInfo>> GetFunctionMetadata(Kernel kernel)
    {
        var metadata = new List<AgentFunctionInfo>();
        
        try
        {
            foreach (var plugin in kernel.Plugins)
            {
                foreach (var function in plugin)
                {
                    var isAgentComm = plugin.Name == "AgentCommunication" || function.Name.StartsWith("Call_");
                    var functionInfo = AgentFunctionInfo.FromKernelFunctionMetadata(
                        function.Metadata, 
                        plugin.Name, 
                        isAgentComm);
                    metadata.Add(functionInfo);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function metadata for ConfigurableAgent {AgentId}", _state.AgentId);
        }

        return metadata;
    }
    
    // New methods for callable agent management
    
    public async Task<(bool Success, string Message)> SetCallableAgentsAsync(IEnumerable<CallableAgent> callableAgents)
    {
        try
        {
            var agentList = callableAgents.ToList();
            
            _logger.LogInformation("Setting callable agents for {AgentId}: {AgentInfo}", 
                _state.AgentId, 
                string.Join(", ", agentList.Select(a => $"{a.Name} ({a.Id})")));
            
            // Update the state
            _state.CallableAgents = agentList;
            _state.LastUpdated = DateTime.UtcNow;
            
            // Initialize agent function registry if not already done
            if (_state.AgentFunctionRegistry == null)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<AgentFunctionRegistry>>();
                _state.AgentFunctionRegistry = new AgentFunctionRegistry(logger);
            }
            
            // Clear existing agent functions
            _state.AgentFunctionRegistry.ClearAgentFunctions();
            
            // Create agent communication functions for each callable agent
            await CreateAgentCommunicationFunctions(agentList);
            
            // IMPORTANT: Refresh the kernel to include new agent communication functions
            await RefreshKernelWithAgentFunctions();
            
            _logger.LogInformation("Successfully set {Count} callable agents for {AgentId}", agentList.Count, _state.AgentId);
            
            return (true, $"Successfully set {agentList.Count} callable agents: {string.Join(", ", agentList.Select(a => a.Name))}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting callable agents for {AgentId}", _state.AgentId);
            return (false, $"Failed to set callable agents: {ex.Message}");
        }
    }
    
    public Task<List<CallableAgent>> GetCallableAgentsAsync()
    {
        return Task.FromResult(_state.CallableAgents.ToList());
    }
    
    public async Task<(bool Success, string Message)> AddCallableAgentAsync(CallableAgent callableAgent)
    {
        try
        {
            if (callableAgent == null)
            {
                return (false, "Callable agent cannot be null");
            }
            
            if (string.IsNullOrWhiteSpace(callableAgent.Id))
            {
                return (false, "Agent ID cannot be null or empty");
            }
            
            if (_state.CallableAgents.Any(a => a.Id == callableAgent.Id))
            {
                return (false, $"Agent {callableAgent.Id} is already in the callable agents list");
            }
            
            _state.CallableAgents.Add(callableAgent);
            _state.LastUpdated = DateTime.UtcNow;
            
            // Initialize agent function registry if needed
            if (_state.AgentFunctionRegistry == null)
            {
                var logger = ServiceProvider.GetRequiredService<ILogger<AgentFunctionRegistry>>();
                _state.AgentFunctionRegistry = new AgentFunctionRegistry(logger);
            }
            
            // Create communication function for this agent
            await CreateAgentCommunicationFunction(callableAgent);
            
            // Refresh kernel to include the new agent function
            await RefreshKernelWithAgentFunctions();
            
            _logger.LogInformation("Added callable agent {AgentName} ({AgentId}) to {OwnerAgentId}", 
                callableAgent.Name, callableAgent.Id, _state.AgentId);
            
            return (true, $"Successfully added callable agent: {callableAgent.Name} ({callableAgent.Id})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding callable agent {AgentId} to {OwnerAgentId}", callableAgent?.Id, _state.AgentId);
            return (false, $"Failed to add callable agent: {ex.Message}");
        }
    }
    
    public async Task<(bool Success, string Message)> RemoveCallableAgentAsync(string agentId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentId))
            {
                return (false, "Agent ID cannot be null or empty");
            }
            
            var agentToRemove = _state.CallableAgents.FirstOrDefault(a => a.Id == agentId);
            if (agentToRemove == null)
            {
                return (false, $"Agent {agentId} is not in the callable agents list");
            }
            
            _state.CallableAgents.Remove(agentToRemove);
            _state.LastUpdated = DateTime.UtcNow;
            
            // Remove the agent communication function
            var functionName = agentToRemove.GetFunctionName();
            _state.AgentFunctionRegistry?.RemoveAgentFunction(functionName);
            
            // Refresh kernel to remove the agent function
            await RefreshKernelWithAgentFunctions();
            
            _logger.LogInformation("Removed callable agent {AgentName} ({AgentId}) from {OwnerAgentId}", 
                agentToRemove.Name, agentId, _state.AgentId);
            
            return (true, $"Successfully removed callable agent: {agentToRemove.Name} ({agentId})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing callable agent {AgentId} from {OwnerAgentId}", agentId, _state.AgentId);
            return (false, $"Failed to remove callable agent: {ex.Message}");
        }
    }
    
    public Task<List<string>> GetAvailableAgentFunctionNamesAsync()
    {
        var functionNames = _state.AgentFunctionRegistry?.GetAvailableAgentFunctionNames().ToList() ?? new List<string>();
        return Task.FromResult(functionNames);
    }
    
    public Task<bool> CanCallAgentAsync(string agentId)
    {
        return Task.FromResult(_state.CallableAgents.Any(a => a.Id == agentId));
    }
    
    private async Task CreateAgentCommunicationFunctions(IEnumerable<CallableAgent> callableAgents)
    {
        foreach (var agent in callableAgents)
        {
            await CreateAgentCommunicationFunction(agent);
        }
    }
    
    private Task CreateAgentCommunicationFunction(CallableAgent callableAgent)
    {
        try
        {
            // Use the agent's GetFunctionName method for consistency
            var functionName = callableAgent.GetFunctionName();
            
            // Capture the Orleans TaskScheduler during function creation
            var orleansScheduler = TaskScheduler.Current;
            
            // Create a kernel function that maintains Orleans activation context
            var agentCallFunction = KernelFunctionFactory.CreateFromMethod(
                async (string query) => 
                {
                    // Use TaskFactory.StartNew to run the grain call in Orleans context
                    return await Task.Factory.StartNew(async () =>
                    {
                        // This runs in the Orleans activation context
                        // Call the target grain directly (not through self to avoid deadlock)
                        var targetAgent = GrainFactory.GetGrain<IConfigurableAgentGrain>(callableAgent.Id);
                        
                        // Check if the target agent is initialized
                        var isInitialized = await targetAgent.IsInitializedAsync();
                        if (!isInitialized)
                        {
                            return $"Error: Agent {callableAgent.Id} is not initialized";
                        }
                        
                        // Execute the task on the target agent
                        var result = await targetAgent.ExecuteTaskAsync(query);
                        
                        _logger.LogInformation("Successfully called agent {AgentId} from {OwnerAgentId}", callableAgent.Id, _state.AgentId);
                        
                        return result;
                    }, 
                    CancellationToken.None, 
                    TaskCreationOptions.None, 
                    orleansScheduler).Unwrap();
                },
                functionName,
                $"Call '{callableAgent.Name}' agent: {callableAgent.Description}");
            
            _state.AgentFunctionRegistry?.RegisterAgentFunction(functionName, agentCallFunction);
            
            _logger.LogDebug("Created agent communication function: {FunctionName} for {AgentName} ({AgentId})", 
                functionName, callableAgent.Name, callableAgent.Id);
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agent communication function for {AgentName} ({AgentId})", 
                callableAgent.Name, callableAgent.Id);
            return Task.CompletedTask;
        }
    }
    
    private async Task RefreshKernelWithAgentFunctions()
    {
        try
        {
            _logger.LogInformation("Refreshing kernel for {AgentId} to include new agent communication functions", _state.AgentId);

            if (_state.Configuration == null)
            {
                _logger.LogWarning("Cannot refresh kernel: agent configuration is null");
                return;
            }

            // Get original tool names from the function metadata instead of extracting from kernel plugins
            // This preserves the original format like "Math.Add", "Tavily.search" instead of "CustomFunctions.Add"
            var originalToolNames = _state.OriginalToolNames
                .Where(name => !string.IsNullOrEmpty(name)) // Filter out any empty names
                .ToList();

            _logger.LogDebug("Using {Count} original tool names: {Tools}", 
                originalToolNames.Count, string.Join(", ", originalToolNames));

            // Configure kernel with role-appropriate tools using original names
            _kernel = await _roleConfigurator.ConfigureKernelAsync(_state.Role, _state.Configuration, originalToolNames);

            // Add agent communication functions as a separate plugin if they exist
            if (_state.AgentFunctionRegistry != null && _state.AgentFunctionRegistry.GetAgentFunctionCount() > 0)
            {
                var agentFunctions = _state.AgentFunctionRegistry.GetAllAgentFunctions().Values.ToList();
                
                if (agentFunctions.Any())
                {
                    // Create a plugin from the agent communication functions
                    var agentCommunicationPlugin = KernelPluginFactory.CreateFromFunctions(
                        "AgentCommunication", 
                        "Functions for calling other agents",
                        agentFunctions);
                    
                    // Add the plugin to the kernel
                    _kernel.Plugins.Add(agentCommunicationPlugin);
                    
                    _logger.LogInformation("Added {AgentFunctionCount} agent communication functions to kernel", agentFunctions.Count);
                }
            }

            // Update available functions metadata to include the new functions
            _state.AvailableFunctions = await GetFunctionMetadata(_kernel);

            _logger.LogInformation("Kernel for {AgentId} refreshed successfully with {TotalCount} total functions", 
                _state.AgentId, _state.AvailableFunctions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing kernel for ConfigurableAgent {AgentId}", _state.AgentId);
        }
    }
    
    public async Task ReceiveCallbackAsync(string callId, string message, bool isSuccess)
    {
        try
        {
            _logger.LogInformation("Received callback for call {CallId} on agent {AgentId}: Success={IsSuccess}", 
                callId, _state.AgentId, isSuccess);

            // Add the callback message to the chat history as a system message
            var callbackMessage = $"[CALLBACK] {message}";
            _state.AddChatMessage("system", callbackMessage);

            // Update metrics
            if (isSuccess)
            {
                _state.SuccessfulTasks++;
            }
            else
            {
                _state.FailedTasks++;
            }

            // Check if execution was paused and if we should continue
            if (_state.WorkingMemory.ContainsKey("execution_paused") && 
                _state.WorkingMemory["execution_paused"]?.ToString() == "true")
            {
                // Decrement pending calls counter
                if (_state.WorkingMemory.ContainsKey("pending_agent_calls"))
                {
                    var pendingCallsValue = _state.WorkingMemory["pending_agent_calls"]?.ToString();
                    if (!string.IsNullOrEmpty(pendingCallsValue) && int.TryParse(pendingCallsValue, out var pendingCount))
                    {
                        pendingCount--;
                        _state.WorkingMemory["pending_agent_calls"] = pendingCount.ToString();

                        _logger.LogInformation("Agent {AgentId} has {PendingCount} pending calls remaining", 
                            _state.AgentId, pendingCount);

                        // If all pending calls are completed, continue execution
                        if (pendingCount <= 0)
                        {
                            _logger.LogInformation("All agent callbacks completed for {AgentId}, continuing execution", 
                                _state.AgentId);

                            // Clear pause state
                            _state.WorkingMemory["execution_paused"] = "false";
                            _state.WorkingMemory.Remove("pending_agent_calls");
                            _state.WorkingMemory.Remove("pause_timestamp");

                            // Continue LLM execution with updated chat history
                            await ContinueExecutionAfterCallbacksAsync();
                        }
                    }
                }
            }

            _logger.LogDebug("Callback processed successfully for agent {AgentId}", _state.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing callback for agent {AgentId}", _state.AgentId);
        }
    }

    /// <summary>
    /// Continue LLM execution after all agent callbacks have been received
    /// </summary>
    private async Task ContinueExecutionAfterCallbacksAsync()
    {
        try
        {
            if (_kernel == null)
            {
                _logger.LogWarning("Cannot continue execution - kernel not available for agent {AgentId}", _state.AgentId);
                return;
            }

            _logger.LogInformation("Continuing LLM execution for agent {AgentId} after receiving all callbacks", _state.AgentId);

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = _state.ToSemanticKernelChatHistory();

            // Configure execution settings
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions,
                MaxTokens = _state.Configuration?.MaxTokens ?? 4000,
                Temperature = _state.Configuration?.Temperature ?? 0.1
            };

            // Get LLM response now that all callback results are in chat history
            var result = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            // Process any new function calls that might result from this
            if (_kernel.Services.GetService<IManualFunctionCallProcessor>() is { } processor)
            {
                // Process function calls in a loop until we get a final response with content
                const int maxIterations = 10; // Prevent infinite loops
                int iteration = 0;
                
                while (iteration < maxIterations)
                {
                    iteration++;
                    
                    var functionCallResult = await processor.ProcessFunctionCallsAsync(
                        result,
                        _kernel,
                        _state.AgentId,
                        chatHistory);

                    if (!functionCallResult.Success)
                    {
                        _logger.LogError("Function call processing failed during continuation: {Errors}", 
                            string.Join(", ", functionCallResult.ErrorMessages));
                        return;
                    }

                    // If there are new pending calls, update state accordingly and pause again
                    if (functionCallResult.ShouldPauseLLMExecution)
                    {
                        _logger.LogInformation("New agent calls initiated during continuation, pausing again");
                        _state.WorkingMemory["pending_agent_calls"] = functionCallResult.PendingCallsRequiringWait.ToString();
                        _state.WorkingMemory["execution_paused"] = "true";
                        return;
                    }

                    // If no function calls were processed and we have content, we're done
                    if (!functionCallResult.ChatHistoryUpdated && !string.IsNullOrEmpty(result.Content))
                    {
                        break;
                    }

                    // Get next response if function calls were processed
                    if (functionCallResult.ChatHistoryUpdated)
                    {
                        var nextResult = await chatService.GetChatMessageContentAsync(
                            chatHistory,
                            executionSettings,
                            _kernel);
                        
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
                    _logger.LogWarning("Reached maximum function call iterations ({MaxIterations}) during execution continuation", maxIterations);
                }
            }

            // Add the final response to state
            var response = result.Content ?? "Execution continued successfully.";
            _state.AddChatMessage("assistant", response);

            _logger.LogInformation("LLM execution continuation completed for agent {AgentId}", _state.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing execution after callbacks for agent {AgentId}", _state.AgentId);
        }
    }

    public async Task<string> ProcessTaskAsync(string task, string? parentId = null)
    {
        _logger.LogInformation("Agent {AgentId} processing task: {Task} (Parent: {ParentId})", 
            _state.AgentId, task, parentId ?? "none");

        try
        {
            // Store task and parent info
            _state.CurrentTask = task;
            _state.ParentAgentId = parentId;

            // Phase 1: Initial Analysis - Determine agent type
            if (_state.Role == AgentRole.Undecided)
            {
                await AnalyzeTaskAndDetermineRoleAsync(task);
            }

            // Phase 2: Execute based on role
            if (_state.Role == AgentRole.Orchestrator)
            {
                return await ExecuteAsOrchestratorAsync(task);
            }
            else if (_state.Role == AgentRole.Specialized)
            {
                return await ExecuteAsSpecializedAsync(task);
            }
            else
            {
                throw new InvalidOperationException($"Agent role {_state.Role} is not supported for task execution");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing task for agent {AgentId}", _state.AgentId);
            
            // Send failure callback to parent if exists
            if (!string.IsNullOrEmpty(parentId))
            {
                await SendParentCallbackAsync($"Task failed: {ex.Message}", false);
            }
            
            return $"Task processing failed: {ex.Message}";
        }
    }

    private async Task AnalyzeTaskAndDetermineRoleAsync(string task)
    {
        _logger.LogInformation("Analyzing task complexity to determine agent role for {AgentId}", _state.AgentId);

        try
        {
            if (_kernel == null)
            {
                throw new InvalidOperationException("Agent kernel is not initialized");
            }

            // Create analysis prompt
            var analysisPrompt = $@"
Analyze this task and determine if it should be handled as an ORCHESTRATOR (complex task requiring delegation) or SPECIALIZED (specific task requiring direct tools):

Task: {task}

Rules:
- ORCHESTRATOR: Choose this if the task is complex, multi-step, requires coordination, or would benefit from breaking into subtasks
- SPECIALIZED: Choose this if the task is specific, can be handled directly with available tools, or is a focused single-domain task

Respond with exactly one word: 'ORCHESTRATOR' or 'SPECIALIZED'";

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatService.GetChatMessageContentAsync(analysisPrompt);
            
            var decision = result.Content?.Trim().ToUpperInvariant();
            
            if (decision == "ORCHESTRATOR")
            {
                _state.Role = AgentRole.Orchestrator;
                _logger.LogInformation("Agent {AgentId} determined to be ORCHESTRATOR for task analysis", _state.AgentId);
            }
            else if (decision == "SPECIALIZED")
            {
                _state.Role = AgentRole.Specialized;
                _logger.LogInformation("Agent {AgentId} determined to be SPECIALIZED for task analysis", _state.AgentId);
            }
            else
            {
                // Default to specialized if unclear
                _state.Role = AgentRole.Specialized;
                _logger.LogWarning("Agent {AgentId} got unclear decision '{Decision}', defaulting to SPECIALIZED", _state.AgentId, decision);
            }

            // Switch system prompt and available tools based on role
            await ConfigureAgentForRoleAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing task for agent {AgentId}, defaulting to Specialized", _state.AgentId);
            _state.Role = AgentRole.Specialized;
            await ConfigureAgentForRoleAsync();
        }
    }

    private async Task ConfigureAgentForRoleAsync()
    {
        if (_kernel == null || _state.Configuration == null)
        {
            throw new InvalidOperationException("Agent is not properly initialized");
        }

        try
        {
            // ====== Phase 2 Refactoring: Use AgentRoleConfigurator ======
            
            // Get role-appropriate system prompt
            var rolePrompt = _roleConfigurator.GetSystemPromptForRole(_state.Role, _state.Configuration.SystemPrompt);
            
            // Create role-specific configuration
            var roleConfig = new AgentConfiguration
            {
                AgentName = _state.Configuration.AgentName + $"_{_state.Role}",
                SystemPrompt = rolePrompt,
                Temperature = _state.Configuration.Temperature,
                MaxTokens = _state.Configuration.MaxTokens
            };

            // Get original tool names from the function metadata instead of extracting from kernel plugins
            // This preserves the original format like "Math.Add", "Tavily.search" instead of "CustomFunctions.Add"
            var originalToolNames = _state.OriginalToolNames
                .Where(name => !string.IsNullOrEmpty(name)) // Filter out any empty names
                .ToList();

            _logger.LogDebug("Using {Count} original tool names: {Tools}", 
                originalToolNames.Count, string.Join(", ", originalToolNames));

            // Configure kernel with role-appropriate tools using original names
            _kernel = await _roleConfigurator.ConfigureKernelAsync(_state.Role, roleConfig, originalToolNames);

            _logger.LogInformation("Agent {AgentId} configured for role {Role} using new role configurator", _state.AgentId, _state.Role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring agent {AgentId} for role {Role}", _state.AgentId, _state.Role);
            throw;
        }
    }

    private async Task<string> ExecuteAsOrchestratorAsync(string task)
    {
        _logger.LogInformation("Executing as Orchestrator for agent {AgentId}", _state.AgentId);

        if (_kernel == null || _state.Configuration == null)
        {
            throw new InvalidOperationException("Orchestrator agent is not properly configured");
        }

        try
        {
            // Execute with orchestrator tools only
            var result = await _kernelService.ExecuteTaskAsync(_kernel, task, _state, _state.Configuration.SystemPrompt, waitForCompletion: true);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in orchestrator execution for agent {AgentId}", _state.AgentId);
            throw;
        }
    }

    private async Task<string> ExecuteAsSpecializedAsync(string task)
    {
        _logger.LogInformation("Executing as Specialized agent for agent {AgentId} using SpecializedStateMachine", _state.AgentId);

        if (_kernel == null || _state.Configuration == null)
        {
            throw new InvalidOperationException("Specialized agent is not properly configured");
        }

        try
        {
            // ====== Phase 2 Refactoring: Use SpecializedStateMachine ======
            
            // Get the specialized state machine from factory
            var stateMachine = _stateMachineFactory.CreateStateMachine(_state.Role);
            
            // Execute using the new state machine pattern
            var result = await stateMachine.ExecuteTaskAsync(task, _kernel, _state, _state.Configuration);
            
            _logger.LogInformation("SpecializedStateMachine completed execution for agent {AgentId}", _state.AgentId);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in specialized execution for agent {AgentId}", _state.AgentId);
            
            // Send failure callback to parent if exists
            if (!string.IsNullOrEmpty(_state.ParentAgentId))
            {
                await SendParentCallbackAsync($"Specialized task failed: {ex.Message}", false);
            }
            
            throw;
        }
    }

    // State machine orchestrator tools implementation

    public async Task<bool> SendParentCallbackAsync(string message, bool isSuccess = true)
    {
        if (string.IsNullOrEmpty(_state.ParentAgentId))
        {
            _logger.LogWarning("Agent {AgentId} has no parent to send callback to", _state.AgentId);
            return false;
        }

        try
        {
            var parentAgent = GrainFactory.GetGrain<IConfigurableAgentGrain>(_state.ParentAgentId);
            var callId = Guid.NewGuid().ToString();
            
            await parentAgent.ReceiveCallbackAsync(callId, message, isSuccess);
            
            _logger.LogInformation("Sent callback to parent {ParentId} from agent {AgentId}: Success={Success}", 
                _state.ParentAgentId, _state.AgentId, isSuccess);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending callback to parent {ParentId} from agent {AgentId}", 
                _state.ParentAgentId, _state.AgentId);
            return false;
        }
    }

    public async Task<(bool Success, string Message)> CreateAgentAsync(string agentId, AgentConfiguration configuration, IEnumerable<string>? toolNames = null)
    {
        if (_state.Role != AgentRole.Orchestrator)
        {
            var errorMsg = $"Only Orchestrator agents can create child agents. Current role: {_state.Role}";
            _logger.LogError(errorMsg);
            return (false, errorMsg);
        }

        try
        {
            var childAgent = GrainFactory.GetGrain<IConfigurableAgentGrain>(agentId);
            
            // Initialize the child agent with configuration and tools
            var initResult = await childAgent.InitializeAsync(configuration, toolNames);
            
            if (initResult.Success)
            {
                _state.ChildAgentIds.Add(agentId);
                _logger.LogInformation("Agent {AgentId} created and initialized child agent {ChildId}", _state.AgentId, agentId);
            }
            else
            {
                _logger.LogError("Failed to initialize child agent {ChildId}: {Message}", agentId, initResult.Message);
            }
            
            return (initResult.Success, initResult.Message);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to create child agent {agentId}: {ex.Message}";
            _logger.LogError(ex, errorMsg);
            return (false, errorMsg);
        }
    }

    public async Task<string> CallChildAgentAsync(string childAgentId, string task)
    {
        if (_state.Role != AgentRole.Orchestrator)
        {
            throw new InvalidOperationException($"Only Orchestrator agents can call child agents. Current role: {_state.Role}");
        }

        try
        {
            var childAgent = GrainFactory.GetGrain<IConfigurableAgentGrain>(childAgentId);
            var callId = Guid.NewGuid().ToString();
            
            // Process task on child agent (this will trigger callback)
            _ = Task.Run(async () =>
            {
                try
                {
                    await childAgent.ProcessTaskAsync(task, _state.AgentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in child agent {ChildId} task processing", childAgentId);
                    await ReceiveCallbackAsync(callId, $"Child task failed: {ex.Message}", false);
                }
            });
            
            _logger.LogInformation("Agent {AgentId} called child agent {ChildId} with task", 
                _state.AgentId, childAgentId);
                
            return callId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling child agent {ChildId} from agent {AgentId}", 
                childAgentId, _state.AgentId);
            throw;
        }
    }
} 
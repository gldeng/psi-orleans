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
using System.Text;
using System.Text.Json;

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
            // Start grain method tracing for initialization
            using var grainActivity = AgentTracingService.StartGrainMethodActivity("ConfigurableAgentGrain", "InitializeAsync", _state.AgentId);
            
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
        // Start callback processing tracing
        using var callbackActivity = AgentTracingService.StartAgentActivity("ReceiveCallback", _state.AgentId);
        callbackActivity?.SetTag("callback.call_id", callId);
        callbackActivity?.SetTag("callback.success", isSuccess);
        callbackActivity?.SetTag("callback.message_length", message.Length);
        callbackActivity?.SetTag("callback.agent_role", _state.Role.ToString());
        
        try
        {
            _logger.LogInformation("Received callback for call {CallId} on agent {AgentId}: Success={IsSuccess}", 
                callId, _state.AgentId, isSuccess);

            // ====== Phase 3 Refactoring: Use OrchestratorStateMachine for callback processing ======
            if (_state.Role == AgentRole.Orchestrator && _kernel != null && _state.Configuration != null)
            {
                // Phase 1: Process Orchestrator Callback
                using var orchestratorCallbackActivity = AgentTracingService.StartAgentActivity("OrchestratorStateMachine.ProcessCallback", _state.AgentId);
                orchestratorCallbackActivity?.SetTag("orchestrator.callback_id", callId);
                orchestratorCallbackActivity?.SetTag("orchestrator.callback_success", isSuccess);
                orchestratorCallbackActivity?.SetTag("orchestrator.pending_callbacks_before", _state.PendingCallbacks.Count);
                
                // Get the orchestrator state machine and process the callback
                var stateMachine = _stateMachineFactory.CreateStateMachine(_state.Role);
                await stateMachine.ProcessCallbackAsync(callId, message, isSuccess, _state, _kernel);
                
                orchestratorCallbackActivity?.SetTag("orchestrator.pending_callbacks_after", _state.PendingCallbacks.Count);
                orchestratorCallbackActivity?.SetTag("orchestrator.completed_callbacks", _state.CompletedCallbacks.Count);
                
                AgentTracingService.SetSuccess(orchestratorCallbackActivity, "Orchestrator callback processed via state machine");
                
                _logger.LogInformation("OrchestratorStateMachine processed callback {CallId} for agent {AgentId}", 
                    callId, _state.AgentId);
                
                callbackActivity?.SetTag("callback.processing_method", "OrchestratorStateMachine");
                AgentTracingService.SetSuccess(callbackActivity, "Callback processed by orchestrator state machine");
                return;
            }

            // ====== Legacy callback processing for non-orchestrator agents ======
            
            // Phase 2: Legacy Callback Processing
            using var legacyCallbackActivity = AgentTracingService.StartAgentActivity("LegacyCallbackProcessing", _state.AgentId);
            legacyCallbackActivity?.SetTag("legacy.agent_role", _state.Role.ToString());
            
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
            
            legacyCallbackActivity?.SetTag("legacy.chat_history_updated", true);
            legacyCallbackActivity?.SetTag("legacy.metrics_updated", true);

            // Check if execution was paused and if we should continue
            if (_state.WorkingMemory.ContainsKey("execution_paused") && 
                _state.WorkingMemory["execution_paused"]?.ToString() == "true")
            {
                // Phase 3: Handle Execution Resume
                using var executionResumeActivity = AgentTracingService.StartAgentActivity("CheckExecutionResume", _state.AgentId);
                executionResumeActivity?.SetTag("execution.was_paused", true);
                
                // Decrement pending calls counter
                if (_state.WorkingMemory.ContainsKey("pending_agent_calls"))
                {
                    var pendingCallsValue = _state.WorkingMemory["pending_agent_calls"]?.ToString();
                    if (!string.IsNullOrEmpty(pendingCallsValue) && int.TryParse(pendingCallsValue, out var pendingCount))
                    {
                        pendingCount--;
                        _state.WorkingMemory["pending_agent_calls"] = pendingCount.ToString();

                        executionResumeActivity?.SetTag("execution.pending_calls_remaining", pendingCount);
                        
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

                            executionResumeActivity?.SetTag("execution.resume_triggered", true);
                            AgentTracingService.SetSuccess(executionResumeActivity, "All callbacks complete, resuming execution");

                            // Continue LLM execution with updated chat history
                            await ContinueExecutionAfterCallbacksAsync();
                        }
                        else
                        {
                            executionResumeActivity?.SetTag("execution.resume_triggered", false);
                            AgentTracingService.SetSuccess(executionResumeActivity, $"Still waiting for {pendingCount} callbacks");
                        }
                    }
                }
            }
            
            AgentTracingService.SetSuccess(legacyCallbackActivity, "Legacy callback processing completed");

            _logger.LogDebug("Callback processed successfully for agent {AgentId}", _state.AgentId);
            
            callbackActivity?.SetTag("callback.processing_method", "Legacy");
            AgentTracingService.SetSuccess(callbackActivity, "Callback processed via legacy method");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing callback for agent {AgentId}", _state.AgentId);
            AgentTracingService.SetError(callbackActivity, ex);
            throw;
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
        // Start main grain processing activity
        using var grainActivity = AgentTracingService.StartGrainMethodActivity("ConfigurableAgentGrain", "ProcessTaskAsync", _state.AgentId);
        
        // 🟢 FLOW_START: Main task processing flow
        _logger.LogInformation("🟢 FLOW_START: ProcessTaskAsync for agent {AgentId}, task: '{Task}', parent: {ParentId}", 
            _state.AgentId, task.Length > 100 ? task.Substring(0, 100) + "..." : task, parentId ?? "none");

        if (!_state.IsInitialized || _kernel == null || _state.Configuration == null)
        {
            var errorMsg = "❌ FLOW_ERROR: Agent is not initialized. Please call InitializeAsync first.";
            _logger.LogError(errorMsg);
            return errorMsg;
        }

        // 🔄 FLOW_STEP: Set up state and context
        _logger.LogInformation("🔄 FLOW_STEP: Setting up task context - AgentId: {AgentId}, HasParent: {HasParent}", 
            _state.AgentId, !string.IsNullOrEmpty(parentId));

        _state.CurrentTask = task;
        _state.ParentAgentId = parentId;
        _state.LastUpdated = DateTime.UtcNow;

        var startTime = DateTime.UtcNow;

        try
        {
            // 🔄 FLOW_STEP: Analyze task complexity and determine agent role
            _logger.LogInformation("🔄 FLOW_STEP: Starting task analysis and role determination for agent {AgentId}", _state.AgentId);
            
            await AnalyzeTaskAndDetermineRoleAsync(task);
            
            // 🔀 FLOW_DECISION: Role determined - log the decision
            _logger.LogInformation("🔀 FLOW_DECISION: Agent {AgentId} role determined as {Role} for task analysis", 
                _state.AgentId, _state.DeterminedRole);

            // 🔄 FLOW_STEP: Configure agent for determined role  
            _logger.LogInformation("🔄 FLOW_STEP: Configuring agent {AgentId} for role {Role}", 
                _state.AgentId, _state.DeterminedRole);
            
            await ConfigureAgentForRoleAsync();
            
            // 🔀 FLOW_DECISION: Execute based on agent role
            _logger.LogInformation("🔀 FLOW_DECISION: Executing task with {Role} pattern for agent {AgentId}", 
                _state.DeterminedRole, _state.AgentId);

            string result;
            if (_state.DeterminedRole == AgentRole.Orchestrator)
            {
                // 🔄 FLOW_STEP: Execute as orchestrator using OrchestratorStateMachine
                _logger.LogInformation("🔄 FLOW_STEP: Executing as Orchestrator agent for agent {AgentId} using OrchestratorStateMachine", _state.AgentId);
                result = await ExecuteAsOrchestratorUsingStateMachineAsync(task);
            }
            else
            {
                // 🔄 FLOW_STEP: Execute as specialized using SpecializedStateMachine
                _logger.LogInformation("🔄 FLOW_STEP: Executing as Specialized agent for agent {AgentId} using SpecializedStateMachine", _state.AgentId);
                result = await ExecuteAsSpecializedUsingStateMachineAsync(task);
            }

            var executionTime = DateTime.UtcNow - startTime;
            _state.AddExecutionTime(executionTime);
            _state.IncrementSuccessfulTasks();

            // ✅ FLOW_SUCCESS: Task completed successfully
            _logger.LogInformation("✅ FLOW_SUCCESS: ProcessTaskAsync completed for agent {AgentId} in {Duration}ms, role: {Role}, result length: {ResultLength}", 
                _state.AgentId, executionTime.TotalMilliseconds, _state.DeterminedRole, result.Length);

            // 🏁 FLOW_END: Main task processing flow complete
            _logger.LogInformation("🏁 FLOW_END: ProcessTaskAsync for agent {AgentId} - SUCCESS", _state.AgentId);

            return result;
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - startTime;
            _state.AddExecutionTime(executionTime);
            _state.IncrementFailedTasks();

            // ❌ FLOW_ERROR: Task execution failed
            _logger.LogError(ex, "❌ FLOW_ERROR: ProcessTaskAsync failed for agent {AgentId} after {Duration}ms: {Error}", 
                _state.AgentId, executionTime.TotalMilliseconds, ex.Message);

            // 🏁 FLOW_END: Main task processing flow complete with error
            _logger.LogInformation("🏁 FLOW_END: ProcessTaskAsync for agent {AgentId} - FAILED", _state.AgentId);

            var errorMessage = $"Task processing failed: {ex.Message}";
            return errorMessage;
        }
    }

    /// <summary>
    /// Analyze the task complexity and determine whether this agent should be an Orchestrator or Specialized agent
    /// </summary>
    private async Task AnalyzeTaskAndDetermineRoleAsync(string task)
    {
        // 🟢 FLOW_START: Task analysis flow
        _logger.LogInformation("🟢 FLOW_START: AnalyzeTaskAndDetermineRole for agent {AgentId}", _state.AgentId);

        try
        {
            // 🔄 FLOW_STEP: Get chat completion service
            _logger.LogInformation("🔄 FLOW_STEP: Getting chat completion service for task analysis");
            
            var chatService = _kernel!.GetRequiredService<IChatCompletionService>();

            // 🔄 FLOW_STEP: Prepare analysis prompt
            _logger.LogInformation("🔄 FLOW_STEP: Preparing task complexity analysis prompt");
            
            var analysisPrompt = $@"
Analyze this task and determine if it should be handled by an ORCHESTRATOR or SPECIALIZED agent.

Task: {task}

ORCHESTRATOR agents should handle tasks that:
- Require breaking down into multiple subtasks
- Need coordination between different capabilities
- Involve complex multi-step workflows
- Require delegation and result aggregation

SPECIALIZED agents should handle tasks that:
- Can be completed with direct tool usage
- Are focused and specific
- Don't require task decomposition
- Can be solved with available functions

Respond with exactly one word: ORCHESTRATOR or SPECIALIZED";

            // 🔄 FLOW_STEP: Execute LLM analysis
            _logger.LogInformation("🔄 FLOW_STEP: Executing LLM task complexity analysis");
            
            var result = await chatService.GetChatMessageContentAsync(analysisPrompt);
            var response = result.Content?.Trim().ToUpperInvariant() ?? "SPECIALIZED";

            // 🔀 FLOW_DECISION: Parse LLM response and determine role
            _logger.LogInformation("🔀 FLOW_DECISION: LLM analysis response: '{Response}'", response);
            
            _state.DeterminedRole = response.Contains("ORCHESTRATOR") ? AgentRole.Orchestrator : AgentRole.Specialized;

            // ✅ FLOW_SUCCESS: Role determination completed
            _logger.LogInformation("✅ FLOW_SUCCESS: Agent {AgentId} determined to be {Role} for task analysis", 
                _state.AgentId, _state.DeterminedRole);
                
            // 🏁 FLOW_END: Task analysis flow complete
            _logger.LogInformation("🏁 FLOW_END: AnalyzeTaskAndDetermineRole for agent {AgentId} - Role: {Role}", 
                _state.AgentId, _state.DeterminedRole);
        }
        catch (Exception ex)
        {
            // ❌ FLOW_ERROR: Analysis failed, default to specialized
            _logger.LogError(ex, "❌ FLOW_ERROR: Task analysis failed for agent {AgentId}, defaulting to SPECIALIZED: {Error}", 
                _state.AgentId, ex.Message);
            
            _state.DeterminedRole = AgentRole.Specialized;
            
            // 🏁 FLOW_END: Task analysis flow complete with fallback
            _logger.LogInformation("🏁 FLOW_END: AnalyzeTaskAndDetermineRole for agent {AgentId} - FALLBACK to Specialized", _state.AgentId);
        }
    }

    /// <summary>
    /// Configure the agent for the determined role using the role configurator
    /// </summary>
    private async Task ConfigureAgentForRoleAsync()
    {
        // 🟢 FLOW_START: Role configuration flow
        _logger.LogInformation("🟢 FLOW_START: ConfigureAgentForRole - Role: {Role}, Agent: {AgentId}", 
            _state.DeterminedRole, _state.AgentId);

        try
        {
            // 🔄 FLOW_STEP: Create role-specific configuration
            _logger.LogInformation("🔄 FLOW_STEP: Creating role-specific configuration for {Role}", _state.DeterminedRole);
            
            var roleConfig = new AgentConfiguration
            {
                AgentName = $"{_state.Configuration!.AgentName}_{_state.DeterminedRole}",
                SystemPrompt = _roleConfigurator.GetSystemPromptForRole(_state.DeterminedRole, _state.Configuration.SystemPrompt),
                Model = _state.Configuration.Model,
                MaxTokens = _state.Configuration.MaxTokens,
                Temperature = _state.Configuration.Temperature
            };

            // 🔄 FLOW_STEP: Configure kernel for role
            _logger.LogInformation("🔄 FLOW_STEP: Configuring kernel for role {Role} using role configurator", _state.DeterminedRole);
            
            _kernel = await _roleConfigurator.ConfigureKernelAsync(_state.DeterminedRole, roleConfig, _state.OriginalToolNames);

            // 🔄 FLOW_STEP: Update state with role configuration
            _logger.LogInformation("🔄 FLOW_STEP: Updating agent state with role configuration");
            
            _state.Configuration = roleConfig;
            _state.LastUpdated = DateTime.UtcNow;

            // Get updated function metadata
            _state.AvailableFunctions = await GetFunctionMetadata(_kernel);

            // ✅ FLOW_SUCCESS: Role configuration completed
            _logger.LogInformation("✅ FLOW_SUCCESS: Agent {AgentId} configured for role {Role} using new role configurator", 
                _state.AgentId, _state.DeterminedRole);
                
            // 🏁 FLOW_END: Role configuration flow complete
            _logger.LogInformation("🏁 FLOW_END: ConfigureAgentForRole for agent {AgentId} - Success", _state.AgentId);
        }
        catch (Exception ex)
        {
            // ❌ FLOW_ERROR: Role configuration failed
            _logger.LogError(ex, "❌ FLOW_ERROR: Role configuration failed for agent {AgentId}, role {Role}: {Error}", 
                _state.AgentId, _state.DeterminedRole, ex.Message);
                
            // 🏁 FLOW_END: Role configuration flow complete with error
            _logger.LogInformation("🏁 FLOW_END: ConfigureAgentForRole for agent {AgentId} - FAILED", _state.AgentId);
            throw;
        }
    }

    private async Task<string> ExecuteAsOrchestratorUsingStateMachineAsync(string task)
    {
        // Start orchestrator execution tracing
        using var orchestratorActivity = AgentTracingService.StartAgentActivity("ExecuteAsOrchestrator", _state.AgentId, task);
        orchestratorActivity?.SetTag("execution.pattern", "Async Event-Driven");
        orchestratorActivity?.SetTag("agent.role", "Orchestrator");
        
        _logger.LogInformation("Executing as Orchestrator agent for agent {AgentId} using OrchestratorStateMachine", _state.AgentId);

        if (_kernel == null || _state.Configuration == null)
        {
            throw new InvalidOperationException("Orchestrator agent is not properly configured");
        }

        try
        {
            // ====== Phase 3 Refactoring: Use OrchestratorStateMachine ======
            
            // Phase 1: Create State Machine
            using var stateMachineCreationActivity = AgentTracingService.StartAgentActivity("CreateOrchestratorStateMachine", _state.AgentId);
            var stateMachine = _stateMachineFactory.CreateStateMachine(_state.DeterminedRole);
            stateMachineCreationActivity?.SetTag("state_machine.type", "OrchestratorStateMachine");
            AgentTracingService.SetSuccess(stateMachineCreationActivity, "Orchestrator state machine created");
            
            // Phase 2: Execute Orchestrator Pattern (Async Event-Driven)
            using var orchestrationExecutionActivity = AgentTracingService.StartAgentActivity("OrchestratorStateMachine.Execute", _state.AgentId, task);
            orchestrationExecutionActivity?.SetTag("orchestration.task", task.Length > 200 ? task.Substring(0, 200) + "..." : task);
            orchestrationExecutionActivity?.SetTag("orchestration.pattern", "Delegation");
            
            var result = await stateMachine.ExecuteTaskAsync(task, _kernel, _state, _state.Configuration);
            
            orchestrationExecutionActivity?.SetTag("orchestration.result", result.Length > 100 ? result.Substring(0, 100) + "..." : result);
            orchestrationExecutionActivity?.SetTag("orchestration.child_count", _state.ChildAgentIds.Count);
            orchestrationExecutionActivity?.SetTag("orchestration.pending_callbacks", _state.PendingCallbacks.Count);
            
            AgentTracingService.SetSuccess(orchestrationExecutionActivity, "Orchestrator delegation completed");
            
            _logger.LogInformation("OrchestratorStateMachine completed initial delegation for agent {AgentId}", _state.AgentId);
            
            orchestratorActivity?.SetTag("execution.result", "Delegation initiated");
            orchestratorActivity?.SetTag("execution.child_agents_created", _state.ChildAgentIds.Count);
            AgentTracingService.SetSuccess(orchestratorActivity, $"Orchestrator execution initiated with {_state.ChildAgentIds.Count} child agents");
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in orchestrator execution for agent {AgentId}", _state.AgentId);
            
            // Send failure callback to parent if exists
            if (!string.IsNullOrEmpty(_state.ParentAgentId))
            {
                await SendParentCallbackAsync($"Orchestrator task failed: {ex.Message}", false);
            }
            
            AgentTracingService.SetError(orchestratorActivity, ex);
            throw;
        }
    }

    private async Task<string> ExecuteAsSpecializedUsingStateMachineAsync(string task)
    {
        // Start specialized execution tracing
        using var specializedActivity = AgentTracingService.StartAgentActivity("ExecuteAsSpecialized", _state.AgentId, task);
        specializedActivity?.SetTag("execution.pattern", "Sync Direct");
        specializedActivity?.SetTag("agent.role", "Specialized");
        
        _logger.LogInformation("Executing as Specialized agent for agent {AgentId} using SpecializedStateMachine", _state.AgentId);

        if (_kernel == null || _state.Configuration == null)
        {
            throw new InvalidOperationException("Specialized agent is not properly configured");
        }

        try
        {
            // ====== Phase 2 Refactoring: Use SpecializedStateMachine ======
            
            // Phase 1: Create State Machine
            using var stateMachineCreationActivity = AgentTracingService.StartAgentActivity("CreateSpecializedStateMachine", _state.AgentId);
            var stateMachine = _stateMachineFactory.CreateStateMachine(_state.DeterminedRole);
            stateMachineCreationActivity?.SetTag("state_machine.type", "SpecializedStateMachine");
            AgentTracingService.SetSuccess(stateMachineCreationActivity, "Specialized state machine created");
            
            // Phase 2: Execute Specialized Pattern (Sync Direct with Tool Calling)
            using var specializedExecutionActivity = AgentTracingService.StartAgentActivity("SpecializedStateMachine.Execute", _state.AgentId, task);
            specializedExecutionActivity?.SetTag("specialized.task", task.Length > 200 ? task.Substring(0, 200) + "..." : task);
            specializedExecutionActivity?.SetTag("specialized.pattern", "DirectToolExecution");
            
            var result = await stateMachine.ExecuteTaskAsync(task, _kernel, _state, _state.Configuration);
            
            specializedExecutionActivity?.SetTag("specialized.result", result.Length > 100 ? result.Substring(0, 100) + "..." : result);
            specializedExecutionActivity?.SetTag("specialized.parent_callback_sent", !string.IsNullOrEmpty(_state.ParentAgentId));
            
            AgentTracingService.SetSuccess(specializedExecutionActivity, "Specialized execution completed");
            
            _logger.LogInformation("SpecializedStateMachine completed execution for agent {AgentId}", _state.AgentId);
            
            specializedActivity?.SetTag("execution.result", "DirectExecution");
            specializedActivity?.SetTag("execution.completion_callback_sent", !string.IsNullOrEmpty(_state.ParentAgentId));
            AgentTracingService.SetSuccess(specializedActivity, "Specialized execution completed with direct result");
            
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
            
            AgentTracingService.SetError(specializedActivity, ex);
            throw;
        }
    }

    // State machine orchestrator tools implementation

    public async Task<bool> SendParentCallbackAsync(string message, bool isSuccess = true)
    {
        // Start parent callback tracing
        using var parentCallbackActivity = AgentTracingService.StartAgentActivity("SendParentCallback", _state.AgentId);
        parentCallbackActivity?.SetTag("communication.direction", "to_parent");
        parentCallbackActivity?.SetTag("communication.parent_id", _state.ParentAgentId ?? "none");
        parentCallbackActivity?.SetTag("communication.success", isSuccess);
        parentCallbackActivity?.SetTag("communication.message_length", message.Length);
        
        if (string.IsNullOrEmpty(_state.ParentAgentId))
        {
            _logger.LogWarning("Agent {AgentId} has no parent to send callback to", _state.AgentId);
            parentCallbackActivity?.SetTag("communication.result", "no_parent");
            AgentTracingService.SetSuccess(parentCallbackActivity, "No parent agent to send callback to");
            return false;
        }

        try
        {
            // Phase 1: Get Parent Agent Reference
            using var parentLookupActivity = AgentTracingService.StartAgentActivity("GetParentAgentReference", _state.AgentId);
            parentLookupActivity?.SetTag("parent.id", _state.ParentAgentId);
            
            var parentAgent = GrainFactory.GetGrain<IConfigurableAgentGrain>(_state.ParentAgentId);
            var callId = Guid.NewGuid().ToString();
            
            parentLookupActivity?.SetTag("parent.call_id", callId);
            AgentTracingService.SetSuccess(parentLookupActivity, "Parent agent reference obtained");
            
            // Phase 2: Send Callback to Parent
            using var callbackSendActivity = AgentTracingService.StartAgentActivity("InvokeParentCallback", _state.AgentId);
            callbackSendActivity?.SetTag("invoke.parent_id", _state.ParentAgentId);
            callbackSendActivity?.SetTag("invoke.call_id", callId);
            callbackSendActivity?.SetTag("invoke.success", isSuccess);
            
            await parentAgent.ReceiveCallbackAsync(callId, message, isSuccess);
            
            AgentTracingService.SetSuccess(callbackSendActivity, "Callback successfully sent to parent");
            
            _logger.LogInformation("Sent callback to parent {ParentId} from agent {AgentId}: Success={Success}", 
                _state.ParentAgentId, _state.AgentId, isSuccess);
            
            parentCallbackActivity?.SetTag("communication.result", "success");
            parentCallbackActivity?.SetTag("communication.call_id", callId);
            AgentTracingService.SetSuccess(parentCallbackActivity, $"Callback sent to parent {_state.ParentAgentId}");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending callback to parent {ParentId} from agent {AgentId}", 
                _state.ParentAgentId, _state.AgentId);
            
            parentCallbackActivity?.SetTag("communication.result", "error");
            AgentTracingService.SetError(parentCallbackActivity, ex);
            return false;
        }
    }

    public async Task<(bool Success, string Message)> CreateAgentAsync(string agentId, AgentConfiguration configuration, IEnumerable<string>? toolNames = null)
    {
        if (_state.DeterminedRole != AgentRole.Orchestrator)
        {
            var errorMsg = $"Only Orchestrator agents can create child agents. Current role: {_state.DeterminedRole}";
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
        // Start child agent call tracing
        using var childCallActivity = AgentTracingService.StartAgentActivity("CallChildAgent", _state.AgentId, task);
        childCallActivity?.SetTag("communication.direction", "to_child");
        childCallActivity?.SetTag("communication.child_id", childAgentId);
        childCallActivity?.SetTag("communication.task_length", task.Length);
        
        if (_state.DeterminedRole != AgentRole.Orchestrator)
        {
            var error = new InvalidOperationException($"Only Orchestrator agents can call child agents. Current role: {_state.DeterminedRole}");
            AgentTracingService.SetError(childCallActivity, error);
            throw error;
        }

        try
        {
            // Phase 1: Get Child Agent Reference
            using var childLookupActivity = AgentTracingService.StartAgentActivity("GetChildAgentReference", _state.AgentId);
            childLookupActivity?.SetTag("child.id", childAgentId);
            
            var childAgent = GrainFactory.GetGrain<IConfigurableAgentGrain>(childAgentId);
            var callId = Guid.NewGuid().ToString();
            
            childLookupActivity?.SetTag("child.call_id", callId);
            AgentTracingService.SetSuccess(childLookupActivity, "Child agent reference obtained");
            
            // Phase 2: Async Task Delegation (Non-blocking)
            using var taskDelegationActivity = AgentTracingService.StartAgentActivity("DelegateTaskToChild", _state.AgentId, task);
            taskDelegationActivity?.SetTag("delegation.child_id", childAgentId);
            taskDelegationActivity?.SetTag("delegation.call_id", callId);
            taskDelegationActivity?.SetTag("delegation.task", task.Length > 200 ? task.Substring(0, 200) + "..." : task);
            
            // Process task on child agent (this will trigger callback)
            _ = Task.Run(async () =>
            {
                try
                {
                    // Create child execution span
                    using var childExecutionActivity = AgentTracingService.StartAgentActivity("ChildAgentExecution", childAgentId, task);
                    childExecutionActivity?.SetTag("child_execution.parent_id", _state.AgentId);
                    childExecutionActivity?.SetTag("child_execution.call_id", callId);
                    
                    await childAgent.ProcessTaskAsync(task, _state.AgentId);
                    
                    AgentTracingService.SetSuccess(childExecutionActivity, "Child agent task completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in child agent {ChildId} task processing", childAgentId);
                    await ReceiveCallbackAsync(callId, $"Child task failed: {ex.Message}", false);
                }
            });
            
            AgentTracingService.SetSuccess(taskDelegationActivity, $"Task delegated to child {childAgentId}");
            
            _logger.LogInformation("Agent {AgentId} called child agent {ChildId} with task", 
                _state.AgentId, childAgentId);
            
            childCallActivity?.SetTag("communication.result", "delegated");
            childCallActivity?.SetTag("communication.call_id", callId);
            AgentTracingService.SetSuccess(childCallActivity, $"Task delegated to child {childAgentId}, awaiting callback");
                
            return callId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling child agent {ChildId} from agent {AgentId}", 
                childAgentId, _state.AgentId);
            
            childCallActivity?.SetTag("communication.result", "error");
            AgentTracingService.SetError(childCallActivity, ex);
            throw;
        }
    }

    /// <summary>
    /// Get current state information for monitoring and debugging.
    /// Provides detailed view of subtasks, callbacks, and orchestration progress.
    /// </summary>
    public async Task<string> GetStateInfoAsync()
    {
        // Start grain method tracing
        using var grainActivity = AgentTracingService.StartGrainMethodActivity("ConfigurableAgentGrain", "GetStateInfoAsync", _state.AgentId);
        
        await Task.Delay(1); // Ensure async context

        try
        {
            var stateInfo = new StringBuilder();
            stateInfo.AppendLine($"Agent ID: {_state.AgentId}");
            stateInfo.AppendLine($"Role: {_state.DeterminedRole}");
            stateInfo.AppendLine($"Current Task: {_state.CurrentTask}");
            stateInfo.AppendLine();
            
            // SubTasks information
            if (_state.CurrentSubTasks.Count > 0)
            {
                stateInfo.AppendLine($"Current SubTasks ({_state.CurrentSubTasks.Count}):");
                for (int i = 0; i < _state.CurrentSubTasks.Count; i++)
                {
                    var subTask = _state.CurrentSubTasks[i];
                    stateInfo.AppendLine($"  {i + 1}. [{subTask.Status}] {subTask.Task}");
                    stateInfo.AppendLine($"     SubTask ID: {subTask.SubTaskId}");
                    stateInfo.AppendLine($"     Priority: {subTask.Priority}");
                    if (subTask.RequiredTools.Count > 0)
                    {
                        stateInfo.AppendLine($"     Required Tools: {string.Join(", ", subTask.RequiredTools)}");
                    }
                    if (!string.IsNullOrEmpty(subTask.ChildAgentId))
                    {
                        stateInfo.AppendLine($"     Child Agent: {subTask.ChildAgentId}");
                    }
                }
                stateInfo.AppendLine();
            }
            
            // Pending Callbacks
            if (_state.PendingCallbacks.Count > 0)
            {
                stateInfo.AppendLine($"Pending Callbacks ({_state.PendingCallbacks.Count}):");
                foreach (var callback in _state.PendingCallbacks.Values)
                {
                    stateInfo.AppendLine($"  CallID: {callback.CallId}");
                    stateInfo.AppendLine($"  Child: {callback.ChildAgentId}");
                    stateInfo.AppendLine($"  Task: {callback.Task}");
                    stateInfo.AppendLine($"  Created: {callback.CreatedAt:HH:mm:ss}");
                    stateInfo.AppendLine();
                }
            }
            
            // Completed Callbacks
            if (_state.CompletedCallbacks.Count > 0)
            {
                stateInfo.AppendLine($"Completed Callbacks ({_state.CompletedCallbacks.Count}):");
                foreach (var callback in _state.CompletedCallbacks)
                {
                    var status = callback.IsSuccess ? "✅" : "❌";
                    stateInfo.AppendLine($"  {status} {callback.Task}");
                    var result = callback.ResultMessage.Length > 150 ? callback.ResultMessage.Substring(0, 150) + "..." : callback.ResultMessage;
                    stateInfo.AppendLine($"     Result: {result}");
                    stateInfo.AppendLine($"     Completed: {callback.CompletedAt:HH:mm:ss}");
                    stateInfo.AppendLine();
                }
            }
            
            // Child Agents
            if (_state.ChildAgentIds.Count > 0)
            {
                stateInfo.AppendLine($"Child Agents ({_state.ChildAgentIds.Count}):");
                foreach (var childId in _state.ChildAgentIds)
                {
                    stateInfo.AppendLine($"  - {childId}");
                }
                stateInfo.AppendLine();
            }
            
            return stateInfo.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting state info: {ex.Message}";
        }
    }
} 
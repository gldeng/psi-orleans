using Orleans;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using PsiOrleans.Models;
using PsiOrleans.Services;

namespace PsiOrleans.Grains;

/// <summary>
/// Orleans Grain implementation of a configurable agent that can be initialized with custom prompts and tools
/// </summary>
public class ConfigurableAgentGrain : Grain, IConfigurableAgentGrain
{
    private readonly IConfigurableKernelService _kernelService;
    private readonly ILogger<ConfigurableAgentGrain> _logger;
    private ConfigurableAgentState _state = new();
    private Kernel? _kernel;

    public ConfigurableAgentGrain(IConfigurableKernelService kernelService, ILogger<ConfigurableAgentGrain> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
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

            // Create kernel with the provided configuration and unified tools
            _kernel = await _kernelService.CreateKernelAsync(configuration, toolNames);
            
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
            var result = await _kernelService.ExecuteTaskAsync(_kernel, task, _state, _state.Configuration.SystemPrompt);
            
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
            
            // Create a kernel function that delegates to the target agent
            var agentCallFunction = KernelFunctionFactory.CreateFromMethod(
                async (string query) => await CallAgentDirectly(callableAgent.Id, query),
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
    private async Task<string> CallAgentDirectly(string agentId, string query)
    {
        try
        {
            var targetAgent = GrainFactory.GetGrain<IConfigurableAgentGrain>(agentId);
        
            // Check if the target agent is initialized
            var isInitialized = await targetAgent.IsInitializedAsync();
            if (!isInitialized)
            {
                return $"Error: Agent {agentId} is not initialized";
            }
        
            // Execute the task on the target agent
            return await targetAgent.ExecuteTaskAsync(query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling agent {AgentId} from {OwnerAgentId}", agentId, _state.AgentId);
            return $"Error calling agent {agentId}: {ex.Message}";
        }
    }
    private async Task<string> CallAgentDirectly_Backup(string agentId, string query)
    {
        try
        {

            // Use the grain's task scheduler to ensure proper Orleans context
            var result = await Task.Factory.StartNew(async () =>
            {
                var targetAgent = GrainFactory.GetGrain<IConfigurableAgentGrain>(agentId);
                
                // Check if the target agent is initialized
                var isInitialized = await targetAgent.IsInitializedAsync();
                if (!isInitialized)
                {
                    return $"Error: Agent {agentId} is not initialized";
                }
                
                // Execute the task on the target agent
                return await targetAgent.ExecuteTaskAsync(query);
            }, 
            CancellationToken.None, 
            TaskCreationOptions.None, 
            TaskScheduler.Current).Unwrap();
            
            _logger.LogInformation("Successfully called agent {AgentId} from {OwnerAgentId}", agentId, _state.AgentId);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling agent {AgentId} from {OwnerAgentId}", agentId, _state.AgentId);
            return $"Error calling agent {agentId}: {ex.Message}";
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

            // Get only the original tool names (exclude agent communication functions)
            // Agent communication functions have the pattern "Call_*" so we filter them out
            var originalToolNames = _state.AvailableFunctions
                .Where(f => !f.IsAgentCommunicationFunction && !f.Name.StartsWith("Call_"))
                .Select(f => f.Name)
                .ToList();
            
            _logger.LogInformation("Recreating kernel with {OriginalCount} original tools", originalToolNames.Count);

            // Recreate the kernel with only the original tools (that the service knows about)
            _kernel = await _kernelService.CreateKernelAsync(_state.Configuration, originalToolNames);

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
} 
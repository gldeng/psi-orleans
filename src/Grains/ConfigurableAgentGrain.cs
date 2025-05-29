using Orleans;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
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
        // TODO: state and kernel need to be initialized here
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

    private async Task<List<KernelFunctionMetadata>> GetFunctionMetadata(Kernel kernel)
    {
        var metadata = new List<KernelFunctionMetadata>();
        
        try
        {
            foreach (var plugin in kernel.Plugins)
            {
                foreach (var function in plugin)
                {
                    metadata.Add(function.Metadata);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function metadata for ConfigurableAgent {AgentId}", _state.AgentId);
        }

        return metadata;
    }
} 
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Orleans;
using PsiOrleans.Models;
using PsiOrleans.Services;

namespace PsiOrleans.Grains;

/// <summary>
/// Agent X - Task Dispatcher Grain Implementation
/// Intelligent agent that analyzes individual subtasks for agent matching
/// Implements IConfigurableAgentGrain so it can be called by LLM
/// </summary>
public class TaskDispatcherGrain : Grain, IConfigurableAgentGrain
{
    private readonly ILogger<TaskDispatcherGrain> _logger;
    private readonly ISemanticKernelService _kernelService;
    
    // State 
    private ConfigurableAgentState _state = new ConfigurableAgentState();
    private Kernel? _kernel;
    
    // Task Dispatcher specific state
    private Dictionary<string, CallableAgent> _availableAgents = new();
    private List<string> _availableTools = new();

    public TaskDispatcherGrain(
        ILogger<TaskDispatcherGrain> logger,
        ISemanticKernelService kernelService)
    {
        _logger = logger;
        _kernelService = kernelService;
    }

    #region IConfigurableAgentGrain Implementation

    public async Task<(bool Success, string Message, string AgentId)> InitializeAsync(
        AgentConfiguration configuration,
        IEnumerable<string>? functionNames = null,
        IEnumerable<string>? pluginNames = null)
    {
        try
        {
            _logger.LogInformation("🔧 Initializing Task Dispatcher (Agent X)");
            
            _state.Configuration = configuration;
            _state.IsInitialized = true;
            
            // Create basic kernel for this agent
            _kernel = _kernelService.GetKernel();
                
            var message = $"Task Dispatcher (Agent X) initialized: {configuration.AgentName}";
            _logger.LogInformation("✅ {Message}", message);
            
            return (true, message, this.GetPrimaryKeyString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize Task Dispatcher");
            return (false, $"Initialization failed: {ex.Message}", this.GetPrimaryKeyString());
        }
    }

    public async Task<(bool Success, string Message, string AgentId)> InitializeAsync(
        AgentConfiguration configuration,
        IEnumerable<string>? toolNames)
    {
        try
        {
            _logger.LogInformation("🔧 Initializing Task Dispatcher (Agent X) with tools");
            
            _state.Configuration = configuration;
            _state.IsInitialized = true;
            
            // Create basic kernel for this agent
            _kernel = _kernelService.GetKernel();
                
            var message = $"Task Dispatcher (Agent X) initialized: {configuration.AgentName}";
            _logger.LogInformation("✅ {Message}", message);
            
            return (true, message, this.GetPrimaryKeyString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize Task Dispatcher");
            return (false, $"Initialization failed: {ex.Message}", this.GetPrimaryKeyString());
        }
    }

    public async Task<string> ExecuteTaskAsync(string task)
    {
        if (!_state.IsInitialized || _kernel == null)
        {
            return "Error: Task Dispatcher not initialized";
        }

        try
        {
            _logger.LogInformation("🎯 Task Dispatcher analyzing subtask: {Task}", task);
            _state.TotalTasks++;

            // Simple subtask analysis
            var analysisResult = await AnalyzeSubtaskAsync(task);
            
            _state.SuccessfulTasks++;
            
            _logger.LogInformation("✅ Task Dispatcher completed analysis");
            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in Task Dispatcher execution");
            _state.FailedTasks++;
            return $"Error analyzing subtask: {ex.Message}";
        }
    }

    public Task<string> ContinueConversationAsync(string message)
    {
        return ExecuteTaskAsync(message);
    }

    public Task<bool> IsInitializedAsync() => Task.FromResult(_state.IsInitialized);

    public Task<AgentConfiguration?> GetConfigurationAsync() => Task.FromResult(_state.Configuration);

    public Task<List<(string FunctionName, string Description, List<string> Parameters)>> GetAvailableToolsAsync()
    {
        var tools = new List<(string FunctionName, string Description, List<string> Parameters)>();
        if (_kernel != null)
        {
            foreach (var func in _kernel.Plugins.GetFunctionsMetadata())
            {
                var parameters = func.Parameters.Select(p => p.Name).ToList();
                tools.Add((func.Name, func.Description, parameters));
            }
        }
        return Task.FromResult(tools);
    }

    public Task<(int TotalTasks, int SuccessfulTasks, int FailedTasks, TimeSpan TotalExecutionTime)> GetMetricsAsync()
    {
        return Task.FromResult((_state.TotalTasks, _state.SuccessfulTasks, _state.FailedTasks, _state.TotalExecutionTime));
    }

    public Task<ConfigurableAgentState> GetStateAsync() => Task.FromResult(_state);

    public Task<bool> ResetAsync()
    {
        _state = new ConfigurableAgentState();
        _availableAgents.Clear();
        _availableTools.Clear();
        _logger.LogInformation("🔄 Task Dispatcher state reset");
        return Task.FromResult(true);
    }

    public Task<List<ChatMessage>> GetChatHistoryAsync()
    {
        return Task.FromResult(_state.ChatHistory);
    }

    public Task<bool> ClearChatHistoryAsync()
    {
        _state.ChatHistory.Clear();
        return Task.FromResult(true);
    }

    public Task<(bool Success, string Message)> SetCallableAgentsAsync(IEnumerable<CallableAgent> agents)
    {
        var agentsList = agents.ToList();
        _availableAgents = agentsList.ToDictionary(a => a.Id, a => a);
        _state.CallableAgents = agentsList;
        return Task.FromResult((true, $"Set {agentsList.Count} available agents for Task Dispatcher"));
    }

    public Task<List<CallableAgent>> GetCallableAgentsAsync()
    {
        return Task.FromResult(_state.CallableAgents);
    }

    public Task<(bool Success, string Message)> AddCallableAgentAsync(CallableAgent callableAgent)
    {
        _availableAgents[callableAgent.Id] = callableAgent;
        if (!_state.CallableAgents.Any(a => a.Id == callableAgent.Id))
        {
            _state.CallableAgents.Add(callableAgent);
        }
        return Task.FromResult((true, $"Added callable agent: {callableAgent.Id}"));
    }

    public Task<(bool Success, string Message)> RemoveCallableAgentAsync(string agentId)
    {
        _availableAgents.Remove(agentId);
        _state.CallableAgents.RemoveAll(a => a.Id == agentId);
        return Task.FromResult((true, $"Removed callable agent: {agentId}"));
    }

    public Task<List<string>> GetAvailableFunctionNamesAsync()
    {
        if (_kernel == null) return Task.FromResult(new List<string>());
        return Task.FromResult(_kernel.Plugins.GetFunctionsMetadata().Select(f => f.Name).ToList());
    }

    public Task<List<string>> GetAvailablePluginNamesAsync()
    {
        if (_kernel == null) return Task.FromResult(new List<string>());
        return Task.FromResult(_kernel.Plugins.Select(p => p.Name).ToList());
    }

    public Task<List<string>> GetAllAvailableToolNamesAsync()
    {
        return GetAvailableFunctionNamesAsync();
    }

    public Task<List<string>> GetAvailableAgentFunctionNamesAsync()
    {
        return Task.FromResult(_availableAgents.Keys.Select(id => $"Call{id.Replace("-", "")}").ToList());
    }

    public Task<bool> CanCallAgentAsync(string agentId)
    {
        return Task.FromResult(_availableAgents.ContainsKey(agentId));
    }

    #endregion

    #region Task Dispatcher Specific Methods

    /// <summary>
    /// Initialize Task Dispatcher with available agents and tools
    /// </summary>
    public async Task<(bool Success, string Message)> InitializeTaskDispatcherAsync(
        Dictionary<string, CallableAgent> availableAgents,
        List<string> availableTools)
    {
        try
        {
            _availableAgents = availableAgents ?? new Dictionary<string, CallableAgent>();
            _availableTools = availableTools ?? new List<string>();
            _state.CallableAgents = _availableAgents.Values.ToList();
            
            var message = $"Task Dispatcher state updated with {_availableAgents.Count} agents and {_availableTools.Count} tools";
            _logger.LogInformation("✅ {Message}", message);
            
            return (true, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize Task Dispatcher state");
            return (false, $"Initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyze a single subtask to determine handling approach
    /// </summary>
    private async Task<string> AnalyzeSubtaskAsync(string subtask)
    {
        if (_kernel == null)
        {
            return "Error: Task Dispatcher kernel not available";
        }

        try
        {
            // Simple analysis using available agents
            var matchingAgent = FindBestMatchingAgent(subtask);
            
            if (matchingAgent != null)
            {
                return $@"🎯 Task Dispatcher Analysis for Subtask

**Subtask:** {subtask}

**Analysis Result:** ExistingAgentMatch

**Recommended Action:** Use existing agent '{matchingAgent.Id}' 

**Existing Agent:** {matchingAgent.Id}
**Agent Name:** {matchingAgent.Name}
**Notes:** {matchingAgent.Description}";
            }

            // If no agent matches, suggest creating one
            return $@"🎯 Task Dispatcher Analysis for Subtask

**Subtask:** {subtask}

**Analysis Result:** RequiresNewAgent

**Recommended Action:** Create new specialized agent for this subtask

**New Agent Specification:**
- Agent ID: specialized-agent-{Guid.NewGuid().ToString("N")[..8]}
- Purpose: Handle subtask: {subtask}
- Required Tools: To be determined based on subtask requirements
- Can Auto-Create: true";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to analyze subtask: {Subtask}", subtask);
            return $@"🎯 Task Dispatcher Analysis for Subtask

**Subtask:** {subtask}

**Analysis Result:** AnalysisError

**Recommended Action:** Retry analysis or escalate to human intervention

**Error:** {ex.Message}";
        }
    }

    private CallableAgent? FindBestMatchingAgent(string subtask)
    {
        return null;// Disabled for now
        // Simple keyword matching for now
        var lowerSubtask = subtask.ToLower();
        
        foreach (var agent in _availableAgents.Values)
        {
            var lowerDesc = agent.Description.ToLower();
            
            // Check for web search tasks
            if ((lowerSubtask.Contains("search") || lowerSubtask.Contains("find") || lowerSubtask.Contains("gdp") || lowerSubtask.Contains("data")) &&
                (lowerDesc.Contains("search") || lowerDesc.Contains("web")))
            {
                return agent;
            }
            
            // Check for math tasks
            if ((lowerSubtask.Contains("calculate") || lowerSubtask.Contains("percentage") || lowerSubtask.Contains("divide") || lowerSubtask.Contains("ratio")) &&
                (lowerDesc.Contains("math") || lowerDesc.Contains("calculation")))
            {
                return agent;
            }
        }
        
        return null;
    }

    #endregion
} 
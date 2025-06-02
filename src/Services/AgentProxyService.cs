using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Orleans;
using PsiOrleans.Grains;

namespace PsiOrleans.Services;

/// <summary>
/// Agent Proxy Service - Exposes agents as KernelFunctions
/// These functions act as proxies to ConfigurableAgentGrain instances, taking natural language queries
/// and delegating to specialized agent grains that contain the actual logic
/// </summary>
public class AgentProxyService
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<AgentProxyService> _logger;

    public AgentProxyService(IClusterClient clusterClient, ILogger<AgentProxyService> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    /// <summary>
    /// Task Dispatcher Agent Proxy - Analyzes a single subtask to determine handling approach
    /// </summary>
    [KernelFunction("task_dispatcher")]
    [Description("Analyzes a single subtask to determine if existing agents can handle it, if a new agent needs to be created, or if it's impossible. Use this for each subtask individually.")]
    public async Task<string> TaskDispatcherAsync(
        [Description("Single subtask to analyze (e.g., 'Find US GDP data for 2024', 'Calculate percentage of two numbers')")] string subtask)
    {
        _logger.LogInformation("🎯 TaskDispatcher proxy called for subtask: {Subtask}", subtask);
        
        try
        {
            var taskDispatcher = _clusterClient.GetGrain<IConfigurableAgentGrain>("task-dispatcher");
            
            // Check if the agent is initialized
            var isInitialized = await taskDispatcher.IsInitializedAsync();
            if (!isInitialized)
            {
                return "Error: Task Dispatcher (Agent X) is not initialized. Please initialize it first.";
            }
            
            // Execute the subtask analysis through the configurable agent interface
            var analysisResult = await taskDispatcher.ExecuteTaskAsync(subtask);
            
            _logger.LogInformation("✅ TaskDispatcher proxy completed subtask analysis");
            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in TaskDispatcher proxy");
            return $"Error in Task Dispatcher: {ex.Message}";
        }
    }

    /// <summary>
    /// Task Feasibility Check - Quick feasibility assessment without full breakdown
    /// </summary>
    [KernelFunction("check_task_feasibility")]
    [Description("Quickly assess if a task can be completed with current agent ecosystem. Returns feasibility status without full analysis.")]
    public async Task<string> CheckTaskFeasibilityAsync(
        [Description("Task to assess for feasibility")] string task)
    {
        _logger.LogInformation("📊 TaskFeasibility check called for: {Task}", task);
        
        try
        {
            var taskDispatcher = _clusterClient.GetGrain<IConfigurableAgentGrain>("task-dispatcher");
            
            var isInitialized = await taskDispatcher.IsInitializedAsync();
            if (!isInitialized)
            {
                return "Error: Task Dispatcher not initialized.";
            }
            
            // Simple feasibility check by analyzing the task
            var analysisResult = await taskDispatcher.ExecuteTaskAsync($"Assess feasibility of this task: {task}");
            
            // Extract feasibility status from the analysis
            if (analysisResult.Contains("ExistingAgentMatch"))
            {
                return "✅ Fully Feasible - Can be completed with existing agents";
            }
            else if (analysisResult.Contains("RequiresNewAgent"))
            {
                return "🔧 Feasible with New Agents - Requires creating specialized agents";
            }
            else if (analysisResult.Contains("CannotComplete"))
            {
                return "❌ Not Feasible - Cannot be completed with available capabilities";
            }
            else
            {
                return "🔄 Analysis in Progress";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in feasibility check");
            return $"Error checking feasibility: {ex.Message}";
        }
    }

    /// <summary>
    /// Web Search Agent Proxy - Delegates web search queries to Agent B (web-search-agent grain)
    /// </summary>
    [KernelFunction("web_search_agent")]
    [Description("Delegates web search tasks to the specialized Web Search Agent (Agent B). Pass natural language queries about finding web data.")]
    public async Task<string> WebSearchAgentAsync(
        [Description("Natural language query for web search (e.g., 'Find US GDP for 2024', 'Search for New York State economic data')")] string query)
    {
        _logger.LogInformation("🔗 WebSearchAgent proxy called with query: {Query}", query);
        
        try
        {
            var webSearchAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>("web-search-agent");
            
            // Check if the agent is initialized
            var isInitialized = await webSearchAgent.IsInitializedAsync();
            if (!isInitialized)
            {
                return "Error: Web Search Agent (Agent B) is not initialized. Please initialize it first.";
            }
            
            // Delegate the natural language query to the specialized agent
            var result = await webSearchAgent.ExecuteTaskAsync(query);
            
            _logger.LogInformation("✅ WebSearchAgent proxy completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in WebSearchAgent proxy");
            return $"Error in Web Search Agent: {ex.Message}";
        }
    }

    /// <summary>
    /// Math Agent Proxy - Delegates mathematical calculations to Agent C (math-agent grain)
    /// </summary>
    [KernelFunction("math_agent")]
    [Description("Delegates mathematical calculation tasks to the specialized Math Agent (Agent C). Pass natural language queries about calculations.")]
    public async Task<string> MathAgentAsync(
        [Description("Natural language query for mathematical operations (e.g., 'Calculate what percentage 2.0 trillion is of 28.7 trillion', 'Divide these GDP numbers')")] string query)
    {
        _logger.LogInformation("🔗 MathAgent proxy called with query: {Query}", query);
        
        try
        {
            var mathAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>("math-agent");
            
            // Check if the agent is initialized
            var isInitialized = await mathAgent.IsInitializedAsync();
            if (!isInitialized)
            {
                return "Error: Math Agent (Agent C) is not initialized. Please initialize it first.";
            }
            
            // Delegate the natural language query to the specialized agent
            var result = await mathAgent.ExecuteTaskAsync(query);
            
            _logger.LogInformation("✅ MathAgent proxy completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in MathAgent proxy");
            return $"Error in Math Agent: {ex.Message}";
        }
    }

    /// <summary>
    /// Generic Agent Proxy - Calls any ConfigurableAgentGrain by ID
    /// </summary>
    [KernelFunction("call_agent")]
    [Description("Calls any ConfigurableAgentGrain by its ID with a natural language query")]
    public async Task<string> CallAgentAsync(
        [Description("The unique ID of the agent to call (e.g., 'web-search-agent', 'math-agent')")] string agentId,
        [Description("Natural language query for the agent")] string query)
    {
        _logger.LogInformation("🔗 Generic agent proxy called for {AgentId} with query: {Query}", agentId, query);
        
        try
        {
            var agent = _clusterClient.GetGrain<IConfigurableAgentGrain>(agentId);
            
            // Check if the agent is initialized
            var isInitialized = await agent.IsInitializedAsync();
            if (!isInitialized)
            {
                return $"Error: Agent '{agentId}' is not initialized.";
            }
            
            // Delegate the natural language query to the specified agent
            var result = await agent.ExecuteTaskAsync(query);
            
            _logger.LogInformation("✅ Generic agent proxy for {AgentId} completed successfully", agentId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in generic agent proxy for {AgentId}", agentId);
            return $"Error calling agent '{agentId}': {ex.Message}";
        }
    }

    /// <summary>
    /// Check agent status - useful for debugging multi-agent systems
    /// </summary>
    [KernelFunction("check_agent_status")]
    [Description("Checks if a specific agent is initialized and ready to handle requests")]
    public async Task<string> CheckAgentStatusAsync(
        [Description("The unique ID of the agent to check (e.g., 'web-search-agent', 'math-agent')")] string agentId)
    {
        _logger.LogInformation("🔍 Checking status of agent: {AgentId}", agentId);
        
        try
        {
            var agent = _clusterClient.GetGrain<IConfigurableAgentGrain>(agentId);
            var isInitialized = await agent.IsInitializedAsync();
            
            if (isInitialized)
            {
                var config = await agent.GetConfigurationAsync();
                var tools = await agent.GetAvailableToolsAsync();
                var metrics = await agent.GetMetricsAsync();
                
                var status = $"Agent '{agentId}' Status:\n" +
                           $"✅ Initialized: {isInitialized}\n" +
                           $"🤖 Name: {config?.AgentName ?? "Unknown"}\n" +
                           $"🔧 Tools Available: {tools.Count}\n" +
                           $"📊 Tasks Completed: {metrics.TotalTasks}\n" +
                           $"📈 Success Rate: {(metrics.TotalTasks > 0 ? (double)metrics.SuccessfulTasks / metrics.TotalTasks * 100 : 0):F1}%";
                
                return status;
            }
            else
            {
                return $"Agent '{agentId}': ❌ Not Initialized";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error checking agent status for {AgentId}", agentId);
            return $"Error checking agent '{agentId}': {ex.Message}";
        }
    }
} 
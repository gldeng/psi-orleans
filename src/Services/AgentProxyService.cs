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
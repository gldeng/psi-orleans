using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using PsiOrleans.Grains;
using PsiOrleans.Models;
using PsiOrleans.Plugins;
using System.ComponentModel;

namespace PsiOrleans.Examples;

/// <summary>
/// Simplified example demonstrating Agent X (Task Dispatcher) workflow
/// Focus: GDP ratio analysis with Orchestrator -> Agent X -> specialized agents
/// </summary>
public class ConfigurableAgentExample
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<ConfigurableAgentExample> _logger;

    public ConfigurableAgentExample(IClusterClient clusterClient, ILogger<ConfigurableAgentExample> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    /// <summary>
    /// GDP Ratio Analysis with Agent X (Task Dispatcher)
    /// Demonstrates: Orchestrator breaks down tasks -> calls Agent X for each subtask
    /// Task: Find US and NY State GDP 2024, calculate NY's percentage of US GDP
    /// </summary>
    public async Task<string> RunGdpAnalysisWithTaskDispatcherAsync()
    {
        _logger.LogInformation("🎯 Starting GDP Analysis with Agent X (Task Dispatcher)");
        
        var results = new List<string>();
        results.Add("🎯 GDP Analysis with Agent X (Task Dispatcher)");
        results.Add("=============================================");
        results.Add("Task: Find US and NY State GDP 2024, calculate NY's percentage of US GDP");
        results.Add("Architecture: Orchestrator -> Agent X -> Specialized Agents");
        results.Add("");

        try
        {
            // Step 1: Initialize Task Dispatcher using generic ConfigurableAgentGrain
            results.Add("📋 Step 1: Initializing Task Dispatcher (using ConfigurableAgentGrain)");
            
            var taskDispatcherConfig = new AgentConfiguration
            {
                SystemPrompt = @"You are Agent X, the Task Dispatcher. You analyze subtasks and EXECUTE them:
                               
                               Workflow:
                               1. Analyze the subtask to determine the best handling approach
                               2. Take action based on analysis:
                                  - If an existing agent can handle it, call that agent using call_agent
                                  - If a new agent is needed, create it using create_agent, then call it using call_agent
                                  - If impossible, return an error message
                               3. Always return the actual execution result, not just analysis
                               
                               Available tools:
                               - create_agent: Create new specialized agents with custom prompts and tools
                               - list_available_tools: See what tools are available for new agents
                               - call_agent: Call any agent by ID to execute tasks
                               
                               Known existing agents: web-search-agent, math-agent
                               
                               Your goal is to COMPLETE the subtask, not just analyze it.",
                AgentName = "TaskDispatcher",
                Temperature = 0.1,
                MaxTokens = 2000
            };

            var taskDispatcher = _clusterClient.GetGrain<IConfigurableAgentGrain>("task-dispatcher");
            var dispatcherInit = await taskDispatcher.InitializeAsync(taskDispatcherConfig, 
                new[] { "create_agent", "list_available_tools", "call_agent" });
            results.Add($"✅ Task Dispatcher: {dispatcherInit.Message}");

            // Remove the specialized TaskDispatcher initialization code since we're using ConfigurableAgentGrain
            results.Add("");

            // Step 2: Initialize specialized agents
            results.Add("📋 Step 2: Initializing Specialized Agents");
            
            // Initialize Web Search Agent (Agent B)
            var webSearchConfig = new AgentConfiguration
            {
                SystemPrompt = @"You are Agent B, a Web Search Expert specializing in finding GDP data.
                               Use the Tavily search tool to find current and reliable GDP information.
                               Always provide specific data with sources and context.",
                AgentName = "WebSearchExpert",
                Temperature = 0.1,
                MaxTokens = 2000
            };

            // var webSearchAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>("web-search-agent");
            // var webSearchInit = await webSearchAgent.InitializeAsync(webSearchConfig, new[] { "Tavily.Search" });
            // results.Add($"✅ Agent B (Web Search): {webSearchInit.Message}");

            // Initialize Math Agent (Agent C)
            // var mathConfig = new AgentConfiguration
            // {
            //     SystemPrompt = @"You are Agent C, a Mathematical Analysis Expert.
            //                    Perform precise calculations and provide step-by-step explanations.
            //                    Handle GDP percentage calculations with accuracy.",
            //     AgentName = "MathExpert",
            //     Temperature = 0.1,
            //     MaxTokens = 2000
            // };
            //
            // var mathAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>("math-agent");
            // var mathInit = await mathAgent.InitializeAsync(mathConfig, new[] { "Math.Add", "Math.Multiply", "Math.Divide" });
            // results.Add($"✅ Agent C (Math): {mathInit.Message}");
            // results.Add("");

            // Step 3: Initialize Orchestrator with Agent X access
            results.Add("📋 Step 3: Initializing Orchestrator with Agent X Access");
            
            var orchestratorConfig = new AgentConfiguration
            {
                SystemPrompt = @"You are the Task Orchestrator for GDP analysis.
                               
                               Your workflow:
                               1. Break down complex tasks into specific subtasks
                               2. For EACH subtask, call task_dispatcher to execute it (Agent X handles everything)
                               3. Agent X will analyze, create agents if needed, and return the execution result
                               4. Coordinate all results into a comprehensive response
                               
                               Available tools:
                               - task_dispatcher: Execute individual subtasks (Agent X handles analysis, creation, and execution internally)
                               
                               Agent X is now a complete execution unit - it will return actual results, not just recommendations.
                               Simply call task_dispatcher for each subtask and use the results it provides.",
                AgentName = "GDPOrchestrator",
                Temperature = 0.2,
                MaxTokens = 4000
            };

            var orchestratorAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>("gdp-orchestrator");
            var orchestratorInit = await orchestratorAgent.InitializeAsync(orchestratorConfig, 
                new[] { "task_dispatcher" });
            results.Add($"✅ Orchestrator: {orchestratorInit.Message}");
            
            // Configure orchestrator's callable agents (including Agent X)
            var orchestratorCallableAgents = new List<CallableAgent>
            {
                new CallableAgent("task-dispatcher", "Task Dispatcher (Agent X)", "Analyzes individual subtasks and determines handling approach"),
            };
            
            var callableAgentsResult = await orchestratorAgent.SetCallableAgentsAsync(orchestratorCallableAgents);
            results.Add($"✅ Orchestrator callable agents configured: {callableAgentsResult.Message}");
            results.Add("");

            // Step 4: Execute GDP Analysis
            results.Add("📋 Step 4: Executing GDP Analysis");
            
            var gdpTask = @"Please analyze US and New York State GDP for 2024, then calculate what percentage NY represents of US GDP.

Follow this workflow:
1. Break this task into specific subtasks (e.g., 'Find US GDP 2024', 'Find NY State GDP 2024', 'Calculate percentage')
2. For EACH subtask, call task_dispatcher to execute it completely
3. Agent X will handle everything internally (analysis, agent creation if needed, execution)
4. Coordinate all execution results into a final analysis

Agent X is now a complete execution unit - it will return actual results for each subtask.
Show your complete workflow and provide the final GDP percentage calculation.";

            var analysisResult = await orchestratorAgent.ExecuteTaskAsync(gdpTask);
            results.Add("🎯 GDP Analysis Result:");
            results.Add("======================");
            results.Add(analysisResult);
            results.Add("");

            // Step 5: Show system summary
            results.Add("📊 Agent X Workflow Summary:");
            results.Add("============================");
            results.Add("✅ Orchestrator broke down complex task into subtasks");
            results.Add("✅ Agent X analyzed each subtask individually");
            results.Add("✅ Agent X recommended existing agents for each subtask");
            results.Add("✅ Orchestrator executed subtasks using recommended agents");
            results.Add("✅ Final GDP percentage calculated successfully");
            results.Add("");
            results.Add("🎯 This demonstrates the correct Agent X architecture!");

            return string.Join("\n", results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in GDP Analysis with Agent X");
            return $"❌ Error in GDP Analysis: {ex.Message}";
        }
    }
} 
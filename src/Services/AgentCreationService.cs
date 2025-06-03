using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Orleans;
using PsiOrleans.Grains;
using PsiOrleans.Models;
using System.Collections.Concurrent;

namespace PsiOrleans.Services;

/// <summary>
/// Service providing agent creation capabilities as a KernelFunction
/// Can be used by any agent that needs to create new specialized agents
/// Now includes agent tracking and reuse capabilities
/// </summary>
public class AgentCreationService
{
    private readonly IClusterClient _clusterClient;
    private readonly IKernelFunctionRegistry _functionRegistry;
    private readonly ILogger<AgentCreationService> _logger;
    
    // Static registry to track all created agents across the system
    private static readonly ConcurrentDictionary<string, CreatedAgent> _createdAgents = new();

    public AgentCreationService(
        IClusterClient clusterClient,
        IKernelFunctionRegistry functionRegistry,
        ILogger<AgentCreationService> logger)
    {
        _clusterClient = clusterClient;
        _functionRegistry = functionRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Create a new specialized agent with custom prompt and tools
    /// </summary>
    [KernelFunction("create_agent")]
    [Description("Creates a new specialized agent with custom system prompt and specified tools. The LLM decides the prompt and tool selection.")]
    public async Task<string> CreateAgentAsync(
        [Description("Unique ID for the new agent (e.g., 'gdp-data-agent')")] string agentId,
        [Description("Name for the new agent (e.g., 'GDP Data Specialist')")] string agentName,
        [Description("Custom system prompt for the new agent - this defines the agent's behavior and expertise")] string systemPrompt,
        [Description("Comma-separated list of tool names this agent should have access to (e.g., 'Tavily.Search,Math.Add,Math.Divide')")] string toolNames,
        [Description("Brief description of what this agent specializes in (optional)")] string specialization = "")
    {
        try
        {
            // Check if agent already exists
            if (_createdAgents.ContainsKey(agentId))
            {
                var existingAgent = _createdAgents[agentId];
                existingAgent.RecordUsage();
                
                return $@"ℹ️ Agent '{agentId}' already exists and will be reused.
Agent: {existingAgent.AgentName}
Usage Count: {existingAgent.UsageCount}
Tools: {string.Join(", ", existingAgent.Tools)}

No need to create a new agent. Use call_agent with ID '{agentId}' to execute tasks.";
            }
            
            _logger.LogInformation("🤖 Creating new agent: {AgentId} with tools: {Tools}", agentId, toolNames);
            
            // Parse and validate tool names
            var requestedTools = toolNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                         .Select(t => t.Trim())
                                         .ToList();
            
            // Get available tools from registry
            var availableTools = _functionRegistry.GetAllAvailableToolNames().ToList();
            var validTools = requestedTools.Where(t => availableTools.Contains(t)).ToList();
            var invalidTools = requestedTools.Except(validTools).ToList();
            
            if (invalidTools.Any())
            {
                _logger.LogWarning("⚠️ Some requested tools are not available: {InvalidTools}", string.Join(", ", invalidTools));
            }
            
            if (!validTools.Any())
            {
                return $"❌ Error: None of the requested tools are available. Available tools: {string.Join(", ", availableTools.Take(10))}...";
            }
            
            // Create agent configuration with custom prompt
            var agentConfig = new AgentConfiguration
            {
                SystemPrompt = systemPrompt,
                AgentName = agentName,
                Temperature = 0.1,
                MaxTokens = 2000
            };
            
            // Create and initialize the new agent
            var newAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>(agentId);
            var initResult = await newAgent.InitializeAsync(agentConfig, validTools);
            
            if (initResult.Success)
            {
                // Track the created agent
                var createdAgent = new CreatedAgent(agentId, agentName, systemPrompt, validTools, specialization);
                _createdAgents.TryAdd(agentId, createdAgent);
                
                var successMessage = $@"✅ Successfully created and tracked new agent: {agentName}
- Agent ID: {agentId}
- Specialization: {specialization}
- Tools: {string.Join(", ", validTools)}
- Custom Prompt: {systemPrompt.Substring(0, Math.Min(100, systemPrompt.Length))}...
- Status: {initResult.Message}
- Total Created Agents: {_createdAgents.Count}";
                
                _logger.LogInformation("✅ Created and tracked agent {AgentId} successfully", agentId);
                return successMessage;
            }
            else
            {
                var errorMessage = $"❌ Failed to initialize agent {agentId}: {initResult.Message}";
                _logger.LogError("❌ Failed to create agent {AgentId}: {Error}", agentId, initResult.Message);
                return errorMessage;
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"❌ Error creating agent {agentId}: {ex.Message}";
            _logger.LogError(ex, "❌ Error creating agent {AgentId}", agentId);
            return errorMessage;
        }
    }

    /// <summary>
    /// Get all available tools from the function registry
    /// </summary>
    [KernelFunction("list_available_tools")]
    [Description("Lists all available tools that can be assigned to new agents")]
    public async Task<string> ListAvailableToolsAsync()
    {
        try
        {
            var availableTools = _functionRegistry.GetAllAvailableToolNames().ToList();
            
            var result = $@"📋 Available Tools for Agent Creation:
Total: {availableTools.Count} tools

Tools:
{string.Join("\n", availableTools.Select(t => $"- {t}"))}

Usage: Use these tool names in the create_agent function's toolNames parameter.";
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error listing available tools");
            return $"❌ Error listing tools: {ex.Message}";
        }
    }
    
    /// <summary>
    /// List all created agents with their metadata
    /// </summary>
    [KernelFunction("list_created_agents")]
    [Description("Lists all agents that have been created by the system with their usage statistics")]
    public async Task<string> ListCreatedAgentsAsync()
    {
        try
        {
            if (!_createdAgents.Any())
            {
                return "📋 No agents have been created yet by the system.";
            }
            
            var agents = _createdAgents.Values.OrderByDescending(a => a.UsageCount).ToList();
            
            var result = $@"📋 Created Agents Registry:
Total: {agents.Count} agents

Agents (sorted by usage):
{string.Join("\n", agents.Select(a => $"- {a}"))}

You can reuse these agents by calling them with call_agent using their Agent ID.";
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error listing created agents");
            return $"❌ Error listing created agents: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Find suitable existing agents for a given task
    /// </summary>
    [KernelFunction("find_suitable_agent")]
    [Description("Finds existing created agents that might be suitable for handling a specific task")]
    public async Task<string> FindSuitableAgentAsync(
        [Description("Description of the task to find a suitable agent for")] string taskDescription)
    {
        try
        {
            if (!_createdAgents.Any())
            {
                return "🔍 No created agents available. You may need to create a new agent for this task.";
            }
            
            var suitableAgents = _createdAgents.Values
                .Where(a => a.MightHandleTask(taskDescription))
                .OrderByDescending(a => a.UsageCount)
                .ToList();
            
            if (!suitableAgents.Any())
            {
                var allAgents = string.Join(", ", _createdAgents.Values.Select(a => $"{a.AgentName} ({a.AgentId})"));
                return $@"🔍 No suitable existing agents found for task: '{taskDescription}'

Available agents: {allAgents}

Consider creating a new specialized agent for this task.";
            }
            
            var bestMatch = suitableAgents.First();
            var alternatives = suitableAgents.Skip(1).Take(2).ToList();
            
            var result = $@"🎯 Found suitable agent for task: '{taskDescription}'

Best Match: {bestMatch.AgentName} ({bestMatch.AgentId})
- Specialization: {bestMatch.Specialization}
- Usage Count: {bestMatch.UsageCount}
- Tools: {string.Join(", ", bestMatch.Tools)}";

            if (alternatives.Any())
            {
                result += $@"

Alternative options:
{string.Join("\n", alternatives.Select(a => $"- {a.AgentName} ({a.AgentId}) - Used {a.UsageCount} times"))}";
            }
            
            result += $"\n\nRecommendation: Use call_agent with agent ID '{bestMatch.AgentId}' to execute this task.";
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error finding suitable agent");
            return $"❌ Error finding suitable agent: {ex.Message}";
        }
    }
} 
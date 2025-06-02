using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Orleans;
using PsiOrleans.Grains;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// Service providing agent creation capabilities as a KernelFunction
/// Can be used by any agent that needs to create new specialized agents
/// </summary>
public class AgentCreationService
{
    private readonly IClusterClient _clusterClient;
    private readonly IKernelFunctionRegistry _functionRegistry;
    private readonly ILogger<AgentCreationService> _logger;

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
        [Description("Comma-separated list of tool names this agent should have access to (e.g., 'Tavily.Search,Math.Add,Math.Divide')")] string toolNames)
    {
        try
        {
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
                var successMessage = $@"✅ Successfully created new agent: {agentName}
- Agent ID: {agentId}
- Tools: {string.Join(", ", validTools)}
- Custom Prompt: {systemPrompt.Substring(0, Math.Min(100, systemPrompt.Length))}...
- Status: {initResult.Message}";
                
                _logger.LogInformation("✅ Created agent {AgentId} successfully", agentId);
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
} 
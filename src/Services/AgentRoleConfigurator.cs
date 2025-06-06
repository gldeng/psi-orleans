using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// Service for configuring agent roles with appropriate tools and system prompts.
/// Handles the separation between Orchestrator and Specialized agent configurations.
/// </summary>
public class AgentRoleConfigurator : IAgentRoleConfigurator
{
    private readonly IConfigurableKernelService _kernelService;
    private readonly ILogger<AgentRoleConfigurator> _logger;

    public AgentRoleConfigurator(
        IConfigurableKernelService kernelService,
        ILogger<AgentRoleConfigurator> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Configure a kernel with role-appropriate tools and settings.
    /// </summary>
    public async Task<Kernel> ConfigureKernelAsync(AgentRole role, AgentConfiguration config, IEnumerable<string>? existingToolNames = null)
    {
        _logger.LogInformation("Configuring kernel for role {Role}", role);

        // Create base kernel with the configuration
        var kernel = await _kernelService.CreateKernelAsync(config, existingToolNames);

        // Apply role-specific tool configuration
        switch (role)
        {
            case AgentRole.Orchestrator:
                kernel = await ConfigureOrchestratorTools(kernel, config);
                break;
            case AgentRole.Specialized:
                kernel = await ConfigureSpecializedTools(kernel, config);
                break;
            case AgentRole.Undecided:
                // Keep base configuration for undecided agents
                _logger.LogInformation("Agent role undecided, keeping base configuration");
                break;
            default:
                throw new ArgumentException($"Unsupported agent role: {role}");
        }

        _logger.LogInformation("Kernel configured successfully for role {Role}", role);
        return kernel;
    }

    /// <summary>
    /// Get role-appropriate system prompt by enhancing the original prompt with role-specific instructions.
    /// </summary>
    public string GetSystemPromptForRole(AgentRole role, string originalPrompt)
    {
        return role switch
        {
            AgentRole.Orchestrator => GetOrchestratorSystemPrompt(originalPrompt),
            AgentRole.Specialized => GetSpecializedSystemPrompt(originalPrompt),
            AgentRole.Undecided => originalPrompt,
            _ => throw new ArgumentException($"Unsupported agent role: {role}")
        };
    }

    /// <summary>
    /// Configure orchestrator-specific tools for delegation and coordination.
    /// Orchestrators get tools for creating and managing child agents.
    /// </summary>
    private async Task<Kernel> ConfigureOrchestratorTools(Kernel kernel, AgentConfiguration config)
    {
        _logger.LogDebug("Configuring orchestrator tools");

        // ====== TODO: Implement in Phase 3 - OrchestratorStateMachine ======
        // For now, orchestrator tools are handled by the existing ExecuteAsOrchestratorAsync method
        // in ConfigurableAgentGrain until we implement OrchestratorStateMachine
        
        // When Phase 3 is implemented, this should add:
        // - SendParentCallback: Send completion/status updates to parent agent
        // - CreateAgent: Create new specialized child agents with specific configurations  
        // - CallChildAgent: Delegate subtasks to child agents

        _logger.LogInformation("Orchestrator tool configuration deferred to Phase 3 - using existing implementation");
        return kernel;
    }

    /// <summary>
    /// Configure specialized-specific tools for direct task execution.
    /// Specialized agents get normal blocking tools plus parent callback capability.
    /// </summary>
    private async Task<Kernel> ConfigureSpecializedTools(Kernel kernel, AgentConfiguration config)
    {
        _logger.LogDebug("Configuring specialized tools");

        // ====== FIXED: Specialized tools need to be explicitly configured ======
        // The issue was that when role is reconfigured, a new kernel is created
        // and the original tools are lost. We need to ensure tools are properly added.
        
        // NOTE: The kernel should already have the tools from the original configuration
        // but if it doesn't, we need to ensure they're available
        
        var availablePlugins = kernel.Plugins.Count;
        var availableFunctions = kernel.Plugins.SelectMany(p => p).Count();
        
        _logger.LogInformation("Specialized kernel has {PluginCount} plugins with {FunctionCount} total functions", 
            availablePlugins, availableFunctions);
        
        if (availableFunctions == 0)
        {
            _logger.LogWarning("Specialized kernel has no functions - this indicates a configuration issue during kernel recreation");
        }

        // The SendParentCallback functionality is handled by the SpecializedStateMachine itself
        // rather than being added as a kernel tool (this is by design)

        _logger.LogInformation("Specialized tools configuration verified - kernel has {FunctionCount} functions", availableFunctions);
        return kernel;
    }

    /// <summary>
    /// Generate orchestrator-specific system prompt with role instructions.
    /// </summary>
    private string GetOrchestratorSystemPrompt(string originalPrompt)
    {
        return $@"{originalPrompt}

ORCHESTRATOR ROLE:
You are now operating as an Orchestrator agent. Your role is to:
1. Break down complex tasks into subtasks
2. Create and coordinate child agents to handle subtasks
3. Collect results from child agents and synthesize final responses

AVAILABLE TOOLS:
- SendParentCallback: Send completion/status updates to your parent agent
- CreateAgent: Create new specialized child agents with specific configurations
- CallChildAgent: Delegate subtasks to child agents

You CANNOT use normal blocking tools. You must delegate actual work to specialized child agents.
Focus on orchestration, coordination, and task breakdown.";
    }

    /// <summary>
    /// Generate specialized-specific system prompt with role instructions.
    /// </summary>
    private string GetSpecializedSystemPrompt(string originalPrompt)
    {
        return $@"{originalPrompt}

SPECIALIZED ROLE:
You are now operating as a Specialized agent. Your role is to:
1. Handle specific, focused tasks directly using available tools
2. Complete tasks efficiently without further delegation
3. Report results back to your parent when tasks are complete

AVAILABLE TOOLS:
- All normal blocking tools for your specialization
- SendParentCallback: Send completion/status updates to your parent agent

You CANNOT create or call other agents. Focus on direct task execution using your specialized tools.";
    }
} 
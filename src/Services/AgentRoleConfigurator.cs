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
        // Start role configurator tracing
        using var roleConfiguratorActivity = AgentTracingService.StartAgentActivity("AgentRoleConfigurator.ConfigureKernel", null);
        roleConfiguratorActivity?.SetTag("role_configurator.role", role.ToString());
        roleConfiguratorActivity?.SetTag("role_configurator.agent_name", config.AgentName);
        roleConfiguratorActivity?.SetTag("role_configurator.existing_tools_count", existingToolNames?.Count() ?? 0);
        
        _logger.LogInformation("Configuring kernel for role {Role}", role);

        try
        {
            // Phase 1: Create base kernel with configuration
            using var baseKernelActivity = AgentTracingService.StartKernelActivity("CreateBaseKernel", null);
            baseKernelActivity?.SetTag("base_kernel.agent_name", config.AgentName);
            baseKernelActivity?.SetTag("base_kernel.model_id", config.Model.ModelId);
            baseKernelActivity?.SetTag("base_kernel.temperature", config.Temperature.ToString());
            baseKernelActivity?.SetTag("base_kernel.max_tokens", config.MaxTokens.ToString());
            
            var kernel = await _kernelService.CreateKernelAsync(config, existingToolNames);
            
            var baseFunctionCount = kernel.Plugins.SelectMany(p => p).Count();
            baseKernelActivity?.SetTag("base_kernel.function_count", baseFunctionCount);
            AgentTracingService.SetSuccess(baseKernelActivity, $"Base kernel created with {baseFunctionCount} functions");

            // Phase 2: Apply role-specific tool configuration
            using var roleSpecificActivity = AgentTracingService.StartAgentActivity("ApplyRoleSpecificConfiguration", null);
            roleSpecificActivity?.SetTag("role_specific.role", role.ToString());
            roleSpecificActivity?.SetTag("role_specific.base_functions", baseFunctionCount);
            
            switch (role)
            {
                case AgentRole.Orchestrator:
                    kernel = await ConfigureOrchestratorTools(kernel, config);
                    roleSpecificActivity?.SetTag("role_specific.type", "OrchestratorTools");
                    break;
                case AgentRole.Specialized:
                    kernel = await ConfigureSpecializedTools(kernel, config);
                    roleSpecificActivity?.SetTag("role_specific.type", "SpecializedTools");
                    break;
                case AgentRole.Undecided:
                    // Keep base configuration for undecided agents
                    _logger.LogInformation("Agent role undecided, keeping base configuration");
                    roleSpecificActivity?.SetTag("role_specific.type", "BaseConfiguration");
                    break;
                default:
                    var error = new ArgumentException($"Unsupported agent role: {role}");
                    AgentTracingService.SetError(roleSpecificActivity, error);
                    throw error;
            }
            
            var finalFunctionCount = kernel.Plugins.SelectMany(p => p).Count();
            roleSpecificActivity?.SetTag("role_specific.final_functions", finalFunctionCount);
            AgentTracingService.SetSuccess(roleSpecificActivity, $"Role configuration applied, final count: {finalFunctionCount}");

            _logger.LogInformation("Kernel configured successfully for role {Role}", role);
            
            roleConfiguratorActivity?.SetTag("role_configurator.success", true);
            roleConfiguratorActivity?.SetTag("role_configurator.base_functions", baseFunctionCount);
            roleConfiguratorActivity?.SetTag("role_configurator.final_functions", finalFunctionCount);
            AgentTracingService.SetSuccess(roleConfiguratorActivity, $"Kernel configured for {role}: {baseFunctionCount} → {finalFunctionCount} functions");
            
            return kernel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring kernel for role {Role}", role);
            AgentTracingService.SetError(roleConfiguratorActivity, ex);
            throw;
        }
    }

    /// <summary>
    /// Get role-appropriate system prompt by enhancing the original prompt with role-specific instructions.
    /// </summary>
    public string GetSystemPromptForRole(AgentRole role, string originalPrompt)
    {
        // Start system prompt generation tracing
        using var promptGenerationActivity = AgentTracingService.StartAgentActivity("AgentRoleConfigurator.GetSystemPrompt", null);
        promptGenerationActivity?.SetTag("prompt_generation.role", role.ToString());
        promptGenerationActivity?.SetTag("prompt_generation.original_length", originalPrompt.Length);
        
        try
        {
            var enhancedPrompt = role switch
            {
                AgentRole.Orchestrator => GetOrchestratorSystemPrompt(originalPrompt),
                AgentRole.Specialized => GetSpecializedSystemPrompt(originalPrompt),
                AgentRole.Undecided => originalPrompt,
                _ => throw new ArgumentException($"Unsupported agent role: {role}")
            };
            
            promptGenerationActivity?.SetTag("prompt_generation.enhanced_length", enhancedPrompt.Length);
            promptGenerationActivity?.SetTag("prompt_generation.enhancement_ratio", (double)enhancedPrompt.Length / originalPrompt.Length);
            AgentTracingService.SetSuccess(promptGenerationActivity, $"System prompt enhanced for {role}: {originalPrompt.Length} → {enhancedPrompt.Length} chars");
            
            return enhancedPrompt;
        }
        catch (Exception ex)
        {
            AgentTracingService.SetError(promptGenerationActivity, ex);
            throw;
        }
    }

    /// <summary>
    /// Configure orchestrator-specific tools for delegation and coordination.
    /// Orchestrators get tools for creating and managing child agents.
    /// </summary>
    private async Task<Kernel> ConfigureOrchestratorTools(Kernel kernel, AgentConfiguration config)
    {
        // Start orchestrator tools configuration tracing
        using var orchestratorToolsActivity = AgentTracingService.StartAgentActivity("ConfigureOrchestratorTools", null);
        orchestratorToolsActivity?.SetTag("orchestrator_tools.input_functions", kernel.Plugins.SelectMany(p => p).Count());
        orchestratorToolsActivity?.SetTag("orchestrator_tools.pattern", "AsyncEventDriven");
        orchestratorToolsActivity?.SetTag("orchestrator_tools.capabilities", "Delegation,ChildManagement,CallbackProcessing");
        
        _logger.LogDebug("Configuring orchestrator tools");

        // ====== TODO: Implement in Phase 3 - OrchestratorStateMachine ======
        // For now, orchestrator tools are handled by the existing ExecuteAsOrchestratorAsync method
        // in ConfigurableAgentGrain until we implement OrchestratorStateMachine
        
        // When Phase 3 is implemented, this should add:
        // - SendParentCallback: Send completion/status updates to parent agent
        // - CreateAgent: Create new specialized child agents with specific configurations  
        // - CallChildAgent: Delegate subtasks to child agents

        _logger.LogInformation("Orchestrator tool configuration deferred to Phase 3 - using existing implementation");
        
        var finalFunctionCount = kernel.Plugins.SelectMany(p => p).Count();
        orchestratorToolsActivity?.SetTag("orchestrator_tools.final_functions", finalFunctionCount);
        orchestratorToolsActivity?.SetTag("orchestrator_tools.implementation_status", "Phase3Deferred");
        AgentTracingService.SetSuccess(orchestratorToolsActivity, $"Orchestrator tools configuration completed (deferred to existing implementation): {finalFunctionCount} functions");
        
        return kernel;
    }

    /// <summary>
    /// Configure specialized-specific tools for direct task execution.
    /// Specialized agents get normal blocking tools plus parent callback capability.
    /// </summary>
    private async Task<Kernel> ConfigureSpecializedTools(Kernel kernel, AgentConfiguration config)
    {
        // Start specialized tools configuration tracing
        using var specializedToolsActivity = AgentTracingService.StartAgentActivity("ConfigureSpecializedTools", null);
        specializedToolsActivity?.SetTag("specialized_tools.input_functions", kernel.Plugins.SelectMany(p => p).Count());
        specializedToolsActivity?.SetTag("specialized_tools.pattern", "SyncDirect");
        specializedToolsActivity?.SetTag("specialized_tools.capabilities", "DirectToolExecution,CompletionCallbacks");
        
        _logger.LogDebug("Configuring specialized tools");

        // Phase 1: Verify existing tool availability
        using var toolVerificationActivity = AgentTracingService.StartAgentActivity("VerifySpecializedTools", null);
        var availablePlugins = kernel.Plugins.Count;
        var availableFunctions = kernel.Plugins.SelectMany(p => p).Count();
        
        toolVerificationActivity?.SetTag("tool_verification.plugins", availablePlugins);
        toolVerificationActivity?.SetTag("tool_verification.functions", availableFunctions);
        
        _logger.LogInformation("Specialized kernel has {PluginCount} plugins with {FunctionCount} total functions", 
            availablePlugins, availableFunctions);
        
        if (availableFunctions == 0)
        {
            _logger.LogWarning("Specialized kernel has no functions - this indicates a configuration issue during kernel recreation");
            toolVerificationActivity?.SetTag("tool_verification.warning", "no_functions_available");
            AgentTracingService.SetSuccess(toolVerificationActivity, "Tool verification completed with warning: no functions available");
        }
        else
        {
            toolVerificationActivity?.SetTag("tool_verification.status", "functions_available");
            AgentTracingService.SetSuccess(toolVerificationActivity, $"Tool verification completed: {availableFunctions} functions available");
        }

        // The SendParentCallback functionality is handled by the SpecializedStateMachine itself
        // rather than being added as a kernel tool (this is by design)

        _logger.LogInformation("Specialized tools configuration verified - kernel has {FunctionCount} functions", availableFunctions);
        
        specializedToolsActivity?.SetTag("specialized_tools.final_functions", availableFunctions);
        specializedToolsActivity?.SetTag("specialized_tools.callback_handling", "StateMachineManaged");
        AgentTracingService.SetSuccess(specializedToolsActivity, $"Specialized tools configuration completed: {availableFunctions} functions verified");
        
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
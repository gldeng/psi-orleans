using Microsoft.Extensions.DependencyInjection;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// Factory implementation for creating appropriate state machine implementations based on agent role.
/// Uses dependency injection to resolve state machine instances.
/// </summary>
public class StateMachineFactory : IStateMachineFactory
{
    private readonly IServiceProvider _serviceProvider;

    public StateMachineFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Create the appropriate state machine implementation based on the agent role.
    /// </summary>
    public IAgentStateMachine CreateStateMachine(AgentRole role)
    {
        // Start state machine factory tracing
        using var factoryActivity = AgentTracingService.StartAgentActivity("StateMachineFactory.Create", null);
        factoryActivity?.SetTag("factory.role_requested", role.ToString());
        factoryActivity?.SetTag("factory.pattern_type", GetPatternType(role));
        
        try
        {
            var stateMachine = role switch
            {
                AgentRole.Orchestrator => CreateOrchestratorStateMachine(),
                AgentRole.Specialized => CreateSpecializedStateMachine(),
                AgentRole.Undecided => CreateSpecializedStateMachine(), // Default to specialized
                _ => throw new ArgumentException($"Unsupported agent role: {role}")
            };
            
            factoryActivity?.SetTag("factory.state_machine_type", stateMachine.GetType().Name);
            AgentTracingService.SetSuccess(factoryActivity, $"Created {stateMachine.GetType().Name} for {role} role");
            
            return stateMachine;
        }
        catch (Exception ex)
        {
            AgentTracingService.SetError(factoryActivity, ex);
            throw;
        }
    }
    
    private IAgentStateMachine CreateOrchestratorStateMachine()
    {
        using var orchestratorCreationActivity = AgentTracingService.StartAgentActivity("CreateOrchestratorStateMachine", null);
        orchestratorCreationActivity?.SetTag("creation.pattern", "AsyncEventDriven");
        orchestratorCreationActivity?.SetTag("creation.capabilities", "Delegation,ChildManagement,CallbackProcessing");
        
        try
        {
            var stateMachine = _serviceProvider.GetRequiredService<OrchestratorStateMachine>();
            AgentTracingService.SetSuccess(orchestratorCreationActivity, "OrchestratorStateMachine instance created");
            return stateMachine;
        }
        catch (Exception ex)
        {
            AgentTracingService.SetError(orchestratorCreationActivity, ex);
            throw;
        }
    }
    
    private IAgentStateMachine CreateSpecializedStateMachine()
    {
        using var specializedCreationActivity = AgentTracingService.StartAgentActivity("CreateSpecializedStateMachine", null);
        specializedCreationActivity?.SetTag("creation.pattern", "SyncDirect");
        specializedCreationActivity?.SetTag("creation.capabilities", "DirectToolExecution,CompletionCallbacks");
        
        try
        {
            var stateMachine = _serviceProvider.GetRequiredService<SpecializedStateMachine>();
            AgentTracingService.SetSuccess(specializedCreationActivity, "SpecializedStateMachine instance created");
            return stateMachine;
        }
        catch (Exception ex)
        {
            AgentTracingService.SetError(specializedCreationActivity, ex);
            throw;
        }
    }
    
    private static string GetPatternType(AgentRole role)
    {
        return role switch
        {
            AgentRole.Orchestrator => "AsyncEventDriven",
            AgentRole.Specialized => "SyncDirect",
            AgentRole.Undecided => "SyncDirect", // Default
            _ => "Unknown"
        };
    }
} 
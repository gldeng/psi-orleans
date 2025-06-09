using Microsoft.Extensions.DependencyInjection;
using PsiOrleans.Models;
using Microsoft.Extensions.Logging;

namespace PsiOrleans.Services;

/// <summary>
/// Factory implementation for creating appropriate state machine implementations based on agent role.
/// Uses dependency injection to resolve state machine instances.
/// </summary>
public class StateMachineFactory : IStateMachineFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StateMachineFactory> _logger;

    public StateMachineFactory(IServiceProvider serviceProvider, ILogger<StateMachineFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
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
        
        // 🟢 FLOW_START: State machine factory creation flow
        _logger.LogInformation("🟢 FLOW_START: StateMachineFactory.CreateStateMachine for role {Role}", role);
        
        try
        {
            // 🔄 FLOW_STEP: Determine execution pattern
            var patternType = GetPatternType(role);
            _logger.LogInformation("🔄 FLOW_STEP: Execution pattern determined - Role: {Role}, Pattern: {Pattern}", 
                role, patternType);

            // 🔀 FLOW_DECISION: Create appropriate state machine based on role
            _logger.LogInformation("🔀 FLOW_DECISION: Creating state machine for role {Role}", role);
            
            var stateMachine = role switch
            {
                AgentRole.Orchestrator => CreateOrchestratorStateMachine(),
                AgentRole.Specialized => CreateSpecializedStateMachine(),
                AgentRole.Undecided => CreateSpecializedStateMachine(), // Default to specialized
                _ => throw new ArgumentException($"Unsupported agent role: {role}")
            };
            
            factoryActivity?.SetTag("factory.state_machine_type", stateMachine.GetType().Name);
            
            // ✅ FLOW_SUCCESS: State machine created successfully
            _logger.LogInformation("✅ FLOW_SUCCESS: Created {StateMachineType} for {Role} role", 
                stateMachine.GetType().Name, role);
            
            // 🏁 FLOW_END: State machine factory creation flow complete
            _logger.LogInformation("🏁 FLOW_END: StateMachineFactory.CreateStateMachine - SUCCESS");
            
            AgentTracingService.SetSuccess(factoryActivity, $"Created {stateMachine.GetType().Name} for {role} role");
            
            return stateMachine;
        }
        catch (Exception ex)
        {
            // ❌ FLOW_ERROR: State machine creation failed
            _logger.LogError(ex, "❌ FLOW_ERROR: StateMachineFactory.CreateStateMachine failed for role {Role}: {Error}", 
                role, ex.Message);
                
            // 🏁 FLOW_END: State machine factory creation flow complete with error
            _logger.LogInformation("🏁 FLOW_END: StateMachineFactory.CreateStateMachine - FAILED");
            
            AgentTracingService.SetError(factoryActivity, ex);
            throw;
        }
    }
    
    private IAgentStateMachine CreateOrchestratorStateMachine()
    {
        using var orchestratorCreationActivity = AgentTracingService.StartAgentActivity("CreateOrchestratorStateMachine", null);
        orchestratorCreationActivity?.SetTag("creation.pattern", "AsyncEventDriven");
        orchestratorCreationActivity?.SetTag("creation.capabilities", "Delegation,ChildManagement,CallbackProcessing");
        
        // 🔄 FLOW_STEP: Creating orchestrator state machine
        _logger.LogInformation("🔄 FLOW_STEP: Creating OrchestratorStateMachine with AsyncEventDriven pattern");
        
        try
        {
            var stateMachine = _serviceProvider.GetRequiredService<OrchestratorStateMachine>();
            
            // ✅ FLOW_SUCCESS: Orchestrator state machine created
            _logger.LogInformation("✅ FLOW_SUCCESS: OrchestratorStateMachine instance created");
            
            AgentTracingService.SetSuccess(orchestratorCreationActivity, "OrchestratorStateMachine instance created");
            return stateMachine;
        }
        catch (Exception ex)
        {
            // ❌ FLOW_ERROR: Orchestrator state machine creation failed
            _logger.LogError(ex, "❌ FLOW_ERROR: OrchestratorStateMachine creation failed: {Error}", ex.Message);
            
            AgentTracingService.SetError(orchestratorCreationActivity, ex);
            throw;
        }
    }
    
    private IAgentStateMachine CreateSpecializedStateMachine()
    {
        using var specializedCreationActivity = AgentTracingService.StartAgentActivity("CreateSpecializedStateMachine", null);
        specializedCreationActivity?.SetTag("creation.pattern", "SyncDirect");
        specializedCreationActivity?.SetTag("creation.capabilities", "DirectToolExecution,CompletionCallbacks");
        
        // 🔄 FLOW_STEP: Creating specialized state machine
        _logger.LogInformation("🔄 FLOW_STEP: Creating SpecializedStateMachine with SyncDirect pattern");
        
        try
        {
            var stateMachine = _serviceProvider.GetRequiredService<SpecializedStateMachine>();
            
            // ✅ FLOW_SUCCESS: Specialized state machine created
            _logger.LogInformation("✅ FLOW_SUCCESS: SpecializedStateMachine instance created");
            
            AgentTracingService.SetSuccess(specializedCreationActivity, "SpecializedStateMachine instance created");
            return stateMachine;
        }
        catch (Exception ex)
        {
            // ❌ FLOW_ERROR: Specialized state machine creation failed
            _logger.LogError(ex, "❌ FLOW_ERROR: SpecializedStateMachine creation failed: {Error}", ex.Message);
            
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
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// Factory interface for creating appropriate state machine implementations based on agent role.
/// Provides the selection logic between async event-driven (Orchestrator) and sync direct execution (Specialized) patterns.
/// </summary>
public interface IStateMachineFactory
{
    /// <summary>
    /// Create the appropriate state machine implementation based on the agent role.
    /// 
    /// Orchestrator: Returns OrchestratorStateMachine for async event-driven execution
    /// Specialized: Returns SpecializedStateMachine for sync direct execution
    /// Undecided: Returns default implementation (SpecializedStateMachine)
    /// </summary>
    /// <param name="role">The agent role to create a state machine for</param>
    /// <returns>Appropriate state machine implementation</returns>
    IAgentStateMachine CreateStateMachine(AgentRole role);
} 
using PsiOrleans.Common.Models;

namespace PsiOrleans.Common.Interfaces;

/// <summary>
/// Defines the contract for orchestrating and coordinating multiple agents.
/// Implemented by the Orchestrator package to provide agent coordination capabilities.
/// </summary>
public interface IOrchestrator
{
    /// <summary>
    /// Coordinates multiple agents to work together on a complex task.
    /// </summary>
    /// <param name="agentIds">The collection of agent identifiers to coordinate.</param>
    /// <param name="taskDescription">The description of the task requiring coordination.</param>
    /// <param name="context">The agent context providing orchestration context.</param>
    /// <returns>The result of the orchestration including execution plan and coordinated agents.</returns>
    Task<OrchestratorResult> CoordinateAgentsAsync(IEnumerable<AgentId> agentIds, string taskDescription, IAgentContext context);

    /// <summary>
    /// Assigns a specific task to a target agent.
    /// </summary>
    /// <param name="targetAgent">The identifier of the agent to receive the task assignment.</param>
    /// <param name="taskDescription">The description of the task to assign.</param>
    /// <param name="context">The agent context providing assignment context.</param>
    /// <returns>True if the task was successfully assigned, false otherwise.</returns>
    Task<bool> AssignTaskAsync(AgentId targetAgent, string taskDescription, IAgentContext context);
} 
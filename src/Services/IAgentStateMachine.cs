using Microsoft.SemanticKernel;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// Base interface for agent state machine implementations.
/// Provides separation between async event-driven (Orchestrator) and sync direct execution (Specialized) patterns.
/// </summary>
public interface IAgentStateMachine
{
    /// <summary>
    /// Execute a task using the appropriate execution pattern for this state machine type.
    /// 
    /// Orchestrator Pattern: Async event-driven execution with delegation to child agents
    /// Specialized Pattern: Sync direct execution with immediate tool usage
    /// </summary>
    /// <param name="task">The task to execute</param>
    /// <param name="kernel">The configured Semantic Kernel instance</param>
    /// <param name="state">The current agent state</param>
    /// <param name="config">The agent configuration</param>
    /// <returns>Task execution result string</returns>
    Task<string> ExecuteTaskAsync(string task, Kernel kernel, ConfigurableAgentState state, AgentConfiguration config);

    /// <summary>
    /// Process callbacks received from child agents (primarily used by Orchestrator state machines).
    /// 
    /// Orchestrator Pattern: Uses LLM to analyze progress and make orchestration decisions
    /// Specialized Pattern: Generally unused as specialized agents don't create children
    /// </summary>
    /// <param name="callId">Unique identifier for the callback</param>
    /// <param name="message">Callback message content</param>
    /// <param name="isSuccess">Whether the callback indicates success or failure</param>
    /// <param name="state">The current agent state</param>
    /// <param name="kernel">The configured Semantic Kernel instance</param>
    Task ProcessCallbackAsync(string callId, string message, bool isSuccess, ConfigurableAgentState state, Kernel kernel);
} 
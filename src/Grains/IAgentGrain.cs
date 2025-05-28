using Orleans;
using PsiOrleans.Models;

namespace PsiOrleans.Grains;

/// <summary>
/// Orleans Grain interface for React Agent with Semantic Kernel integration
/// </summary>
public interface IAgentGrain : IGrainWithStringKey
{
    /// <summary>
    /// Execute a task using the React Agent pattern with Semantic Kernel
    /// </summary>
    Task<string> ExecuteTaskAsync(string task);
    
    /// <summary>
    /// Get the current agent state
    /// </summary>
    Task<AgentState> GetStateAsync();
    
    /// <summary>
    /// Get the execution history
    /// </summary>
    Task<List<AgentStep>> GetExecutionHistoryAsync();
    
    /// <summary>
    /// Reset the agent to initial state
    /// </summary>
    Task ResetAsync();
    
    /// <summary>
    /// Check if the current task is completed
    /// </summary>
    Task<bool> IsTaskCompletedAsync();
    
    /// <summary>
    /// Get performance metrics
    /// </summary>
    Task<(int TotalSteps, int SuccessfulSteps, int FailedSteps, TimeSpan ExecutionTime)> GetMetricsAsync();
} 
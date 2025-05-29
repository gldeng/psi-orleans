using Microsoft.SemanticKernel;
using Orleans;

namespace PsiOrleans.Models;

/// <summary>
/// Extended agent state for configurable agents
/// </summary>
[Serializable]
[GenerateSerializer]
public class ConfigurableAgentState : AgentState
{
    /// <summary>
    /// Agent configuration used to initialize this agent
    /// </summary>
    [Id(18)]
    public AgentConfiguration? Configuration { get; set; }
    
    /// <summary>
    /// Whether the agent has been properly initialized
    /// </summary>
    [Id(19)]
    public bool IsInitialized { get; set; } = false;
    
    /// <summary>
    /// Timestamp when the agent was initialized
    /// </summary>
    [Id(20)]
    public DateTime? InitializedAt { get; set; }
    
    /// <summary>
    /// Number of successful task executions
    /// </summary>
    [Id(21)]
    public int SuccessfulTasks { get; set; } = 0;
    
    /// <summary>
    /// Number of failed task executions
    /// </summary>
    [Id(22)]
    public int FailedTasks { get; set; } = 0;
    
    /// <summary>
    /// Total number of tasks attempted
    /// </summary>
    [Id(23)]
    public int TotalTasks { get; set; } = 0;
    
    /// <summary>
    /// Total execution time across all tasks
    /// </summary>
    [Id(24)]
    public new TimeSpan TotalExecutionTime { get; set; } = TimeSpan.Zero;
    
    /// <summary>
    /// Available kernel functions for this agent instance
    /// </summary>
    [Id(25)]
    public List<KernelFunctionMetadata> AvailableFunctions { get; set; } = new();
    
    /// <summary>
    /// Custom metadata for the agent
    /// </summary>
    [Id(26)]
    public Dictionary<string, object> CustomMetadata { get; set; } = new();
    
    /// <summary>
    /// Increment successful task counter
    /// </summary>
    public void IncrementSuccessfulTasks()
    {
        SuccessfulTasks++;
        TotalTasks++;
        LastUpdated = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Increment failed task counter
    /// </summary>
    public void IncrementFailedTasks()
    {
        FailedTasks++;
        TotalTasks++;
        LastUpdated = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Add execution time to total
    /// </summary>
    public void AddExecutionTime(TimeSpan duration)
    {
        TotalExecutionTime = TotalExecutionTime.Add(duration);
        LastUpdated = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Get success rate as percentage
    /// </summary>
    public double GetSuccessRate()
    {
        return TotalTasks == 0 ? 0.0 : (double)SuccessfulTasks / TotalTasks * 100.0;
    }
} 
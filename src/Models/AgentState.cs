using Orleans;

namespace PsiOrleans.Models;

[Serializable]
[GenerateSerializer]
public class AgentState
{
    [Id(0)]
    public string AgentId { get; set; } = string.Empty;
    
    [Id(1)]
    public string CurrentTask { get; set; } = string.Empty;
    
    [Id(2)]
    public List<AgentStep> ExecutionHistory { get; set; } = new();
    
    [Id(3)]
    public Dictionary<string, object> WorkingMemory { get; set; } = new();
    
    [Id(4)]
    public Dictionary<string, string> LongTermMemory { get; set; } = new();
    
    // Task execution state
    [Id(5)]
    public bool IsTaskCompleted { get; set; }
    
    [Id(6)]
    public string? FinalAnswer { get; set; }
    
    [Id(7)]
    public int CurrentStepNumber { get; set; } = 0;
    
    [Id(8)]
    public int MaxSteps { get; set; } = 15;
    
    // Timestamps
    [Id(9)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Id(10)]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    [Id(11)]
    public DateTime? TaskStartedAt { get; set; }
    
    [Id(12)]
    public DateTime? TaskCompletedAt { get; set; }
    
    // Performance metrics
    [Id(13)]
    public int TotalTokensUsed { get; set; }
    
    [Id(14)]
    public TimeSpan TotalExecutionTime { get; set; }
    
    [Id(15)]
    public int SuccessfulSteps { get; set; }
    
    [Id(16)]
    public int FailedSteps { get; set; }
} 
using Orleans;

namespace PsiOrleans.Models;

public enum StepType
{
    Thought,
    Action,
    Observation,
    Planning,
    FinalAnswer
}

[Serializable]
[GenerateSerializer]
public class AgentStep
{
    [Id(0)]
    public int StepNumber { get; set; }
    
    [Id(1)]
    public StepType Type { get; set; }
    
    [Id(2)]
    public string Content { get; set; } = string.Empty;
    
    [Id(3)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Semantic Kernel specific properties
    [Id(4)]
    public string? PluginName { get; set; }
    
    [Id(5)]
    public string? FunctionName { get; set; }
    
    [Id(6)]
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    [Id(7)]
    public string? Result { get; set; }
    
    [Id(8)]
    public bool IsSuccess { get; set; } = true;
    
    [Id(9)]
    public string? ErrorMessage { get; set; }
    
    // Token usage tracking
    [Id(10)]
    public int? InputTokens { get; set; }
    
    [Id(11)]
    public int? OutputTokens { get; set; }
} 
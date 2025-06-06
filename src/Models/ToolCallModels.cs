using Orleans;

namespace PsiOrleans.Models;

/// <summary>
/// Types of tool calls in the agent system
/// </summary>
[Serializable]
[GenerateSerializer]
public enum ToolCallType
{
    /// <summary>
    /// Normal tool call that executes and returns a result immediately
    /// </summary>
    [Id(0)]
    Normal,
    
    /// <summary>
    /// Task completion callback to parent agent
    /// </summary>
    [Id(1)]
    TaskCompletion,
    
    /// <summary>
    /// Subtask delegation to child agent
    /// </summary>
    [Id(2)]
    SubtaskDelegation
}

[Serializable]
[GenerateSerializer]
public abstract class BaseToolCall
{
    [Id(0)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Id(1)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Serializable]
[GenerateSerializer]
public class TaskCompletionToolCall : BaseToolCall
{
    [Id(0)]
    public ToolCallType Type { get; set; } = ToolCallType.TaskCompletion;
    
    [Id(1)]
    public string Result { get; set; } = string.Empty;
    
    [Id(2)]
    public bool IsSuccess { get; set; } = true;
    
    [Id(3)]
    public string? ErrorMessage { get; set; }
}

[Serializable]
[GenerateSerializer]
public class SubtaskDelegationToolCall : BaseToolCall
{
    [Id(0)]
    public ToolCallType Type { get; set; } = ToolCallType.SubtaskDelegation;
    
    [Id(1)]
    public string Subtask { get; set; } = string.Empty;
    
    [Id(2)]
    public string? ChildAgentId { get; set; }
    
    [Id(3)]
    public string? Instructions { get; set; }
    
    [Id(4)]
    public Dictionary<string, object> Parameters { get; set; } = new();
}

[Serializable]
[GenerateSerializer]
public class NormalToolCall : BaseToolCall
{
    [Id(0)]
    public ToolCallType Type { get; set; } = ToolCallType.Normal;
    
    [Id(1)]
    public string ToolName { get; set; } = string.Empty;
    
    [Id(2)]
    public string FunctionName { get; set; } = string.Empty;
    
    [Id(3)]
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    [Id(4)]
    public string? Result { get; set; }
    
    [Id(5)]
    public bool IsExecuted { get; set; }
    
    [Id(6)]
    public DateTime? ExecutedAt { get; set; }
}

[Serializable]
[GenerateSerializer]
public class LLMAnalysisResult
{
    [Id(0)]
    public string Analysis { get; set; } = string.Empty;
    
    [Id(1)]
    public List<BaseToolCall> ToolCalls { get; set; } = new();
    
    [Id(2)]
    public ToolCallType DominantToolType { get; set; }
    
    [Id(3)]
    public string Reasoning { get; set; } = string.Empty;
    
    [Id(4)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
} 
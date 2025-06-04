using Orleans;
using Microsoft.SemanticKernel;

namespace PsiOrleans.Models;

/// <summary>
/// Represents a pending agent function call that will be executed asynchronously
/// </summary>
[Serializable]
[GenerateSerializer]
public class PendingAgentCall
{
    /// <summary>
    /// Unique identifier for this function call
    /// </summary>
    [Id(0)]
    public string CallId { get; set; } = string.Empty;
    
    /// <summary>
    /// The function that was called
    /// </summary>
    [Id(1)]
    public string FunctionName { get; set; } = string.Empty;
    
    /// <summary>
    /// Plugin name if applicable
    /// </summary>
    [Id(2)]
    public string? PluginName { get; set; }
    
    /// <summary>
    /// Arguments passed to the function
    /// </summary>
    [Id(3)]
    public Dictionary<string, object?> Arguments { get; set; } = new();
    
    /// <summary>
    /// ID of the agent that initiated this call
    /// </summary>
    [Id(4)]
    public string CallingAgentId { get; set; } = string.Empty;
    
    /// <summary>
    /// ID of the target agent to call
    /// </summary>
    [Id(5)]
    public string TargetAgentId { get; set; } = string.Empty;
    
    /// <summary>
    /// The query/task to execute on target agent
    /// </summary>
    [Id(6)]
    public string Query { get; set; } = string.Empty;
    
    /// <summary>
    /// Current status of this call
    /// </summary>
    [Id(7)]
    public PendingCallStatus Status { get; set; } = PendingCallStatus.Pending;
    
    /// <summary>
    /// Timestamp when call was created
    /// </summary>
    [Id(8)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Timestamp when call was completed (success or failure)
    /// </summary>
    [Id(9)]
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Result returned from the target agent (if successful)
    /// </summary>
    [Id(10)]
    public string? Result { get; set; }
    
    /// <summary>
    /// Error message if the call failed
    /// </summary>
    [Id(11)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Indicates if this call should pause LLM execution until callback
    /// </summary>
    [Id(12)]
    public bool ShouldPauseLLMExecution { get; set; } = true;
}

/// <summary>
/// Status of a pending agent call
/// </summary>
[Serializable]
[GenerateSerializer]
public enum PendingCallStatus
{
    [Id(0)] Pending,
    [Id(1)] InProgress, 
    [Id(2)] Completed,
    [Id(3)] Failed,
    [Id(4)] Cancelled
} 
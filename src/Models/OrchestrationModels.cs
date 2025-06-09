using Orleans;

namespace PsiOrleans.Models;

/// <summary>
/// Orchestration decision types for LLM-based orchestration logic
/// </summary>
[GenerateSerializer]
public enum OrchestrationDecision
{
    /// <summary>
    /// All required callbacks received, task can be completed
    /// </summary>
    CompleteTask,
    
    /// <summary>
    /// Still waiting for pending callbacks from child agents
    /// </summary>
    WaitForMoreCallbacks,
    
    /// <summary>
    /// Need to create additional subtasks based on current progress
    /// </summary>
    CreateAdditionalTasks,
    
    /// <summary>
    /// Retry subtasks that failed due to timeouts
    /// </summary>
    RetryTimeouts
}

/// <summary>
/// Represents a subtask for delegation in orchestrator execution
/// </summary>
[Serializable]
[GenerateSerializer]
public class SubTask
{
    /// <summary>
    /// The task description to be delegated
    /// </summary>
    [Id(0)]
    public string Task { get; set; } = string.Empty;
    
    /// <summary>
    /// Suggested role for the agent handling this subtask
    /// </summary>
    [Id(1)]
    public AgentRole SuggestedRole { get; set; } = AgentRole.Specialized;
    
    /// <summary>
    /// List of tools/functions required for this subtask
    /// </summary>
    [Id(2)]
    public List<string> RequiredTools { get; set; } = new();
    
    /// <summary>
    /// Priority level for task execution (higher numbers = higher priority)
    /// </summary>
    [Id(3)]
    public int Priority { get; set; } = 1;
    
    /// <summary>
    /// ID of the child agent assigned to this subtask
    /// </summary>
    [Id(4)]
    public string? ChildAgentId { get; set; }
    
    /// <summary>
    /// Unique identifier for tracking this subtask
    /// </summary>
    [Id(5)]
    public string SubTaskId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Status of this subtask
    /// </summary>
    [Id(6)]
    public SubTaskStatus Status { get; set; } = SubTaskStatus.Pending;
}

/// <summary>
/// Status of a subtask in orchestration
/// </summary>
[GenerateSerializer]
public enum SubTaskStatus
{
    /// <summary>
    /// Subtask has been identified but not yet delegated
    /// </summary>
    Pending,
    
    /// <summary>
    /// Subtask has been delegated to a child agent
    /// </summary>
    Delegated,
    
    /// <summary>
    /// Subtask has been completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// Subtask failed during execution
    /// </summary>
    Failed
}

/// <summary>
/// Represents callback data for tracking child agent responses
/// </summary>
[Serializable]
[GenerateSerializer]
public class CallbackData
{
    /// <summary>
    /// Unique identifier for this callback
    /// </summary>
    [Id(0)]
    public string CallId { get; set; } = string.Empty;
    
    /// <summary>
    /// ID of the child agent that will send the callback
    /// </summary>
    [Id(1)]
    public string ChildAgentId { get; set; } = string.Empty;
    
    /// <summary>
    /// The subtask that was delegated
    /// </summary>
    [Id(2)]
    public string Task { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when the callback was expected to start
    /// </summary>
    [Id(3)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether this callback has been received
    /// </summary>
    [Id(4)]
    public bool IsReceived { get; set; } = false;
    
    /// <summary>
    /// The result message when callback is received
    /// </summary>
    [Id(5)]
    public string? ResultMessage { get; set; }
    
    /// <summary>
    /// Whether the delegated task was successful
    /// </summary>
    [Id(6)]
    public bool IsSuccess { get; set; } = false;
    
    /// <summary>
    /// Timestamp when the callback was received
    /// </summary>
    [Id(7)]
    public DateTime? ReceivedAt { get; set; }
}

/// <summary>
/// Represents a completed callback for historical tracking
/// </summary>
[Serializable]
[GenerateSerializer]
public class CompletedCallback
{
    /// <summary>
    /// Unique identifier for this callback
    /// </summary>
    [Id(0)]
    public string CallId { get; set; } = string.Empty;
    
    /// <summary>
    /// ID of the child agent that sent the callback
    /// </summary>
    [Id(1)]
    public string ChildAgentId { get; set; } = string.Empty;
    
    /// <summary>
    /// The task that was completed
    /// </summary>
    [Id(2)]
    public string Task { get; set; } = string.Empty;
    
    /// <summary>
    /// The result message from the child agent
    /// </summary>
    [Id(3)]
    public string ResultMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the task was completed successfully
    /// </summary>
    [Id(4)]
    public bool IsSuccess { get; set; } = false;
    
    /// <summary>
    /// Timestamp when the callback was received
    /// </summary>
    [Id(5)]
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// How long the task took to complete
    /// </summary>
    [Id(6)]
    public TimeSpan ExecutionTime { get; set; }
} 
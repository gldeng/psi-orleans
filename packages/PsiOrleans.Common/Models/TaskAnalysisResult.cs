namespace PsiOrleans.Common.Models;

/// <summary>
/// Represents the recommended approach for handling a task.
/// </summary>
public enum TaskApproach
{
    /// <summary>
    /// Task should be handled by direct execution (SPECIALIZED mode).
    /// </summary>
    DirectExecution,

    /// <summary>
    /// Task should be handled by orchestration (ORCHESTRATOR mode).
    /// </summary>
    Orchestration
}

/// <summary>
/// Represents the result of analyzing a task to determine the appropriate agent mode.
/// </summary>
public class TaskAnalysisResult
{
    /// <summary>
    /// Gets the recommended approach for handling this task.
    /// </summary>
    public TaskApproach RecommendedApproach { get; init; }

    /// <summary>
    /// Gets whether this task can be broken down into smaller subtasks.
    /// True for ORCHESTRATOR mode, false for SPECIALIZED mode.
    /// </summary>
    public bool CanBeDecomposed { get; init; }

    /// <summary>
    /// Gets any additional analysis notes or insights.
    /// </summary>
    public string AnalysisNotes { get; init; } = string.Empty;
} 
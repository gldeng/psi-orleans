namespace PsiOrleans.Common.Models;

/// <summary>
/// Represents the result of analyzing a task for complexity, dependencies, and requirements.
/// </summary>
public class TaskAnalysisResult
{
    /// <summary>
    /// Gets the complexity level of the task (1-10 scale).
    /// </summary>
    public int ComplexityLevel { get; init; }

    /// <summary>
    /// Gets the estimated effort required to complete the task.
    /// </summary>
    public TimeSpan EstimatedEffort { get; init; }

    /// <summary>
    /// Gets the identified dependencies for this task.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = new List<string>();

    /// <summary>
    /// Gets the recommended approach for handling this task.
    /// </summary>
    public string RecommendedApproach { get; init; } = string.Empty;

    /// <summary>
    /// Gets the required capabilities/skills needed to execute this task.
    /// </summary>
    public IReadOnlyList<string> RequiredCapabilities { get; init; } = new List<string>();

    /// <summary>
    /// Gets whether this task can be broken down into smaller subtasks.
    /// </summary>
    public bool CanBeDecomposed { get; init; }

    /// <summary>
    /// Gets any additional analysis notes or insights.
    /// </summary>
    public string AnalysisNotes { get; init; } = string.Empty;
} 
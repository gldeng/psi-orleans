using System.ComponentModel.DataAnnotations;
using Orleans;

namespace PsiOrleans.Models;

/// <summary>
/// Represents a specification for creating a new specialized agent
/// </summary>
[GenerateSerializer]
public class AgentSpecification
{
    /// <summary>
    /// Unique identifier for the agent specification
    /// </summary>
    [Id(0)]
    public string SpecId { get; set; } = string.Empty;
    
    /// <summary>
    /// Suggested agent ID for instantiation
    /// </summary>
    [Id(1)]
    public string SuggestedAgentId { get; set; } = string.Empty;
    
    /// <summary>
    /// Agent configuration including system prompt and settings
    /// </summary>
    [Id(2)]
    public AgentConfiguration Configuration { get; set; } = new AgentConfiguration();
    
    /// <summary>
    /// Required tools/functions for this agent
    /// </summary>
    [Id(3)]
    public List<string> RequiredTools { get; set; } = new List<string>();
    
    /// <summary>
    /// Description of what this agent is designed to do
    /// </summary>
    [Id(4)]
    public string Purpose { get; set; } = string.Empty;
    
    /// <summary>
    /// Capability assessment - what types of tasks this agent can handle
    /// </summary>
    [Id(5)]
    public List<string> Capabilities { get; set; } = new List<string>();
    
    /// <summary>
    /// Priority level for this agent specification (1-10, 10 being highest)
    /// </summary>
    [Id(6)]
    public int Priority { get; set; } = 5;
    
    /// <summary>
    /// Whether this agent specification can be automatically instantiated
    /// </summary>
    [Id(7)]
    public bool CanAutoInstantiate { get; set; } = true;
    
    /// <summary>
    /// Reasons why this agent cannot be created (if CanAutoInstantiate is false)
    /// </summary>
    [Id(8)]
    public List<string> BlockingReasons { get; set; } = new List<string>();
}

/// <summary>
/// Represents the analysis result for a subtask
/// </summary>
[GenerateSerializer]
public class SubtaskAnalysis
{
    /// <summary>
    /// The original subtask description
    /// </summary>
    [Id(0)]
    public string Subtask { get; set; } = string.Empty;
    
    /// <summary>
    /// Unique identifier for this subtask analysis
    /// </summary>
    [Id(1)]
    public string AnalysisId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Result type of the analysis
    /// </summary>
    [Id(2)]
    public SubtaskAnalysisResult ResultType { get; set; }
    
    /// <summary>
    /// ID of existing agent that can handle this subtask (if found)
    /// </summary>
    [Id(3)]
    public string? ExistingAgentId { get; set; }
    
    /// <summary>
    /// Confidence level that the existing agent can handle this subtask (0.0-1.0)
    /// </summary>
    [Id(4)]
    public double MatchConfidence { get; set; }
    
    /// <summary>
    /// Agent specification for creating a new agent (if needed)
    /// </summary>
    [Id(5)]
    public AgentSpecification? NewAgentSpec { get; set; }
    
    /// <summary>
    /// Reasons why this subtask cannot be completed
    /// </summary>
    [Id(6)]
    public List<string> BlockingReasons { get; set; } = new List<string>();
    
    /// <summary>
    /// Recommended action for the orchestrator
    /// </summary>
    [Id(7)]
    public string RecommendedAction { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional context or notes about the analysis
    /// </summary>
    [Id(8)]
    public string AnalysisNotes { get; set; } = string.Empty;
}

/// <summary>
/// Types of subtask analysis results
/// </summary>
[GenerateSerializer]
public enum SubtaskAnalysisResult
{
    /// <summary>
    /// Found an existing agent that can handle this subtask
    /// </summary>
    ExistingAgentMatch,
    
    /// <summary>
    /// Need to create a new specialized agent for this subtask
    /// </summary>
    RequiresNewAgent,
    
    /// <summary>
    /// This subtask cannot be completed with available tools/capabilities
    /// </summary>
    CannotComplete,
    
    /// <summary>
    /// The subtask analysis is still in progress
    /// </summary>
    AnalysisInProgress,
    
    /// <summary>
    /// The subtask analysis encountered an error
    /// </summary>
    AnalysisError
}

/// <summary>
/// Represents the overall task breakdown and analysis results
/// </summary>
[GenerateSerializer]
public class TaskBreakdownAnalysis
{
    /// <summary>
    /// Original task that was broken down
    /// </summary>
    [Id(0)]
    public string OriginalTask { get; set; } = string.Empty;
    
    /// <summary>
    /// Unique identifier for this task breakdown
    /// </summary>
    [Id(1)]
    public string BreakdownId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// List of subtasks identified
    /// </summary>
    [Id(2)]
    public List<string> Subtasks { get; set; } = new List<string>();
    
    /// <summary>
    /// Analysis results for each subtask
    /// </summary>
    [Id(3)]
    public List<SubtaskAnalysis> SubtaskAnalyses { get; set; } = new List<SubtaskAnalysis>();
    
    /// <summary>
    /// Overall feasibility assessment
    /// </summary>
    [Id(4)]
    public TaskFeasibility OverallFeasibility { get; set; }
    
    /// <summary>
    /// Agents that need to be created for this task
    /// </summary>
    [Id(5)]
    public List<AgentSpecification> RequiredNewAgents { get; set; } = new List<AgentSpecification>();
    
    /// <summary>
    /// Existing agents that will be used
    /// </summary>
    [Id(6)]
    public List<string> ExistingAgentsToUse { get; set; } = new List<string>();
    
    /// <summary>
    /// Execution plan for the orchestrator
    /// </summary>
    [Id(7)]
    public string ExecutionPlan { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when this analysis was created
    /// </summary>
    [Id(8)]
    public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Overall task feasibility assessment
/// </summary>
[GenerateSerializer]
public enum TaskFeasibility
{
    /// <summary>
    /// Task can be completed with existing agents
    /// </summary>
    FullyFeasible,
    
    /// <summary>
    /// Task can be completed but requires creating new agents
    /// </summary>
    FeasibleWithNewAgents,
    
    /// <summary>
    /// Task has some subtasks that cannot be completed
    /// </summary>
    PartiallyFeasible,
    
    /// <summary>
    /// Task cannot be completed with available capabilities
    /// </summary>
    NotFeasible,
    
    /// <summary>
    /// Task feasibility analysis is still in progress
    /// </summary>
    AnalysisInProgress
} 
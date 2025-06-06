using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Orleans;
using PsiOrleans.Services;

namespace PsiOrleans.Models;

/// <summary>
/// Agent roles based on task analysis and orchestration requirements
/// </summary>
[GenerateSerializer]
public enum AgentRole
{
    /// <summary>
    /// Agent role has not been determined yet (initial state)
    /// </summary>
    Undecided,
    
    /// <summary>
    /// Agent orchestrates complex tasks by delegating to child agents
    /// Can use: SendParentCallback, CreateAgent, CallChildAgent
    /// Cannot use: Normal blocking tools
    /// </summary>
    Orchestrator,
    
    /// <summary>
    /// Agent specializes in handling specific tasks directly
    /// Can use: Normal blocking tools, SendParentCallback
    /// Cannot use: CreateAgent, CallChildAgent
    /// </summary>
    Specialized
}

/// <summary>
/// Agent state for configurable agents containing all necessary state information
/// </summary>
[Serializable]
[GenerateSerializer]
public class ConfigurableAgentState
{
    /// <summary>
    /// Unique identifier for this agent
    /// </summary>
    [Id(0)]
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp
    /// </summary>
    [Id(1)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    [Id(2)]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Chat history for this agent
    /// </summary>
    [Id(3)]
    public List<ChatMessage> ChatHistory { get; set; } = new();

    /// <summary>
    /// Current task being processed
    /// </summary>
    [Id(4)]
    public string? CurrentTask { get; set; }

    /// <summary>
    /// Child agent IDs created by this agent
    /// </summary>
    [Id(5)]
    public List<string> ChildAgentIds { get; set; } = new();

    /// <summary>
    /// Total execution time across all tasks
    /// </summary>
    [Id(6)]
    public TimeSpan TotalExecutionTime { get; set; } = TimeSpan.Zero;

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
    /// Available kernel functions for this agent instance
    /// </summary>
    [Id(25)]
    public List<AgentFunctionInfo> AvailableFunctions { get; set; } = new();
    
    /// <summary>
    /// Custom metadata for the agent
    /// </summary>
    [Id(26)]
    public Dictionary<string, object> CustomMetadata { get; set; } = new();
    
    /// <summary>
    /// Original tool names requested during initialization (e.g., "Math.Add", "Tavily.search")
    /// This preserves the exact names before kernel plugin transformation
    /// </summary>
    [Id(31)]
    public List<string> OriginalToolNames { get; set; } = new();
    
    /// <summary>
    /// List of agents that this agent can call, with names and descriptions
    /// </summary>
    [Id(27)]
    public List<CallableAgent> CallableAgents { get; set; } = new();
    
    /// <summary>
    /// Current role of the agent determined by task analysis
    /// </summary>
    [Id(28)]
    public AgentRole Role { get; set; } = AgentRole.Undecided;
    
    /// <summary>
    /// Parent agent ID if this agent was created by another agent
    /// </summary>
    [Id(29)]
    public string? ParentAgentId { get; set; }
    
    /// <summary>
    /// Working memory for temporary data storage during task execution
    /// </summary>
    [Id(30)]
    public Dictionary<string, object> WorkingMemory { get; set; } = new();
    
    // Non-serialized field for agent function registry
    [NonSerialized]
    private IAgentFunctionRegistry? _agentFunctionRegistry;
    
    /// <summary>
    /// Registry of functions that enable calling other agents
    /// Note: This is not serialized directly, but recreated from CallableAgents during initialization
    /// </summary>
    public IAgentFunctionRegistry? AgentFunctionRegistry 
    { 
        get => _agentFunctionRegistry;
        set => _agentFunctionRegistry = value;
    }

    /// <summary>
    /// Clear the chat history
    /// </summary>
    public void ClearChatHistory()
    {
        ChatHistory.Clear();
        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Add a chat message to the history
    /// </summary>
    public void AddChatMessage(ChatMessage message)
    {
        ChatHistory.Add(message);
        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Add a chat message to the history with role and content
    /// </summary>
    public void AddChatMessage(string role, string content)
    {
        ChatHistory.Add(new ChatMessage(role, content));
        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Convert chat history to Semantic Kernel ChatHistory format
    /// </summary>
    public ChatHistory ToSemanticKernelChatHistory()
    {
        var skChatHistory = new ChatHistory();
        
        foreach (var message in ChatHistory)
        {
            var role = message.Role switch
            {
                "user" => AuthorRole.User,
                "assistant" => AuthorRole.Assistant,
                "system" => AuthorRole.System,
                "tool" => AuthorRole.Tool,
                _ => AuthorRole.User
            };
            
            skChatHistory.Add(new ChatMessageContent(role, message.Content));
        }
        
        return skChatHistory;
    }

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
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
    
    // Chat History for persistent conversation context
    [Id(5)]
    public List<ChatMessage> ChatHistory { get; set; } = new();
    
    // Task execution state
    [Id(6)]
    public bool IsTaskCompleted { get; set; }
    
    [Id(7)]
    public string? FinalAnswer { get; set; }
    
    [Id(8)]
    public int CurrentStepNumber { get; set; } = 0;
    
    [Id(9)]
    public int MaxSteps { get; set; } = 15;
    
    // Timestamps
    [Id(10)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Id(11)]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    [Id(12)]
    public DateTime? TaskStartedAt { get; set; }
    
    [Id(13)]
    public DateTime? TaskCompletedAt { get; set; }
    
    // Performance metrics
    [Id(14)]
    public int TotalTokensUsed { get; set; }
    
    [Id(15)]
    public TimeSpan TotalExecutionTime { get; set; }
    
    [Id(16)]
    public int SuccessfulSteps { get; set; }
    
    [Id(17)]
    public int FailedSteps { get; set; }
    
    /// <summary>
    /// Convert stored chat history to Semantic Kernel ChatHistory
    /// </summary>
    public Microsoft.SemanticKernel.ChatCompletion.ChatHistory ToSemanticKernelChatHistory()
    {
        var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        
        foreach (var message in ChatHistory)
        {
            chatHistory.Add(message.ToSemanticKernelMessage());
        }
        
        return chatHistory;
    }
    
    /// <summary>
    /// Add a message to the chat history
    /// </summary>
    public void AddChatMessage(string role, string content, string? name = null)
    {
        ChatHistory.Add(new ChatMessage(role, content, name));
        LastUpdated = DateTime.UtcNow;
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
    /// Get the number of messages in chat history
    /// </summary>
    public int GetChatHistoryCount() => ChatHistory.Count;
} 
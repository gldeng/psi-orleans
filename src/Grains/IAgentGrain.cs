using Orleans;
using PsiOrleans.Models;

namespace PsiOrleans.Grains;

/// <summary>
/// Orleans Grain interface for React Agent with Semantic Kernel integration
/// </summary>
public interface IAgentGrain : IGrainWithStringKey
{
    /// <summary>
    /// Execute a task using the React Agent pattern with automatic function calling
    /// </summary>
    Task<string> ExecuteTaskAsync(string task);
    
    /// <summary>
    /// Get the current agent state
    /// </summary>
    Task<AgentState> GetStateAsync();
    
    /// <summary>
    /// Get the execution history
    /// </summary>
    Task<List<AgentStep>> GetExecutionHistoryAsync();
    
    /// <summary>
    /// Reset the agent to initial state
    /// </summary>
    Task ResetAsync();
    
    /// <summary>
    /// Check if the current task is completed
    /// </summary>
    Task<bool> IsTaskCompletedAsync();
    
    /// <summary>
    /// Get performance metrics
    /// </summary>
    Task<(int TotalSteps, int SuccessfulSteps, int FailedSteps, TimeSpan ExecutionTime)> GetMetricsAsync();

    /// <summary>
    /// Get the chat history
    /// </summary>
    Task<List<ChatMessage>> GetChatHistoryAsync();

    /// <summary>
    /// Add a message to the chat history
    /// </summary>
    Task AddChatMessageAsync(string role, string content, string? name = null);

    /// <summary>
    /// Clear the chat history
    /// </summary>
    Task ClearChatHistoryAsync();

    /// <summary>
    /// Get the number of messages in chat history
    /// </summary>
    Task<int> GetChatHistoryCountAsync();

    /// <summary>
    /// Continue a conversation with a new message (maintains chat history)
    /// </summary>
    Task<string> ContinueConversationAsync(string userMessage);
} 
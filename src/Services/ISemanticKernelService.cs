using Microsoft.SemanticKernel;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// Service interface for Semantic Kernel integration with Orleans
/// </summary>
public interface ISemanticKernelService
{
    /// <summary>
    /// Get the configured Kernel instance
    /// </summary>
    Kernel GetKernel();
    
    /// <summary>
    /// Execute a task using the React Agent pattern with automatic function calling
    /// </summary>
    Task<string> ExecuteTaskAsync(string task, AgentState state);
    
    /// <summary>
    /// Continue a conversation with a new message (maintains chat history)
    /// </summary>
    Task<string> ContinueConversationAsync(string userMessage, AgentState state);
    
    /// <summary>
    /// Get the chat history as formatted strings
    /// </summary>
    Task<List<string>> GetChatHistoryAsync(AgentState state);
} 
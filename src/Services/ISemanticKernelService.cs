using Microsoft.SemanticKernel;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

public interface ISemanticKernelService
{
    /// <summary>
    /// Get the configured Semantic Kernel instance
    /// </summary>
    Kernel GetKernel();
    
    /// <summary>
    /// Execute a task using Semantic Kernel's automatic function calling
    /// </summary>
    Task<string> ExecuteTaskAsync(string task, AgentState state);
    
    /// <summary>
    /// Get chat history for the agent
    /// </summary>
    Task<List<string>> GetChatHistoryAsync(AgentState state);
} 
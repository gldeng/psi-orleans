using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// Interface for manual function call processing that distinguishes between 
/// agent calls (non-blocking) and regular tool calls (blocking execution)
/// </summary>
public interface IManualFunctionCallProcessor
{
    /// <summary>
    /// Process function calls from chat completion result
    /// Agent calls are handled asynchronously, regular tools are executed immediately
    /// </summary>
    /// <param name="chatResult">Chat completion result containing function calls</param>
    /// <param name="kernel">Kernel for function execution</param>
    /// <param name="agentId">ID of the calling agent</param>
    /// <param name="chatHistory">Chat history to update with results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending agent calls and any immediate error messages</returns>
    Task<ManualFunctionCallResult> ProcessFunctionCallsAsync(
        ChatMessageContent chatResult,
        Kernel kernel,
        string agentId,
        ChatHistory chatHistory,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a function is an agent communication function
    /// </summary>
    /// <param name="functionName">Function name to check</param>
    /// <param name="pluginName">Plugin name if applicable</param>
    /// <returns>True if this is an agent communication function</returns>
    bool IsAgentFunction(string functionName, string? pluginName = null);
    
    /// <summary>
    /// Extract target agent ID from agent function name
    /// </summary>
    /// <param name="functionName">Agent function name (e.g., "Call_DataAnalyst")</param>
    /// <returns>Target agent ID or null if not an agent function</returns>
    string? ExtractTargetAgentId(string functionName);
}

/// <summary>
/// Result of manual function call processing
/// </summary>
public class ManualFunctionCallResult
{
    /// <summary>
    /// Pending agent calls that were initiated but not awaited
    /// </summary>
    public List<PendingAgentCall> PendingAgentCalls { get; set; } = new();
    
    /// <summary>
    /// Whether all function calls were processed successfully
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// Any error messages from processing
    /// </summary>
    public List<string> ErrorMessages { get; set; } = new();
    
    /// <summary>
    /// Whether the chat history was updated with function results
    /// </summary>
    public bool ChatHistoryUpdated { get; set; } = false;

    /// <summary>
    /// Whether LLM execution should be paused to wait for agent callbacks
    /// True when there are pending agent calls that require waiting
    /// </summary>
    public bool ShouldPauseLLMExecution => PendingAgentCalls.Any(call => call.ShouldPauseLLMExecution);

    /// <summary>
    /// Number of pending agent calls that require waiting for callbacks
    /// </summary>
    public int PendingCallsRequiringWait => PendingAgentCalls.Count(call => call.ShouldPauseLLMExecution);
} 
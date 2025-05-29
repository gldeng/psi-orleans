using Orleans;
using Microsoft.SemanticKernel;
using PsiOrleans.Models;

namespace PsiOrleans.Grains;

/// <summary>
/// Orleans Grain interface for configurable agents that can be initialized with custom prompts and tools
/// </summary>
public interface IConfigurableAgentGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initialize the agent with a system prompt and named tools
    /// </summary>
    /// <param name="configuration">Agent configuration including prompt and behavior settings</param>
    /// <param name="functionNames">Names/IDs of kernel functions (tools) to register</param>
    /// <param name="pluginNames">Names/IDs of kernel plugins to register</param>
    /// <returns>Success status and agent information</returns>
    Task<(bool Success, string Message, string AgentId)> InitializeAsync(
        AgentConfiguration configuration,
        IEnumerable<string>? functionNames = null,
        IEnumerable<string>? pluginNames = null);
    
    /// <summary>
    /// Initialize the agent with a system prompt and unified tool names (RECOMMENDED)
    /// Supports both individual functions and plugin functions using qualified names
    /// </summary>
    /// <param name="configuration">Agent configuration including prompt and behavior settings</param>
    /// <param name="toolNames">Fully qualified tool names (e.g., "Math.Add", "MathematicalOperations.Multiply")</param>
    /// <returns>Success status and agent information</returns>
    Task<(bool Success, string Message, string AgentId)> InitializeAsync(
        AgentConfiguration configuration,
        IEnumerable<string>? toolNames);
    
    /// <summary>
    /// Execute a task using the configured prompt and tools
    /// </summary>
    /// <param name="task">Task to execute</param>
    /// <returns>Task execution result</returns>
    Task<string> ExecuteTaskAsync(string task);
    
    /// <summary>
    /// Continue a conversation with the agent
    /// </summary>
    /// <param name="userMessage">User message to respond to</param>
    /// <returns>Agent response</returns>
    Task<string> ContinueConversationAsync(string userMessage);
    
    /// <summary>
    /// Get the current agent state and configuration
    /// </summary>
    /// <returns>Current agent state</returns>
    Task<ConfigurableAgentState> GetStateAsync();
    
    /// <summary>
    /// Get the agent's configuration
    /// </summary>
    /// <returns>Agent configuration</returns>
    Task<AgentConfiguration?> GetConfigurationAsync();
    
    /// <summary>
    /// Check if the agent has been initialized
    /// </summary>
    /// <returns>True if initialized, false otherwise</returns>
    Task<bool> IsInitializedAsync();
    
    /// <summary>
    /// Reset the agent and clear all state
    /// </summary>
    /// <returns>Success status</returns>
    Task<bool> ResetAsync();
    
    /// <summary>
    /// Get execution metrics for the agent
    /// </summary>
    /// <returns>Execution metrics</returns>
    Task<(int TotalTasks, int SuccessfulTasks, int FailedTasks, TimeSpan TotalExecutionTime)> GetMetricsAsync();
    
    /// <summary>
    /// Get available kernel functions (tools) for this agent
    /// </summary>
    /// <returns>List of available function names and descriptions</returns>
    Task<List<(string FunctionName, string Description, List<string> Parameters)>> GetAvailableToolsAsync();
    
    /// <summary>
    /// Get the chat history for this agent
    /// </summary>
    /// <returns>Chat history</returns>
    Task<List<ChatMessage>> GetChatHistoryAsync();
    
    /// <summary>
    /// Clear the chat history
    /// </summary>
    /// <returns>Success status</returns>
    Task<bool> ClearChatHistoryAsync();
    
    /// <summary>
    /// Get all available function names from the registry
    /// </summary>
    /// <returns>List of all available function names</returns>
    Task<List<string>> GetAvailableFunctionNamesAsync();
    
    /// <summary>
    /// Get all available plugin names from the registry
    /// </summary>
    /// <returns>List of all available plugin names</returns>
    Task<List<string>> GetAvailablePluginNamesAsync();

    /// <summary>
    /// Get all available tool names using unified naming (individual functions + plugin.function combinations)
    /// </summary>
    /// <returns>List of all available tool names</returns>
    Task<List<string>> GetAllAvailableToolNamesAsync();
} 
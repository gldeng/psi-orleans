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
    
    // New methods for callable agent management
    
    /// <summary>
    /// Set the list of agents that this agent can call
    /// This will create agent communication functions for each callable agent
    /// </summary>
    /// <param name="callableAgents">List of callable agents with names and descriptions</param>
    /// <returns>Success status and message</returns>
    Task<(bool Success, string Message)> SetCallableAgentsAsync(IEnumerable<CallableAgent> callableAgents);
    
    /// <summary>
    /// Get the list of agents that this agent can call
    /// </summary>
    /// <returns>List of callable agents with names and descriptions</returns>
    Task<List<CallableAgent>> GetCallableAgentsAsync();
    
    /// <summary>
    /// Add a single agent to the callable agents list
    /// </summary>
    /// <param name="callableAgent">Callable agent with name and description</param>
    /// <returns>Success status and message</returns>
    Task<(bool Success, string Message)> AddCallableAgentAsync(CallableAgent callableAgent);
    
    /// <summary>
    /// Remove a single agent from the callable agents list
    /// </summary>
    /// <param name="agentId">Agent ID to remove</param>
    /// <returns>Success status and message</returns>
    Task<(bool Success, string Message)> RemoveCallableAgentAsync(string agentId);
    
    /// <summary>
    /// Get the names of available agent communication functions
    /// </summary>
    /// <returns>List of agent function names</returns>
    Task<List<string>> GetAvailableAgentFunctionNamesAsync();
    
    /// <summary>
    /// Check if this agent can call a specific agent
    /// </summary>
    /// <param name="agentId">Agent ID to check</param>
    /// <returns>True if the agent can be called</returns>
    Task<bool> CanCallAgentAsync(string agentId);
    
    /// <summary>
    /// Receive a callback notification when an agent call completes
    /// </summary>
    /// <param name="callId">ID of the completed call</param>
    /// <param name="message">Callback message containing results or error information</param>
    /// <param name="isSuccess">Whether the call was successful</param>
    /// <returns>Task completion</returns>
    Task ReceiveCallbackAsync(string callId, string message, bool isSuccess);
} 
using Microsoft.SemanticKernel;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// Service interface for creating and managing configurable Semantic Kernel instances
/// </summary>
public interface IConfigurableKernelService
{
    /// <summary>
    /// Create a kernel with specific configuration and named tools
    /// </summary>
    /// <param name="configuration">Agent configuration for model and behavior settings</param>
    /// <param name="functionNames">Names/IDs of kernel functions to register</param>
    /// <param name="pluginNames">Names/IDs of kernel plugins to register</param>
    /// <returns>Configured kernel instance</returns>
    Task<Kernel> CreateKernelAsync(
        AgentConfiguration configuration, 
        IEnumerable<string>? functionNames = null,
        IEnumerable<string>? pluginNames = null);
    
    /// <summary>
    /// Create a kernel with specific configuration and unified tool names (RECOMMENDED)
    /// Supports both individual functions and plugin functions using qualified names
    /// </summary>
    /// <param name="configuration">Agent configuration for model and behavior settings</param>
    /// <param name="toolNames">Fully qualified tool names (e.g., "Math.Add", "MathematicalOperations.Multiply")</param>
    /// <returns>Configured kernel instance</returns>
    Task<Kernel> CreateKernelAsync(
        AgentConfiguration configuration,
        IEnumerable<string>? toolNames);
    
    /// <summary>
    /// Execute a task using the configured kernel
    /// </summary>
    /// <param name="kernel">The kernel to use for execution</param>
    /// <param name="task">The task to execute</param>
    /// <param name="state">The agent state</param>
    /// <param name="systemPrompt">The system prompt to use</param>
    /// <param name="waitForCompletion">Whether to wait for all agent callbacks before returning (default: true for programmatic calls)</param>
    /// <returns>Task execution result</returns>
    Task<string> ExecuteTaskAsync(Kernel kernel, string task, ConfigurableAgentState state, string systemPrompt, bool waitForCompletion = true);
    
    /// <summary>
    /// Continue a conversation using a configured kernel
    /// </summary>
    /// <param name="kernel">Kernel instance to use</param>
    /// <param name="userMessage">User message</param>
    /// <param name="state">Agent state for context</param>
    /// <param name="systemPrompt">System prompt to use</param>
    /// <returns>Assistant response</returns>
    Task<string> ContinueConversationAsync(Kernel kernel, string userMessage, ConfigurableAgentState state, string systemPrompt);
    
    /// <summary>
    /// Get metadata for all functions in a kernel
    /// </summary>
    /// <param name="kernel">Kernel to analyze</param>
    /// <returns>Function metadata</returns>
    Task<List<(string FunctionName, string Description, List<string> Parameters)>> GetKernelFunctionMetadataAsync(Kernel kernel);
    
    /// <summary>
    /// Validate agent configuration
    /// </summary>
    /// <param name="configuration">Configuration to validate</param>
    /// <returns>Validation result</returns>
    Task<(bool IsValid, string ErrorMessage)> ValidateConfigurationAsync(AgentConfiguration configuration);
    
    /// <summary>
    /// Get all available function names from the registry
    /// </summary>
    /// <returns>List of available function names</returns>
    IEnumerable<string> GetAvailableFunctionNames();
    
    /// <summary>
    /// Get all available plugin names from the registry
    /// </summary>
    /// <returns>List of available plugin names</returns>
    IEnumerable<string> GetAvailablePluginNames();

    /// <summary>
    /// Get all available tool names using unified naming (individual functions + plugin.function combinations)
    /// </summary>
    /// <returns>List of all available tool names</returns>
    IEnumerable<string> GetAllAvailableToolNames();
} 
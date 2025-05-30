using Microsoft.SemanticKernel;

namespace PsiOrleans.Services;

/// <summary>
/// Registry interface for managing kernel functions that enable agent-to-agent communication
/// Similar to IKernelFunctionRegistry but specialized for inter-agent function calls
/// </summary>
public interface IAgentFunctionRegistry
{
    /// <summary>
    /// Register a kernel function for calling a specific agent
    /// </summary>
    /// <param name="name">Unique name/ID for the agent function (e.g., "CallDataAnalyst")</param>
    /// <param name="function">Kernel function that enables calling the agent</param>
    void RegisterAgentFunction(string name, KernelFunction function);
    
    /// <summary>
    /// Get an agent communication function by name
    /// </summary>
    /// <param name="name">Function name/ID</param>
    /// <returns>Kernel function if found, null otherwise</returns>
    KernelFunction? GetAgentFunction(string name);
    
    /// <summary>
    /// Remove an agent communication function by name
    /// </summary>
    /// <param name="name">Function name/ID to remove</param>
    /// <returns>True if function was found and removed, false otherwise</returns>
    bool RemoveAgentFunction(string name);
    
    /// <summary>
    /// Get multiple agent functions by names
    /// </summary>
    /// <param name="names">Function names/IDs</param>
    /// <returns>List of found kernel functions</returns>
    IEnumerable<KernelFunction> GetAgentFunctions(IEnumerable<string> names);
    
    /// <summary>
    /// Get all available agent function names
    /// </summary>
    /// <returns>List of registered agent function names</returns>
    IEnumerable<string> GetAvailableAgentFunctionNames();
    
    /// <summary>
    /// Check if an agent function is registered
    /// </summary>
    /// <param name="name">Function name/ID</param>
    /// <returns>True if function is registered</returns>
    bool HasAgentFunction(string name);
    
    /// <summary>
    /// Get all agent functions as a dictionary
    /// </summary>
    /// <returns>Dictionary of function name to kernel function</returns>
    Dictionary<string, KernelFunction> GetAllAgentFunctions();
    
    /// <summary>
    /// Clear all registered agent functions
    /// </summary>
    void ClearAgentFunctions();
    
    /// <summary>
    /// Get the count of registered agent functions
    /// </summary>
    /// <returns>Number of registered functions</returns>
    int GetAgentFunctionCount();
} 
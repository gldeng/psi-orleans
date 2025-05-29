using Microsoft.SemanticKernel;

namespace PsiOrleans.Services;

/// <summary>
/// Registry interface for managing kernel functions and plugins by name/ID
/// </summary>
public interface IKernelFunctionRegistry
{
    /// <summary>
    /// Register a kernel function with a unique name
    /// </summary>
    /// <param name="name">Unique name/ID for the function</param>
    /// <param name="function">Kernel function to register</param>
    void RegisterFunction(string name, KernelFunction function);
    
    /// <summary>
    /// Register a kernel plugin with a unique name
    /// </summary>
    /// <param name="name">Unique name/ID for the plugin</param>
    /// <param name="plugin">Kernel plugin to register</param>
    void RegisterPlugin(string name, KernelPlugin plugin);
    
    /// <summary>
    /// Get a kernel function by name
    /// </summary>
    /// <param name="name">Function name/ID</param>
    /// <returns>Kernel function if found, null otherwise</returns>
    KernelFunction? GetFunction(string name);
    
    /// <summary>
    /// Get a kernel plugin by name
    /// </summary>
    /// <param name="name">Plugin name/ID</param>
    /// <returns>Kernel plugin if found, null otherwise</returns>
    KernelPlugin? GetPlugin(string name);
    
    /// <summary>
    /// Get multiple functions by names
    /// </summary>
    /// <param name="names">Function names/IDs</param>
    /// <returns>List of found kernel functions</returns>
    IEnumerable<KernelFunction> GetFunctions(IEnumerable<string> names);
    
    /// <summary>
    /// Get multiple plugins by names
    /// </summary>
    /// <param name="names">Plugin names/IDs</param>
    /// <returns>List of found kernel plugins</returns>
    IEnumerable<KernelPlugin> GetPlugins(IEnumerable<string> names);
    
    /// <summary>
    /// Get all available function names
    /// </summary>
    /// <returns>List of registered function names</returns>
    IEnumerable<string> GetAvailableFunctionNames();
    
    /// <summary>
    /// Get all available plugin names
    /// </summary>
    /// <returns>List of registered plugin names</returns>
    IEnumerable<string> GetAvailablePluginNames();
    
    /// <summary>
    /// Check if a function is registered
    /// </summary>
    /// <param name="name">Function name/ID</param>
    /// <returns>True if function is registered</returns>
    bool HasFunction(string name);
    
    /// <summary>
    /// Check if a plugin is registered
    /// </summary>
    /// <param name="name">Plugin name/ID</param>
    /// <returns>True if plugin is registered</returns>
    bool HasPlugin(string name);

    /// <summary>
    /// Get a tool (function) by fully qualified name
    /// Supports both individual functions ("Math.Add") and plugin functions ("MathematicalOperations.Add")
    /// </summary>
    /// <param name="qualifiedName">Fully qualified tool name</param>
    /// <returns>Kernel function if found, null otherwise</returns>
    KernelFunction? GetToolByQualifiedName(string qualifiedName);

    /// <summary>
    /// Get multiple tools by fully qualified names
    /// </summary>
    /// <param name="qualifiedNames">Fully qualified tool names</param>
    /// <returns>Dictionary of tool name to kernel function for found tools</returns>
    Dictionary<string, KernelFunction> GetToolsByQualifiedNames(IEnumerable<string> qualifiedNames);

    /// <summary>
    /// Get all available tool names (both individual functions and plugin.function combinations)
    /// </summary>
    /// <returns>List of all available tool names</returns>
    IEnumerable<string> GetAllAvailableToolNames();

    /// <summary>
    /// Check if a tool is available by qualified name
    /// </summary>
    /// <param name="qualifiedName">Fully qualified tool name</param>
    /// <returns>True if tool is available</returns>
    bool HasTool(string qualifiedName);
} 
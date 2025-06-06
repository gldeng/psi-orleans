using Orleans;

namespace PsiOrleans.Models;

/// <summary>
/// Orleans-serializable information about a kernel function
/// Replaces KernelFunctionMetadata which is not Orleans-serializable
/// </summary>
[Serializable]
[GenerateSerializer]
public record AgentFunctionInfo
{
    /// <summary>
    /// Name of the function
    /// </summary>
    [Id(0)]
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// Description of what the function does
    /// </summary>
    [Id(1)]
    public string Description { get; init; } = string.Empty;
    
    /// <summary>
    /// List of parameter names for the function
    /// </summary>
    [Id(2)]
    public List<string> ParameterNames { get; init; } = new();
    
    /// <summary>
    /// Plugin name if the function belongs to a plugin
    /// </summary>
    [Id(3)]
    public string? PluginName { get; init; }
    
    /// <summary>
    /// Whether this function was added during agent communication setup
    /// </summary>
    [Id(4)]
    public bool IsAgentCommunicationFunction { get; init; } = false;

    /// <summary>
    /// Create AgentFunctionInfo from KernelFunctionMetadata
    /// </summary>
    public static AgentFunctionInfo FromKernelFunctionMetadata(Microsoft.SemanticKernel.KernelFunctionMetadata metadata, string? pluginName = null, bool isAgentComm = false)
    {
        // Store the full qualified name for proper tool resolution
        // Format: PluginName.FunctionName (e.g., "Math.Add", "Tavily.search")
        var fullName = !string.IsNullOrEmpty(pluginName) ? $"{pluginName}.{metadata.Name}" : metadata.Name;
        
        return new AgentFunctionInfo
        {
            Name = fullName, // Store full qualified name instead of just function name
            Description = metadata.Description ?? string.Empty,
            ParameterNames = metadata.Parameters.Select(p => p.Name).ToList(),
            PluginName = pluginName,
            IsAgentCommunicationFunction = isAgentComm
        };
    }
} 
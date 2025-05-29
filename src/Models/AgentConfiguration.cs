using Microsoft.SemanticKernel;
using Orleans;

namespace PsiOrleans.Models;

/// <summary>
/// Configuration for a configurable agent, defining its behavior and model settings
/// </summary>
[Serializable]
[GenerateSerializer]
public class AgentConfiguration
{
    /// <summary>
    /// The system prompt that defines the agent's personality and behavior
    /// </summary>
    [Id(0)]
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the agent
    /// </summary>
    [Id(1)]
    public string AgentName { get; set; } = "ConfigurableAgent";

    /// <summary>
    /// Temperature for AI responses (0.0 = deterministic, 2.0 = very creative)
    /// </summary>
    [Id(2)]
    public double Temperature { get; set; } = 0.1;

    /// <summary>
    /// Maximum number of tokens for AI responses
    /// </summary>
    [Id(3)]
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Model configuration for AI integration
    /// </summary>
    [Id(4)]
    public ModelConfiguration Model { get; set; } = new();
}

/// <summary>
/// Configuration for the AI model to use
/// </summary>
[Serializable]
[GenerateSerializer]
public class ModelConfiguration
{
    /// <summary>
    /// The model ID to use (e.g., "gpt-4o-mini")
    /// </summary>
    [Id(0)]
    public string ModelId { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// API key for the model provider (optional - can use environment variable)
    /// </summary>
    [Id(1)]
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for custom model providers (optional)
    /// </summary>
    [Id(2)]
    public string? BaseUrl { get; set; }
} 
using System.Text.Json.Serialization;

namespace PsiOrleans.Common.Models;

/// <summary>
/// Configuration for a configurable agent, defining its behavior and model settings
/// </summary>
public class AgentConfiguration
{
    /// <summary>
    /// The system prompt that defines the agent's personality and behavior
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the agent
    /// </summary>
    public string AgentName { get; set; } = "ConfigurableAgent";

    /// <summary>
    /// Temperature for AI responses (0.0 = deterministic, 2.0 = very creative)
    /// </summary>
    public double Temperature { get; set; } = 0.1;

    /// <summary>
    /// Maximum number of tokens for AI responses
    /// </summary>
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Model configuration for AI integration
    /// </summary>
    public ModelConfiguration Model { get; set; } = new();

    /// <summary>
    /// Validates the agent configuration
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(SystemPrompt))
            errors.Add("SystemPrompt cannot be empty");

        if (string.IsNullOrWhiteSpace(AgentName))
            errors.Add("AgentName cannot be empty");

        if (Temperature < 0.0 || Temperature > 2.0)
            errors.Add("Temperature must be between 0.0 and 2.0");

        if (MaxTokens <= 0)
            errors.Add("MaxTokens must be greater than 0");

        var modelValidation = Model.Validate();
        if (!modelValidation.IsValid)
            errors.Add($"Model validation failed: {modelValidation.ErrorMessage}");

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            ErrorMessage = string.Join("; ", errors)
        };
    }
}

/// <summary>
/// Configuration for the AI model to use - supports both OpenAI and Azure OpenAI
/// </summary>
public class ModelConfiguration
{
    /// <summary>
    /// The model ID to use (e.g., "gpt-4o-mini") for OpenAI or deployment name for Azure OpenAI
    /// </summary>
    public string ModelId { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// API key for the model provider (optional - can use environment variable)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for custom model providers (optional) - for standard OpenAI
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Azure OpenAI deployment name (required for Azure OpenAI)
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Azure OpenAI endpoint URL (required for Azure OpenAI, e.g., "https://contoso.openai.azure.com/")
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Azure OpenAI API version (optional, defaults to latest if not specified)
    /// </summary>
    public string? ApiVersion { get; set; }

    /// <summary>
    /// Determines if this configuration is for Azure OpenAI based on presence of Endpoint
    /// </summary>
    [JsonIgnore]
    public bool IsAzureOpenAI => !string.IsNullOrEmpty(Endpoint);

    /// <summary>
    /// Validates the model configuration
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ModelId))
            errors.Add("ModelId cannot be empty");

        if (IsAzureOpenAI)
        {
            if (string.IsNullOrWhiteSpace(Endpoint))
                errors.Add("Endpoint is required for Azure OpenAI");
            
            if (string.IsNullOrWhiteSpace(DeploymentName))
                errors.Add("DeploymentName is required for Azure OpenAI");
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            ErrorMessage = string.Join("; ", errors)
        };
    }
}

/// <summary>
/// Result of a validation operation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
} 
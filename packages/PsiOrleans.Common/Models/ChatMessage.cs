namespace PsiOrleans.Common.Models;

/// <summary>
/// Framework-agnostic chat message for agent communication
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Role of the message sender (user, assistant, system, function)
    /// </summary>
    public string Role { get; set; } = string.Empty;
    
    /// <summary>
    /// Content of the message
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional name of the message sender
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Additional metadata for the message
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Default constructor
    /// </summary>
    public ChatMessage() { }

    /// <summary>
    /// Constructor with role and content
    /// </summary>
    public ChatMessage(string role, string? content, string? name = null)
    {
        Role = role ?? string.Empty;
        Content = content ?? string.Empty;
        Name = name;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a system message
    /// </summary>
    public static ChatMessage CreateSystemMessage(string content)
    {
        return new ChatMessage("system", content);
    }

    /// <summary>
    /// Creates a user message
    /// </summary>
    public static ChatMessage CreateUserMessage(string content)
    {
        return new ChatMessage("user", content);
    }

    /// <summary>
    /// Creates an assistant message
    /// </summary>
    public static ChatMessage CreateAssistantMessage(string content)
    {
        return new ChatMessage("assistant", content);
    }

    /// <summary>
    /// Validates the chat message
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Role))
            errors.Add("Role cannot be empty");

        // Content can be empty, but we could add more validation rules here if needed

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            ErrorMessage = string.Join("; ", errors)
        };
    }
} 
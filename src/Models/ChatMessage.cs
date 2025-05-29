using Orleans;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace PsiOrleans.Models;

/// <summary>
/// Serializable chat message for Orleans grain state storage
/// </summary>
[Serializable]
[GenerateSerializer]
public class ChatMessage
{
    [Id(0)]
    public string Role { get; set; } = string.Empty;
    
    [Id(1)]
    public string Content { get; set; } = string.Empty;
    
    [Id(2)]
    public string? Name { get; set; }
    
    [Id(3)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [Id(4)]
    public Dictionary<string, object> Metadata { get; set; } = new();

    public ChatMessage() { }

    public ChatMessage(string role, string content, string? name = null)
    {
        Role = role;
        Content = content;
        Name = name;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Convert to Semantic Kernel ChatMessageContent
    /// </summary>
    public ChatMessageContent ToSemanticKernelMessage()
    {
        return new ChatMessageContent(
            new AuthorRole(Role), 
            Content);
    }

    /// <summary>
    /// Create from Semantic Kernel ChatMessageContent
    /// </summary>
    public static ChatMessage FromSemanticKernelMessage(ChatMessageContent message)
    {
        return new ChatMessage(
            message.Role.Label, 
            message.Content ?? string.Empty);
    }
} 
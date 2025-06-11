using FluentAssertions;
using PsiOrleans.Common.Models;
using Xunit;
using System.Text.Json;

namespace PsiOrleans.Common.Tests.Models;

/// <summary>
/// Tests for ChatMessage model - TDD approach
/// </summary>
public class ChatMessageTests
{
    [Fact]
    public void ChatMessage_ShouldHaveDefaultConstructor()
    {
        // Act
        var message = new ChatMessage();
        
        // Assert
        message.Role.Should().Be(string.Empty);
        message.Content.Should().Be(string.Empty);
        message.Name.Should().BeNull();
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        message.Metadata.Should().NotBeNull();
        message.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void ChatMessage_ShouldSetPropertiesViaConstructor()
    {
        // Arrange
        var role = "user";
        var content = "Hello, how can I help you?";
        var name = "TestUser";

        // Act
        var message = new ChatMessage(role, content, name);

        // Assert
        message.Role.Should().Be(role);
        message.Content.Should().Be(content);
        message.Name.Should().Be(name);
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        message.Metadata.Should().NotBeNull();
        message.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void ChatMessage_ShouldSetPropertiesViaConstructorWithoutName()
    {
        // Arrange
        var role = "assistant";
        var content = "I'm here to help!";

        // Act
        var message = new ChatMessage(role, content);

        // Assert
        message.Role.Should().Be(role);
        message.Content.Should().Be(content);
        message.Name.Should().BeNull();
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ChatMessage_ShouldBeSerializableToJson()
    {
        // Arrange
        var message = new ChatMessage("user", "Test message", "TestUser");
        message.Metadata["key1"] = "value1";
        message.Metadata["key2"] = 42;

        // Act
        var json = JsonSerializer.Serialize(message);
        var deserializedMessage = JsonSerializer.Deserialize<ChatMessage>(json);

        // Assert
        deserializedMessage.Should().NotBeNull();
        deserializedMessage!.Role.Should().Be(message.Role);
        deserializedMessage.Content.Should().Be(message.Content);
        deserializedMessage.Name.Should().Be(message.Name);
        deserializedMessage.Timestamp.Should().BeCloseTo(message.Timestamp, TimeSpan.FromMilliseconds(1));
        deserializedMessage.Metadata.Should().HaveCount(2);
        deserializedMessage.Metadata["key1"].ToString().Should().Be("value1");
    }

    [Theory]
    [InlineData("user")]
    [InlineData("assistant")]
    [InlineData("system")]
    [InlineData("function")]
    public void ChatMessage_ShouldAcceptValidRoles(string role)
    {
        // Act
        var message = new ChatMessage(role, "test content");

        // Assert
        message.Role.Should().Be(role);
    }

    [Fact]
    public void ChatMessage_ShouldHandleEmptyContent()
    {
        // Act
        var message = new ChatMessage("user", "");

        // Assert
        message.Content.Should().Be("");
        message.Role.Should().Be("user");
    }

    [Fact]
    public void ChatMessage_ShouldHandleNullContent()
    {
        // Act
        var message = new ChatMessage("user", null!);

        // Assert
        message.Content.Should().Be(string.Empty);
        message.Role.Should().Be("user");
    }

    [Fact]
    public void ChatMessage_ShouldAllowMetadataModification()
    {
        // Arrange
        var message = new ChatMessage("user", "test");

        // Act
        message.Metadata["custom_key"] = "custom_value";
        message.Metadata["timestamp"] = DateTimeOffset.UtcNow;

        // Assert
        message.Metadata.Should().HaveCount(2);
        message.Metadata["custom_key"].Should().Be("custom_value");
        message.Metadata.Should().ContainKey("timestamp");
    }

    [Fact]
    public void ChatMessage_ShouldProvideTimestampProperty()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var message = new ChatMessage("user", "test");
        var afterCreation = DateTime.UtcNow;

        // Assert
        message.Timestamp.Should().BeOnOrAfter(beforeCreation);
        message.Timestamp.Should().BeOnOrBefore(afterCreation);
    }

    [Fact]
    public void ChatMessage_ShouldAllowTimestampModification()
    {
        // Arrange
        var message = new ChatMessage("user", "test");
        var customTimestamp = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        message.Timestamp = customTimestamp;

        // Assert
        message.Timestamp.Should().Be(customTimestamp);
    }

    [Fact]
    public void ChatMessage_ShouldValidateSuccessfully()
    {
        // Arrange
        var message = new ChatMessage("user", "Valid message content");

        // Act
        var validationResult = message.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
        validationResult.ErrorMessage.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "Valid content")]
    [InlineData("   ", "Valid content")]
    [InlineData(null, "Valid content")]
    public void ChatMessage_ShouldReturnValidationErrorsForInvalidRole(string? invalidRole, string content)
    {
        // Arrange
        var message = new ChatMessage(invalidRole!, content);

        // Act
        var validationResult = message.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.ErrorMessage.Should().Contain("Role cannot be empty");
    }

    [Fact]
    public void ChatMessage_ShouldCreateSystemMessage()
    {
        // Arrange
        var content = "You are a helpful assistant";

        // Act
        var message = ChatMessage.CreateSystemMessage(content);

        // Assert
        message.Role.Should().Be("system");
        message.Content.Should().Be(content);
        message.Name.Should().BeNull();
    }

    [Fact]
    public void ChatMessage_ShouldCreateUserMessage()
    {
        // Arrange
        var content = "Hello there!";

        // Act
        var message = ChatMessage.CreateUserMessage(content);

        // Assert
        message.Role.Should().Be("user");
        message.Content.Should().Be(content);
        message.Name.Should().BeNull();
    }

    [Fact]
    public void ChatMessage_ShouldCreateAssistantMessage()
    {
        // Arrange
        var content = "How can I help you?";

        // Act
        var message = ChatMessage.CreateAssistantMessage(content);

        // Assert
        message.Role.Should().Be("assistant");
        message.Content.Should().Be(content);
        message.Name.Should().BeNull();
    }
} 
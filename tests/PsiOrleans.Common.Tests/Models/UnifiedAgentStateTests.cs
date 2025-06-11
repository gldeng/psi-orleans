using FluentAssertions;
using PsiOrleans.Common.Models;
using System.Text.Json;
using Xunit;
using AutoFixture;

namespace PsiOrleans.Common.Tests.Models;

public class UnifiedAgentStateTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange
        var agentId = "test-agent-001";
        var configuration = _fixture.Create<AgentConfiguration>();

        // Act
        var state = new UnifiedAgentState(agentId, configuration);

        // Assert
        state.AgentId.Should().Be(agentId);
        state.Configuration.Should().Be(configuration);
        state.ChatMessages.Should().BeEmpty();
        state.WorkingMemory.Should().BeEmpty();
        state.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        state.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAgentId_ShouldThrowArgumentException(string invalidAgentId)
    {
        // Arrange
        var configuration = _fixture.Create<AgentConfiguration>();

        // Act & Assert
        Action act = () => new UnifiedAgentState(invalidAgentId, configuration);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*agentId*");
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Arrange
        var agentId = "test-agent-001";

        // Act & Assert
        Action act = () => new UnifiedAgentState(agentId, null);
        act.Should().Throw<ArgumentNullException>()
           .WithMessage("*configuration*");
    }

    [Fact]
    public void AddChatMessage_WithValidParameters_ShouldAddMessage()
    {
        // Arrange
        var state = CreateValidState();
        var role = "user";
        var content = "Hello, how are you?";

        // Act
        state.AddChatMessage(role, content);

        // Assert
        state.ChatMessages.Should().HaveCount(1);
        var message = state.ChatMessages.First();
        message.Role.Should().Be(role);
        message.Content.Should().Be(content);
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        state.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AddChatMessage_WithChatMessageObject_ShouldAddMessage()
    {
        // Arrange
        var state = CreateValidState();
        var message = ChatMessage.CreateUserMessage("Test message");

        // Act
        state.AddChatMessage(message);

        // Assert
        state.ChatMessages.Should().HaveCount(1);
        state.ChatMessages.First().Should().Be(message);
        state.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AddChatMessage_WithNullMessage_ShouldThrowArgumentNullException()
    {
        // Arrange
        var state = CreateValidState();

        // Act & Assert
        Action act = () => state.AddChatMessage(null);
        act.Should().Throw<ArgumentNullException>()
           .WithMessage("*message*");
    }

    [Fact]
    public void AddChatMessage_MultipleCalls_ShouldMaintainOrder()
    {
        // Arrange
        var state = CreateValidState();
        var messages = new[]
        {
            ("system", "You are a helpful assistant"),
            ("user", "Hello"),
            ("assistant", "Hi there!"),
            ("user", "How are you?")
        };

        // Act
        foreach (var (role, content) in messages)
        {
            state.AddChatMessage(role, content);
        }

        // Assert
        state.ChatMessages.Should().HaveCount(4);
        for (int i = 0; i < messages.Length; i++)
        {
            state.ChatMessages[i].Role.Should().Be(messages[i].Item1);
            state.ChatMessages[i].Content.Should().Be(messages[i].Item2);
        }
    }

    [Fact]
    public void SetWorkingMemoryValue_WithValidKeyValue_ShouldStoreValue()
    {
        // Arrange
        var state = CreateValidState();
        var key = "test_key";
        var value = "test_value";

        // Act
        state.SetWorkingMemoryValue(key, value);

        // Assert
        state.WorkingMemory.Should().ContainKey(key);
        state.WorkingMemory[key].Should().Be(value);
        state.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SetWorkingMemoryValue_WithComplexObject_ShouldStoreValue()
    {
        // Arrange
        var state = CreateValidState();
        var key = "complex_object";
        var value = new { Name = "Test", Count = 42, Items = new[] { "a", "b", "c" } };

        // Act
        state.SetWorkingMemoryValue(key, value);

        // Assert
        state.WorkingMemory.Should().ContainKey(key);
        state.WorkingMemory[key].Should().Be(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetWorkingMemoryValue_WithInvalidKey_ShouldThrowArgumentException(string invalidKey)
    {
        // Arrange
        var state = CreateValidState();

        // Act & Assert
        Action act = () => state.SetWorkingMemoryValue(invalidKey, "value");
        act.Should().Throw<ArgumentException>()
           .WithMessage("*key*");
    }

    [Fact]
    public void GetWorkingMemoryValue_WithExistingKey_ShouldReturnValue()
    {
        // Arrange
        var state = CreateValidState();
        var key = "test_key";
        var value = "test_value";
        state.SetWorkingMemoryValue(key, value);

        // Act
        var result = state.GetWorkingMemoryValue<string>(key);

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public void GetWorkingMemoryValue_WithNonExistentKey_ShouldReturnDefault()
    {
        // Arrange
        var state = CreateValidState();

        // Act
        var result = state.GetWorkingMemoryValue<string>("non_existent_key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetWorkingMemoryValue_WithWrongType_ShouldThrowInvalidCastException()
    {
        // Arrange
        var state = CreateValidState();
        var key = "test_key";
        state.SetWorkingMemoryValue(key, "string_value");

        // Act & Assert
        Action act = () => state.GetWorkingMemoryValue<int>(key);
        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void TryGetWorkingMemoryValue_WithExistingKey_ShouldReturnTrueAndValue()
    {
        // Arrange
        var state = CreateValidState();
        var key = "test_key";
        var value = 42;
        state.SetWorkingMemoryValue(key, value);

        // Act
        var success = state.TryGetWorkingMemoryValue<int>(key, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().Be(value);
    }

    [Fact]
    public void TryGetWorkingMemoryValue_WithNonExistentKey_ShouldReturnFalseAndDefault()
    {
        // Arrange
        var state = CreateValidState();

        // Act
        var success = state.TryGetWorkingMemoryValue<string>("non_existent_key", out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetWorkingMemoryValue_WithWrongType_ShouldReturnFalseAndDefault()
    {
        // Arrange
        var state = CreateValidState();
        var key = "test_key";
        state.SetWorkingMemoryValue(key, "string_value");

        // Act
        var success = state.TryGetWorkingMemoryValue<int>(key, out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().Be(default);
    }

    [Fact]
    public void RemoveWorkingMemoryValue_WithExistingKey_ShouldRemoveAndReturnTrue()
    {
        // Arrange
        var state = CreateValidState();
        var key = "test_key";
        state.SetWorkingMemoryValue(key, "test_value");

        // Act
        var result = state.RemoveWorkingMemoryValue(key);

        // Assert
        result.Should().BeTrue();
        state.WorkingMemory.Should().NotContainKey(key);
        state.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RemoveWorkingMemoryValue_WithNonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var state = CreateValidState();

        // Act
        var result = state.RemoveWorkingMemoryValue("non_existent_key");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ClearWorkingMemory_ShouldRemoveAllEntries()
    {
        // Arrange
        var state = CreateValidState();
        state.SetWorkingMemoryValue("key1", "value1");
        state.SetWorkingMemoryValue("key2", "value2");
        state.SetWorkingMemoryValue("key3", "value3");

        // Act
        state.ClearWorkingMemory();

        // Assert
        state.WorkingMemory.Should().BeEmpty();
        state.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ClearChatMessages_ShouldRemoveAllMessages()
    {
        // Arrange
        var state = CreateValidState();
        state.AddChatMessage("system", "System message");
        state.AddChatMessage("user", "User message");
        state.AddChatMessage("assistant", "Assistant message");

        // Act
        state.ClearChatMessages();

        // Assert
        state.ChatMessages.Should().BeEmpty();
        state.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetChatMessagesSince_WithValidTimestamp_ShouldReturnFilteredMessages()
    {
        // Arrange
        var state = CreateValidState();
        var timestampBefore = DateTime.UtcNow.AddMinutes(-1);
        
        state.AddChatMessage("user", "Old message");
        Thread.Sleep(10); // Ensure different timestamps
        
        var timestampAfter = DateTime.UtcNow;
        Thread.Sleep(10);
        
        state.AddChatMessage("assistant", "New message 1");
        state.AddChatMessage("user", "New message 2");

        // Act
        var recentMessages = state.GetChatMessagesSince(timestampAfter);

        // Assert
        recentMessages.Should().HaveCount(2);
        recentMessages.All(m => m.Timestamp > timestampAfter).Should().BeTrue();
    }

    [Fact]
    public void GetChatMessagesByRole_WithValidRole_ShouldReturnFilteredMessages()
    {
        // Arrange
        var state = CreateValidState();
        state.AddChatMessage("system", "System message");
        state.AddChatMessage("user", "User message 1");
        state.AddChatMessage("assistant", "Assistant message");
        state.AddChatMessage("user", "User message 2");

        // Act
        var userMessages = state.GetChatMessagesByRole("user");

        // Assert
        userMessages.Should().HaveCount(2);
        userMessages.All(m => m.Role == "user").Should().BeTrue();
        userMessages[0].Content.Should().Be("User message 1");
        userMessages[1].Content.Should().Be("User message 2");
    }

    [Fact]
    public void UpdateConfiguration_WithValidConfiguration_ShouldUpdateAndTimestamp()
    {
        // Arrange
        var state = CreateValidState();
        var newConfiguration = _fixture.Create<AgentConfiguration>();

        // Act
        state.UpdateConfiguration(newConfiguration);

        // Assert
        state.Configuration.Should().Be(newConfiguration);
        state.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateConfiguration_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Arrange
        var state = CreateValidState();

        // Act & Assert
        Action act = () => state.UpdateConfiguration(null);
        act.Should().Throw<ArgumentNullException>()
           .WithMessage("*configuration*");
    }

    [Fact]
    public void GetStateSnapshot_ShouldReturnDeepCopy()
    {
        // Arrange
        var state = CreateValidState();
        state.AddChatMessage("user", "Test message");
        state.SetWorkingMemoryValue("test_key", "test_value");

        // Act
        var snapshot = state.GetStateSnapshot();

        // Assert
        snapshot.Should().NotBeSameAs(state);
        snapshot.AgentId.Should().Be(state.AgentId);
        snapshot.Configuration.Should().Be(state.Configuration);
        snapshot.ChatMessages.Should().HaveCount(state.ChatMessages.Count);
        snapshot.WorkingMemory.Should().HaveCount(state.WorkingMemory.Count);
        
        // Verify it's a deep copy by modifying original
        state.AddChatMessage("assistant", "Another message");
        snapshot.ChatMessages.Should().HaveCount(state.ChatMessages.Count - 1);
    }

    [Fact]
    public void Validate_WithValidState_ShouldReturnSuccess()
    {
        // Arrange
        var state = CreateValidState();

        // Act
        var result = state.Validate();

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithInvalidConfiguration_ShouldReturnErrors()
    {
        // Arrange
        var invalidConfig = new AgentConfiguration
        {
            AgentName = "", // Invalid
            SystemPrompt = "", // Invalid
            Model = new ModelConfiguration { ModelId = "" } // Invalid
        };
        var state = new UnifiedAgentState("test-agent", invalidConfig);

        // Act
        var result = state.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeEmpty();
    }

    [Fact] 
    public void JsonSerialization_ShouldSerializeAndDeserializeCorrectly()
    {
        // Arrange
        var state = CreateValidState();
        state.AddChatMessage("user", "Test message");
        state.SetWorkingMemoryValue("test_key", "test_value");
        state.SetWorkingMemoryValue("number_key", 42);

        // Act
        var json = JsonSerializer.Serialize(state);
        var deserializedState = JsonSerializer.Deserialize<UnifiedAgentState>(json);

        // Assert
        deserializedState.Should().NotBeNull();
        deserializedState.AgentId.Should().Be(state.AgentId);
        deserializedState.Configuration.AgentName.Should().Be(state.Configuration.AgentName);
        deserializedState.ChatMessages.Should().HaveCount(state.ChatMessages.Count);
        deserializedState.WorkingMemory.Should().HaveCount(state.WorkingMemory.Count);
        deserializedState.CreatedAt.Should().Be(state.CreatedAt);
        deserializedState.LastModified.Should().Be(state.LastModified);
    }

    [Fact]
    public void ChatMessages_Collection_ShouldBeReadOnly()
    {
        // Arrange
        var state = CreateValidState();

        // Act & Assert
        state.ChatMessages.Should().BeAssignableTo<IReadOnlyList<ChatMessage>>();
    }

    [Fact]
    public void WorkingMemory_Collection_ShouldBeReadOnly()
    {
        // Arrange
        var state = CreateValidState();

        // Act & Assert
        state.WorkingMemory.Should().BeAssignableTo<IReadOnlyDictionary<string, object>>();
    }

    [Fact]
    public void ToString_ShouldProvideUsefulRepresentation()
    {
        // Arrange
        var state = CreateValidState();
        state.AddChatMessage("user", "Test");

        // Act
        var stringRepresentation = state.ToString();

        // Assert
        stringRepresentation.Should().Contain(state.AgentId);
        stringRepresentation.Should().Contain("1"); // Message count
        stringRepresentation.Should().Contain("0"); // Working memory count
    }

    private UnifiedAgentState CreateValidState()
    {
        var configuration = new AgentConfiguration
        {
            AgentName = "TestAgent",
            SystemPrompt = "You are a helpful assistant",
            Temperature = 0.7,
            MaxTokens = 2000,
            Model = new ModelConfiguration
            {
                ModelId = "gpt-4o-mini"
            }
        };
        return new UnifiedAgentState("test-agent-001", configuration);
    }
} 
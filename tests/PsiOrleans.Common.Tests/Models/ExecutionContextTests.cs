using FluentAssertions;
using PsiOrleans.Common.Models;
using System.Text.Json;
using Xunit;

namespace PsiOrleans.Common.Tests.Models;

public class ExecutionContextTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateValidExecutionContext()
    {
        // Arrange
        const string executionId = "exec-123";
        var agentId = AgentId.NewId();
        const string taskDescription = "Test task";
        const ExecutionStatus status = ExecutionStatus.Running;
        var startedAt = DateTime.UtcNow;

        // Act
        var context = ExecutionContext.Create(executionId, agentId, taskDescription, status, startedAt);

        // Assert
        context.ExecutionId.Should().Be(executionId);
        context.AgentId.Should().Be(agentId);
        context.TaskDescription.Should().Be(taskDescription);
        context.Status.Should().Be(status);
        context.StartedAt.Should().Be(startedAt);
        context.CompletedAt.Should().BeNull();
        context.Duration.Should().BeNull();
        context.Metadata.Should().NotBeNull().And.BeEmpty();
        context.ErrorMessage.Should().BeNull();
        context.IsValid.Should().BeTrue();
        context.IsRunning.Should().BeTrue();
        context.IsFinished.Should().BeFalse();
        context.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public void Create_WithMetadata_ShouldIncludeMetadata()
    {
        // Arrange
        var agentId = AgentId.NewId();
        var metadata = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42,
            ["key3"] = true
        };

        // Act
        var context = ExecutionContext.Create("exec-123", agentId, "Test task", ExecutionStatus.Running, DateTime.UtcNow, metadata: metadata);

        // Assert
        context.Metadata.Should().HaveCount(3);
        context.Metadata["key1"].Should().Be("value1");
        context.Metadata["key2"].Should().Be(42);
        context.Metadata["key3"].Should().Be(true);
    }

    [Fact]
    public void CreateStarted_ShouldGenerateExecutionIdAndUseCurrentTime()
    {
        // Arrange
        var agentId = AgentId.NewId();
        const string taskDescription = "Test task";
        var beforeCreate = DateTime.UtcNow;

        // Act
        var context = ExecutionContext.CreateStarted(agentId, taskDescription);
        var afterCreate = DateTime.UtcNow;

        // Assert
        context.ExecutionId.Should().StartWith("exec-").And.HaveLength(37); // "exec-" + 32 char GUID
        context.AgentId.Should().Be(agentId);
        context.TaskDescription.Should().Be(taskDescription);
        context.Status.Should().Be(ExecutionStatus.Running);
        context.StartedAt.Should().BeOnOrAfter(beforeCreate).And.BeOnOrBefore(afterCreate);
        context.IsValid.Should().BeTrue();
        context.IsRunning.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidExecutionId_ShouldCreateInvalidContext(string? invalidExecutionId)
    {
        // Arrange
        var agentId = AgentId.NewId();

        // Act
        var context = ExecutionContext.Create(invalidExecutionId!, agentId, "Test task", ExecutionStatus.Running, DateTime.UtcNow);

        // Assert
        context.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_WithInvalidAgentId_ShouldCreateInvalidContext()
    {
        // Arrange
        var invalidAgentId = AgentId.Empty;

        // Act
        var context = ExecutionContext.Create("exec-123", invalidAgentId, "Test task", ExecutionStatus.Running, DateTime.UtcNow);

        // Assert
        context.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidTaskDescription_ShouldCreateInvalidContext(string? invalidTaskDescription)
    {
        // Arrange
        var agentId = AgentId.NewId();

        // Act
        var context = ExecutionContext.Create("exec-123", agentId, invalidTaskDescription!, ExecutionStatus.Running, DateTime.UtcNow);

        // Assert
        context.IsValid.Should().BeFalse();
    }

    [Fact]
    public void WithStatus_ToCompletedStatus_ShouldSetCompletedAtAutomatically()
    {
        // Arrange
        var originalContext = ExecutionContext.CreateStarted(AgentId.NewId(), "Test task");
        var beforeComplete = DateTime.UtcNow;

        // Act
        var completedContext = originalContext.WithStatus(ExecutionStatus.Completed);
        var afterComplete = DateTime.UtcNow;

        // Assert
        completedContext.Status.Should().Be(ExecutionStatus.Completed);
        completedContext.CompletedAt.Should().NotBeNull()
            .And.BeOnOrAfter(beforeComplete)
            .And.BeOnOrBefore(afterComplete);
        completedContext.Duration.Should().NotBeNull();
        completedContext.IsFinished.Should().BeTrue();
        completedContext.IsSuccessful.Should().BeTrue();
        completedContext.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void WithStatus_ToRunningStatus_ShouldNotSetCompletedAt()
    {
        // Arrange
        var originalContext = ExecutionContext.Create("exec-123", AgentId.NewId(), "Test task", ExecutionStatus.Created, DateTime.UtcNow);

        // Act
        var runningContext = originalContext.WithStatus(ExecutionStatus.Running);

        // Assert
        runningContext.Status.Should().Be(ExecutionStatus.Running);
        runningContext.CompletedAt.Should().BeNull();
        runningContext.Duration.Should().BeNull();
        runningContext.IsRunning.Should().BeTrue();
        runningContext.IsFinished.Should().BeFalse();
    }

    [Fact]
    public void WithCompleted_ShouldMarkAsCompleted()
    {
        // Arrange
        var originalContext = ExecutionContext.CreateStarted(AgentId.NewId(), "Test task");
        var completedAt = DateTime.UtcNow.AddMinutes(5);

        // Act
        var completedContext = originalContext.WithCompleted(completedAt);

        // Assert
        completedContext.Status.Should().Be(ExecutionStatus.Completed);
        completedContext.CompletedAt.Should().Be(completedAt);
        completedContext.Duration.Should().Be(completedAt - originalContext.StartedAt);
        completedContext.IsSuccessful.Should().BeTrue();
        completedContext.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void WithError_ShouldMarkAsFailedWithErrorMessage()
    {
        // Arrange
        var originalContext = ExecutionContext.CreateStarted(AgentId.NewId(), "Test task");
        const string errorMessage = "Something went wrong";
        var failedAt = DateTime.UtcNow.AddMinutes(2);

        // Act
        var failedContext = originalContext.WithError(errorMessage, failedAt);

        // Assert
        failedContext.Status.Should().Be(ExecutionStatus.Failed);
        failedContext.ErrorMessage.Should().Be(errorMessage);
        failedContext.CompletedAt.Should().Be(failedAt);
        failedContext.Duration.Should().Be(failedAt - originalContext.StartedAt);
        failedContext.IsFinished.Should().BeTrue();
        failedContext.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public void WithMetadata_ShouldReplaceAllMetadata()
    {
        // Arrange
        var originalMetadata = new Dictionary<string, object> { ["old"] = "value" };
        var originalContext = ExecutionContext.Create("exec-123", AgentId.NewId(), "Test task", ExecutionStatus.Running, DateTime.UtcNow, metadata: originalMetadata);
        
        var newMetadata = new Dictionary<string, object>
        {
            ["new1"] = "value1",
            ["new2"] = 42
        };

        // Act
        var updatedContext = originalContext.WithMetadata(newMetadata);

        // Assert
        updatedContext.Metadata.Should().HaveCount(2);
        updatedContext.Metadata.Should().ContainKey("new1").WhoseValue.Should().Be("value1");
        updatedContext.Metadata.Should().ContainKey("new2").WhoseValue.Should().Be(42);
        updatedContext.Metadata.Should().NotContainKey("old");
    }

    [Fact]
    public void WithMetadata_WithKeyValue_ShouldAddOrUpdateSingleEntry()
    {
        // Arrange
        var originalMetadata = new Dictionary<string, object> { ["existing"] = "old_value" };
        var originalContext = ExecutionContext.Create("exec-123", AgentId.NewId(), "Test task", ExecutionStatus.Running, DateTime.UtcNow, metadata: originalMetadata);

        // Act
        var updatedContext = originalContext.WithMetadata("new_key", "new_value")
                                          .WithMetadata("existing", "updated_value");

        // Assert
        updatedContext.Metadata.Should().HaveCount(2);
        updatedContext.Metadata["new_key"].Should().Be("new_value");
        updatedContext.Metadata["existing"].Should().Be("updated_value");
    }

    [Fact]
    public void Duration_WhenCompletedAtIsSet_ShouldCalculateCorrectDuration()
    {
        // Arrange
        var startedAt = new DateTime(2023, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2023, 1, 1, 10, 5, 30, DateTimeKind.Utc);
        var expectedDuration = TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30));

        var context = ExecutionContext.Create("exec-123", AgentId.NewId(), "Test task", ExecutionStatus.Completed, startedAt, completedAt);

        // Act
        var duration = context.Duration;

        // Assert
        duration.Should().Be(expectedDuration);
    }

    [Fact]
    public void Duration_WhenCompletedAtIsNull_ShouldReturnNull()
    {
        // Arrange
        var context = ExecutionContext.CreateStarted(AgentId.NewId(), "Test task");

        // Act
        var duration = context.Duration;

        // Assert
        duration.Should().BeNull();
    }

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        var agentId = AgentId.NewId();
        var startedAt = DateTime.UtcNow;
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        var context1 = ExecutionContext.Create("exec-123", agentId, "Test task", ExecutionStatus.Running, startedAt, metadata: metadata);
        var context2 = ExecutionContext.Create("exec-123", agentId, "Test task", ExecutionStatus.Running, startedAt, metadata: metadata);

        // Act & Assert
        context1.Should().Be(context2);
        context1.Equals(context2).Should().BeTrue();
        (context1 == context2).Should().BeTrue();
        (context1 != context2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentExecutionId_ShouldReturnFalse()
    {
        // Arrange
        var agentId = AgentId.NewId();
        var startedAt = DateTime.UtcNow;

        var context1 = ExecutionContext.Create("exec-123", agentId, "Test task", ExecutionStatus.Running, startedAt);
        var context2 = ExecutionContext.Create("exec-456", agentId, "Test task", ExecutionStatus.Running, startedAt);

        // Act & Assert
        context1.Should().NotBe(context2);
    }

    [Fact]
    public void Equals_WithDifferentMetadata_ShouldReturnFalse()
    {
        // Arrange
        var agentId = AgentId.NewId();
        var startedAt = DateTime.UtcNow;
        var metadata1 = new Dictionary<string, object> { ["key"] = "value1" };
        var metadata2 = new Dictionary<string, object> { ["key"] = "value2" };

        var context1 = ExecutionContext.Create("exec-123", agentId, "Test task", ExecutionStatus.Running, startedAt, metadata: metadata1);
        var context2 = ExecutionContext.Create("exec-123", agentId, "Test task", ExecutionStatus.Running, startedAt, metadata: metadata2);

        // Act & Assert
        context1.Should().NotBe(context2);
    }

    [Fact]
    public void GetHashCode_WithSameValues_ShouldReturnSameHashCode()
    {
        // Arrange
        var agentId = AgentId.NewId();
        var startedAt = DateTime.UtcNow;
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        var context1 = ExecutionContext.Create("exec-123", agentId, "Test task", ExecutionStatus.Running, startedAt, metadata: metadata);
        var context2 = ExecutionContext.Create("exec-123", agentId, "Test task", ExecutionStatus.Running, startedAt, metadata: metadata);

        // Act & Assert
        context1.GetHashCode().Should().Be(context2.GetHashCode());
    }

    [Fact]
    public void ToString_WithValidContext_ShouldReturnFormattedString()
    {
        // Arrange
        var agentId = AgentId.Parse("test-agent-123");
        var startedAt = new DateTime(2023, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2023, 1, 1, 10, 5, 30, DateTimeKind.Utc);

        var context = ExecutionContext.Create("exec-123", agentId, "Test task", ExecutionStatus.Completed, startedAt, completedAt);

        // Act
        var result = context.ToString();

        // Assert
        result.Should().Contain("exec-123");
        result.Should().Contain("test-agent-123");
        result.Should().Contain("Completed");
        result.Should().Contain("00:05:30");
    }

    [Fact]
    public void Empty_ShouldReturnInvalidExecutionContext()
    {
        // Act
        var emptyContext = ExecutionContext.Empty;

        // Assert
        emptyContext.IsValid.Should().BeFalse();
        emptyContext.ExecutionId.Should().BeEmpty();
        emptyContext.AgentId.Should().Be(AgentId.Empty);
        emptyContext.TaskDescription.Should().BeEmpty();
        emptyContext.Status.Should().Be(ExecutionStatus.Created);
        emptyContext.StartedAt.Should().Be(DateTime.MinValue);
        emptyContext.CompletedAt.Should().BeNull();
        emptyContext.Metadata.Should().BeEmpty();
        emptyContext.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void JsonSerialization_WithValidContext_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var agentId = AgentId.NewId();
        var metadata = new Dictionary<string, object>
        {
            ["stringValue"] = "test",
            ["intValue"] = 42,
            ["boolValue"] = true
        };

        var originalContext = ExecutionContext.Create(
            "exec-123",
            agentId,
            "Test task",
            ExecutionStatus.Completed,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(5),
            metadata,
            "No errors");

        var options = new JsonSerializerOptions
        {
            Converters = { new ExecutionContextJsonConverter(), new AgentIdJsonConverter() }
        };

        // Act
        var json = JsonSerializer.Serialize(originalContext, options);
        var deserializedContext = JsonSerializer.Deserialize<ExecutionContext>(json, options);

        // Assert
        deserializedContext.Should().NotBeNull();
        deserializedContext!.ExecutionId.Should().Be(originalContext.ExecutionId);
        deserializedContext.AgentId.Should().Be(originalContext.AgentId);
        deserializedContext.TaskDescription.Should().Be(originalContext.TaskDescription);
        deserializedContext.Status.Should().Be(originalContext.Status);
        deserializedContext.StartedAt.Should().Be(originalContext.StartedAt);
        deserializedContext.CompletedAt.Should().Be(originalContext.CompletedAt);
        deserializedContext.ErrorMessage.Should().Be(originalContext.ErrorMessage);
        deserializedContext.Metadata.Should().HaveCount(3);
    }

    [Fact]
    public void JsonSerialization_WithInvalidContext_ShouldSerializeAsNull()
    {
        // Arrange
        var invalidContext = ExecutionContext.Empty;
        var options = new JsonSerializerOptions
        {
            Converters = { new ExecutionContextJsonConverter() }
        };

        // Act
        var json = JsonSerializer.Serialize(invalidContext, options);

        // Assert
        json.Should().Be("null");
    }
} 
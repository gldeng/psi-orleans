using FluentAssertions;
using PsiOrleans.Common.Models;
using System.Text.Json;
using Xunit;
using ExecutionContext = PsiOrleans.Common.Models.ExecutionContext;

namespace PsiOrleans.Common.Tests.Models;

public class AgentStepTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateValidAgentStep()
    {
        // Arrange
        const string stepId = "step-123";
        const StepType stepType = StepType.Task;
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        const string instruction = "Perform test task";
        var inputs = new Dictionary<string, object> { ["param1"] = "value1" };
        var outputs = new Dictionary<string, object> { ["result"] = "success" };
        var dependencies = new List<string> { "step-1", "step-2" };
        const int priority = 5;

        // Act
        var step = AgentStep.Create(stepId, stepType, agent, instruction, inputs, outputs, dependencies, priority);

        // Assert
        step.StepId.Should().Be(stepId);
        step.StepType.Should().Be(stepType);
        step.Agent.Should().Be(agent);
        step.Instruction.Should().Be(instruction);
        step.Inputs.Should().HaveCount(1).And.ContainKey("param1");
        step.Outputs.Should().HaveCount(1).And.ContainKey("result");
        step.Dependencies.Should().HaveCount(2).And.Contain("step-1").And.Contain("step-2");
        step.Priority.Should().Be(priority);
        step.IsValid.Should().BeTrue();
        step.CanExecute.Should().BeTrue();
        step.IsRunning.Should().BeFalse();
        step.IsCompleted.Should().BeFalse();
        step.IsSuccessful.Should().BeFalse();
        step.HasFailed.Should().BeFalse();
    }

    [Fact]
    public void CreateTask_ShouldCreateTaskStep()
    {
        // Arrange
        const string stepId = "task-123";
        var agent = AgentIdentity.Create(AgentId.NewId(), "TaskAgent", AgentRole.Specialized);
        const string instruction = "Perform task";
        var inputs = new Dictionary<string, object> { ["data"] = "test" };

        // Act
        var step = AgentStep.CreateTask(stepId, agent, instruction, inputs, 3);

        // Assert
        step.StepId.Should().Be(stepId);
        step.StepType.Should().Be(StepType.Task);
        step.Agent.Should().Be(agent);
        step.Instruction.Should().Be(instruction);
        step.Inputs.Should().HaveCount(1);
        step.Priority.Should().Be(3);
        step.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateDecision_ShouldCreateDecisionStep()
    {
        // Arrange
        const string stepId = "decision-123";
        var agent = AgentIdentity.Create(AgentId.NewId(), "DecisionAgent", AgentRole.Orchestrator);
        const string instruction = "Make decision";

        // Act
        var step = AgentStep.CreateDecision(stepId, agent, instruction);

        // Assert
        step.StepId.Should().Be(stepId);
        step.StepType.Should().Be(StepType.Decision);
        step.Agent.Should().Be(agent);
        step.Instruction.Should().Be(instruction);
        step.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidStepId_ShouldCreateInvalidStep(string? invalidStepId)
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);

        // Act
        var step = AgentStep.Create(invalidStepId!, StepType.Task, agent, "Test instruction");

        // Assert
        step.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_WithInvalidAgent_ShouldCreateInvalidStep()
    {
        // Arrange
        var invalidAgent = AgentIdentity.Empty;

        // Act
        var step = AgentStep.Create("step-123", StepType.Task, invalidAgent, "Test instruction");

        // Assert
        step.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidInstruction_ShouldCreateInvalidStep(string? invalidInstruction)
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);

        // Act
        var step = AgentStep.Create("step-123", StepType.Task, agent, invalidInstruction!);

        // Assert
        step.IsValid.Should().BeFalse();
    }

    [Fact]
    public void WithAgent_ShouldUpdateAgentAndExecution()
    {
        // Arrange
        var originalAgent = AgentIdentity.Create(AgentId.NewId(), "OriginalAgent", AgentRole.Specialized);
        var newAgent = AgentIdentity.Create(AgentId.NewId(), "NewAgent", AgentRole.Orchestrator);
        var originalStep = AgentStep.CreateTask("step-123", originalAgent, "Test task");

        // Act
        var updatedStep = originalStep.WithAgent(newAgent);

        // Assert
        updatedStep.Agent.Should().Be(newAgent);
        updatedStep.Execution.AgentId.Should().Be(newAgent.Id);
        updatedStep.StepId.Should().Be(originalStep.StepId);
        updatedStep.Instruction.Should().Be(originalStep.Instruction);
    }

    [Fact]
    public void WithExecution_ShouldUpdateExecutionContext()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var originalStep = AgentStep.CreateTask("step-123", agent, "Test task");
        var newExecution = ExecutionContext.CreateStarted(agent.Id, "Updated task");

        // Act
        var updatedStep = originalStep.WithExecution(newExecution);

        // Assert
        updatedStep.Execution.Should().Be(newExecution);
        updatedStep.StepId.Should().Be(originalStep.StepId);
        updatedStep.Agent.Should().Be(originalStep.Agent);
    }

    [Fact]
    public void WithDependency_ShouldAddNewDependency()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var step = AgentStep.CreateTask("step-123", agent, "Test task");

        // Act
        var updatedStep = step.WithDependency("dependency-1")
                             .WithDependency("dependency-2");

        // Assert
        updatedStep.Dependencies.Should().HaveCount(2)
                                .And.Contain("dependency-1")
                                .And.Contain("dependency-2");
    }

    [Fact]
    public void WithDependency_WithDuplicateDependency_ShouldNotAddDuplicate()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var step = AgentStep.CreateTask("step-123", agent, "Test task")
                           .WithDependency("dependency-1");

        // Act
        var updatedStep = step.WithDependency("dependency-1");

        // Assert
        updatedStep.Dependencies.Should().HaveCount(1)
                                .And.Contain("dependency-1");
        updatedStep.Should().BeSameAs(step); // Should return same instance
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void WithDependency_WithInvalidDependency_ShouldReturnSameInstance(string? invalidDependency)
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var step = AgentStep.CreateTask("step-123", agent, "Test task");

        // Act
        var updatedStep = step.WithDependency(invalidDependency!);

        // Assert
        updatedStep.Should().BeSameAs(step);
    }

    [Fact]
    public void WithInputs_ShouldReplaceAllInputs()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var originalInputs = new Dictionary<string, object> { ["old"] = "value" };
        var step = AgentStep.CreateTask("step-123", agent, "Test task", originalInputs);
        
        var newInputs = new Dictionary<string, object>
        {
            ["new1"] = "value1",
            ["new2"] = 42
        };

        // Act
        var updatedStep = step.WithInputs(newInputs);

        // Assert
        updatedStep.Inputs.Should().HaveCount(2);
        updatedStep.Inputs.Should().ContainKey("new1").WhoseValue.Should().Be("value1");
        updatedStep.Inputs.Should().ContainKey("new2").WhoseValue.Should().Be(42);
        updatedStep.Inputs.Should().NotContainKey("old");
    }

    [Fact]
    public void WithInput_ShouldAddOrUpdateSingleInput()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var originalInputs = new Dictionary<string, object> { ["existing"] = "old_value" };
        var step = AgentStep.CreateTask("step-123", agent, "Test task", originalInputs);

        // Act
        var updatedStep = step.WithInput("new_key", "new_value")
                             .WithInput("existing", "updated_value");

        // Assert
        updatedStep.Inputs.Should().HaveCount(2);
        updatedStep.Inputs["new_key"].Should().Be("new_value");
        updatedStep.Inputs["existing"].Should().Be("updated_value");
    }

    [Fact]
    public void WithOutputs_ShouldReplaceAllOutputs()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var step = AgentStep.CreateTask("step-123", agent, "Test task");
        
        var outputs = new Dictionary<string, object>
        {
            ["result"] = "success",
            ["count"] = 5
        };

        // Act
        var updatedStep = step.WithOutputs(outputs);

        // Assert
        updatedStep.Outputs.Should().HaveCount(2);
        updatedStep.Outputs.Should().ContainKey("result").WhoseValue.Should().Be("success");
        updatedStep.Outputs.Should().ContainKey("count").WhoseValue.Should().Be(5);
    }

    [Fact]
    public void WithOutput_ShouldAddOrUpdateSingleOutput()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var step = AgentStep.CreateTask("step-123", agent, "Test task");

        // Act
        var updatedStep = step.WithOutput("result", "success")
                             .WithOutput("timestamp", DateTime.UtcNow);

        // Assert
        updatedStep.Outputs.Should().HaveCount(2);
        updatedStep.Outputs["result"].Should().Be("success");
        updatedStep.Outputs.Should().ContainKey("timestamp");
    }

    [Fact]
    public void MarkReady_ShouldSetExecutionStatusToCreated()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var step = AgentStep.CreateTask("step-123", agent, "Test task");

        // Act
        var readyStep = step.MarkReady();

        // Assert
        readyStep.Execution.Status.Should().Be(ExecutionStatus.Created);
        readyStep.CanExecute.Should().BeTrue();
        readyStep.IsRunning.Should().BeFalse();
        readyStep.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void MarkRunning_ShouldSetExecutionStatusToRunning()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var step = AgentStep.CreateTask("step-123", agent, "Test task");

        // Act
        var runningStep = step.MarkRunning();

        // Assert
        runningStep.Execution.Status.Should().Be(ExecutionStatus.Running);
        runningStep.IsRunning.Should().BeTrue();
        runningStep.CanExecute.Should().BeFalse();
        runningStep.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void MarkCompleted_WithoutOutputs_ShouldSetExecutionStatusToCompleted()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var step = AgentStep.CreateTask("step-123", agent, "Test task").MarkRunning();

        // Act
        var completedStep = step.MarkCompleted();

        // Assert
        completedStep.Execution.Status.Should().Be(ExecutionStatus.Completed);
        completedStep.IsCompleted.Should().BeTrue();
        completedStep.IsSuccessful.Should().BeTrue();
        completedStep.IsRunning.Should().BeFalse();
        completedStep.HasFailed.Should().BeFalse();
        completedStep.Execution.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkCompleted_WithOutputs_ShouldSetExecutionStatusAndUpdateOutputs()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var step = AgentStep.CreateTask("step-123", agent, "Test task").MarkRunning();
        var outputs = new Dictionary<string, object>
        {
            ["result"] = "completed successfully",
            ["duration"] = TimeSpan.FromSeconds(30)
        };

        // Act
        var completedStep = step.MarkCompleted(outputs);

        // Assert
        completedStep.Execution.Status.Should().Be(ExecutionStatus.Completed);
        completedStep.IsCompleted.Should().BeTrue();
        completedStep.IsSuccessful.Should().BeTrue();
        completedStep.Outputs.Should().HaveCount(2);
        completedStep.Outputs["result"].Should().Be("completed successfully");
        completedStep.Outputs.Should().ContainKey("duration");
    }

    [Fact]
    public void MarkFailed_ShouldSetExecutionStatusToFailedWithErrorMessage()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var step = AgentStep.CreateTask("step-123", agent, "Test task").MarkRunning();
        const string errorMessage = "Task execution failed due to timeout";

        // Act
        var failedStep = step.MarkFailed(errorMessage);

        // Assert
        failedStep.Execution.Status.Should().Be(ExecutionStatus.Failed);
        failedStep.HasFailed.Should().BeTrue();
        failedStep.IsCompleted.Should().BeTrue();
        failedStep.IsSuccessful.Should().BeFalse();
        failedStep.IsRunning.Should().BeFalse();
        failedStep.Execution.ErrorMessage.Should().Be(errorMessage);
        failedStep.Execution.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var inputs = new Dictionary<string, object> { ["key"] = "value" };
        var dependencies = new List<string> { "dep-1" };
        var createdAt = DateTime.UtcNow;

        var step1 = AgentStep.Create("step-123", StepType.Task, agent, "Test instruction", inputs, null, dependencies, 5, null, createdAt);
        var step2 = AgentStep.Create("step-123", StepType.Task, agent, "Test instruction", inputs, null, dependencies, 5, null, createdAt);

        // Act & Assert
        step1.Should().Be(step2);
        step1.Equals(step2).Should().BeTrue();
        (step1 == step2).Should().BeTrue();
        (step1 != step2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentStepId_ShouldReturnFalse()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);

        var step1 = AgentStep.CreateTask("step-123", agent, "Test instruction");
        var step2 = AgentStep.CreateTask("step-456", agent, "Test instruction");

        // Act & Assert
        step1.Should().NotBe(step2);
    }

    [Fact]
    public void Equals_WithDifferentInputs_ShouldReturnFalse()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var inputs1 = new Dictionary<string, object> { ["key"] = "value1" };
        var inputs2 = new Dictionary<string, object> { ["key"] = "value2" };

        var step1 = AgentStep.CreateTask("step-123", agent, "Test instruction", inputs1);
        var step2 = AgentStep.CreateTask("step-123", agent, "Test instruction", inputs2);

        // Act & Assert
        step1.Should().NotBe(step2);
    }

    [Fact]
    public void GetHashCode_WithSameValues_ShouldReturnSameHashCode()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var inputs = new Dictionary<string, object> { ["key"] = "value" };
        var createdAt = DateTime.UtcNow;

        var step1 = AgentStep.Create("step-123", StepType.Task, agent, "Test instruction", inputs, null, null, 5, null, createdAt);
        var step2 = AgentStep.Create("step-123", StepType.Task, agent, "Test instruction", inputs, null, null, 5, null, createdAt);

        // Act & Assert
        step1.GetHashCode().Should().Be(step2.GetHashCode());
    }

    [Fact]
    public void ToString_WithValidStep_ShouldReturnFormattedString()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.Parse("agent-123"), "TestAgent", AgentRole.Orchestrator);
        var step = AgentStep.CreateTask("step-123", agent, "Test task")
                           .WithDependency("dep-1")
                           .WithDependency("dep-2")
                           .MarkRunning();

        // Act
        var result = step.ToString();

        // Assert
        result.Should().Contain("step-123");
        result.Should().Contain("Task");
        result.Should().Contain("TestAgent");
        result.Should().Contain("Running");
        result.Should().Contain("dep-1");
        result.Should().Contain("dep-2");
    }

    [Fact]
    public void Empty_ShouldReturnInvalidAgentStep()
    {
        // Act
        var emptyStep = AgentStep.Empty;

        // Assert
        emptyStep.IsValid.Should().BeFalse();
        emptyStep.StepId.Should().BeEmpty();
        emptyStep.Agent.Should().Be(AgentIdentity.Empty);
        emptyStep.Execution.Should().Be(ExecutionContext.Empty);
        emptyStep.Instruction.Should().BeEmpty();
        emptyStep.Inputs.Should().BeEmpty();
        emptyStep.Outputs.Should().BeEmpty();
        emptyStep.Dependencies.Should().BeEmpty();
        emptyStep.Priority.Should().Be(0);
        emptyStep.CreatedAt.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void JsonSerialization_WithValidStep_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var agent = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var inputs = new Dictionary<string, object>
        {
            ["stringValue"] = "test",
            ["intValue"] = 42,
            ["boolValue"] = true
        };
        var outputs = new Dictionary<string, object>
        {
            ["result"] = "success"
        };
        var dependencies = new List<string> { "dep-1", "dep-2" };
        var createdAt = new DateTime(2023, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        var originalStep = AgentStep.Create(
            "step-123",
            StepType.Decision,
            agent,
            "Make decision",
            inputs,
            outputs,
            dependencies,
            5,
            null,
            createdAt);

        var options = new JsonSerializerOptions
        {
            Converters = 
            { 
                new AgentStepJsonConverter(), 
                new AgentIdentityJsonConverter(),
                new ExecutionContextJsonConverter(),
                new AgentIdJsonConverter() 
            }
        };

        // Act
        var json = JsonSerializer.Serialize(originalStep, options);
        var deserializedStep = JsonSerializer.Deserialize<AgentStep>(json, options);

        // Assert
        deserializedStep.Should().NotBeNull();
        deserializedStep!.StepId.Should().Be(originalStep.StepId);
        deserializedStep.StepType.Should().Be(originalStep.StepType);
        deserializedStep.Agent.Should().Be(originalStep.Agent);
        deserializedStep.Instruction.Should().Be(originalStep.Instruction);
        deserializedStep.Inputs.Should().HaveCount(3);
        deserializedStep.Outputs.Should().HaveCount(1);
        deserializedStep.Dependencies.Should().HaveCount(2);
        deserializedStep.Priority.Should().Be(originalStep.Priority);
        deserializedStep.CreatedAt.Should().Be(originalStep.CreatedAt);
    }

    [Fact]
    public void JsonSerialization_WithInvalidStep_ShouldSerializeAsNull()
    {
        // Arrange
        var invalidStep = AgentStep.Empty;
        var options = new JsonSerializerOptions
        {
            Converters = { new AgentStepJsonConverter() }
        };

        // Act
        var json = JsonSerializer.Serialize(invalidStep, options);

        // Assert
        json.Should().Be("null");
    }
} 
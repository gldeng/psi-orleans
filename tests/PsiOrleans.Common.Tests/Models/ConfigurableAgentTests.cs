using FluentAssertions;
using PsiOrleans.Common.Models;
using System.Text.Json;
using Xunit;
using ExecutionContext = PsiOrleans.Common.Models.ExecutionContext;

namespace PsiOrleans.Common.Tests.Models;

public class ConfigurableAgentTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateValidConfigurableAgent()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration
        {
            SystemPrompt = "You are a test agent",
            AgentName = "TestAgent",
            Temperature = 0.7,
            MaxTokens = 2000
        };
        var execution = ExecutionContext.CreateStarted(identity.Id, "Test execution");
        var metrics = AgentMetrics.Create(5, 3, 2, TimeSpan.FromMinutes(10));
        var workingMemory = new Dictionary<string, object> { ["test"] = "value" };

        // Act
        var agent = ConfigurableAgent.Create(
            identity,
            configuration,
            execution,
            null,
            null,
            metrics,
            workingMemory);

        // Assert
        agent.Identity.Should().Be(identity);
        agent.Configuration.Should().Be(configuration);
        agent.Execution.Should().Be(execution);
        agent.WorkflowSteps.Should().BeEmpty();
        agent.CurrentStep.Should().BeNull();
        agent.Metrics.Should().Be(metrics);
        agent.WorkingMemory.Should().HaveCount(1);
        agent.IsValid.Should().BeTrue();
        agent.IsExecuting.Should().BeFalse();
        agent.IsCompleted.Should().BeTrue(); // No steps = completed
        agent.HasFailures.Should().BeFalse();
        agent.CompletionPercentage.Should().Be(0.0); // No steps
    }

    [Fact]
    public void Create_WithMinimalParameters_ShouldCreateValidAgent()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "MinimalAgent", AgentRole.Orchestrator);
        var configuration = new AgentConfiguration { SystemPrompt = "You are a minimal agent" };

        // Act
        var agent = ConfigurableAgent.Create(identity, configuration);

        // Assert
        agent.Identity.Should().Be(identity);
        agent.Configuration.Should().Be(configuration);
        agent.Execution.Should().NotBe(ExecutionContext.Empty);
        agent.Execution.AgentId.Should().Be(identity.Id);
        agent.WorkflowSteps.Should().BeEmpty();
        agent.Metrics.Should().Be(AgentMetrics.Empty);
        agent.WorkingMemory.Should().BeEmpty();
        agent.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WithStep_ShouldAddWorkflowStep()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var agent = ConfigurableAgent.Create(identity, configuration);
        
        var step = AgentStep.CreateTask("step-1", identity, "Test task");

        // Act
        var updatedAgent = agent.WithStep(step);

        // Assert
        updatedAgent.WorkflowSteps.Should().HaveCount(1);
        updatedAgent.WorkflowSteps[0].Should().Be(step);
        updatedAgent.Should().NotBeSameAs(agent); // Immutable
    }

    [Fact]
    public void WithSteps_ShouldAddMultipleWorkflowSteps()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var agent = ConfigurableAgent.Create(identity, configuration);
        
        var steps = new[]
        {
            AgentStep.CreateTask("step-1", identity, "First task"),
            AgentStep.CreateTask("step-2", identity, "Second task"),
            AgentStep.CreateDecision("step-3", identity, "Decision step")
        };

        // Act
        var updatedAgent = agent.WithSteps(steps);

        // Assert
        updatedAgent.WorkflowSteps.Should().HaveCount(3);
        updatedAgent.WorkflowSteps.Should().BeEquivalentTo(steps);
    }

    [Fact]
    public void NextExecutableStep_ShouldReturnFirstExecutableStepByPriority()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var agent = ConfigurableAgent.Create(identity, configuration);
        
        var step1 = AgentStep.Create("step-1", StepType.Task, identity, "Low priority", null, null, null, 1);
        var step2 = AgentStep.Create("step-2", StepType.Task, identity, "High priority", null, null, null, 10);
        var step3 = AgentStep.Create("step-3", StepType.Task, identity, "Medium priority", null, null, null, 5);
        
        var updatedAgent = agent.WithSteps(new[] { step1, step2, step3 });

        // Act
        var nextStep = updatedAgent.NextExecutableStep;

        // Assert
        nextStep.Should().NotBeNull();
        nextStep!.StepId.Should().Be("step-2"); // Highest priority
    }

    [Fact]
    public void StartStep_ShouldMarkStepAsRunningAndSetCurrentStep()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var step = AgentStep.CreateTask("step-1", identity, "Test task");
        var agent = ConfigurableAgent.Create(identity, configuration).WithStep(step);

        // Act
        var updatedAgent = agent.StartStep("step-1");

        // Assert
        updatedAgent.CurrentStep.Should().NotBeNull();
        updatedAgent.CurrentStep!.StepId.Should().Be("step-1");
        updatedAgent.CurrentStep.IsRunning.Should().BeTrue();
        updatedAgent.IsExecuting.Should().BeTrue();
        updatedAgent.WorkflowSteps[0].IsRunning.Should().BeTrue();
    }

    [Fact]
    public void CompleteStep_ShouldMarkStepAsCompletedAndUpdateMetrics()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var step = AgentStep.CreateTask("step-1", identity, "Test task");
        var agent = ConfigurableAgent.Create(identity, configuration)
                                   .WithStep(step)
                                   .StartStep("step-1");

        var outputs = new Dictionary<string, object> { ["result"] = "success" };

        // Act
        var updatedAgent = agent.CompleteStep("step-1", outputs);

        // Assert
        updatedAgent.WorkflowSteps[0].IsCompleted.Should().BeTrue();
        updatedAgent.WorkflowSteps[0].IsSuccessful.Should().BeTrue();
        updatedAgent.WorkflowSteps[0].Outputs.Should().ContainKey("result");
        updatedAgent.CurrentStep.Should().BeNull(); // Cleared after completion
        updatedAgent.Metrics.SuccessfulTasks.Should().Be(1);
        updatedAgent.CompletionPercentage.Should().Be(1.0); // 100% completed
    }

    [Fact]
    public void FailStep_ShouldMarkStepAsFailedAndUpdateMetrics()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var step = AgentStep.CreateTask("step-1", identity, "Test task");
        var agent = ConfigurableAgent.Create(identity, configuration)
                                   .WithStep(step)
                                   .StartStep("step-1");

        const string errorMessage = "Task failed due to timeout";

        // Act
        var updatedAgent = agent.FailStep("step-1", errorMessage);

        // Assert
        updatedAgent.WorkflowSteps[0].HasFailed.Should().BeTrue();
        updatedAgent.WorkflowSteps[0].Execution.ErrorMessage.Should().Be(errorMessage);
        updatedAgent.CurrentStep.Should().BeNull(); // Cleared after failure
        updatedAgent.Metrics.FailedTasks.Should().Be(1);
        updatedAgent.HasFailures.Should().BeTrue();
    }

    [Fact]
    public void GetStep_ShouldReturnCorrectStep()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var step1 = AgentStep.CreateTask("step-1", identity, "First task");
        var step2 = AgentStep.CreateTask("step-2", identity, "Second task");
        var agent = ConfigurableAgent.Create(identity, configuration)
                                   .WithSteps(new[] { step1, step2 });

        // Act
        var foundStep = agent.GetStep("step-2");

        // Assert
        foundStep.Should().NotBeNull();
        foundStep!.StepId.Should().Be("step-2");
        foundStep.Instruction.Should().Be("Second task");
    }

    [Fact]
    public void GetStepsByStatus_ShouldReturnFilteredSteps()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var step1 = AgentStep.CreateTask("step-1", identity, "Running task").MarkRunning();
        var step2 = AgentStep.CreateTask("step-2", identity, "Completed task").MarkCompleted();
        var step3 = AgentStep.CreateTask("step-3", identity, "Pending task");
        
        var agent = ConfigurableAgent.Create(identity, configuration)
                                   .WithSteps(new[] { step1, step2, step3 });

        // Act
        var runningSteps = agent.GetStepsByStatus(ExecutionStatus.Running);
        var completedSteps = agent.GetStepsByStatus(ExecutionStatus.Completed);

        // Assert
        runningSteps.Should().HaveCount(1);
        runningSteps[0].StepId.Should().Be("step-1");
        completedSteps.Should().HaveCount(1);
        completedSteps[0].StepId.Should().Be("step-2");
    }

    [Fact]
    public void GetPendingSteps_ShouldReturnExecutableSteps()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var step1 = AgentStep.CreateTask("step-1", identity, "Pending task");
        var step2 = AgentStep.CreateTask("step-2", identity, "Running task").MarkRunning();
        var step3 = AgentStep.CreateTask("step-3", identity, "Another pending task");
        
        var agent = ConfigurableAgent.Create(identity, configuration)
                                   .WithSteps(new[] { step1, step2, step3 });

        // Act
        var pendingSteps = agent.GetPendingSteps();

        // Assert
        pendingSteps.Should().HaveCount(2);
        pendingSteps.Should().Contain(s => s.StepId == "step-1");
        pendingSteps.Should().Contain(s => s.StepId == "step-3");
    }

    [Fact]
    public void WithMemory_ShouldAddWorkingMemoryEntry()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var agent = ConfigurableAgent.Create(identity, configuration);

        // Act
        var updatedAgent = agent.WithMemory("key1", "value1")
                               .WithMemory("key2", 42);

        // Assert
        updatedAgent.WorkingMemory.Should().HaveCount(2);
        updatedAgent.WorkingMemory["key1"].Should().Be("value1");
        updatedAgent.WorkingMemory["key2"].Should().Be(42);
    }

    [Fact]
    public void CompletionPercentage_ShouldCalculateCorrectly()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var step1 = AgentStep.CreateTask("step-1", identity, "Task 1").MarkCompleted();
        var step2 = AgentStep.CreateTask("step-2", identity, "Task 2").MarkCompleted();
        var step3 = AgentStep.CreateTask("step-3", identity, "Task 3"); // Pending
        var step4 = AgentStep.CreateTask("step-4", identity, "Task 4"); // Pending
        
        var agent = ConfigurableAgent.Create(identity, configuration)
                                   .WithSteps(new[] { step1, step2, step3, step4 });

        // Act
        var percentage = agent.CompletionPercentage;

        // Assert
        percentage.Should().Be(0.5); // 2 out of 4 completed = 50%
    }

    [Fact]
    public void WithIdentity_ShouldUpdateIdentityAndExecution()
    {
        // Arrange
        var originalIdentity = AgentIdentity.Create(AgentId.NewId(), "OriginalAgent", AgentRole.Specialized);
        var newIdentity = AgentIdentity.Create(AgentId.NewId(), "NewAgent", AgentRole.Orchestrator);
        var configuration = new AgentConfiguration();
        var agent = ConfigurableAgent.Create(originalIdentity, configuration);

        // Act
        var updatedAgent = agent.WithIdentity(newIdentity);

        // Assert
        updatedAgent.Identity.Should().Be(newIdentity);
        updatedAgent.Execution.AgentId.Should().Be(newIdentity.Id);
        updatedAgent.Should().NotBeSameAs(agent); // Immutable
    }

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration { AgentName = "Test" };
        var step = AgentStep.CreateTask("step-1", identity, "Test task");
        var metrics = AgentMetrics.Create(1, 1, 0, TimeSpan.FromMinutes(5));
        var memory = new Dictionary<string, object> { ["key"] = "value" };
        var createdAt = DateTime.UtcNow;

        var agent1 = ConfigurableAgent.Create(identity, configuration, null, new[] { step }, null, metrics, memory, createdAt, createdAt);
        var agent2 = ConfigurableAgent.Create(identity, configuration, null, new[] { step }, null, metrics, memory, createdAt, createdAt);

        // Act & Assert
        agent1.Should().Be(agent2);
        agent1.Equals(agent2).Should().BeTrue();
        (agent1 == agent2).Should().BeTrue();
        (agent1 != agent2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentIdentity_ShouldReturnFalse()
    {
        // Arrange
        var identity1 = AgentIdentity.Create(AgentId.NewId(), "Agent1", AgentRole.Specialized);
        var identity2 = AgentIdentity.Create(AgentId.NewId(), "Agent2", AgentRole.Specialized);
        var configuration = new AgentConfiguration();

        var agent1 = ConfigurableAgent.Create(identity1, configuration);
        var agent2 = ConfigurableAgent.Create(identity2, configuration);

        // Act & Assert
        agent1.Should().NotBe(agent2);
    }

    [Fact]
    public void GetHashCode_WithSameValues_ShouldReturnSameHashCode()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration { AgentName = "Test" };
        var step = AgentStep.CreateTask("step-1", identity, "Test task");
        var createdAt = DateTime.UtcNow;

        var agent1 = ConfigurableAgent.Create(identity, configuration, null, new[] { step }, null, null, null, createdAt, createdAt);
        var agent2 = ConfigurableAgent.Create(identity, configuration, null, new[] { step }, null, null, null, createdAt, createdAt);

        // Act & Assert
        agent1.GetHashCode().Should().Be(agent2.GetHashCode());
    }

    [Fact]
    public void ToString_WithValidAgent_ShouldReturnFormattedString()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.Parse("agent-123"), "TestAgent", AgentRole.Orchestrator);
        var configuration = new AgentConfiguration();
        var step1 = AgentStep.CreateTask("step-1", identity, "Task 1").MarkCompleted();
        var step2 = AgentStep.CreateTask("step-2", identity, "Task 2").MarkRunning();
        
        var agent = ConfigurableAgent.Create(identity, configuration)
                                   .WithSteps(new[] { step1, step2 })
                                   .WithCurrentStep(step2);

        // Act
        var result = agent.ToString();

        // Assert
        result.Should().Contain("agent-123");
        result.Should().Contain("TestAgent");
        result.Should().Contain("Orchestrator");
        result.Should().Contain("Steps: 2");
        result.Should().Contain("1 completed");
        result.Should().Contain("Current: step-2");
    }

    [Fact]
    public void Empty_ShouldReturnInvalidAgent()
    {
        // Act
        var emptyAgent = ConfigurableAgent.Empty;

        // Assert
        emptyAgent.IsValid.Should().BeFalse();
        emptyAgent.Identity.Should().Be(AgentIdentity.Empty);
        emptyAgent.Configuration.Should().NotBeNull();
        emptyAgent.Execution.Should().Be(ExecutionContext.Empty);
        emptyAgent.WorkflowSteps.Should().BeEmpty();
        emptyAgent.CurrentStep.Should().BeNull();
        emptyAgent.Metrics.Should().Be(AgentMetrics.Empty);
        emptyAgent.WorkingMemory.Should().BeEmpty();
    }

    [Fact]
    public void JsonSerialization_WithValidAgent_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration
        {
            SystemPrompt = "Test prompt",
            AgentName = "TestAgent",
            Temperature = 0.7,
            MaxTokens = 2000
        };
        var step = AgentStep.CreateTask("step-1", identity, "Test task");
        var metrics = AgentMetrics.Create(3, 2, 1, TimeSpan.FromMinutes(15));
        var memory = new Dictionary<string, object>
        {
            ["stringValue"] = "test",
            ["intValue"] = 42,
            ["boolValue"] = true
        };
        var createdAt = new DateTime(2023, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        var originalAgent = ConfigurableAgent.Create(
            identity,
            configuration,
            null,
            new[] { step },
            step,
            metrics,
            memory,
            createdAt,
            createdAt);

        var options = new JsonSerializerOptions
        {
            Converters = 
            { 
                new ConfigurableAgentJsonConverter(),
                new AgentIdentityJsonConverter(),
                new AgentStepJsonConverter(),
                new ExecutionContextJsonConverter(),
                new AgentMetricsJsonConverter(),
                new AgentIdJsonConverter()
            }
        };

        // Act
        var json = JsonSerializer.Serialize(originalAgent, options);
        var deserializedAgent = JsonSerializer.Deserialize<ConfigurableAgent>(json, options);

        // Assert
        deserializedAgent.Should().NotBeNull();
        deserializedAgent!.Identity.Should().Be(originalAgent.Identity);
        deserializedAgent.Configuration.AgentName.Should().Be(originalAgent.Configuration.AgentName);
        deserializedAgent.WorkflowSteps.Should().HaveCount(1);
        deserializedAgent.CurrentStep.Should().NotBeNull();
        deserializedAgent.Metrics.Should().Be(originalAgent.Metrics);
        deserializedAgent.WorkingMemory.Should().HaveCount(3);
        deserializedAgent.CreatedAt.Should().Be(originalAgent.CreatedAt);
    }

    [Fact]
    public void JsonSerialization_WithInvalidAgent_ShouldSerializeAsNull()
    {
        // Arrange
        var invalidAgent = ConfigurableAgent.Empty;
        var options = new JsonSerializerOptions
        {
            Converters = { new ConfigurableAgentJsonConverter() }
        };

        // Act
        var json = JsonSerializer.Serialize(invalidAgent, options);

        // Assert
        json.Should().Be("null");
    }

    [Fact]
    public void StartStep_WithNonExistentStep_ShouldReturnUnchangedAgent()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var agent = ConfigurableAgent.Create(identity, configuration);

        // Act
        var updatedAgent = agent.StartStep("non-existent-step");

        // Assert
        updatedAgent.Should().BeSameAs(agent); // No change
    }

    [Fact]
    public void CompleteStep_WithNonExistentStep_ShouldReturnUnchangedAgent()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var agent = ConfigurableAgent.Create(identity, configuration);

        // Act
        var updatedAgent = agent.CompleteStep("non-existent-step");

        // Assert
        updatedAgent.Should().BeSameAs(agent); // No change
    }

    [Fact]
    public void FailStep_WithNonExistentStep_ShouldReturnUnchangedAgent()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var agent = ConfigurableAgent.Create(identity, configuration);

        // Act
        var updatedAgent = agent.FailStep("non-existent-step", "Error");

        // Assert
        updatedAgent.Should().BeSameAs(agent); // No change
    }

    [Fact]
    public void GetStep_WithNonExistentStep_ShouldReturnNull()
    {
        // Arrange
        var identity = AgentIdentity.Create(AgentId.NewId(), "TestAgent", AgentRole.Specialized);
        var configuration = new AgentConfiguration();
        var agent = ConfigurableAgent.Create(identity, configuration);

        // Act
        var step = agent.GetStep("non-existent-step");

        // Assert
        step.Should().BeNull();
    }
} 
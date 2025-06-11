using FluentAssertions;
using PsiOrleans.Common.Models;
using Xunit;

namespace PsiOrleans.Common.Tests.Models;

/// <summary>
/// Tests for AgentMetrics value object - TDD approach
/// </summary>
public class AgentMetricsTests
{
    [Fact]
    public void AgentMetrics_ShouldHaveDefaultConstructor()
    {
        // Act
        var metrics = new AgentMetrics();
        
        // Assert
        metrics.TotalTasks.Should().Be(0);
        metrics.SuccessfulTasks.Should().Be(0);
        metrics.FailedTasks.Should().Be(0);
        metrics.TotalExecutionTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void AgentMetrics_ShouldSetPropertiesViaConstructor()
    {
        // Arrange
        var totalTasks = 10;
        var successfulTasks = 8;
        var failedTasks = 2;
        var totalExecutionTime = TimeSpan.FromMinutes(5);

        // Act
        var metrics = new AgentMetrics(totalTasks, successfulTasks, failedTasks, totalExecutionTime);

        // Assert
        metrics.TotalTasks.Should().Be(totalTasks);
        metrics.SuccessfulTasks.Should().Be(successfulTasks);
        metrics.FailedTasks.Should().Be(failedTasks);
        metrics.TotalExecutionTime.Should().Be(totalExecutionTime);
    }

    [Fact]
    public void AgentMetrics_IncrementSuccessful_ShouldReturnNewInstance()
    {
        // Arrange
        var originalMetrics = new AgentMetrics(5, 3, 2, TimeSpan.FromMinutes(2));

        // Act
        var newMetrics = originalMetrics.IncrementSuccessful();

        // Assert
        newMetrics.Should().NotBeSameAs(originalMetrics); // Different instances
        newMetrics.TotalTasks.Should().Be(6);
        newMetrics.SuccessfulTasks.Should().Be(4);
        newMetrics.FailedTasks.Should().Be(2);
        newMetrics.TotalExecutionTime.Should().Be(TimeSpan.FromMinutes(2));
        
        // Original should be unchanged
        originalMetrics.TotalTasks.Should().Be(5);
        originalMetrics.SuccessfulTasks.Should().Be(3);
    }

    [Fact]
    public void AgentMetrics_IncrementFailed_ShouldReturnNewInstance()
    {
        // Arrange
        var originalMetrics = new AgentMetrics(5, 3, 2, TimeSpan.FromMinutes(2));

        // Act
        var newMetrics = originalMetrics.IncrementFailed();

        // Assert
        newMetrics.Should().NotBeSameAs(originalMetrics); // Different instances
        newMetrics.TotalTasks.Should().Be(6);
        newMetrics.SuccessfulTasks.Should().Be(3);
        newMetrics.FailedTasks.Should().Be(3);
        newMetrics.TotalExecutionTime.Should().Be(TimeSpan.FromMinutes(2));
        
        // Original should be unchanged
        originalMetrics.TotalTasks.Should().Be(5);
        originalMetrics.FailedTasks.Should().Be(2);
    }

    [Fact]
    public void AgentMetrics_AddExecutionTime_ShouldReturnNewInstance()
    {
        // Arrange
        var originalMetrics = new AgentMetrics(5, 3, 2, TimeSpan.FromMinutes(2));
        var additionalTime = TimeSpan.FromMinutes(3);

        // Act
        var newMetrics = originalMetrics.AddExecutionTime(additionalTime);

        // Assert
        newMetrics.Should().NotBeSameAs(originalMetrics); // Different instances
        newMetrics.TotalTasks.Should().Be(5);
        newMetrics.SuccessfulTasks.Should().Be(3);
        newMetrics.FailedTasks.Should().Be(2);
        newMetrics.TotalExecutionTime.Should().Be(TimeSpan.FromMinutes(5));
        
        // Original should be unchanged
        originalMetrics.TotalExecutionTime.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void AgentMetrics_SuccessRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var metrics = new AgentMetrics(10, 8, 2, TimeSpan.FromMinutes(5));

        // Act
        var successRate = metrics.SuccessRate;

        // Assert
        successRate.Should().Be(0.8); // 8/10 = 0.8
    }

    [Fact]
    public void AgentMetrics_SuccessRate_ShouldReturn0ForZeroTotalTasks()
    {
        // Arrange
        var metrics = new AgentMetrics(0, 0, 0, TimeSpan.Zero);

        // Act
        var successRate = metrics.SuccessRate;

        // Assert
        successRate.Should().Be(0.0);
    }

    [Fact]
    public void AgentMetrics_AverageExecutionTime_ShouldCalculateCorrectly()
    {
        // Arrange
        var metrics = new AgentMetrics(5, 3, 2, TimeSpan.FromMinutes(10));

        // Act
        var averageTime = metrics.AverageExecutionTime;

        // Assert
        averageTime.Should().Be(TimeSpan.FromMinutes(2)); // 10 minutes / 5 tasks = 2 minutes per task
    }

    [Fact]
    public void AgentMetrics_AverageExecutionTime_ShouldReturnZeroForZeroTotalTasks()
    {
        // Arrange
        var metrics = new AgentMetrics(0, 0, 0, TimeSpan.Zero);

        // Act
        var averageTime = metrics.AverageExecutionTime;

        // Assert
        averageTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void AgentMetrics_ShouldImplementValueEquality()
    {
        // Arrange
        var metrics1 = new AgentMetrics(5, 3, 2, TimeSpan.FromMinutes(10));
        var metrics2 = new AgentMetrics(5, 3, 2, TimeSpan.FromMinutes(10));
        var metrics3 = new AgentMetrics(6, 3, 2, TimeSpan.FromMinutes(10));

        // Act & Assert
        metrics1.Should().Be(metrics2); // Equal values
        metrics1.Should().NotBe(metrics3); // Different values
        (metrics1 == metrics2).Should().BeTrue();
        (metrics1 != metrics3).Should().BeTrue();
    }

    [Fact]
    public void AgentMetrics_ShouldHaveConsistentHashCode()
    {
        // Arrange
        var metrics1 = new AgentMetrics(5, 3, 2, TimeSpan.FromMinutes(10));
        var metrics2 = new AgentMetrics(5, 3, 2, TimeSpan.FromMinutes(10));

        // Act & Assert
        metrics1.GetHashCode().Should().Be(metrics2.GetHashCode());
    }

    // TODO: Add JSON serialization test later - requires custom JsonConverter for immutable object

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(5, 6, 0)] // Successful > Total
    [InlineData(5, 0, 6)] // Failed > Total
    [InlineData(5, 3, 3)] // Successful + Failed > Total
    public void AgentMetrics_ShouldValidateInputValues(int totalTasks, int successfulTasks, int failedTasks)
    {
        // Act & Assert
        var act = () => new AgentMetrics(totalTasks, successfulTasks, failedTasks, TimeSpan.Zero);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AgentMetrics_ShouldAllowNegativeExecutionTime()
    {
        // This test documents that we allow negative execution time
        // (might be useful for time corrections or adjustments)
        
        // Act
        var act = () => new AgentMetrics(1, 1, 0, TimeSpan.FromMinutes(-1));

        // Assert
        act.Should().NotThrow();
    }
} 
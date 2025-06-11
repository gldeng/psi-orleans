using FluentAssertions;
using PsiOrleans.Common.Models;
using Xunit;

namespace PsiOrleans.Common.Tests.Models;

public class ExecutionStatusTests
{
    [Theory]
    [InlineData(ExecutionStatus.Created, 0)]
    [InlineData(ExecutionStatus.Running, 1)]
    [InlineData(ExecutionStatus.Paused, 2)]
    [InlineData(ExecutionStatus.Completed, 3)]
    [InlineData(ExecutionStatus.Failed, 4)]
    [InlineData(ExecutionStatus.Cancelled, 5)]
    public void ExecutionStatus_EnumValues_ShouldHaveCorrectIntegerValues(ExecutionStatus status, int expectedValue)
    {
        // Act & Assert
        ((int)status).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(ExecutionStatus.Completed, true)]
    [InlineData(ExecutionStatus.Failed, true)]
    [InlineData(ExecutionStatus.Cancelled, true)]
    [InlineData(ExecutionStatus.Created, false)]
    [InlineData(ExecutionStatus.Running, false)]
    [InlineData(ExecutionStatus.Paused, false)]
    public void IsFinished_ShouldReturnCorrectValue(ExecutionStatus status, bool expectedResult)
    {
        // Act
        var result = status.IsFinished();

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(ExecutionStatus.Running, true)]
    [InlineData(ExecutionStatus.Paused, true)]
    [InlineData(ExecutionStatus.Created, false)]
    [InlineData(ExecutionStatus.Completed, false)]
    [InlineData(ExecutionStatus.Failed, false)]
    [InlineData(ExecutionStatus.Cancelled, false)]
    public void IsActive_ShouldReturnCorrectValue(ExecutionStatus status, bool expectedResult)
    {
        // Act
        var result = status.IsActive();

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(ExecutionStatus.Paused, true)]
    [InlineData(ExecutionStatus.Created, false)]
    [InlineData(ExecutionStatus.Running, false)]
    [InlineData(ExecutionStatus.Completed, false)]
    [InlineData(ExecutionStatus.Failed, false)]
    [InlineData(ExecutionStatus.Cancelled, false)]
    public void CanBeResumed_ShouldReturnCorrectValue(ExecutionStatus status, bool expectedResult)
    {
        // Act
        var result = status.CanBeResumed();

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(ExecutionStatus.Completed, true)]
    [InlineData(ExecutionStatus.Created, false)]
    [InlineData(ExecutionStatus.Running, false)]
    [InlineData(ExecutionStatus.Paused, false)]
    [InlineData(ExecutionStatus.Failed, false)]
    [InlineData(ExecutionStatus.Cancelled, false)]
    public void IsSuccessful_ShouldReturnCorrectValue(ExecutionStatus status, bool expectedResult)
    {
        // Act
        var result = status.IsSuccessful();

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void AllEnumValues_ShouldHaveUniqueIntegerValues()
    {
        // Arrange
        var enumValues = Enum.GetValues<ExecutionStatus>().Cast<int>().ToList();

        // Act & Assert
        enumValues.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AllEnumValues_ShouldBeContiguousFromZero()
    {
        // Arrange
        var enumValues = Enum.GetValues<ExecutionStatus>().Cast<int>().OrderBy(x => x).ToList();
        var expectedValues = Enumerable.Range(0, enumValues.Count).ToList();

        // Act & Assert
        enumValues.Should().BeEquivalentTo(expectedValues);
    }
} 
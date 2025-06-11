using FluentAssertions;
using PsiOrleans.Common.Models;
using Xunit;

namespace PsiOrleans.Common.Tests.Models;

public class StepTypeTests
{
    [Theory]
    [InlineData(StepType.Task, 0)]
    [InlineData(StepType.Decision, 1)]
    [InlineData(StepType.Parallel, 2)]
    [InlineData(StepType.Sequential, 3)]
    [InlineData(StepType.Conditional, 4)]
    [InlineData(StepType.Aggregation, 5)]
    public void StepType_EnumValues_ShouldHaveCorrectIntegerValues(StepType stepType, int expectedValue)
    {
        // Act & Assert
        ((int)stepType).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(StepType.Parallel, true)]
    [InlineData(StepType.Task, true)]
    [InlineData(StepType.Conditional, true)]
    [InlineData(StepType.Decision, false)]
    [InlineData(StepType.Sequential, false)]
    [InlineData(StepType.Aggregation, false)]
    public void CanExecuteInParallel_ShouldReturnCorrectValue(StepType stepType, bool expectedResult)
    {
        // Act
        var result = stepType.CanExecuteInParallel();

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(StepType.Sequential, true)]
    [InlineData(StepType.Decision, true)]
    [InlineData(StepType.Task, false)]
    [InlineData(StepType.Parallel, false)]
    [InlineData(StepType.Conditional, false)]
    [InlineData(StepType.Aggregation, false)]
    public void RequiresSequentialExecution_ShouldReturnCorrectValue(StepType stepType, bool expectedResult)
    {
        // Act
        var result = stepType.RequiresSequentialExecution();

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(StepType.Decision, true)]
    [InlineData(StepType.Parallel, true)]
    [InlineData(StepType.Aggregation, true)]
    [InlineData(StepType.Task, false)]
    [InlineData(StepType.Sequential, false)]
    [InlineData(StepType.Conditional, false)]
    public void CanHaveMultipleOutputs_ShouldReturnCorrectValue(StepType stepType, bool expectedResult)
    {
        // Act
        var result = stepType.CanHaveMultipleOutputs();

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(StepType.Decision, true)]
    [InlineData(StepType.Conditional, true)]
    [InlineData(StepType.Aggregation, true)]
    [InlineData(StepType.Task, false)]
    [InlineData(StepType.Parallel, false)]
    [InlineData(StepType.Sequential, false)]
    public void RequiresInputValidation_ShouldReturnCorrectValue(StepType stepType, bool expectedResult)
    {
        // Act
        var result = stepType.RequiresInputValidation();

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(StepType.Task, true)]
    [InlineData(StepType.Sequential, true)]
    [InlineData(StepType.Parallel, true)]
    [InlineData(StepType.Decision, false)]
    [InlineData(StepType.Conditional, false)]
    [InlineData(StepType.Aggregation, false)]
    public void CanBeEntryPoint_ShouldReturnCorrectValue(StepType stepType, bool expectedResult)
    {
        // Act
        var result = stepType.CanBeEntryPoint();

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void AllEnumValues_ShouldHaveUniqueIntegerValues()
    {
        // Arrange
        var enumValues = Enum.GetValues<StepType>().Cast<int>().ToList();

        // Act & Assert
        enumValues.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AllEnumValues_ShouldBeContiguousFromZero()
    {
        // Arrange
        var enumValues = Enum.GetValues<StepType>().Cast<int>().OrderBy(x => x).ToList();
        var expectedValues = Enumerable.Range(0, enumValues.Count).ToList();

        // Act & Assert
        enumValues.Should().BeEquivalentTo(expectedValues);
    }
} 
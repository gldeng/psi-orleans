using FluentAssertions;
using PsiOrleans.Common.Models;
using Xunit;

namespace PsiOrleans.Common.Tests.Models;

/// <summary>
/// Tests for AgentRole enum - TDD approach
/// </summary>
public class AgentRoleTests
{
    [Fact]
    public void AgentRole_ShouldHaveUndecidedAsDefault()
    {
        // Arrange & Act
        var defaultRole = default(AgentRole);
        
        // Assert
        defaultRole.Should().Be(AgentRole.Undecided);
    }

    [Fact]
    public void AgentRole_ShouldHaveAllExpectedValues()
    {
        // Arrange
        var expectedValues = new[] { AgentRole.Undecided, AgentRole.Orchestrator, AgentRole.Specialized };
        
        // Act
        var actualValues = Enum.GetValues<AgentRole>();
        
        // Assert
        actualValues.Should().BeEquivalentTo(expectedValues);
    }

    [Theory]
    [InlineData(AgentRole.Undecided, "Undecided")]
    [InlineData(AgentRole.Orchestrator, "Orchestrator")]
    [InlineData(AgentRole.Specialized, "Specialized")]
    public void AgentRole_ShouldHaveCorrectStringRepresentation(AgentRole role, string expectedString)
    {
        // Act
        var roleString = role.ToString();
        
        // Assert  
        roleString.Should().Be(expectedString);
    }

    [Fact]
    public void AgentRole_ShouldBeSerializableToJson()
    {
        // Arrange
        var role = AgentRole.Orchestrator;
        
        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(role);
        var deserializedRole = System.Text.Json.JsonSerializer.Deserialize<AgentRole>(json);
        
        // Assert
        deserializedRole.Should().Be(role);
    }

    [Theory]
    [InlineData(AgentRole.Undecided, false)]
    [InlineData(AgentRole.Orchestrator, true)]
    [InlineData(AgentRole.Specialized, true)]
    public void AgentRole_IsDecided_ShouldReturnCorrectValue(AgentRole role, bool expectedIsDecided)
    {
        // Act
        var isDecided = role.IsDecided();
        
        // Assert
        isDecided.Should().Be(expectedIsDecided);
    }

    [Theory]
    [InlineData(AgentRole.Orchestrator, true)]
    [InlineData(AgentRole.Specialized, false)]
    [InlineData(AgentRole.Undecided, false)]
    public void AgentRole_CanCreateChildAgents_ShouldReturnCorrectValue(AgentRole role, bool expectedCanCreate)
    {
        // Act
        var canCreate = role.CanCreateChildAgents();
        
        // Assert
        canCreate.Should().Be(expectedCanCreate);
    }

    [Theory]
    [InlineData(AgentRole.Orchestrator, false)]
    [InlineData(AgentRole.Specialized, true)]
    [InlineData(AgentRole.Undecided, false)]
    public void AgentRole_CanUseDirectTools_ShouldReturnCorrectValue(AgentRole role, bool expectedCanUse)
    {
        // Act
        var canUse = role.CanUseDirectTools();
        
        // Assert
        canUse.Should().Be(expectedCanUse);
    }
} 
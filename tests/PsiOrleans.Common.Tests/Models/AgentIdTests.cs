using FluentAssertions;
using PsiOrleans.Common.Models;
using System.Text.Json;
using Xunit;

namespace PsiOrleans.Common.Tests.Models;

public class AgentIdTests
{
    [Fact]
    public void NewId_ShouldCreateValidAgentId()
    {
        // Act
        var agentId = AgentId.NewId();
        
        // Assert
        agentId.IsValid.Should().BeTrue();
        agentId.Value.Should().NotBeNullOrWhiteSpace();
        agentId.Value.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void NewId_ShouldCreateUniqueIds()
    {
        // Act
        var id1 = AgentId.NewId();
        var id2 = AgentId.NewId();
        
        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(id2.Value);
    }

    [Theory]
    [InlineData("agent-123")]
    [InlineData("test_agent_456")]
    [InlineData("AGENT-789")]
    [InlineData("a")]
    [InlineData("12345")]
    public void Parse_WithValidString_ShouldCreateValidAgentId(string validId)
    {
        // Act
        var agentId = AgentId.Parse(validId);
        
        // Assert
        agentId.IsValid.Should().BeTrue();
        agentId.Value.Should().Be(validId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Parse_WithWhitespaceString_ShouldCreateInvalidAgentId(string invalidId)
    {
        // Act
        var agentId = AgentId.Parse(invalidId);
        
        // Assert
        agentId.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Parse_WithNullString_ShouldCreateInvalidAgentId()
    {
        // Act
        var agentId = AgentId.Parse(null);
        
        // Assert
        agentId.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TryParse_WithValidString_ShouldReturnTrueAndValidAgentId()
    {
        // Arrange
        const string validId = "test-agent";
        
        // Act
        var success = AgentId.TryParse(validId, out var agentId);
        
        // Assert
        success.Should().BeTrue();
        agentId.IsValid.Should().BeTrue();
        agentId.Value.Should().Be(validId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void TryParse_WithInvalidString_ShouldReturnFalseAndInvalidAgentId(string? invalidId)
    {
        // Act
        var success = AgentId.TryParse(invalidId, out var agentId);
        
        // Assert
        success.Should().BeFalse();
        agentId.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Equals_WithSameValue_ShouldReturnTrue()
    {
        // Arrange
        var id1 = AgentId.Parse("test-agent");
        var id2 = AgentId.Parse("test-agent");
        
        // Act & Assert
        id1.Should().Be(id2);
        id1.Equals(id2).Should().BeTrue();
        (id1 == id2).Should().BeTrue();
        (id1 != id2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentValue_ShouldReturnFalse()
    {
        // Arrange
        var id1 = AgentId.Parse("agent-1");
        var id2 = AgentId.Parse("agent-2");
        
        // Act & Assert
        id1.Should().NotBe(id2);
        id1.Equals(id2).Should().BeFalse();
        (id1 == id2).Should().BeFalse();
        (id1 != id2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithSameValue_ShouldReturnSameHashCode()
    {
        // Arrange
        var id1 = AgentId.Parse("test-agent");
        var id2 = AgentId.Parse("test-agent");
        
        // Act & Assert
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithDifferentValue_ShouldReturnDifferentHashCode()
    {
        // Arrange
        var id1 = AgentId.Parse("agent-1");
        var id2 = AgentId.Parse("agent-2");
        
        // Act & Assert
        id1.GetHashCode().Should().NotBe(id2.GetHashCode());
    }

    [Fact]
    public void ToString_WithValidId_ShouldReturnValue()
    {
        // Arrange
        const string expectedValue = "test-agent";
        var agentId = AgentId.Parse(expectedValue);
        
        // Act
        var result = agentId.ToString();
        
        // Assert
        result.Should().Be(expectedValue);
    }

    [Fact]
    public void ToString_WithInvalidId_ShouldReturnEmptyString()
    {
        // Arrange
        var agentId = AgentId.Parse("");
        
        // Act
        var result = agentId.ToString();
        
        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void ImplicitConversion_FromString_ShouldCreateAgentId()
    {
        // Arrange
        const string value = "implicit-agent";
        
        // Act
        AgentId agentId = value;
        
        // Assert
        agentId.Value.Should().Be(value);
        agentId.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_ToString_ShouldReturnValue()
    {
        // Arrange
        var agentId = AgentId.Parse("test-agent");
        
        // Act
        string result = agentId;
        
        // Assert
        result.Should().Be("test-agent");
    }

    [Fact]
    public void Empty_ShouldReturnInvalidAgentId()
    {
        // Act
        var emptyId = AgentId.Empty;
        
        // Assert
        emptyId.IsValid.Should().BeFalse();
        emptyId.Value.Should().Be(string.Empty);
    }

    [Fact]
    public void JsonSerialization_WithValidId_ShouldSerializeAndDeserializeCorrectly()
    {
        // Arrange
        var originalId = AgentId.Parse("test-agent-123");
        
        // Act
        var json = JsonSerializer.Serialize(originalId);
        var deserializedId = JsonSerializer.Deserialize<AgentId>(json);
        
        // Assert
        json.Should().Be("\"test-agent-123\"");
        deserializedId.Should().Be(originalId);
        deserializedId.IsValid.Should().BeTrue();
        deserializedId.Value.Should().Be("test-agent-123");
    }

    [Fact]
    public void JsonSerialization_WithInvalidId_ShouldSerializeAsNull()
    {
        // Arrange
        var invalidId = AgentId.Empty;
        
        // Act
        var json = JsonSerializer.Serialize(invalidId);
        var deserializedId = JsonSerializer.Deserialize<AgentId>(json);
        
        // Assert
        json.Should().Be("null");
        deserializedId.IsValid.Should().BeFalse();
        deserializedId.Should().Be(AgentId.Empty);
    }
} 
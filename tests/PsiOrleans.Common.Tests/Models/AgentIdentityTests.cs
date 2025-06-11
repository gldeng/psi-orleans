using FluentAssertions;
using PsiOrleans.Common.Models;
using Xunit;

namespace PsiOrleans.Common.Tests.Models;

public class AgentIdentityTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateValidAgentIdentity()
    {
        // Arrange
        var agentId = AgentId.NewId();
        const string name = "TestAgent";
        const AgentRole role = AgentRole.Specialized;
        var createdAt = DateTime.UtcNow;

        // Act
        var identity = AgentIdentity.Create(agentId, name, role, createdAt);

        // Assert
        identity.Id.Should().Be(agentId);
        identity.Name.Should().Be(name);
        identity.Role.Should().Be(role);
        identity.CreatedAt.Should().Be(createdAt);
        identity.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_WithDefaultCreatedAt_ShouldUseCurrentTime()
    {
        // Arrange
        var agentId = AgentId.NewId();
        const string name = "TestAgent";
        const AgentRole role = AgentRole.Orchestrator;
        var beforeCreate = DateTime.UtcNow;

        // Act
        var identity = AgentIdentity.Create(agentId, name, role);
        var afterCreate = DateTime.UtcNow;

        // Assert
        identity.CreatedAt.Should().BeOnOrAfter(beforeCreate);
        identity.CreatedAt.Should().BeOnOrBefore(afterCreate);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Create_WithInvalidName_ShouldCreateInvalidIdentity(string invalidName)
    {
        // Arrange
        var agentId = AgentId.NewId();

        // Act
        var identity = AgentIdentity.Create(agentId, invalidName, AgentRole.Specialized);

        // Assert
        identity.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_WithInvalidAgentId_ShouldCreateInvalidIdentity()
    {
        // Arrange
        var invalidAgentId = AgentId.Empty;

        // Act
        var identity = AgentIdentity.Create(invalidAgentId, "TestAgent", AgentRole.Specialized);

        // Assert
        identity.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateNew_WithValidParameters_ShouldGenerateNewAgentId()
    {
        // Arrange
        const string name = "NewAgent";
        const AgentRole role = AgentRole.Orchestrator;

        // Act
        var identity = AgentIdentity.CreateNew(name, role);

        // Assert
        identity.Id.IsValid.Should().BeTrue();
        identity.Name.Should().Be(name);
        identity.Role.Should().Be(role);
        identity.IsValid.Should().BeTrue();
        identity.Id.Value.Should().StartWith("agent-");
    }

    [Fact]
    public void CreateNew_ShouldGenerateUniqueIdentities()
    {
        // Act
        var identity1 = AgentIdentity.CreateNew("Agent1", AgentRole.Specialized);
        var identity2 = AgentIdentity.CreateNew("Agent2", AgentRole.Orchestrator);

        // Assert
        identity1.Id.Should().NotBe(identity2.Id);
        identity1.Should().NotBe(identity2);
    }

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        var agentId = AgentId.NewId();
        const string name = "TestAgent";
        const AgentRole role = AgentRole.Specialized;
        var createdAt = DateTime.UtcNow;

        var identity1 = AgentIdentity.Create(agentId, name, role, createdAt);
        var identity2 = AgentIdentity.Create(agentId, name, role, createdAt);

        // Act & Assert
        identity1.Should().Be(identity2);
        identity1.Equals(identity2).Should().BeTrue();
        (identity1 == identity2).Should().BeTrue();
        (identity1 != identity2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentAgentId_ShouldReturnFalse()
    {
        // Arrange
        var identity1 = AgentIdentity.CreateNew("TestAgent", AgentRole.Specialized);
        var identity2 = AgentIdentity.CreateNew("TestAgent", AgentRole.Specialized);

        // Act & Assert
        identity1.Should().NotBe(identity2);
        identity1.Equals(identity2).Should().BeFalse();
        (identity1 == identity2).Should().BeFalse();
        (identity1 != identity2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentName_ShouldReturnFalse()
    {
        // Arrange
        var agentId = AgentId.NewId();
        var createdAt = DateTime.UtcNow;

        var identity1 = AgentIdentity.Create(agentId, "Agent1", AgentRole.Specialized, createdAt);
        var identity2 = AgentIdentity.Create(agentId, "Agent2", AgentRole.Specialized, createdAt);

        // Act & Assert
        identity1.Should().NotBe(identity2);
    }

    [Fact]
    public void GetHashCode_WithSameValues_ShouldReturnSameHashCode()
    {
        // Arrange
        var agentId = AgentId.NewId();
        const string name = "TestAgent";
        const AgentRole role = AgentRole.Specialized;
        var createdAt = DateTime.UtcNow;

        var identity1 = AgentIdentity.Create(agentId, name, role, createdAt);
        var identity2 = AgentIdentity.Create(agentId, name, role, createdAt);

        // Act & Assert
        identity1.GetHashCode().Should().Be(identity2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithDifferentValues_ShouldReturnDifferentHashCode()
    {
        // Arrange
        var identity1 = AgentIdentity.CreateNew("Agent1", AgentRole.Specialized);
        var identity2 = AgentIdentity.CreateNew("Agent2", AgentRole.Orchestrator);

        // Act & Assert
        identity1.GetHashCode().Should().NotBe(identity2.GetHashCode());
    }

    [Fact]
    public void ToString_WithValidIdentity_ShouldReturnFormattedString()
    {
        // Arrange
        var agentId = AgentId.Parse("test-agent-123");
        const string name = "TestAgent";
        const AgentRole role = AgentRole.Specialized;

        var identity = AgentIdentity.Create(agentId, name, role);

        // Act
        var result = identity.ToString();

        // Assert
        result.Should().Contain("TestAgent");
        result.Should().Contain("test-agent-123");
        result.Should().Contain("Specialized");
    }

    [Fact]
    public void WithName_ShouldCreateNewIdentityWithUpdatedName()
    {
        // Arrange
        var originalIdentity = AgentIdentity.CreateNew("OriginalName", AgentRole.Specialized);
        const string newName = "UpdatedName";

        // Act
        var updatedIdentity = originalIdentity.WithName(newName);

        // Assert
        updatedIdentity.Id.Should().Be(originalIdentity.Id);
        updatedIdentity.Name.Should().Be(newName);
        updatedIdentity.Role.Should().Be(originalIdentity.Role);
        updatedIdentity.CreatedAt.Should().Be(originalIdentity.CreatedAt);
        
        // Original should be unchanged
        originalIdentity.Name.Should().Be("OriginalName");
    }

    [Fact]
    public void WithRole_ShouldCreateNewIdentityWithUpdatedRole()
    {
        // Arrange
        var originalIdentity = AgentIdentity.CreateNew("TestAgent", AgentRole.Specialized);
        const AgentRole newRole = AgentRole.Orchestrator;

        // Act
        var updatedIdentity = originalIdentity.WithRole(newRole);

        // Assert
        updatedIdentity.Id.Should().Be(originalIdentity.Id);
        updatedIdentity.Name.Should().Be(originalIdentity.Name);
        updatedIdentity.Role.Should().Be(newRole);
        updatedIdentity.CreatedAt.Should().Be(originalIdentity.CreatedAt);
        
        // Original should be unchanged
        originalIdentity.Role.Should().Be(AgentRole.Specialized);
    }

    [Fact]
    public void Empty_ShouldReturnInvalidAgentIdentity()
    {
        // Act
        var emptyIdentity = AgentIdentity.Empty;

        // Assert
        emptyIdentity.IsValid.Should().BeFalse();
        emptyIdentity.Id.Should().Be(AgentId.Empty);
        emptyIdentity.Name.Should().Be(string.Empty);
        emptyIdentity.Role.Should().Be(AgentRole.Undecided);
    }
} 
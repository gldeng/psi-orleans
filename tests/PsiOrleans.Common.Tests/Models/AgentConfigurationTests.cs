using FluentAssertions;
using PsiOrleans.Common.Models;
using Xunit;
using System.Text.Json;

namespace PsiOrleans.Common.Tests.Models;

/// <summary>
/// Tests for AgentConfiguration model - TDD approach
/// </summary>
public class AgentConfigurationTests
{
    [Fact]
    public void AgentConfiguration_ShouldHaveDefaultValues()
    {
        // Act
        var config = new AgentConfiguration();
        
        // Assert
        config.SystemPrompt.Should().Be(string.Empty);
        config.AgentName.Should().Be("ConfigurableAgent");
        config.Temperature.Should().Be(0.1);
        config.MaxTokens.Should().Be(4000);
        config.Model.Should().NotBeNull();
    }

    [Fact]
    public void AgentConfiguration_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var systemPrompt = "You are a helpful assistant";
        var agentName = "TestAgent";
        var temperature = 0.7;
        var maxTokens = 2000;
        var model = new ModelConfiguration { ModelId = "gpt-4" };

        // Act
        var config = new AgentConfiguration
        {
            SystemPrompt = systemPrompt,
            AgentName = agentName,
            Temperature = temperature,
            MaxTokens = maxTokens,
            Model = model
        };

        // Assert
        config.SystemPrompt.Should().Be(systemPrompt);
        config.AgentName.Should().Be(agentName);
        config.Temperature.Should().Be(temperature);
        config.MaxTokens.Should().Be(maxTokens);
        config.Model.Should().Be(model);
    }

    [Fact]
    public void AgentConfiguration_ShouldBeSerializableToJson()
    {
        // Arrange
        var config = new AgentConfiguration
        {
            SystemPrompt = "Test prompt",
            AgentName = "TestAgent",
            Temperature = 0.5,
            MaxTokens = 3000,
            Model = new ModelConfiguration { ModelId = "gpt-3.5-turbo" }
        };

        // Act
        var json = JsonSerializer.Serialize(config);
        var deserializedConfig = JsonSerializer.Deserialize<AgentConfiguration>(json);

        // Assert
        deserializedConfig.Should().NotBeNull();
        deserializedConfig!.SystemPrompt.Should().Be(config.SystemPrompt);
        deserializedConfig.AgentName.Should().Be(config.AgentName);
        deserializedConfig.Temperature.Should().Be(config.Temperature);
        deserializedConfig.MaxTokens.Should().Be(config.MaxTokens);
        deserializedConfig.Model.ModelId.Should().Be(config.Model.ModelId);
    }

    [Fact]
    public void AgentConfiguration_ShouldValidateSuccessfully()
    {
        // Arrange
        var config = new AgentConfiguration
        {
            SystemPrompt = "Valid prompt",
            AgentName = "ValidAgent",
            Temperature = 0.7,
            MaxTokens = 2000
        };

        // Act
        var validationResult = config.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
        validationResult.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void AgentConfiguration_ShouldReturnValidationErrors()
    {
        // Arrange
        var config = new AgentConfiguration
        {
            SystemPrompt = "",
            AgentName = "",
            Temperature = -1,
            MaxTokens = 0
        };

        // Act
        var validationResult = config.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.ErrorMessage.Should().NotBeEmpty();
        validationResult.ErrorMessage.Should().Contain("SystemPrompt");
        validationResult.ErrorMessage.Should().Contain("AgentName");
        validationResult.ErrorMessage.Should().Contain("Temperature");
        validationResult.ErrorMessage.Should().Contain("MaxTokens");
    }
} 
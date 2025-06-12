using Xunit;
using FluentAssertions;
using Moq;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Common.Models;
using PsiOrleans.Analysis.Services;

namespace PsiOrleans.Analysis.Tests.Services
{
    public class TaskAnalyzerTests
    {
        private readonly Mock<IAgentContext> _mockContext;
        private readonly AgentConfiguration _configuration;
        private readonly TaskAnalyzer _taskAnalyzer;

        public TaskAnalyzerTests()
        {
            _mockContext = new Mock<IAgentContext>();
            _configuration = new AgentConfiguration
            {
                AgentName = "TestAgent",
                SystemPrompt = "Test prompt",
                Model = new ModelConfiguration
                {
                    ModelId = "gpt-4",
                    ApiKey = "test-api-key"
                }
            };
            _taskAnalyzer = new TaskAnalyzer();
        }

        [Fact]
        public async Task AnalyzeTaskAsync_WithNullTaskDescription_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _taskAnalyzer.AnalyzeTaskAsync(null!, _mockContext.Object, _configuration));
        }

        [Fact]
        public async Task AnalyzeTaskAsync_WithEmptyTaskDescription_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _taskAnalyzer.AnalyzeTaskAsync("", _mockContext.Object, _configuration));
        }

        [Fact]
        public async Task AnalyzeTaskAsync_WithNullContext_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _taskAnalyzer.AnalyzeTaskAsync("test task", null!, _configuration));
        }

        [Fact]
        public async Task AnalyzeTaskAsync_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _taskAnalyzer.AnalyzeTaskAsync("test task", _mockContext.Object, null!));
        }

        [Fact]
        public async Task AnalyzeTaskAsync_WithMissingApiKey_ThrowsInvalidOperationException()
        {
            // Arrange
            var configWithoutApiKey = new AgentConfiguration
            {
                AgentName = "TestAgent",
                SystemPrompt = "Test prompt",
                Model = new ModelConfiguration
                {
                    ModelId = "gpt-4",
                    ApiKey = null // No API key
                }
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _taskAnalyzer.AnalyzeTaskAsync("test task", _mockContext.Object, configWithoutApiKey));
            
            exception.Message.Should().Contain("API key is required");
        }

        [Fact]
        public async Task BreakdownTaskAsync_WithNullTaskDescription_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _taskAnalyzer.BreakdownTaskAsync(null!, _mockContext.Object, _configuration));
        }

        [Fact]
        public async Task BreakdownTaskAsync_WithEmptyTaskDescription_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _taskAnalyzer.BreakdownTaskAsync("", _mockContext.Object, _configuration));
        }

        [Fact]
        public async Task BreakdownTaskAsync_WithNullContext_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _taskAnalyzer.BreakdownTaskAsync("test task", null!, _configuration));
        }

        [Fact]
        public async Task BreakdownTaskAsync_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _taskAnalyzer.BreakdownTaskAsync("test task", _mockContext.Object, null!));
        }

        [Fact]
        public async Task BreakdownTaskAsync_WithMissingApiKey_ThrowsInvalidOperationException()
        {
            // Arrange
            var configWithoutApiKey = new AgentConfiguration
            {
                AgentName = "TestAgent",
                SystemPrompt = "Test prompt",
                Model = new ModelConfiguration
                {
                    ModelId = "gpt-4",
                    ApiKey = null // No API key
                }
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _taskAnalyzer.BreakdownTaskAsync("test task", _mockContext.Object, configWithoutApiKey));
            
            exception.Message.Should().Contain("API key is required");
        }

        // Note: Integration tests with actual LLM calls would require real API keys
        // and are better suited for integration test projects rather than unit tests
    }
} 
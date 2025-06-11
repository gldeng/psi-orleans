using Xunit;
using FluentAssertions;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Moq;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Common.Models;
using PsiOrleans.Analysis.Services;
using Microsoft.Extensions.DependencyInjection;

namespace PsiOrleans.Analysis.Tests.Services
{
    public class TaskAnalyzerTests
    {
        private readonly Mock<IChatCompletionService> _mockChatService;
        private readonly Kernel _kernel;
        private readonly Mock<IAgentContext> _mockContext;
        private readonly AgentConfiguration _testConfiguration;
        private readonly TaskAnalyzer _taskAnalyzer;

        public TaskAnalyzerTests()
        {
            _mockChatService = new Mock<IChatCompletionService>();
            _mockContext = new Mock<IAgentContext>();
            
            // Create test configuration using Common models
            _testConfiguration = new AgentConfiguration
            {
                AgentName = "TestAgent",
                SystemPrompt = "Test system prompt",
                Temperature = 0.7,
                MaxTokens = 2000,
                Model = new ModelConfiguration
                {
                    ModelId = "gpt-4",
                    ApiKey = "test-key"
                }
            };
            
            // Create a real kernel with the mocked chat service
            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton(_mockChatService.Object);
            _kernel = builder.Build();
            
            _taskAnalyzer = new TaskAnalyzer(_kernel);
        }

        [Fact]
        public void TaskAnalyzer_Constructor_WithNullKernel_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new TaskAnalyzer(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task AnalyzeTaskAsync_WithOrchestratorResponse_ReturnsOrchestrationApproach()
        {
            // Arrange
            SetupMockLLMResponse("ORCHESTRATOR");

            // Act
            var result = await _taskAnalyzer.AnalyzeTaskAsync("Plan a marketing campaign", _mockContext.Object, _testConfiguration);

            // Assert
            result.Should().NotBeNull();
            result.RecommendedApproach.Should().Be(TaskApproach.Orchestration);
            result.CanBeDecomposed.Should().BeTrue();
            result.AnalysisNotes.Should().Contain("Orchestrator");
        }

        [Fact]
        public async Task AnalyzeTaskAsync_WithSpecializedResponse_ReturnsDirectExecutionApproach()
        {
            // Arrange
            SetupMockLLMResponse("SPECIALIZED");

            // Act
            var result = await _taskAnalyzer.AnalyzeTaskAsync("Calculate square root of 144", _mockContext.Object, _testConfiguration);

            // Assert
            result.Should().NotBeNull();
            result.RecommendedApproach.Should().Be(TaskApproach.DirectExecution);
            result.CanBeDecomposed.Should().BeFalse();
            result.AnalysisNotes.Should().Contain("Specialized");
        }

        [Fact]
        public async Task AnalyzeTaskAsync_WithAmbiguousResponse_DefaultsToSpecialized()
        {
            // Arrange
            SetupMockLLMResponse("UNCLEAR RESPONSE");

            // Act
            var result = await _taskAnalyzer.AnalyzeTaskAsync("Some ambiguous task", _mockContext.Object, _testConfiguration);

            // Assert
            result.Should().NotBeNull();
            result.RecommendedApproach.Should().Be(TaskApproach.DirectExecution);
            result.CanBeDecomposed.Should().BeFalse();
        }

        [Fact]
        public async Task AnalyzeTaskAsync_WithLLMFailure_FallsBackToSpecialized()
        {
            // Arrange
            _mockChatService.Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("LLM Service Error"));

            // Act
            var result = await _taskAnalyzer.AnalyzeTaskAsync("Any task", _mockContext.Object, _testConfiguration);

            // Assert
            result.Should().NotBeNull();
            result.RecommendedApproach.Should().Be(TaskApproach.DirectExecution);
            result.CanBeDecomposed.Should().BeFalse();
            result.AnalysisNotes.Should().Contain("Specialized mode");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("null")]
        public async Task AnalyzeTaskAsync_WithInvalidTaskDescription_ThrowsArgumentException(string invalidTask)
        {
            var taskToTest = invalidTask == "null" ? null : invalidTask;
            
            // Act & Assert
            var act = async () => await _taskAnalyzer.AnalyzeTaskAsync(taskToTest!, _mockContext.Object, _testConfiguration);
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task AnalyzeTaskAsync_WithNullContext_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = async () => await _taskAnalyzer.AnalyzeTaskAsync("Valid task", null!, _testConfiguration);
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task AnalyzeTaskAsync_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = async () => await _taskAnalyzer.AnalyzeTaskAsync("Valid task", _mockContext.Object, null!);
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task BreakdownTaskAsync_WithSpecializedTask_ReturnsOriginalTask()
        {
            // Arrange
            SetupMockLLMResponse("SPECIALIZED");

            // Act
            var result = await _taskAnalyzer.BreakdownTaskAsync("Simple calculation", _mockContext.Object, _testConfiguration);

            // Assert
            result.Should().NotBeNull();
            result.Should().ContainSingle().Which.Should().Be("Simple calculation");
        }

        [Fact]
        public async Task BreakdownTaskAsync_WithOrchestratorTask_ReturnsMultipleSubtasks()
        {
            // Arrange
            SetupMockLLMResponse("ORCHESTRATOR");
            SetupMockLLMBreakdownResponse("1. Research phase\n2. Planning phase\n3. Execution phase");

            // Act
            var result = await _taskAnalyzer.BreakdownTaskAsync("Complex project", _mockContext.Object, _testConfiguration);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCountGreaterThan(1);
            result.Should().Contain("Research phase");
            result.Should().Contain("Planning phase");
            result.Should().Contain("Execution phase");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("null")]
        public async Task BreakdownTaskAsync_WithInvalidTaskDescription_ThrowsArgumentException(string invalidTask)
        {
            var taskToTest = invalidTask == "null" ? null : invalidTask;
            
            // Act & Assert
            var act = async () => await _taskAnalyzer.BreakdownTaskAsync(taskToTest!, _mockContext.Object, _testConfiguration);
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task BreakdownTaskAsync_WithNullContext_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = async () => await _taskAnalyzer.BreakdownTaskAsync("Valid task", null!, _testConfiguration);
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task BreakdownTaskAsync_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = async () => await _taskAnalyzer.BreakdownTaskAsync("Valid task", _mockContext.Object, null!);
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        private void SetupMockLLMResponse(string response)
        {
            var chatMessage = new ChatMessageContent(AuthorRole.Assistant, response);
            
            _mockChatService.Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(), 
                It.IsAny<PromptExecutionSettings>(), 
                It.IsAny<Kernel>(), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ChatMessageContent> { chatMessage });
        }

        private void SetupMockLLMBreakdownResponse(string response)
        {
            var chatMessage = new ChatMessageContent(AuthorRole.Assistant, response);
            
            _mockChatService.SetupSequence(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ChatMessageContent> { new ChatMessageContent(AuthorRole.Assistant, "ORCHESTRATOR") })
                .ReturnsAsync(new List<ChatMessageContent> { chatMessage });
        }
    }
} 
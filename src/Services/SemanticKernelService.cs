using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using PsiOrleans.Models;
using PsiOrleans.Plugins;
using System.Text.Json;

namespace PsiOrleans.Services;

public class SemanticKernelService : ISemanticKernelService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<SemanticKernelService> _logger;

    public SemanticKernelService(ILogger<SemanticKernelService> logger)
    {
        _logger = logger;
        
        // Build the kernel with OpenAI
        var builder = Kernel.CreateBuilder();
        
        // Get OpenAI API key from environment variable
        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            throw new InvalidOperationException(
                "OPENAI_API_KEY environment variable is required. " +
                "Please set it with your OpenAI API key: export OPENAI_API_KEY='your-api-key-here'");
        }
        
        // Add OpenAI chat completion service
        builder.AddOpenAIChatCompletion(
            modelId: "gpt-4o-mini", // Using GPT-4o-mini for cost efficiency
            apiKey: openAiApiKey);
        
        // Add plugins
        builder.Plugins.AddFromType<GDPSearchPlugin>();
        builder.Plugins.AddFromType<MathematicalOperationsPlugin>();
        
        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        
        _logger.LogInformation("SemanticKernelService initialized with OpenAI GPT-4o-mini and automatic function calling");
    }

    public Kernel GetKernel() => _kernel;

    public async Task<string> ExecuteTaskAsync(string task, AgentState state)
    {
        try
        {
            _logger.LogInformation("Executing task with automatic function calling: {Task}", task);
            
            // Create chat history
            var chatHistory = new ChatHistory();
            
            // Add system message with context
            var systemMessage = BuildSystemPrompt(state);
            chatHistory.AddSystemMessage(systemMessage);
            
            // Add the user task
            chatHistory.AddUserMessage(task);
            
            // Configure OpenAI settings for automatic function calling
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 4000,
                Temperature = 0.1 // Lower temperature for more consistent results
            };
            
            // Execute with automatic function calling
            var result = await _chatService.GetChatMessageContentAsync(
                chatHistory, 
                executionSettings, 
                _kernel);
            
            var response = result.Content ?? "Task completed but no response generated.";
            
            // Store the interaction in agent state
            state.WorkingMemory["last_task"] = task;
            state.WorkingMemory["last_response"] = response;
            state.WorkingMemory["execution_timestamp"] = DateTime.UtcNow.ToString("O");
            
            // Add execution step for tracking
            await AddExecutionStep(state, StepType.FinalAnswer, response);
            
            _logger.LogInformation("Task execution completed successfully");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task with automatic function calling");
            var errorMessage = $"Task execution failed: {ex.Message}";
            await AddExecutionStep(state, StepType.Observation, errorMessage);
            return errorMessage;
        }
    }

    public async Task<List<string>> GetChatHistoryAsync(AgentState state)
    {
        return await Task.FromResult(state.ExecutionHistory
            .Select(h => $"Step {h.StepNumber} ({h.Type}): {h.Content}")
            .ToList());
    }

    private string BuildSystemPrompt(AgentState state)
    {
        var availableFunctions = string.Join("\n", new[]
        {
            "- SearchGDP: Search for GDP data by location and type (parameters: location, gdpType)",
            "- GetAvailableLocations: Get list of available locations for GDP data",
            "- CalculateGDPPercentage: Calculate percentage between two GDP values (parameters: value1, value2)",
            "- BasicArithmetic: Perform basic arithmetic operations (parameters: a, b, operation)",
            "- PowerAndRoot: Calculate power and root operations (parameters: baseNumber, exponent, operation)",
            "- FactorialAndCombinatorics: Calculate factorial, combinations, permutations (parameters: n, r, operation)",
            "- PrimeOperations: Prime number operations (parameters: number, operation, rangeEnd)",
            "- SequenceOperations: Generate mathematical sequences (parameters: sequenceType, terms, firstTerm, commonValue)",
            "- ComplexCalculation: Perform complex multi-step calculations (parameters: expression, numbersJson)"
        });

        var workingMemoryText = state.WorkingMemory.Any() 
            ? JsonSerializer.Serialize(state.WorkingMemory, new JsonSerializerOptions { WriteIndented = true })
            : "No previous context";

        return $$$"""
            You are an intelligent React Agent specialized in economic data analysis, research, and mathematical computations.
            
            Your capabilities include:
            {{{availableFunctions}}}
            
            Current context:
            - Agent ID: {{{state.AgentId}}}
            - Working Memory: {{{workingMemoryText}}}
            
            Instructions:
            1. Analyze the user's request carefully
            2. Use available functions when you need specific data or mathematical calculations
            3. For mathematical problems, break down complex calculations into steps using appropriate functions
            4. Provide comprehensive, accurate answers with specific numbers when available
            5. If you need to search for data, use the appropriate search functions
            6. For mathematical operations, use the mathematical functions to ensure accuracy
            7. Always explain your reasoning and show calculations when relevant
            8. Be concise but thorough in your responses
            
            You have access to GDP data and comprehensive mathematical operations. Use the functions as needed to provide accurate, data-driven responses and precise mathematical calculations.
            """;
    }

    private async Task AddExecutionStep(AgentState state, StepType type, string content)
    {
        var step = new AgentStep
        {
            StepNumber = ++state.CurrentStepNumber,
            Type = type,
            Content = content,
            Timestamp = DateTime.UtcNow
        };
        
        state.ExecutionHistory.Add(step);
        state.LastUpdated = DateTime.UtcNow;
        
        await Task.CompletedTask; // For consistency with async pattern
    }
} 
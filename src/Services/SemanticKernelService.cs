using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Plugins.Web.Tavily;
using Microsoft.SemanticKernel.Data;
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
        // Replace GDPSearchPlugin with Tavily web search
        var tavilyApiKey = Environment.GetEnvironmentVariable("TAVILY_API_KEY");
        if (!string.IsNullOrEmpty(tavilyApiKey))
        {
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates
            var tavilySearch = new TavilyTextSearch(tavilyApiKey);
            
            // Create a kernel function for web search that properly handles async enumerable
            var searchFunction = KernelFunctionFactory.CreateFromMethod(
                async (string query) => 
                {
                    var searchResults = await tavilySearch.SearchAsync(query);
                    var results = new List<string>();
                    
                    await foreach (var result in searchResults.Results)
                    {
                        results.Add(result);
                    }
                    
                    return string.Join("\n\n", results);
                },
                functionName: "Search",
                description: "Search the web for information");
            
            var searchPlugin = KernelPluginFactory.CreateFromFunctions("WebSearch", "Search the web for information", [searchFunction]);
            builder.Plugins.Add(searchPlugin);
#pragma warning restore SKEXP0050
            _logger.LogInformation("Tavily web search plugin initialized");
        }
        else
        {
            _logger.LogWarning("TAVILY_API_KEY not found. Web search functionality will not be available. " +
                             "Please set TAVILY_API_KEY environment variable to enable web search.");
        }
        
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
            
            // Use existing chat history from state or create new one
            var chatHistory = state.ToSemanticKernelChatHistory();
            
            // If this is a fresh conversation, add system message
            if (chatHistory.Count == 0)
            {
                var systemMessage = BuildSystemPrompt(state);
                chatHistory.AddSystemMessage(systemMessage);
                state.AddChatMessage("system", systemMessage);
            }
            
            // Add the user task
            chatHistory.AddUserMessage(task);
            state.AddChatMessage("user", task);
            
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
            
            // Add assistant response to chat history
            state.AddChatMessage("assistant", response);
            
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

    public async Task<string> ContinueConversationAsync(string userMessage, AgentState state)
    {
        try
        {
            _logger.LogInformation("Continuing conversation with message: {Message}", userMessage);
            
            // Use existing chat history from state
            var chatHistory = state.ToSemanticKernelChatHistory();
            
            // If no chat history exists, initialize with system message
            if (chatHistory.Count == 0)
            {
                var systemMessage = BuildSystemPrompt(state);
                chatHistory.AddSystemMessage(systemMessage);
                state.AddChatMessage("system", systemMessage);
            }
            
            // Add the user message
            chatHistory.AddUserMessage(userMessage);
            state.AddChatMessage("user", userMessage);
            
            // Configure OpenAI settings for automatic function calling
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 4000,
                Temperature = 0.1
            };
            
            // Execute with automatic function calling
            var result = await _chatService.GetChatMessageContentAsync(
                chatHistory, 
                executionSettings, 
                _kernel);
            
            var response = result.Content ?? "No response generated.";
            
            // Add assistant response to chat history
            state.AddChatMessage("assistant", response);
            
            // Update working memory
            state.WorkingMemory["last_user_message"] = userMessage;
            state.WorkingMemory["last_assistant_response"] = response;
            state.WorkingMemory["conversation_timestamp"] = DateTime.UtcNow.ToString("O");
            
            _logger.LogInformation("Conversation continued successfully");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing conversation");
            var errorMessage = $"Conversation failed: {ex.Message}";
            return errorMessage;
        }
    }

    public async Task<List<string>> GetChatHistoryAsync(AgentState state)
    {
        return await Task.FromResult(state.ChatHistory
            .Select(h => $"{h.Role}: {h.Content}")
            .ToList());
    }

    private string BuildSystemPrompt(AgentState state)
    {
        var availableFunctions = string.Join("\n", new[]
        {
            "- Search: Search the web for current information on any topic (parameter: query)",
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
            You are an intelligent React Agent specialized in research, data analysis, and mathematical computations.
            
            Your capabilities include:
            {{{availableFunctions}}}
            
            Current context:
            - Agent ID: {{{state.AgentId}}}
            - Working Memory: {{{workingMemoryText}}}
            - Chat History Messages: {{{state.GetChatHistoryCount()}}}
            
            Instructions:
            1. Analyze the user's request carefully
            2. Use available functions when you need specific data or mathematical calculations
            3. For research questions, use the Search function to find current, accurate information from the web
            4. For mathematical problems, break down complex calculations into steps using appropriate functions
            5. Provide comprehensive, accurate answers with specific numbers when available
            6. When searching for information, use clear and specific search queries
            7. For mathematical operations, use the mathematical functions to ensure accuracy
            8. Always explain your reasoning and show calculations when relevant
            9. Be concise but thorough in your responses
            10. Maintain conversation context from previous messages
            11. When providing information from web search, mention that it's from current web sources
            
            You have access to real-time web search and comprehensive mathematical operations. Use the functions as needed to provide accurate, up-to-date, data-driven responses and precise mathematical calculations.
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
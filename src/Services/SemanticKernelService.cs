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
        
        // Build the kernel with OpenAI (you can configure this via appsettings)
        var builder = Kernel.CreateBuilder();
        
        // For demo purposes, using a mock chat service
        // In production, you would configure with real OpenAI/Azure OpenAI
        builder.Services.AddSingleton<IChatCompletionService>(sp => 
            new MockChatCompletionService());
        
        // Add plugins
        builder.Plugins.AddFromType<GDPSearchPlugin>();
        
        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
    }

    public Kernel GetKernel() => _kernel;

    public async Task<string> GenerateThoughtAsync(AgentState state)
    {
        try
        {
            var prompt = BuildThoughtPrompt(state);
            var result = await _chatService.GetChatMessageContentAsync(prompt);
            return result.Content ?? "I need to think about this task.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating thought");
            return $"Error in thinking: {ex.Message}";
        }
    }

    public async Task<string> PlanNextActionAsync(AgentState state, string currentThought)
    {
        try
        {
            var prompt = BuildActionPlanPrompt(state, currentThought);
            var result = await _chatService.GetChatMessageContentAsync(prompt);
            return result.Content ?? "No action planned";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error planning action");
            return $"Error in planning: {ex.Message}";
        }
    }

    public async Task<FunctionResult> ExecuteFunctionAsync(string pluginName, string functionName, KernelArguments arguments)
    {
        try
        {
            var function = _kernel.Plugins[pluginName][functionName];
            return await _kernel.InvokeAsync(function, arguments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {Plugin}.{Function}", pluginName, functionName);
            throw;
        }
    }

    public async Task<string> GenerateFinalAnswerAsync(AgentState state)
    {
        try
        {
            var prompt = BuildFinalAnswerPrompt(state);
            var result = await _chatService.GetChatMessageContentAsync(prompt);
            return result.Content ?? "Unable to generate final answer";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating final answer");
            return $"Error generating answer: {ex.Message}";
        }
    }

    public async Task<bool> ShouldCompleteTaskAsync(AgentState state, string currentThought)
    {
        try
        {
            var prompt = $"""
                Task: {state.CurrentTask}
                Current thought: {currentThought}
                Working memory: {JsonSerializer.Serialize(state.WorkingMemory)}
                
                Based on the current state, should this task be completed? 
                Answer only 'YES' or 'NO'.
                """;
            
            var result = await _chatService.GetChatMessageContentAsync(prompt);
            return result.Content?.Trim().ToUpper() == "YES";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining task completion");
            return false;
        }
    }

    private string BuildThoughtPrompt(AgentState state)
    {
        var historyText = string.Join("\n", state.ExecutionHistory
            .TakeLast(5)
            .Select(h => $"Step {h.StepNumber} ({h.Type}): {h.Content}"));

        return $"""
            You are a React Agent working on this task: {state.CurrentTask}
            
            Current step: {state.CurrentStepNumber}
            Working memory: {JsonSerializer.Serialize(state.WorkingMemory)}
            
            Recent execution history:
            {historyText}
            
            What should you think about next? Be specific and actionable.
            Focus on what information you need or what action to take.
            """;
    }

    private string BuildActionPlanPrompt(AgentState state, string currentThought)
    {
        return $$"""
            Task: {{state.CurrentTask}}
            Current thought: {{currentThought}}
            
            Available functions:
            - GDPSearchPlugin.SearchGDP: Search for GDP data by location
            
            Based on your current thought, what specific action should you take?
            If you need to call a function, specify:
            - Plugin name
            - Function name  
            - Parameters
            
            If no action is needed, say "NO_ACTION".
            
            Format your response as: PLUGIN_NAME.FUNCTION_NAME with parameters: {"param1": "value1"}
            """;
    }

    private string BuildFinalAnswerPrompt(AgentState state)
    {
        var historyText = string.Join("\n", state.ExecutionHistory
            .Select(h => $"Step {h.StepNumber} ({h.Type}): {h.Content}"));

        return $"""
            Task: {state.CurrentTask}
            
            Complete execution history:
            {historyText}
            
            Working memory: {JsonSerializer.Serialize(state.WorkingMemory)}
            
            Based on all the information gathered, provide a comprehensive final answer to the task.
            Be specific, include numbers and calculations where relevant.
            """;
    }
}

// Mock chat completion service for demo purposes
public class MockChatCompletionService : IChatCompletionService
{
    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory, 
        PromptExecutionSettings? executionSettings = null, 
        Kernel? kernel = null, 
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken); // Simulate API call
        
        var lastMessage = chatHistory.LastOrDefault()?.Content ?? "";
        var response = GenerateMockResponse(lastMessage);
        
        return new List<ChatMessageContent>
        {
            new(AuthorRole.Assistant, response)
        };
    }

    public async Task<ChatMessageContent> GetChatMessageContentAsync(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        var response = GenerateMockResponse(prompt);
        return new ChatMessageContent(AuthorRole.Assistant, response);
    }

    private string GenerateMockResponse(string prompt)
    {
        var lowerPrompt = prompt.ToLower();
        
        // Mock responses based on prompt content
        if (lowerPrompt.Contains("what should you think"))
        {
            if (lowerPrompt.Contains("gdp") && lowerPrompt.Contains("step: 1"))
                return "I need to search for US GDP data first to establish the baseline for comparison.";
            if (lowerPrompt.Contains("usa") && lowerPrompt.Contains("working memory"))
                return "I have US GDP data. Now I need to search for New York state GDP data.";
            if (lowerPrompt.Contains("new york") && lowerPrompt.Contains("working memory"))
                return "I have both US and New York GDP data. I can now calculate the percentage.";
        }
        
        if (lowerPrompt.Contains("what specific action"))
        {
            if (lowerPrompt.Contains("us gdp data"))
                return "GDPSearchPlugin.SearchGDP with parameters: {\"location\": \"USA\", \"type\": \"country\"}";
            if (lowerPrompt.Contains("new york"))
                return "GDPSearchPlugin.SearchGDP with parameters: {\"location\": \"New York\", \"type\": \"state\"}";
            if (lowerPrompt.Contains("calculate"))
                return "NO_ACTION";
        }
        
        if (lowerPrompt.Contains("should this task be completed"))
        {
            if (lowerPrompt.Contains("calculate") || lowerPrompt.Contains("percentage"))
                return "YES";
            return "NO";
        }
        
        if (lowerPrompt.Contains("comprehensive final answer"))
        {
            return """
                Based on 2024 data:
                - US GDP: $27,360.9 billion
                - New York State GDP: $2,000.0 billion
                - New York represents 7.31% of US GDP
                
                This means New York state contributes approximately 7.31% to the total US GDP, making it one of the largest state economies in the country.
                """;
        }
        
        return "I need to analyze this further.";
    }

    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory, 
        PromptExecutionSettings? executionSettings = null, 
        Kernel? kernel = null, 
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
} 
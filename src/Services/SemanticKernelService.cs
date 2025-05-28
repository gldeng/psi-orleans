using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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
        
        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        
        _logger.LogInformation("SemanticKernelService initialized with OpenAI GPT-4o-mini");
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
            var prompt = $$$"""
                Task: {{{state.CurrentTask}}}
                Current thought: {{{currentThought}}}
                Working memory: {{{JsonSerializer.Serialize(state.WorkingMemory)}}}
                
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

        return $$$"""
            You are a React Agent working on this task: {{{state.CurrentTask}}}
            
            Current step: {{{state.CurrentStepNumber}}}
            Working memory: {{{JsonSerializer.Serialize(state.WorkingMemory)}}}
            
            Recent execution history:
            {{{historyText}}}
            
            What should you think about next? Be specific and actionable.
            Focus on what information you need or what action to take.
            Keep your response concise and focused.
            """;
    }

    private string BuildActionPlanPrompt(AgentState state, string currentThought)
    {
        return $$$"""
            Task: {{{state.CurrentTask}}}
            Current thought: {{{currentThought}}}
            
            Available functions:
            - GDPSearchPlugin.SearchGDP: Search for GDP data by location and type
            - GDPSearchPlugin.GetAvailableLocations: Get list of available locations
            - GDPSearchPlugin.CalculateGDPPercentage: Calculate percentage between two GDP values
            
            Based on your current thought, what specific action should you take?
            If you need to call a function, specify:
            - Plugin name
            - Function name  
            - Parameters as JSON
            
            If no action is needed, say "NO_ACTION".
            
            Format your response as: PLUGIN_NAME.FUNCTION_NAME with parameters: {"param1": "value1"}
            
            Be precise with parameter names and values.
            """;
    }

    private string BuildFinalAnswerPrompt(AgentState state)
    {
        var historyText = string.Join("\n", state.ExecutionHistory
            .Select(h => $"Step {h.StepNumber} ({h.Type}): {h.Content}"));

        return $$$"""
            Task: {{{state.CurrentTask}}}
            
            Complete execution history:
            {{{historyText}}}
            
            Working memory: {{{JsonSerializer.Serialize(state.WorkingMemory)}}}
            
            Based on all the information gathered, provide a comprehensive final answer to the task.
            Be specific, include numbers and calculations where relevant.
            Format your response clearly and professionally.
            """;
    }
} 
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Analysis.Services;

/// <summary>
/// Core task analysis service that determines whether tasks should be handled by 
/// ORCHESTRATOR mode (complex, needs decomposition) or SPECIALIZED mode (direct execution).
/// </summary>
public class TaskAnalyzer : ITaskAnalyzer
{
    private readonly Kernel _kernel;

    public TaskAnalyzer(Kernel kernel)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public async Task<TaskAnalysisResult> AnalyzeTaskAsync(string taskDescription, IAgentContext context, AgentConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(taskDescription))
            throw new ArgumentException("Task description cannot be null or empty", nameof(taskDescription));

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        try
        {
            var role = await DetermineRoleAsync(taskDescription);
            var isOrchestrator = role == AgentRole.Orchestrator;

            return new TaskAnalysisResult
            {
                RecommendedApproach = isOrchestrator ? TaskApproach.Orchestration : TaskApproach.DirectExecution,
                CanBeDecomposed = isOrchestrator,
                AnalysisNotes = $"Task requires {role} mode"
            };
        }
        catch (Exception ex)
        {
            return new TaskAnalysisResult
            {
                RecommendedApproach = TaskApproach.DirectExecution,
                CanBeDecomposed = false,
                AnalysisNotes = $"Fallback to SPECIALIZED mode: {ex.Message}"
            };
        }
    }

    public async Task<IEnumerable<string>> BreakdownTaskAsync(string taskDescription, IAgentContext context, AgentConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(taskDescription))
            throw new ArgumentException("Task description cannot be null or empty", nameof(taskDescription));

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        var role = await DetermineRoleAsync(taskDescription);
        
        // SPECIALIZED tasks don't need breakdown
        if (role == AgentRole.Specialized)
            return new[] { taskDescription };

        // For ORCHESTRATOR tasks, provide basic breakdown
        // Note: Full breakdown would typically be handled by Orchestrator package
        try
        {
            return await GetBasicBreakdownAsync(taskDescription);
        }
        catch
        {
            return new[] { taskDescription };
        }
    }

    /// <summary>
    /// Determines whether a task should be handled by ORCHESTRATOR or SPECIALIZED mode.
    /// </summary>
    private async Task<AgentRole> DetermineRoleAsync(string taskDescription)
    {
        var prompt = $@"Analyze this task and determine if it should be handled by an ORCHESTRATOR or SPECIALIZED agent.

Task: {taskDescription}

Guidelines:
- ORCHESTRATOR: Complex tasks requiring coordination, multiple steps, or project management
- SPECIALIZED: Direct execution tasks completed with specific tools in one interaction

Examples:
- ORCHESTRATOR: ""Plan and execute a marketing campaign""
- SPECIALIZED: ""Calculate the square root of 144""

Respond with exactly: ORCHESTRATOR or SPECIALIZED";

        try
        {
            // Get chat service from kernel using pattern from ConfigurableKernelService
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);
            
            var results = await chatService.GetChatMessageContentsAsync(chatHistory);
            var response = results.FirstOrDefault()?.Content?.Trim().ToUpperInvariant() ?? "";
            
            return response.Contains("ORCHESTRATOR") ? AgentRole.Orchestrator : AgentRole.Specialized;
        }
        catch
        {
            return AgentRole.Specialized; // Safe fallback
        }
    }

    /// <summary>
    /// Provides basic task breakdown for ORCHESTRATOR tasks.
    /// </summary>
    private async Task<string[]> GetBasicBreakdownAsync(string taskDescription)
    {
        var prompt = $@"Break down this task into 2-4 specific subtasks:

Task: {taskDescription}

Provide a numbered list of actionable subtasks.";

        try
        {
            // Get chat service from kernel using pattern from ConfigurableKernelService
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);
            
            var results = await chatService.GetChatMessageContentsAsync(chatHistory);
            var response = results.FirstOrDefault()?.Content ?? "";
            
            var subtasks = ExtractSubtasks(response);
            return subtasks.Length > 0 ? subtasks : new[] { taskDescription };
        }
        catch
        {
            return new[] { taskDescription };
        }
    }

    /// <summary>
    /// Extracts subtasks from LLM response.
    /// </summary>
    private static string[] ExtractSubtasks(string response)
    {
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var subtasks = new List<string>();

        foreach (var line in lines)
        {
            var cleaned = line.Trim();
            
            // Look for numbered items (1., 2., etc.) or bullet points
            if (cleaned.Length > 5 && 
                (char.IsDigit(cleaned[0]) || cleaned.StartsWith('-') || cleaned.StartsWith('•')))
            {
                // Remove numbering/bullets and clean up
                var colonIndex = cleaned.IndexOf(':');
                var dotIndex = cleaned.IndexOf('.');
                var startIndex = Math.Max(colonIndex, dotIndex);
                
                if (startIndex > 0 && startIndex < cleaned.Length - 1)
                {
                    var subtask = cleaned.Substring(startIndex + 1).Trim().Trim('"');
                    if (!string.IsNullOrWhiteSpace(subtask))
                        subtasks.Add(subtask);
                }
            }
        }

        return subtasks.ToArray();
    }
} 
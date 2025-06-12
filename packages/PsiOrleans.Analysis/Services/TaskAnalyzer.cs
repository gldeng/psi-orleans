using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Analysis.Services;

/// <summary>
/// Core task analysis service that determines whether tasks should be handled by 
/// ORCHESTRATOR mode (complex, needs decomposition) or SPECIALIZED mode (direct execution).
/// Implements the same simple LLM-based analysis as the original ConfigurableAgentGrain.
/// </summary>
public class TaskAnalyzer : ITaskAnalyzer
{
    /// <summary>
    /// Analyzes a task to determine its complexity and recommended agent role.
    /// Uses the same LLM prompt logic as the original ConfigurableAgentGrain.AnalyzeTaskAndDetermineRoleAsync.
    /// </summary>
    public async Task<TaskAnalysisResult> AnalyzeTaskAsync(string taskDescription, IAgentContext context, AgentConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(taskDescription))
            throw new ArgumentException("Task description cannot be null or empty", nameof(taskDescription));

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        // Validate API key before proceeding
        if (string.IsNullOrEmpty(configuration.Model.ApiKey))
            throw new InvalidOperationException("API key is required for task analysis");

        try
        {
            // Create a kernel for LLM analysis (same approach as original)
            var kernelBuilder = Kernel.CreateBuilder();
            
            // Add chat completion service based on configuration
            if (configuration.Model.IsAzureOpenAI)
            {
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    configuration.Model.DeploymentName ?? configuration.Model.ModelId,
                    configuration.Model.Endpoint!,
                    configuration.Model.ApiKey);
            }
            else
            {
                kernelBuilder.AddOpenAIChatCompletion(
                    configuration.Model.ModelId,
                    configuration.Model.ApiKey);
            }

            var kernel = kernelBuilder.Build();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            // Use the exact same analysis prompt as the original ConfigurableAgentGrain
            var analysisPrompt = $@"
Analyze this task and determine if it should be handled by an ORCHESTRATOR or SPECIALIZED agent.

Task: {taskDescription}

ORCHESTRATOR agents should handle tasks that:
- Require breaking down into multiple subtasks
- Need coordination between different capabilities
- Involve complex multi-step workflows
- Require delegation and result aggregation

SPECIALIZED agents should handle tasks that:
- Can be completed with direct tool usage
- Are focused and specific
- Don't require task decomposition
- Can be solved with available functions

Respond with exactly one word: ORCHESTRATOR or SPECIALIZED";

            // Execute LLM analysis (same as original)
            var result = await chatService.GetChatMessageContentAsync(analysisPrompt);
            var response = result.Content?.Trim().ToUpperInvariant() ?? "SPECIALIZED";

            // Parse response and determine role (same logic as original)
            var isOrchestrator = response.Contains("ORCHESTRATOR");

            // Return analysis result using existing TaskAnalysisResult structure
            return new TaskAnalysisResult
            {
                RecommendedApproach = isOrchestrator ? TaskApproach.Orchestration : TaskApproach.DirectExecution,
                CanBeDecomposed = isOrchestrator,
                AnalysisNotes = $"LLM analysis result: {response} (same logic as original ConfigurableAgentGrain)"
            };
        }
        catch (Exception ex)
        {
            // Default to SPECIALIZED on error (same as original)
            return new TaskAnalysisResult
            {
                RecommendedApproach = TaskApproach.DirectExecution,
                CanBeDecomposed = false,
                AnalysisNotes = $"Fallback to SPECIALIZED mode due to error: {ex.Message}"
            };
        }
    }


} 
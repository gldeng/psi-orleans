using Microsoft.SemanticKernel;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

public interface ISemanticKernelService
{
    /// <summary>
    /// Get the configured Semantic Kernel instance
    /// </summary>
    Kernel GetKernel();
    
    /// <summary>
    /// Generate a thought based on current agent state
    /// </summary>
    Task<string> GenerateThoughtAsync(AgentState state);
    
    /// <summary>
    /// Plan the next action based on current thought and state
    /// </summary>
    Task<string> PlanNextActionAsync(AgentState state, string currentThought);
    
    /// <summary>
    /// Execute a function with the given parameters
    /// </summary>
    Task<FunctionResult> ExecuteFunctionAsync(string pluginName, string functionName, KernelArguments arguments);
    
    /// <summary>
    /// Generate final answer based on execution history
    /// </summary>
    Task<string> GenerateFinalAnswerAsync(AgentState state);
    
    /// <summary>
    /// Determine if the task should be completed
    /// </summary>
    Task<bool> ShouldCompleteTaskAsync(AgentState state, string currentThought);
} 
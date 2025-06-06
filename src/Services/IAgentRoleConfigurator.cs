using Microsoft.SemanticKernel;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// Interface for configuring agent roles with appropriate tools and system prompts.
/// Handles the separation between Orchestrator and Specialized agent configurations.
/// </summary>
public interface IAgentRoleConfigurator
{
    /// <summary>
    /// Configure a kernel with role-appropriate tools and settings.
    /// 
    /// Orchestrator: Gets delegation tools (SendParentCallback, CreateAgent, CallChildAgent)
    /// Specialized: Gets normal blocking tools + SendParentCallback (no agent creation tools)
    /// </summary>
    /// <param name="role">The agent role to configure for</param>
    /// <param name="config">The agent configuration</param>
    /// <param name="existingToolNames">Existing tools that should be preserved</param>
    /// <returns>Configured kernel instance</returns>
    Task<Kernel> ConfigureKernelAsync(AgentRole role, AgentConfiguration config, IEnumerable<string>? existingToolNames = null);

    /// <summary>
    /// Get role-appropriate system prompt by enhancing the original prompt with role-specific instructions.
    /// </summary>
    /// <param name="role">The agent role</param>
    /// <param name="originalPrompt">The base system prompt</param>
    /// <returns>Enhanced system prompt with role-specific instructions</returns>
    string GetSystemPromptForRole(AgentRole role, string originalPrompt);
} 
using Microsoft.Extensions.DependencyInjection;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// Factory implementation for creating appropriate state machine implementations based on agent role.
/// Uses dependency injection to resolve state machine instances.
/// </summary>
public class StateMachineFactory : IStateMachineFactory
{
    private readonly IServiceProvider _serviceProvider;

    public StateMachineFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Create the appropriate state machine implementation based on the agent role.
    /// </summary>
    public IAgentStateMachine CreateStateMachine(AgentRole role)
    {
        return role switch
        {
            AgentRole.Orchestrator => _serviceProvider.GetRequiredService<OrchestratorStateMachine>(),
            AgentRole.Specialized => _serviceProvider.GetRequiredService<SpecializedStateMachine>(),
            AgentRole.Undecided => _serviceProvider.GetRequiredService<SpecializedStateMachine>(), // Default to specialized
            _ => throw new ArgumentException($"Unsupported agent role: {role}")
        };
    }
} 
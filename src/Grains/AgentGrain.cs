using Orleans;
using Microsoft.Extensions.Logging;
using PsiOrleans.Models;
using PsiOrleans.Services;

namespace PsiOrleans.Grains;

/// <summary>
/// Orleans Grain implementation of React Agent using Semantic Kernel with automatic function calling
/// </summary>
public class AgentGrain : Grain, IAgentGrain
{
    private readonly ISemanticKernelService _kernelService;
    private readonly ILogger<AgentGrain> _logger;
    private AgentState _state = new();

    public AgentGrain(ISemanticKernelService kernelService, ILogger<AgentGrain> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _state.AgentId = this.GetPrimaryKeyString();
        _logger.LogInformation("Agent {AgentId} activated", _state.AgentId);
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<string> ExecuteTaskAsync(string task)
    {
        _logger.LogInformation("Agent {AgentId} starting task: {Task}", _state.AgentId, task);
        
        // Initialize task execution
        _state.CurrentTask = task;
        _state.IsTaskCompleted = false;
        _state.FinalAnswer = null;
        _state.CurrentStepNumber = 0;
        _state.TaskStartedAt = DateTime.UtcNow;
        _state.LastUpdated = DateTime.UtcNow;

        var startTime = DateTime.UtcNow;

        try
        {
            // Execute the task using Semantic Kernel's automatic function calling
            var result = await _kernelService.ExecuteTaskAsync(task, _state);
            
            _state.TaskCompletedAt = DateTime.UtcNow;
            _state.TotalExecutionTime = DateTime.UtcNow - startTime;
            _state.IsTaskCompleted = true;
            _state.FinalAnswer = result;
            _state.SuccessfulSteps++;
            
            _logger.LogInformation("Agent {AgentId} completed task in {Duration}ms", 
                _state.AgentId, _state.TotalExecutionTime.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task for agent {AgentId}", _state.AgentId);
            _state.FailedSteps++;
            _state.TaskCompletedAt = DateTime.UtcNow;
            _state.TotalExecutionTime = DateTime.UtcNow - startTime;
            
            var errorMessage = $"Task execution failed: {ex.Message}";
            _state.FinalAnswer = errorMessage;
            return errorMessage;
        }
    }

    public async Task<string> ContinueConversationAsync(string userMessage)
    {
        _logger.LogInformation("Agent {AgentId} continuing conversation with: {Message}", _state.AgentId, userMessage);
        
        _state.LastUpdated = DateTime.UtcNow;

        try
        {
            // Continue the conversation using the persistent chat history
            var result = await _kernelService.ContinueConversationAsync(userMessage, _state);
            
            _state.SuccessfulSteps++;
            
            _logger.LogInformation("Agent {AgentId} continued conversation successfully", _state.AgentId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing conversation for agent {AgentId}", _state.AgentId);
            _state.FailedSteps++;
            
            var errorMessage = $"Conversation failed: {ex.Message}";
            return errorMessage;
        }
    }

    public Task<AgentState> GetStateAsync()
    {
        return Task.FromResult(_state);
    }

    public Task<List<AgentStep>> GetExecutionHistoryAsync()
    {
        return Task.FromResult(_state.ExecutionHistory);
    }

    public Task<List<ChatMessage>> GetChatHistoryAsync()
    {
        return Task.FromResult(_state.ChatHistory);
    }

    public Task AddChatMessageAsync(string role, string content, string? name = null)
    {
        _state.AddChatMessage(role, content, name);
        _logger.LogInformation("Agent {AgentId} added chat message: {Role}", _state.AgentId, role);
        return Task.CompletedTask;
    }

    public Task ClearChatHistoryAsync()
    {
        _state.ClearChatHistory();
        _logger.LogInformation("Agent {AgentId} cleared chat history", _state.AgentId);
        return Task.CompletedTask;
    }

    public Task<int> GetChatHistoryCountAsync()
    {
        return Task.FromResult(_state.GetChatHistoryCount());
    }

    public Task ResetAsync()
    {
        _state = new AgentState
        {
            AgentId = this.GetPrimaryKeyString()
        };
        _logger.LogInformation("Agent {AgentId} reset", _state.AgentId);
        return Task.CompletedTask;
    }

    public Task<bool> IsTaskCompletedAsync()
    {
        return Task.FromResult(_state.IsTaskCompleted);
    }

    public Task<(int TotalSteps, int SuccessfulSteps, int FailedSteps, TimeSpan ExecutionTime)> GetMetricsAsync()
    {
        return Task.FromResult((
            TotalSteps: _state.CurrentStepNumber,
            SuccessfulSteps: _state.SuccessfulSteps,
            FailedSteps: _state.FailedSteps,
            ExecutionTime: _state.TotalExecutionTime
        ));
    }
} 
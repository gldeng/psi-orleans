using Orleans;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PsiOrleans.Models;
using PsiOrleans.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PsiOrleans.Grains;

/// <summary>
/// Orleans Grain implementation of React Agent using Semantic Kernel
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
        _state.WorkingMemory.Clear();

        var startTime = DateTime.UtcNow;

        try
        {
            // Execute the React loop
            await ExecuteReactLoopAsync();
            
            _state.TaskCompletedAt = DateTime.UtcNow;
            _state.TotalExecutionTime = DateTime.UtcNow - startTime;
            
            _logger.LogInformation("Agent {AgentId} completed task in {Duration}ms", 
                _state.AgentId, _state.TotalExecutionTime.TotalMilliseconds);

            return _state.FinalAnswer ?? "Task execution completed but no final answer generated.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task for agent {AgentId}", _state.AgentId);
            _state.FailedSteps++;
            return $"Task execution failed: {ex.Message}";
        }
    }

    private async Task ExecuteReactLoopAsync()
    {
        while (!_state.IsTaskCompleted && _state.CurrentStepNumber < _state.MaxSteps)
        {
            _state.CurrentStepNumber++;
            _state.LastUpdated = DateTime.UtcNow;

            try
            {
                // THOUGHT: Generate next thought using Semantic Kernel
                var thought = await _kernelService.GenerateThoughtAsync(_state);
                await AddExecutionStep(StepType.Thought, thought);

                // Check if we should complete the task
                if (await _kernelService.ShouldCompleteTaskAsync(_state, thought))
                {
                    var finalAnswer = await _kernelService.GenerateFinalAnswerAsync(_state);
                    await AddExecutionStep(StepType.FinalAnswer, finalAnswer);
                    
                    _state.IsTaskCompleted = true;
                    _state.FinalAnswer = finalAnswer;
                    _state.SuccessfulSteps++;
                    break;
                }

                // ACTION: Plan and execute next action
                var actionPlan = await _kernelService.PlanNextActionAsync(_state, thought);
                await AddExecutionStep(StepType.Planning, $"Action plan: {actionPlan}");

                // Parse and execute the action if it's not NO_ACTION
                if (!actionPlan.Contains("NO_ACTION"))
                {
                    var actionResult = await ExecuteActionAsync(actionPlan);
                    await AddExecutionStep(StepType.Action, $"Executed action: {actionPlan}");
                    
                    // OBSERVATION: Process the action result
                    await AddExecutionStep(StepType.Observation, $"Action result: {actionResult}");
                    
                    // Store result in working memory
                    _state.WorkingMemory[$"step_{_state.CurrentStepNumber}_result"] = actionResult;
                }

                _state.SuccessfulSteps++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in React loop step {Step} for agent {AgentId}", 
                    _state.CurrentStepNumber, _state.AgentId);
                
                await AddExecutionStep(StepType.Observation, $"Error in step: {ex.Message}");
                _state.FailedSteps++;
                
                // Continue execution unless we've hit too many failures
                if (_state.FailedSteps > 3)
                {
                    _logger.LogWarning("Too many failures for agent {AgentId}, stopping execution", _state.AgentId);
                    break;
                }
            }
        }

        // If we exit the loop without completion, generate a final answer anyway
        if (!_state.IsTaskCompleted)
        {
            try
            {
                var finalAnswer = await _kernelService.GenerateFinalAnswerAsync(_state);
                _state.FinalAnswer = finalAnswer;
                _state.IsTaskCompleted = true;
                await AddExecutionStep(StepType.FinalAnswer, finalAnswer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating final answer for agent {AgentId}", _state.AgentId);
                _state.FinalAnswer = "Unable to complete task due to execution errors.";
            }
        }
    }

    private async Task<string> ExecuteActionAsync(string actionPlan)
    {
        try
        {
            // Parse the action plan to extract plugin, function, and parameters
            var (pluginName, functionName, parameters) = ParseActionPlan(actionPlan);
            
            if (string.IsNullOrEmpty(pluginName) || string.IsNullOrEmpty(functionName))
            {
                return "Invalid action plan format";
            }

            // Create kernel arguments from parameters
            var arguments = new KernelArguments();
            foreach (var param in parameters)
            {
                arguments[param.Key] = param.Value;
            }

            // Execute the function using Semantic Kernel
            var result = await _kernelService.ExecuteFunctionAsync(pluginName, functionName, arguments);
            
            return result.ToString() ?? "Function executed but returned no result";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing action: {ActionPlan}", actionPlan);
            return $"Action execution failed: {ex.Message}";
        }
    }

    private (string PluginName, string FunctionName, Dictionary<string, object> Parameters) ParseActionPlan(string actionPlan)
    {
        try
        {
            // Expected format: "PluginName.FunctionName with parameters: {"param1": "value1", "param2": "value2"}"
            var match = Regex.Match(actionPlan, @"(\w+)\.(\w+)\s+with\s+parameters:\s*(\{.*\})", RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var pluginName = match.Groups[1].Value;
                var functionName = match.Groups[2].Value;
                var parametersJson = match.Groups[3].Value;
                
                var parameters = new Dictionary<string, object>();
                
                if (!string.IsNullOrEmpty(parametersJson))
                {
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(parametersJson);
                        foreach (var property in jsonDoc.RootElement.EnumerateObject())
                        {
                            parameters[property.Name] = property.Value.GetString() ?? "";
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning("Failed to parse parameters JSON: {Json}, Error: {Error}", 
                            parametersJson, ex.Message);
                    }
                }
                
                return (pluginName, functionName, parameters);
            }
            
            // Fallback: try simpler format "PluginName.FunctionName"
            var simpleMatch = Regex.Match(actionPlan, @"(\w+)\.(\w+)", RegexOptions.IgnoreCase);
            if (simpleMatch.Success)
            {
                return (simpleMatch.Groups[1].Value, simpleMatch.Groups[2].Value, new Dictionary<string, object>());
            }
            
            return ("", "", new Dictionary<string, object>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing action plan: {ActionPlan}", actionPlan);
            return ("", "", new Dictionary<string, object>());
        }
    }

    private async Task AddExecutionStep(StepType type, string content)
    {
        var step = new AgentStep
        {
            StepNumber = _state.CurrentStepNumber,
            Type = type,
            Content = content,
            Timestamp = DateTime.UtcNow,
            IsSuccess = true
        };

        _state.ExecutionHistory.Add(step);
        _state.LastUpdated = DateTime.UtcNow;
        
        _logger.LogDebug("Agent {AgentId} step {StepNumber} ({Type}): {Content}", 
            _state.AgentId, step.StepNumber, type, content);
    }

    public Task<AgentState> GetStateAsync()
    {
        return Task.FromResult(_state);
    }

    public Task<List<AgentStep>> GetExecutionHistoryAsync()
    {
        return Task.FromResult(_state.ExecutionHistory);
    }

    public Task ResetAsync()
    {
        _logger.LogInformation("Resetting agent {AgentId}", _state.AgentId);
        
        var agentId = _state.AgentId;
        _state = new AgentState
        {
            AgentId = agentId
        };
        
        return Task.CompletedTask;
    }

    public Task<bool> IsTaskCompletedAsync()
    {
        return Task.FromResult(_state.IsTaskCompleted);
    }

    public Task<(int TotalSteps, int SuccessfulSteps, int FailedSteps, TimeSpan ExecutionTime)> GetMetricsAsync()
    {
        return Task.FromResult((
            TotalSteps: _state.ExecutionHistory.Count,
            SuccessfulSteps: _state.SuccessfulSteps,
            FailedSteps: _state.FailedSteps,
            ExecutionTime: _state.TotalExecutionTime
        ));
    }
}
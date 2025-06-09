using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Orleans;
using PsiOrleans.Grains;
using PsiOrleans.Models;

namespace PsiOrleans.Services;

/// <summary>
/// State machine implementation for Orchestrator agents that use async event-driven execution pattern.
/// 
/// Execution Pattern:
/// - Analyzes and decomposes complex tasks into subtasks using LLM
/// - Creates child agents and delegates subtasks (non-blocking)
/// - Processes callbacks from child agents as events arrive
/// - Uses LLM to make orchestration decisions based on progress
/// - Aggregates results and sends completion callbacks to parent
/// </summary>
public class OrchestratorStateMachine : IAgentStateMachine
{
    private readonly IConfigurableKernelService _kernelService;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<OrchestratorStateMachine> _logger;

    public OrchestratorStateMachine(
        IConfigurableKernelService kernelService,
        IGrainFactory grainFactory,
        ILogger<OrchestratorStateMachine> logger)
    {
        _kernelService = kernelService;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    /// <summary>
    /// Execute task using async event-driven pattern.
    /// Orchestrator agents decompose tasks, delegate to children, and return immediately.
    /// </summary>
    public async Task<string> ExecuteTaskAsync(string task, Kernel kernel, ConfigurableAgentState state, AgentConfiguration config)
    {
        _logger.LogInformation("OrchestratorStateMachine executing task for agent {AgentId}: {Task}", 
            state.AgentId, task);

        if (kernel == null)
        {
            throw new InvalidOperationException("Kernel is not configured for orchestrator execution");
        }

        try
        {
            // Async Event-Driven Execution Pattern:
            // 1. Plan delegation using LLM
            var subTasks = await PlanDelegation(task, kernel, state);
            
            // 2. Create child agents for subtasks
            await CreateChildAgents(subTasks, state, config);
            
            // 3. Delegate tasks to child agents (non-blocking)
            await DelegateTasks(subTasks, state);
            
            _logger.LogInformation("OrchestratorStateMachine initiated delegation for {SubTaskCount} subtasks", subTasks.Count);
            
            // Return immediately after delegation - callbacks will be processed separately
            return $"Orchestration initiated: {subTasks.Count} subtasks delegated. Awaiting child agent callbacks.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OrchestratorStateMachine execution failed for agent {AgentId}", state.AgentId);
            
            // Send failure callback to parent if exists
            if (!string.IsNullOrEmpty(state.ParentAgentId))
            {
                var errorMessage = $"Orchestration failed: {ex.Message}";
                await SendCompletionCallback(state.ParentAgentId, errorMessage, false);
            }
            
            throw;
        }
    }

    /// <summary>
    /// Process callbacks from child agents using async event-driven pattern.
    /// Uses LLM to analyze progress and make orchestration decisions.
    /// </summary>
    public async Task ProcessCallbackAsync(string callId, string message, bool isSuccess, ConfigurableAgentState state, Kernel kernel)
    {
        _logger.LogInformation("OrchestratorStateMachine processing callback {CallId} for agent {AgentId}", 
            callId, state.AgentId);

        try
        {
            // Process the child callback and update state
            await ProcessChildCallback(callId, message, isSuccess, state);
            
            // Use LLM to analyze current progress and make orchestration decision
            var decision = await AnalyzeProgressWithLLM(state, kernel);
            
            // Execute the orchestration decision
            await ExecuteOrchestrationDecision(decision, state, kernel);
            
            _logger.LogInformation("OrchestratorStateMachine processed callback and made decision: {Decision}", decision);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing callback {CallId} for agent {AgentId}", callId, state.AgentId);
            throw;
        }

    }

    /// <summary>
    /// Use LLM to break down complex task into manageable subtasks.
    /// This is the foundation of orchestrator intelligence.
    /// </summary>
    private async Task<List<SubTask>> PlanDelegation(string task, Kernel kernel, ConfigurableAgentState state)
    {
        _logger.LogDebug("Planning task delegation using LLM for agent {AgentId}", state.AgentId);
        
        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            var delegationPrompt = $@"
You are an expert task orchestrator. Break down this complex task into 2-4 manageable subtasks that can be handled by specialized agents.

Original Task: {task}

Requirements:
1. Each subtask should be specific and actionable
2. Subtasks should be relatively independent (minimal dependencies)
3. Each subtask should be suitable for a specialized agent with focused tools
4. Provide a brief rationale for the breakdown

Respond in this exact JSON format:
{{
    ""subtasks"": [
        {{
            ""task"": ""Specific subtask description"",
            ""rationale"": ""Why this subtask is needed"",
            ""suggestedTools"": [""tool1"", ""tool2""]
        }}
    ],
    ""overallStrategy"": ""Brief explanation of the delegation strategy""
}}";

            var result = await chatService.GetChatMessageContentAsync(delegationPrompt);
            var responseContent = result.Content ?? "";
            
            _logger.LogInformation("LLM delegation planning response: {Response}", responseContent);
            
            // Parse the LLM response to create SubTask objects
            var subTasks = ParseDelegationResponse(responseContent, state);
            
            // Store subtasks in state for tracking
            state.CurrentSubTasks = subTasks;
            
            _logger.LogInformation("Planned {SubTaskCount} subtasks for delegation", subTasks.Count);
            return subTasks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM task delegation planning for agent {AgentId}", state.AgentId);
            throw;
        }
    }

    /// <summary>
    /// Parse LLM response to extract subtasks.
    /// Uses basic JSON parsing with fallback to text parsing.
    /// </summary>
    private List<SubTask> ParseDelegationResponse(string response, ConfigurableAgentState state)
    {
        var subTasks = new List<SubTask>();
        
        try
        {
            // Try to parse JSON response
            using var doc = System.Text.Json.JsonDocument.Parse(response);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("subtasks", out var subtasksArray))
            {
                foreach (var subtaskElement in subtasksArray.EnumerateArray())
                {
                    var subtask = new SubTask
                    {
                        Task = subtaskElement.GetProperty("task").GetString() ?? "",
                        SuggestedRole = AgentRole.Specialized, // Default to specialized
                        RequiredTools = new List<string>(),
                        Priority = 1
                    };
                    
                    if (subtaskElement.TryGetProperty("suggestedTools", out var toolsArray))
                    {
                        foreach (var tool in toolsArray.EnumerateArray())
                        {
                            subtask.RequiredTools.Add(tool.GetString() ?? "");
                        }
                    }
                    
                    subTasks.Add(subtask);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON delegation response, falling back to text parsing");
            
            // Fallback: Create simple subtasks from text
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Trim().Length > 10 && !line.Contains("strategy", StringComparison.OrdinalIgnoreCase))
                {
                    subTasks.Add(new SubTask
                    {
                        Task = line.Trim(),
                        SuggestedRole = AgentRole.Specialized,
                        RequiredTools = new List<string>(),
                        Priority = 1
                    });
                }
            }
        }
        
        // Ensure we have at least one subtask
        if (subTasks.Count == 0)
        {
            subTasks.Add(new SubTask
            {
                Task = state.CurrentTask ?? "Process task as specialized agent",
                SuggestedRole = AgentRole.Specialized,
                RequiredTools = new List<string>(),
                Priority = 1
            });
        }
        
        return subTasks;
    }

    /// <summary>
    /// Create child agents for handling subtasks.
    /// Each child agent is configured with appropriate tools and prompts.
    /// </summary>
    private async Task CreateChildAgents(List<SubTask> subTasks, ConfigurableAgentState state, AgentConfiguration config)
    {
        _logger.LogDebug("Creating {SubTaskCount} child agents for subtasks", subTasks.Count);
        
        foreach (var subTask in subTasks)
        {
            try
            {
                // Generate unique child agent ID
                var childAgentId = GenerateChildAgentId(state.AgentId, subTask.SubTaskId);
                subTask.ChildAgentId = childAgentId;
                
                // Create child-specific configuration
                var childConfig = new AgentConfiguration
                {
                    SystemPrompt = GenerateChildSystemPrompt(subTask, config.SystemPrompt),
                    MaxTokens = config.MaxTokens,
                    Temperature = config.Temperature,
                    Model = config.Model
                };
                
                // Get child agent grain
                var childAgent = _grainFactory.GetGrain<IConfigurableAgentGrain>(childAgentId);
                
                // Initialize child agent with specialized tools
                var toolNames = DetermineToolsForSubTask(subTask);
                var (success, message, agentId) = await childAgent.InitializeAsync(childConfig, toolNames);
                
                if (success)
                {
                    state.ChildAgentIds.Add(childAgentId);
                    subTask.Status = SubTaskStatus.Pending;
                    _logger.LogInformation("Created child agent {ChildAgentId} for subtask: {SubTask}", 
                        childAgentId, subTask.Task);
                }
                else
                {
                    _logger.LogError("Failed to create child agent {ChildAgentId}: {Message}", childAgentId, message);
                    subTask.Status = SubTaskStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating child agent for subtask: {SubTask}", subTask.Task);
                subTask.Status = SubTaskStatus.Failed;
            }
        }
    }

    /// <summary>
    /// Generate unique child agent ID based on parent ID and subtask.
    /// </summary>
    private string GenerateChildAgentId(string parentId, string subTaskId)
    {
        var timestamp = DateTime.UtcNow.Ticks;
        return $"{parentId}-child-{subTaskId}-{timestamp}";
    }

    /// <summary>
    /// Generate specialized system prompt for child agent based on subtask.
    /// </summary>
    private string GenerateChildSystemPrompt(SubTask subTask, string? originalPrompt)
    {
        var basePrompt = !string.IsNullOrEmpty(originalPrompt) 
            ? originalPrompt 
            : "You are a specialized AI agent focused on completing specific tasks efficiently.";
        
        return $@"{basePrompt}

SPECIALIZED FOCUS: {subTask.Task}

Your role is to handle this specific subtask as part of a larger coordinated effort. 
Focus on this task and use the available tools to complete it thoroughly.
When finished, you will automatically send a completion callback to your parent orchestrator.";
    }

    /// <summary>
    /// Determine appropriate tools for a subtask.
    /// Maps LLM-suggested tool concepts to actual available tools.
    /// </summary>
    private IEnumerable<string> DetermineToolsForSubTask(SubTask subTask)
    {
        var tools = new List<string>();
        
        // Map LLM-suggested tools to actual available tools
        foreach (var suggestedTool in subTask.RequiredTools)
        {
            var mappedTools = MapConceptualToolToActualTool(suggestedTool);
            tools.AddRange(mappedTools);
        }
        
        // Always ensure some useful tools are available for specialized agents
        // Add these regardless of LLM suggestions to ensure agents can function
        var defaultTools = new[] { "Tavily.search", "Math.Add", "Math.Multiply", "Math.Divide" };
        
        foreach (var tool in defaultTools)
        {
            if (!tools.Contains(tool))
            {
                tools.Add(tool);
            }
        }
        
        // Convert to List to avoid Orleans serialization issues with IEnumerable.Distinct()
        return tools.Distinct().ToList();
    }
    
    /// <summary>
    /// Map conceptual tool names from LLM to actual available tool names.
    /// This bridges the gap between LLM suggestions and real tool registry.
    /// </summary>
    private IEnumerable<string> MapConceptualToolToActualTool(string conceptualTool)
    {
        var lowerTool = conceptualTool.ToLowerInvariant();
        
        // Map economic/research tools to web search
        if (lowerTool.Contains("economic") || lowerTool.Contains("database") || 
            lowerTool.Contains("report") || lowerTool.Contains("research") ||
            lowerTool.Contains("forecast"))
        {
            return new[] { "Tavily.search" };
        }
        
        // Map calculation tools to math functions
        if (lowerTool.Contains("calculation") || lowerTool.Contains("math") || 
            lowerTool.Contains("spreadsheet") || lowerTool.Contains("compute"))
        {
            return new[] { "Math.Add", "Math.Multiply", "Math.Divide" };
        }
        
        // Map file/document tools to file operations
        if (lowerTool.Contains("file") || lowerTool.Contains("document") || 
            lowerTool.Contains("read") || lowerTool.Contains("write"))
        {
            return new[] { "Text.CountWords", "Text.ToTitleCase" };
        }
        
        // If no mapping found, return web search as fallback
        return new[] { "Tavily.search" };
    }

    /// <summary>
    /// Delegate subtasks to child agents (non-blocking).
    /// Stores pending callbacks for tracking.
    /// </summary>
    private async Task DelegateTasks(List<SubTask> subTasks, ConfigurableAgentState state)
    {
        _logger.LogDebug("Delegating {SubTaskCount} tasks to child agents", subTasks.Count);
        
        foreach (var subTask in subTasks.Where(st => st.ChildAgentId != null && st.Status == SubTaskStatus.Pending))
        {
            try
            {
                var childAgent = _grainFactory.GetGrain<IConfigurableAgentGrain>(subTask.ChildAgentId!);
                
                // Create callback tracking data
                var callId = Guid.NewGuid().ToString();
                var callbackData = new CallbackData
                {
                    CallId = callId,
                    ChildAgentId = subTask.ChildAgentId!,
                    Task = subTask.Task,
                    CreatedAt = DateTime.UtcNow
                };
                
                // Store pending callback in state
                state.AddPendingCallback(callId, callbackData);
                
                // Delegate task to child (non-blocking)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await childAgent.ProcessTaskAsync(subTask.Task, state.AgentId);
                        // Child will send callback automatically when complete
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in child agent {ChildId} execution", subTask.ChildAgentId);
                        // Send failure callback manually
                        var parentAgent = _grainFactory.GetGrain<IConfigurableAgentGrain>(state.AgentId);
                        await parentAgent.ReceiveCallbackAsync(callId, $"Child task failed: {ex.Message}", false);
                    }
                });
                
                subTask.Status = SubTaskStatus.Delegated;
                _logger.LogInformation("Delegated subtask to child agent {ChildAgentId}: {SubTask}", 
                    subTask.ChildAgentId, subTask.Task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error delegating subtask to child agent {ChildAgentId}", subTask.ChildAgentId);
                subTask.Status = SubTaskStatus.Failed;
            }
        }
    }

    /// <summary>
    /// Process callback from child agent and update tracking state.
    /// </summary>
    private Task ProcessChildCallback(string callId, string message, bool isSuccess, ConfigurableAgentState state)
    {
        if (state.PendingCallbacks.TryGetValue(callId, out var callbackData))
        {
            // Update callback data
            callbackData.IsReceived = true;
            callbackData.ResultMessage = message;
            callbackData.IsSuccess = isSuccess;
            callbackData.ReceivedAt = DateTime.UtcNow;
            
            // Complete the pending callback (moves to completed list)
            state.CompletePendingCallback(callId);
            
            // Update corresponding subtask status
            var subTask = state.CurrentSubTasks.FirstOrDefault(st => st.ChildAgentId == callbackData.ChildAgentId);
            if (subTask != null)
            {
                subTask.Status = isSuccess ? SubTaskStatus.Completed : SubTaskStatus.Failed;
            }
            
            _logger.LogInformation("Processed callback {CallId} from child {ChildAgentId}: Success={Success}", 
                callId, callbackData.ChildAgentId, isSuccess);
        }
        else
        {
            _logger.LogWarning("Received unexpected callback {CallId} for agent {AgentId}", callId, state.AgentId);
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Use LLM to analyze progress and decide next orchestration action.
    /// This is the key intelligence of the orchestrator.
    /// </summary>
    private async Task<OrchestrationDecision> AnalyzeProgressWithLLM(ConfigurableAgentState state, Kernel kernel)
    {
        _logger.LogDebug("Analyzing progress with LLM for orchestration decision");
        
        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            // Gather current progress information
            var totalSubTasks = state.CurrentSubTasks.Count;
            var completedSubTasks = state.CurrentSubTasks.Count(st => st.Status == SubTaskStatus.Completed);
            var failedSubTasks = state.CurrentSubTasks.Count(st => st.Status == SubTaskStatus.Failed);
            var pendingSubTasks = state.CurrentSubTasks.Count(st => st.Status == SubTaskStatus.Delegated);
            
            var completedResults = state.CompletedCallbacks
                .Where(cb => cb.IsSuccess)
                .Select(cb => $"Task: {cb.Task}\nResult: {cb.ResultMessage}")
                .ToList();
            
            var analysisPrompt = $@"
You are analyzing the progress of a complex task that has been broken down into subtasks and delegated to child agents.

Original Task: {state.CurrentTask}

Progress Status:
- Total subtasks: {totalSubTasks}
- Completed successfully: {completedSubTasks}
- Failed: {failedSubTasks}
- Still pending: {pendingSubTasks}

Completed Results:
{string.Join("\n\n", completedResults)}

Based on this progress, determine the next action:

1. COMPLETE - If you have enough successful results to satisfy the original task
2. WAIT - If you need to wait for more pending callbacks before deciding
3. CREATE_ADDITIONAL - If you need to create additional subtasks to fill gaps

Respond with exactly one word: COMPLETE, WAIT, or CREATE_ADDITIONAL";

            var result = await chatService.GetChatMessageContentAsync(analysisPrompt);
            var decision = result.Content?.Trim().ToUpperInvariant();
            
            return decision switch
            {
                "COMPLETE" => OrchestrationDecision.CompleteTask,
                "WAIT" => OrchestrationDecision.WaitForMoreCallbacks,
                "CREATE_ADDITIONAL" => OrchestrationDecision.CreateAdditionalTasks,
                _ => OrchestrationDecision.WaitForMoreCallbacks // Default to waiting
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM progress analysis for agent {AgentId}", state.AgentId);
            return OrchestrationDecision.WaitForMoreCallbacks; // Safe default
        }
    }

    /// <summary>
    /// Execute orchestration decision based on LLM analysis.
    /// </summary>
    private async Task ExecuteOrchestrationDecision(OrchestrationDecision decision, ConfigurableAgentState state, Kernel kernel)
    {
        switch (decision)
        {
            case OrchestrationDecision.CompleteTask:
                await CompleteTaskAndCallParent(state, kernel);
                break;
                
            case OrchestrationDecision.WaitForMoreCallbacks:
                WaitForMoreCallbacks(state);
                break;
                
            case OrchestrationDecision.CreateAdditionalTasks:
                await CreateAdditionalTasks(state, kernel);
                break;
                
            default:
                _logger.LogWarning("Unknown orchestration decision: {Decision}", decision);
                break;
        }
    }

    /// <summary>
    /// Complete the overall task and send callback to parent.
    /// Uses LLM to aggregate results intelligently.
    /// </summary>
    private async Task CompleteTaskAndCallParent(ConfigurableAgentState state, Kernel kernel)
    {
        _logger.LogInformation("Completing orchestrated task for agent {AgentId}", state.AgentId);
        
        try
        {
            // Use LLM to aggregate results into final response
            var finalResult = await AggregateResultsWithLLM(state, kernel);
            
            // Send completion callback to parent if exists
            if (!string.IsNullOrEmpty(state.ParentAgentId))
            {
                await SendCompletionCallback(state.ParentAgentId, finalResult, true);
            }
            
            _logger.LogInformation("Orchestrated task completed successfully for agent {AgentId}", state.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing orchestrated task for agent {AgentId}", state.AgentId);
            
            if (!string.IsNullOrEmpty(state.ParentAgentId))
            {
                await SendCompletionCallback(state.ParentAgentId, $"Task completion failed: {ex.Message}", false);
            }
        }
    }

    /// <summary>
    /// Continue waiting for more callbacks - no action needed.
    /// </summary>
    private void WaitForMoreCallbacks(ConfigurableAgentState state)
    {
        _logger.LogDebug("Waiting for more callbacks from child agents for agent {AgentId}", state.AgentId);
        // No action needed - just continue waiting for callbacks
    }

    /// <summary>
    /// Create additional subtasks based on progress analysis.
    /// This is a future enhancement for adaptive orchestration.
    /// </summary>
    private async Task CreateAdditionalTasks(ConfigurableAgentState state, Kernel kernel)
    {
        _logger.LogInformation("Creating additional tasks based on progress analysis for agent {AgentId}", state.AgentId);
        
        // TODO: Implement adaptive task creation
        // For now, this is deferred to a later version
        await Task.CompletedTask;
        
        _logger.LogWarning("CreateAdditionalTasks is not yet implemented - falling back to wait");
    }

    /// <summary>
    /// Use LLM to intelligently aggregate all child results into coherent final result.
    /// </summary>
    private async Task<string> AggregateResultsWithLLM(ConfigurableAgentState state, Kernel kernel)
    {
        _logger.LogDebug("Aggregating results with LLM for agent {AgentId}", state.AgentId);
        
        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            var successfulResults = state.CompletedCallbacks
                .Where(cb => cb.IsSuccess)
                .Select(cb => $"Subtask: {cb.Task}\nResult: {cb.ResultMessage}")
                .ToList();
            
            if (!successfulResults.Any())
            {
                return "No successful results to aggregate.";
            }
            
            var aggregationPrompt = $@"
You are aggregating the results of multiple subtasks that were part of a larger complex task.

Original Task: {state.CurrentTask}

Subtask Results:
{string.Join("\n\n---\n\n", successfulResults)}

Please create a coherent, comprehensive final response that:
1. Addresses the original task completely
2. Integrates insights from all subtask results
3. Provides a clear, actionable conclusion
4. Maintains consistency across all information

Final aggregated response:";

            var result = await chatService.GetChatMessageContentAsync(aggregationPrompt);
            var aggregatedResult = result.Content ?? "Task completed successfully with aggregated results from child agents.";
            
            _logger.LogInformation("LLM result aggregation completed for agent {AgentId}", state.AgentId);
            return aggregatedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM result aggregation for agent {AgentId}", state.AgentId);
            
            // Fallback: Simple concatenation of results
            var fallbackResult = string.Join("\n\n", state.CompletedCallbacks
                .Where(cb => cb.IsSuccess)
                .Select(cb => cb.ResultMessage));
            
            return !string.IsNullOrEmpty(fallbackResult) 
                ? $"Task completed. Results:\n\n{fallbackResult}"
                : "Task completed successfully.";
        }
    }

    /// <summary>
    /// Send completion callback to parent agent using Orleans grain communication.
    /// </summary>
    private async Task<bool> SendCompletionCallback(string parentId, string result, bool isSuccess)
    {
        try
        {
            var parentAgent = _grainFactory.GetGrain<IConfigurableAgentGrain>(parentId);
            var callId = Guid.NewGuid().ToString();
            
            await parentAgent.ReceiveCallbackAsync(callId, result, isSuccess);
            
            _logger.LogInformation("Sent completion callback to parent {ParentId}: Success={Success}", 
                parentId, isSuccess);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send completion callback to parent {ParentId}", parentId);
            return false;
        }
    }
} 

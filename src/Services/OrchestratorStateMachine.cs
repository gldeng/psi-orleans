using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Orleans;
using PsiOrleans.Grains;
using PsiOrleans.Models;
using PsiOrleans.Services;
using System.Text;

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
        // Start orchestrator execution tracing
        using var orchestratorExecutionActivity = AgentTracingService.StartAgentActivity("OrchestratorStateMachine.ExecuteTask", state.AgentId, task);
        orchestratorExecutionActivity?.SetTag("orchestrator.execution_pattern", "AsyncEventDriven");
        orchestratorExecutionActivity?.SetTag("orchestrator.agent_id", state.AgentId);
        orchestratorExecutionActivity?.SetTag("orchestrator.task_length", task.Length);
        
        _logger.LogInformation("OrchestratorStateMachine executing task for agent {AgentId}: {Task}", 
            state.AgentId, task);

        if (kernel == null)
        {
            var error = new InvalidOperationException("Kernel is not configured for orchestrator execution");
            AgentTracingService.SetError(orchestratorExecutionActivity, error);
            throw error;
        }

        try
        {
            // Phase 1: Plan delegation using LLM
            using var planningActivity = AgentTracingService.StartKernelActivity("OrchestratorLLM.PlanDelegation", state.AgentId);
            planningActivity?.SetTag("planning.task", task.Length > 200 ? task.Substring(0, 200) + "..." : task);
            
            var subTasks = await PlanDelegation(task, kernel, state);
            
            planningActivity?.SetTag("planning.subtasks_generated", subTasks.Count);
            planningActivity?.SetTag("planning.subtask_titles", string.Join(", ", subTasks.Take(3).Select(st => st.Task.Length > 50 ? st.Task.Substring(0, 50) + "..." : st.Task)));
            AgentTracingService.SetSuccess(planningActivity, $"Generated {subTasks.Count} subtasks via LLM planning");
            
            // Phase 2: Create child agents for subtasks
            using var childCreationActivity = AgentTracingService.StartAgentActivity("CreateChildAgents", state.AgentId);
            childCreationActivity?.SetTag("child_creation.subtask_count", subTasks.Count);
            
            await CreateChildAgents(subTasks, state, config);
            
            var createdChildren = subTasks.Count(st => !string.IsNullOrEmpty(st.ChildAgentId));
            childCreationActivity?.SetTag("child_creation.agents_created", createdChildren);
            childCreationActivity?.SetTag("child_creation.child_ids", string.Join(", ", subTasks.Where(st => !string.IsNullOrEmpty(st.ChildAgentId)).Select(st => st.ChildAgentId).Take(5)));
            AgentTracingService.SetSuccess(childCreationActivity, $"Created {createdChildren} child agents");
            
            // Phase 3: Delegate tasks to child agents (non-blocking)
            using var delegationActivity = AgentTracingService.StartAgentActivity("DelegateTasksNonBlocking", state.AgentId);
            delegationActivity?.SetTag("delegation.subtask_count", subTasks.Count);
            delegationActivity?.SetTag("delegation.pattern", "NonBlocking");
            
            await DelegateTasks(subTasks, state);
            
            var delegatedTasks = subTasks.Count(st => st.Status == SubTaskStatus.Delegated);
            delegationActivity?.SetTag("delegation.tasks_delegated", delegatedTasks);
            delegationActivity?.SetTag("delegation.pending_callbacks", state.PendingCallbacks.Count);
            AgentTracingService.SetSuccess(delegationActivity, $"Delegated {delegatedTasks} tasks, {state.PendingCallbacks.Count} callbacks pending");
            
            _logger.LogInformation("OrchestratorStateMachine initiated delegation for {SubTaskCount} subtasks", subTasks.Count);
            
            var result = $"Orchestration initiated: {subTasks.Count} subtasks delegated. Awaiting child agent callbacks.";
            
            orchestratorExecutionActivity?.SetTag("orchestrator.subtasks_delegated", subTasks.Count);
            orchestratorExecutionActivity?.SetTag("orchestrator.pending_callbacks", state.PendingCallbacks.Count);
            orchestratorExecutionActivity?.SetTag("orchestrator.result", "DelegationInitiated");
            AgentTracingService.SetSuccess(orchestratorExecutionActivity, $"Orchestrator execution completed: {subTasks.Count} subtasks delegated");
            
            // Return immediately after delegation - callbacks will be processed separately
            return result;
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
            
            AgentTracingService.SetError(orchestratorExecutionActivity, ex);
            throw;
        }
    }

    /// <summary>
    /// Process callbacks from child agents using async event-driven pattern.
    /// Uses LLM to analyze progress and make orchestration decisions.
    /// After processing each callback, checks for newly available tasks to delegate.
    /// </summary>
    public async Task ProcessCallbackAsync(string callId, string message, bool isSuccess, ConfigurableAgentState state, Kernel kernel)
    {
        // Start callback processing tracing
        using var callbackProcessingActivity = AgentTracingService.StartAgentActivity("OrchestratorStateMachine.ProcessCallback", state.AgentId);
        callbackProcessingActivity?.SetTag("callback.call_id", callId);
        callbackProcessingActivity?.SetTag("callback.success", isSuccess);
        callbackProcessingActivity?.SetTag("callback.message_length", message.Length);
        callbackProcessingActivity?.SetTag("callback.pending_before", state.PendingCallbacks.Count);
        
        _logger.LogInformation("OrchestratorStateMachine processing callback {CallId} for agent {AgentId}", 
            callId, state.AgentId);

        try
        {
            // Phase 1: Process the child callback and update state
            using var childCallbackActivity = AgentTracingService.StartAgentActivity("ProcessChildCallback", state.AgentId);
            childCallbackActivity?.SetTag("child_callback.call_id", callId);
            childCallbackActivity?.SetTag("child_callback.success", isSuccess);
            
            var completedSubTask = await ProcessChildCallback(callId, message, isSuccess, state);
            
            childCallbackActivity?.SetTag("child_callback.pending_after", state.PendingCallbacks.Count);
            childCallbackActivity?.SetTag("child_callback.completed_total", state.CompletedCallbacks.Count);
            AgentTracingService.SetSuccess(childCallbackActivity, "Child callback processed and state updated");
            
            // Phase 2: Update dependency results and check for newly available tasks
            if (isSuccess && completedSubTask != null)
            {
                using var dependencyUpdateActivity = AgentTracingService.StartAgentActivity("UpdateDependencies", state.AgentId);
                
                await UpdateDependencyResults(completedSubTask, message, state);
                var newlyAvailableTasks = CheckForNewlyAvailableTasks(state);
                
                dependencyUpdateActivity?.SetTag("dependency.completed_subtask_id", completedSubTask.SubTaskId);
                dependencyUpdateActivity?.SetTag("dependency.newly_available_count", newlyAvailableTasks.Count);
                AgentTracingService.SetSuccess(dependencyUpdateActivity, $"Updated dependencies, {newlyAvailableTasks.Count} tasks now available");
                
                // Phase 3: Delegate newly available tasks
                if (newlyAvailableTasks.Count > 0)
                {
                    using var newDelegationActivity = AgentTracingService.StartAgentActivity("DelegateNewlyAvailableTasks", state.AgentId);
                    
                    await DelegateTasks(newlyAvailableTasks, state);
                    
                    newDelegationActivity?.SetTag("new_delegation.task_count", newlyAvailableTasks.Count);
                    AgentTracingService.SetSuccess(newDelegationActivity, $"Delegated {newlyAvailableTasks.Count} newly available tasks");
                    
                    _logger.LogInformation("Delegated {NewTaskCount} newly available tasks after dependency completion", 
                        newlyAvailableTasks.Count);
                }
            }
            
            // Phase 4: Use LLM to analyze current progress and make orchestration decision
            using var orchestrationAnalysisActivity = AgentTracingService.StartKernelActivity("OrchestratorLLM.AnalyzeProgress", state.AgentId);
            orchestrationAnalysisActivity?.SetTag("analysis.pending_callbacks", state.PendingCallbacks.Count);
            orchestrationAnalysisActivity?.SetTag("analysis.completed_callbacks", state.CompletedCallbacks.Count);
            orchestrationAnalysisActivity?.SetTag("analysis.current_subtasks", state.CurrentSubTasks.Count);
            
            var decision = await AnalyzeProgressWithLLM(state, kernel);
            
            orchestrationAnalysisActivity?.SetTag("analysis.decision", decision.ToString());
            AgentTracingService.SetSuccess(orchestrationAnalysisActivity, $"LLM orchestration decision: {decision}");
            
            // Phase 5: Execute the orchestration decision
            using var decisionExecutionActivity = AgentTracingService.StartAgentActivity("ExecuteOrchestrationDecision", state.AgentId);
            decisionExecutionActivity?.SetTag("decision.type", decision.ToString());
            
            await ExecuteOrchestrationDecision(decision, state, kernel);
            
            decisionExecutionActivity?.SetTag("decision.executed", true);
            AgentTracingService.SetSuccess(decisionExecutionActivity, $"Orchestration decision {decision} executed successfully");
            
            _logger.LogInformation("OrchestratorStateMachine processed callback and made decision: {Decision}", decision);
            
            callbackProcessingActivity?.SetTag("callback.processing_result", decision.ToString());
            callbackProcessingActivity?.SetTag("callback.pending_after", state.PendingCallbacks.Count);
            AgentTracingService.SetSuccess(callbackProcessingActivity, $"Callback processed, decision: {decision}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing callback {CallId} for agent {AgentId}", callId, state.AgentId);
            AgentTracingService.SetError(callbackProcessingActivity, ex);
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
2. Identify dependencies between subtasks - which subtasks need results from other subtasks
3. Each subtask should be suitable for a specialized agent with focused tools
4. Provide a brief rationale for the breakdown
5. Use simple numeric IDs (1, 2, 3, etc.) for subtask identification

Example for ""What percentage of US GDP does New York represent?"":
- Subtask 1: Get US GDP data (no dependencies)
- Subtask 2: Get New York GDP data (no dependencies) 
- Subtask 3: Calculate percentage (depends on results from subtasks 1 and 2)

Respond in this exact JSON format:
{{
    ""subtasks"": [
        {{
            ""id"": ""1"",
            ""task"": ""Specific subtask description"",
            ""rationale"": ""Why this subtask is needed"",
            ""suggestedTools"": [""tool1"", ""tool2""],
            ""dependencies"": []
        }},
        {{
            ""id"": ""2"",
            ""task"": ""Another subtask description"",
            ""rationale"": ""Why this subtask is needed"",
            ""suggestedTools"": [""tool1""],
            ""dependencies"": []
        }},
        {{
            ""id"": ""3"",
            ""task"": ""Final calculation subtask"",
            ""rationale"": ""Combine results from previous subtasks"",
            ""suggestedTools"": [""Math.Divide""],
            ""dependencies"": [""1"", ""2""]
        }}
    ],
    ""overallStrategy"": ""Brief explanation of the delegation strategy and dependency flow""
}}";

            var result = await chatService.GetChatMessageContentAsync(delegationPrompt);
            var responseContent = result.Content ?? "";
            
            _logger.LogInformation("LLM delegation planning response: {Response}", responseContent);
            
            // Parse the LLM response to create SubTask objects with dependencies
            var subTasks = ParseDelegationResponse(responseContent, state);
            
            // Update CanStart status based on dependencies
            UpdateSubTaskStartability(subTasks);
            
            // Store subtasks in state for tracking
            state.CurrentSubTasks = subTasks;
            
            _logger.LogInformation("Planned {SubTaskCount} subtasks for delegation with dependencies", subTasks.Count);
            return subTasks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM task delegation planning for agent {AgentId}", state.AgentId);
            throw;
        }
    }

    /// <summary>
    /// Parse LLM response to extract subtasks with dependencies.
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
                        Priority = 1,
                        Dependencies = new List<string>(),
                        DependencyResults = new Dictionary<string, string>(),
                        CanStart = true // Will be updated by UpdateSubTaskStartability
                    };
                    
                    // Set SubTaskId from LLM-provided ID if available
                    if (subtaskElement.TryGetProperty("id", out var idElement))
                    {
                        subtask.SubTaskId = idElement.GetString() ?? Guid.NewGuid().ToString();
                    }
                    
                    // Parse suggested tools
                    if (subtaskElement.TryGetProperty("suggestedTools", out var toolsArray))
                    {
                        foreach (var tool in toolsArray.EnumerateArray())
                        {
                            subtask.RequiredTools.Add(tool.GetString() ?? "");
                        }
                    }
                    
                    // Parse dependencies
                    if (subtaskElement.TryGetProperty("dependencies", out var dependenciesArray))
                    {
                        foreach (var dependency in dependenciesArray.EnumerateArray())
                        {
                            var depId = dependency.GetString();
                            if (!string.IsNullOrEmpty(depId))
                            {
                                subtask.Dependencies.Add(depId);
                            }
                        }
                    }
                    
                    subTasks.Add(subtask);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON delegation response, falling back to text parsing");
            
            // Fallback: Create simple subtasks from text without dependencies
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var taskCounter = 1;
            foreach (var line in lines)
            {
                if (line.Trim().Length > 10 && !line.Contains("strategy", StringComparison.OrdinalIgnoreCase))
                {
                    subTasks.Add(new SubTask
                    {
                        SubTaskId = taskCounter.ToString(),
                        Task = line.Trim(),
                        SuggestedRole = AgentRole.Specialized,
                        RequiredTools = new List<string>(),
                        Priority = 1,
                        Dependencies = new List<string>(),
                        DependencyResults = new Dictionary<string, string>(),
                        CanStart = true
                    });
                    taskCounter++;
                }
            }
        }
        
        // Ensure we have at least one subtask
        if (subTasks.Count == 0)
        {
            subTasks.Add(new SubTask
            {
                SubTaskId = "1",
                Task = state.CurrentTask ?? "Process task as specialized agent",
                SuggestedRole = AgentRole.Specialized,
                RequiredTools = new List<string>(),
                Priority = 1,
                Dependencies = new List<string>(),
                DependencyResults = new Dictionary<string, string>(),
                CanStart = true
            });
        }
        
        return subTasks;
    }

    /// <summary>
    /// Update CanStart status for all subtasks based on their dependencies.
    /// Subtasks can only start if all their dependencies are completed.
    /// </summary>
    private void UpdateSubTaskStartability(List<SubTask> subTasks)
    {
        foreach (var subTask in subTasks)
        {
            if (subTask.Dependencies.Count == 0)
            {
                // No dependencies - can start immediately
                subTask.CanStart = true;
            }
            else
            {
                // Check if all dependencies are completed
                subTask.CanStart = subTask.Dependencies.All(depId =>
                {
                    var dependencyTask = subTasks.FirstOrDefault(st => st.SubTaskId == depId);
                    return dependencyTask?.Status == SubTaskStatus.Completed;
                });
            }
        }
        
        _logger.LogDebug("Updated startability: {StartableCount} of {TotalCount} subtasks can start",
            subTasks.Count(st => st.CanStart), subTasks.Count);
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
    /// Only delegates tasks that have all dependencies satisfied (CanStart=true).
    /// Stores pending callbacks for tracking.
    /// </summary>
    private async Task DelegateTasks(List<SubTask> subTasks, ConfigurableAgentState state)
    {
        var availableTasks = subTasks.Where(st => 
            st.ChildAgentId != null && 
            st.Status == SubTaskStatus.Pending && 
            st.CanStart).ToList();
            
        _logger.LogDebug("Delegating {AvailableTaskCount} of {TotalTaskCount} tasks to child agents (dependency-aware)", 
            availableTasks.Count, subTasks.Count);
        
        if (availableTasks.Count == 0)
        {
            _logger.LogInformation("No subtasks are ready to start due to dependency constraints");
            return;
        }
        
        foreach (var subTask in availableTasks)
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
                
                // Include dependency results in the task context if any - need kernel from state
                var kernel = await GetKernelFromState(state);
                var taskWithContext = await PrepareTaskWithDependencyContextAsync(subTask, kernel);
                
                // Delegate task to child (non-blocking with enhanced timeout handling)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Starting child agent execution for {ChildId} with task: {Task}", 
                            subTask.ChildAgentId, taskWithContext.Length > 100 ? taskWithContext.Substring(0, 100) + "..." : taskWithContext);
                        
                        // Create a cancellation token with timeout for the specific operation
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(8)); // Slightly longer than Orleans timeout
                        
                        try
                        {
                            var result = await childAgent.ProcessTaskAsync(taskWithContext, state.AgentId);
                            
                            _logger.LogInformation("Child agent {ChildId} completed task successfully", subTask.ChildAgentId);
                            // Child will send callback automatically when complete
                        }
                        catch (TimeoutException timeoutEx)
                        {
                            _logger.LogWarning(timeoutEx, "Child agent {ChildId} execution timed out after expected duration", subTask.ChildAgentId);
                            
                            // Send timeout-specific callback
                            var parentAgent = _grainFactory.GetGrain<IConfigurableAgentGrain>(state.AgentId);
                            var timeoutMessage = $"Child agent task timed out: {subTask.Task.Substring(0, Math.Min(100, subTask.Task.Length))}... " +
                                                $"Task may be too complex or external services are slow.";
                            await parentAgent.ReceiveCallbackAsync(callId, timeoutMessage, false);
                        }
                        catch (OperationCanceledException cancelEx) when (timeoutCts.Token.IsCancellationRequested)
                        {
                            _logger.LogWarning(cancelEx, "Child agent {ChildId} execution was cancelled due to timeout", subTask.ChildAgentId);
                            
                            // Send cancellation-specific callback
                            var parentAgent = _grainFactory.GetGrain<IConfigurableAgentGrain>(state.AgentId);
                            var cancelMessage = $"Child agent task was cancelled due to timeout: {subTask.Task.Substring(0, Math.Min(100, subTask.Task.Length))}...";
                            await parentAgent.ReceiveCallbackAsync(callId, cancelMessage, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in child agent {ChildId} execution", subTask.ChildAgentId);
                        
                        // Determine if this is a timeout-related error
                        var isTimeoutRelated = ex is TimeoutException || 
                                             ex.Message.Contains("Response did not arrive on time") ||
                                             ex.Message.Contains("timeout");
                        
                        var errorType = isTimeoutRelated ? "TIMEOUT" : "ERROR";
                        var errorMessage = isTimeoutRelated 
                            ? $"Child task timed out (likely due to external API delays): {ex.Message}"
                            : $"Child task failed: {ex.Message}";
                        
                        _logger.LogError("Child agent {ChildId} failed with {ErrorType}: {Error}", 
                            subTask.ChildAgentId, errorType, ex.Message);
                        
                        // Send failure callback manually with error type information
                        var parentAgent = _grainFactory.GetGrain<IConfigurableAgentGrain>(state.AgentId);
                        await parentAgent.ReceiveCallbackAsync(callId, errorMessage, false);
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
    /// Get kernel from configuration state for LLM operations.
    /// </summary>
    private async Task<Kernel> GetKernelFromState(ConfigurableAgentState state)
    {
        if (state.Configuration == null)
        {
            throw new InvalidOperationException("Agent configuration is null");
        }
        
        // Create a kernel with current configuration
        return await _kernelService.CreateKernelAsync(state.Configuration, state.OriginalToolNames);
    }

    /// <summary>
    /// Prepare task with dependency context by using LLM to intelligently frame the subtask
    /// with relevant information from completed dependencies.
    /// </summary>
    private async Task<string> PrepareTaskWithDependencyContextAsync(SubTask subTask, Kernel kernel)
    {
        if (subTask.Dependencies.Count == 0 || subTask.DependencyResults.Count == 0)
        {
            return subTask.Task;
        }
        
        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            // Gather dependency results
            var dependencyInfo = new StringBuilder();
            foreach (var dependency in subTask.Dependencies)
            {
                if (subTask.DependencyResults.TryGetValue(dependency, out var result))
                {
                    dependencyInfo.AppendLine($"Dependency {dependency} Result: {result}");
                    dependencyInfo.AppendLine();
                }
            }
            
            var contextPrompt = $@"
You are helping to frame a subtask for an AI agent by intelligently incorporating results from prerequisite tasks.

Original Subtask: {subTask.Task}

Results from completed prerequisite tasks:
{dependencyInfo}

Your task is to:
1. Analyze the prerequisite results and extract information relevant to the current subtask
2. Reframe the original subtask description to be more specific and actionable based on the available data
3. Include any specific values, calculations, or context that the agent will need
4. Create a clear, focused task description that incorporates the prerequisite information

Provide a well-framed task description that includes:
- The original task intent
- Specific data from prerequisites that should be used
- Clear instructions on how to use the prerequisite information
- Any calculations or operations that should be performed with the data

Reframed task description:";

            var llmResponse = await chatService.GetChatMessageContentAsync(contextPrompt);
            var reframedTask = llmResponse.Content ?? subTask.Task;
            
            _logger.LogInformation("LLM reframed subtask {SubTaskId}: Original='{Original}', Reframed='{Reframed}'", 
                subTask.SubTaskId, subTask.Task, reframedTask);
                
            return reframedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error using LLM to prepare task context for subtask {SubTaskId}, falling back to simple concatenation", 
                subTask.SubTaskId);
                
            // Fallback to the original simple concatenation approach
            return PrepareTaskWithDependencyContextFallback(subTask);
        }
    }

    /// <summary>
    /// Fallback method for simple text concatenation when LLM context preparation fails.
    /// </summary>
    private string PrepareTaskWithDependencyContextFallback(SubTask subTask)
    {
        if (subTask.Dependencies.Count == 0 || subTask.DependencyResults.Count == 0)
        {
            return subTask.Task;
        }
        
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine($"Task: {subTask.Task}");
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Results from prerequisite tasks:");
        
        foreach (var dependency in subTask.Dependencies)
        {
            if (subTask.DependencyResults.TryGetValue(dependency, out var result))
            {
                contextBuilder.AppendLine($"From subtask {dependency}: {result}");
            }
        }
        
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Use the above prerequisite results to complete your task.");
        
        return contextBuilder.ToString();
    }

    /// <summary>
    /// Process callback from child agent and update tracking state.
    /// Returns the completed SubTask for dependency tracking.
    /// </summary>
    private Task<SubTask?> ProcessChildCallback(string callId, string message, bool isSuccess, ConfigurableAgentState state)
    {
        SubTask? completedSubTask = null;
        
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
            completedSubTask = state.CurrentSubTasks.FirstOrDefault(st => st.ChildAgentId == callbackData.ChildAgentId);
            if (completedSubTask != null)
            {
                completedSubTask.Status = isSuccess ? SubTaskStatus.Completed : SubTaskStatus.Failed;
            }
            
            _logger.LogInformation("Processed callback {CallId} from child {ChildAgentId}: Success={Success}", 
                callId, callbackData.ChildAgentId, isSuccess);
        }
        else
        {
            _logger.LogWarning("Received unexpected callback {CallId} for agent {AgentId}", callId, state.AgentId);
        }
        
        return Task.FromResult(completedSubTask);
    }

    /// <summary>
    /// Update dependency results for subtasks that depend on the completed task.
    /// </summary>
    private Task UpdateDependencyResults(SubTask completedSubTask, string result, ConfigurableAgentState state)
    {
        // Find all subtasks that depend on this completed subtask
        var dependentTasks = state.CurrentSubTasks.Where(st => 
            st.Dependencies.Contains(completedSubTask.SubTaskId)).ToList();
        
        foreach (var dependentTask in dependentTasks)
        {
            // Store the result for this dependency
            dependentTask.DependencyResults[completedSubTask.SubTaskId] = result;
            
            _logger.LogDebug("Updated dependency result for subtask {DependentId} from completed subtask {CompletedId}", 
                dependentTask.SubTaskId, completedSubTask.SubTaskId);
        }
        
        // Update startability for all subtasks after dependency completion
        UpdateSubTaskStartability(state.CurrentSubTasks);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Check for subtasks that are now available to start after dependency completion.
    /// Returns subtasks that have all dependencies satisfied and are ready to be delegated.
    /// </summary>
    private List<SubTask> CheckForNewlyAvailableTasks(ConfigurableAgentState state)
    {
        var newlyAvailable = state.CurrentSubTasks.Where(st =>
            st.Status == SubTaskStatus.Pending &&  // Not yet delegated
            st.CanStart &&                         // Dependencies satisfied
            !string.IsNullOrEmpty(st.ChildAgentId) // Child agent created
        ).ToList();
        
        _logger.LogDebug("Found {Count} newly available tasks after dependency update", newlyAvailable.Count);
        
        return newlyAvailable;
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
            
            var failedResults = state.CompletedCallbacks
                .Where(cb => !cb.IsSuccess)
                .Select(cb => $"Failed Task: {cb.Task}\nError: {cb.ResultMessage}")
                .ToList();
            
            // Categorize failures by type
            var timeoutFailures = state.CompletedCallbacks
                .Where(cb => !cb.IsSuccess && (cb.ResultMessage.Contains("timeout") || cb.ResultMessage.Contains("timed out")))
                .Count();
            
            var otherFailures = failedSubTasks - timeoutFailures;
            
            var analysisPrompt = $@"
You are analyzing the progress of a complex task that has been broken down into subtasks and delegated to child agents.

Original Task: {state.CurrentTask}

Progress Status:
- Total subtasks: {totalSubTasks}
- Completed successfully: {completedSubTasks}
- Failed total: {failedSubTasks} (timeouts: {timeoutFailures}, other errors: {otherFailures})
- Still pending: {pendingSubTasks}

Completed Results:
{string.Join("\n\n", completedResults)}

Failed Results:
{string.Join("\n\n", failedResults)}

Special Considerations:
- If there are timeout failures, they may be due to external API delays rather than task impossibility
- Timeout failures could potentially be retried with different approaches
- Consider if successful results are sufficient to answer the original task

Based on this progress, determine the next action:

1. COMPLETE - If you have enough successful results to satisfy the original task, even with some failures
2. WAIT - If you need to wait for more pending callbacks before deciding
3. CREATE_ADDITIONAL - If you need to create additional or retry subtasks to fill gaps
4. RETRY_TIMEOUTS - If timeout failures should be retried with simpler subtasks

Respond with exactly one word: COMPLETE, WAIT, CREATE_ADDITIONAL, or RETRY_TIMEOUTS";

            var result = await chatService.GetChatMessageContentAsync(analysisPrompt);
            var decision = result.Content?.Trim().ToUpperInvariant();
            
            return decision switch
            {
                "COMPLETE" => OrchestrationDecision.CompleteTask,
                "WAIT" => OrchestrationDecision.WaitForMoreCallbacks,
                "CREATE_ADDITIONAL" => OrchestrationDecision.CreateAdditionalTasks,
                "RETRY_TIMEOUTS" => OrchestrationDecision.RetryTimeouts,
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
                
            case OrchestrationDecision.RetryTimeouts:
                await RetryTimeouts(state, kernel);
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

    /// <summary>
    /// Retry subtasks due to timeouts.
    /// </summary>
    private async Task RetryTimeouts(ConfigurableAgentState state, Kernel kernel)
    {
        _logger.LogInformation("Retrying subtasks due to timeouts for agent {AgentId}", state.AgentId);
        
        // TODO: Implement retry logic
        // For now, this is deferred to a later version
        await Task.CompletedTask;
        
        _logger.LogWarning("RetryTimeouts is not yet implemented - falling back to wait");
    }
} 

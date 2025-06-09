using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Orleans;
using PsiOrleans.Grains;
using PsiOrleans.Models;
using PsiOrleans.Services;

namespace PsiOrleans.Services;

/// <summary>
/// State machine implementation for Specialized agents that use sync direct execution pattern.
/// 
/// Execution Pattern:
/// - Executes tasks directly using available tools
/// - Awaits tool results synchronously (linear execution flow)
/// - Sends completion callbacks to parent agent when finished
/// - Does not create or manage child agents
/// </summary>
public class SpecializedStateMachine : IAgentStateMachine
{
    private readonly IConfigurableKernelService _kernelService;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SpecializedStateMachine> _logger;

    public SpecializedStateMachine(
        IConfigurableKernelService kernelService,
        IGrainFactory grainFactory,
        ILogger<SpecializedStateMachine> logger)
    {
        _kernelService = kernelService;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    /// <summary>
    /// Execute task using sync direct execution pattern.
    /// Specialized agents use their configured tools directly and send completion callbacks.
    /// </summary>
    public async Task<string> ExecuteTaskAsync(string task, Kernel kernel, ConfigurableAgentState state, AgentConfiguration config)
    {
        // Start specialized execution tracing
        using var specializedExecutionActivity = AgentTracingService.StartAgentActivity("SpecializedStateMachine.ExecuteTask", state.AgentId, task);
        specializedExecutionActivity?.SetTag("specialized.execution_pattern", "SyncDirect");
        specializedExecutionActivity?.SetTag("specialized.agent_id", state.AgentId);
        specializedExecutionActivity?.SetTag("specialized.task_length", task.Length);
        specializedExecutionActivity?.SetTag("specialized.parent_id", state.ParentAgentId ?? "none");
        
        // 🟢 FLOW_START: Specialized state machine execution flow
        _logger.LogInformation("🟢 FLOW_START: SpecializedStateMachine.ExecuteTask for agent {AgentId}, task: '{Task}'", 
            state.AgentId, task.Length > 100 ? task.Substring(0, 100) + "..." : task);

        if (kernel == null)
        {
            var error = new InvalidOperationException("Kernel is not configured for specialized execution");
            
            // ❌ FLOW_ERROR: Kernel not configured
            _logger.LogError("❌ FLOW_ERROR: Kernel is not configured for specialized execution - AgentId: {AgentId}", state.AgentId);
            
            AgentTracingService.SetError(specializedExecutionActivity, error);
            throw error;
        }

        // 🔄 FLOW_STEP: Validate kernel configuration
        var availableTools = kernel.Plugins.SelectMany(p => p.Select(f => $"{p.Name}.{f.Name}")).ToList();
        _logger.LogInformation("🔄 FLOW_STEP: Kernel validation - AgentId: {AgentId}, Available tools: {ToolCount} ({Tools})", 
            state.AgentId, availableTools.Count, string.Join(", ", availableTools.Take(3)));

        try
        {
            // Phase 1: Sync Direct Execution Pattern - Execute with specialized tools
            using var directExecutionActivity = AgentTracingService.StartAgentActivity("ExecuteWithDirectTools", state.AgentId, task);
            directExecutionActivity?.SetTag("direct_execution.task", task.Length > 200 ? task.Substring(0, 200) + "..." : task);
            directExecutionActivity?.SetTag("direct_execution.pattern", "ToolCalling");
            directExecutionActivity?.SetTag("direct_execution.kernel_plugins", kernel.Plugins.Count);
            
            // 🔄 FLOW_STEP: Starting direct tool execution
            _logger.LogInformation("🔄 FLOW_STEP: Starting direct tool execution - AgentId: {AgentId}, Pattern: SyncDirect", state.AgentId);
            
            var result = await ExecuteWithDirectTools(task, kernel, state, config);
            
            directExecutionActivity?.SetTag("direct_execution.result_length", result.Length);
            directExecutionActivity?.SetTag("direct_execution.success", true);
            
            // ✅ FLOW_SUCCESS: Direct tool execution completed
            _logger.LogInformation("✅ FLOW_SUCCESS: Direct tool execution completed - AgentId: {AgentId}, Result length: {Length}", 
                state.AgentId, result.Length);
            
            AgentTracingService.SetSuccess(directExecutionActivity, "Direct tool execution completed successfully");
            
            // Phase 2: Send completion callback to parent if exists
            if (!string.IsNullOrEmpty(state.ParentAgentId))
            {
                using var callbackSendActivity = AgentTracingService.StartAgentActivity("SendCompletionCallbackToParent", state.AgentId);
                callbackSendActivity?.SetTag("completion_callback.parent_id", state.ParentAgentId);
                callbackSendActivity?.SetTag("completion_callback.success", true);
                callbackSendActivity?.SetTag("completion_callback.result_length", result.Length);
                
                // 🔄 FLOW_STEP: Sending completion callback to parent
                _logger.LogInformation("🔄 FLOW_STEP: Sending completion callback to parent {ParentId} - AgentId: {AgentId}", 
                    state.ParentAgentId, state.AgentId);
                
                await SendCompletionCallback(state.ParentAgentId, result, true);
                
                // ✅ FLOW_SUCCESS: Callback sent successfully
                _logger.LogInformation("✅ FLOW_SUCCESS: Completion callback sent to parent {ParentId} - AgentId: {AgentId}", 
                    state.ParentAgentId, state.AgentId);
                
                AgentTracingService.SetSuccess(callbackSendActivity, $"Success callback sent to parent {state.ParentAgentId}");
            }
            else
            {
                specializedExecutionActivity?.SetTag("specialized.parent_callback", "not_needed_no_parent");
                
                // 🔄 FLOW_STEP: No parent callback needed
                _logger.LogInformation("🔄 FLOW_STEP: No parent callback needed (no parent agent) - AgentId: {AgentId}", state.AgentId);
            }
            
            // ✅ FLOW_SUCCESS: Specialized execution completed
            _logger.LogInformation("✅ FLOW_SUCCESS: SpecializedStateMachine execution completed - AgentId: {AgentId}, Result: {Length} chars", 
                state.AgentId, result.Length);

            specializedExecutionActivity?.SetTag("specialized.result_length", result.Length);
            specializedExecutionActivity?.SetTag("specialized.completion_callback_sent", !string.IsNullOrEmpty(state.ParentAgentId));
            
            // 🏁 FLOW_END: Specialized state machine execution flow complete
            _logger.LogInformation("🏁 FLOW_END: SpecializedStateMachine.ExecuteTask - SUCCESS for agent {AgentId}", state.AgentId);
            
            AgentTracingService.SetSuccess(specializedExecutionActivity, $"Specialized execution completed: {result.Length} chars result");
            
            return result;
        }
        catch (Exception ex)
        {
            // ❌ FLOW_ERROR: Specialized execution failed
            _logger.LogError(ex, "❌ FLOW_ERROR: SpecializedStateMachine execution failed for agent {AgentId}: {Error}", 
                state.AgentId, ex.Message);
            
            // Send failure callback to parent if exists
            if (!string.IsNullOrEmpty(state.ParentAgentId))
            {
                using var failureCallbackActivity = AgentTracingService.StartAgentActivity("SendFailureCallbackToParent", state.AgentId);
                failureCallbackActivity?.SetTag("failure_callback.parent_id", state.ParentAgentId);
                failureCallbackActivity?.SetTag("failure_callback.error", ex.Message);
                
                // 🔄 FLOW_STEP: Sending failure callback to parent
                _logger.LogInformation("🔄 FLOW_STEP: Sending failure callback to parent {ParentId} - AgentId: {AgentId}, Error: {Error}", 
                    state.ParentAgentId, state.AgentId, ex.Message);
                
                var errorMessage = $"Specialized task failed: {ex.Message}";
                await SendCompletionCallback(state.ParentAgentId, errorMessage, false);
                
                AgentTracingService.SetSuccess(failureCallbackActivity, "Failure callback sent to parent");
            }
            
            // 🏁 FLOW_END: Specialized state machine execution flow complete with error
            _logger.LogInformation("🏁 FLOW_END: SpecializedStateMachine.ExecuteTask - FAILED for agent {AgentId}", state.AgentId);
            
            AgentTracingService.SetError(specializedExecutionActivity, ex);
            throw;
        }
    }

    /// <summary>
    /// Process callbacks from child agents.
    /// Specialized agents typically don't receive callbacks since they don't create children.
    /// </summary>
    public Task ProcessCallbackAsync(string callId, string message, bool isSuccess, ConfigurableAgentState state, Kernel kernel)
    {
        // Start unexpected callback tracing
        using var unexpectedCallbackActivity = AgentTracingService.StartAgentActivity("SpecializedStateMachine.UnexpectedCallback", state.AgentId);
        unexpectedCallbackActivity?.SetTag("unexpected.call_id", callId);
        unexpectedCallbackActivity?.SetTag("unexpected.success", isSuccess);
        unexpectedCallbackActivity?.SetTag("unexpected.message_length", message.Length);
        unexpectedCallbackActivity?.SetTag("unexpected.reason", "specialized_agents_dont_have_children");
        
        // Specialized agents don't typically receive callbacks from children
        // This method can be empty or log unexpected callbacks
        _logger.LogWarning("SpecializedStateMachine received unexpected callback {CallId} for agent {AgentId}: {Message}", 
            callId, state.AgentId, message);
        
        AgentTracingService.SetSuccess(unexpectedCallbackActivity, "Unexpected callback logged (specialized agents don't manage children)");
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Execute task with direct tool usage - the core sync execution pattern.
    /// Uses direct LLM tool calling for optimal performance and simplicity.
    /// </summary>
    private async Task<string> ExecuteWithDirectTools(string task, Kernel kernel, ConfigurableAgentState state, AgentConfiguration config)
    {
        _logger.LogDebug("Executing task with automatic LLM tool calling for agent {AgentId}", state.AgentId);
        
        // 📊 TASK_INFO: Log detailed tool execution start
        _logger.LogInformation("📊 TASK_INFO: Direct Tool Execution Start - Agent: {AgentId}, Task: '{Task}', Available Tools: {ToolCount}", 
            state.AgentId, task, kernel.Plugins.SelectMany(p => p).Count());
        
        try
        {
            // Debug: Check available tools in kernel
            var availableTools = kernel.Plugins.SelectMany(p => p.Select(f => $"{p.Name}.{f.Name}")).ToList();
            _logger.LogInformation("SpecializedStateMachine available tools: {Tools}", string.Join(", ", availableTools));
            
            // 📊 TASK_INFO: Log available tools and their descriptions
            var toolDetails = kernel.Plugins.SelectMany(p => 
                p.Select(f => new 
                { 
                    Name = $"{p.Name}.{f.Name}", 
                    Description = f.Description ?? "No description",
                    ParameterCount = f.Metadata.Parameters.Count 
                })).ToList();
            
            _logger.LogInformation("📊 TASK_INFO: Available Tool Details - Agent: {AgentId}, Tools: {Tools}", 
                state.AgentId, 
                string.Join("; ", toolDetails.Select(t => $"{t.Name}({t.ParameterCount} params): {(t.Description.Length > 50 ? t.Description.Substring(0, 50) + "..." : t.Description)}")));
            
            // Get chat completion service
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            // Configure execution settings for automatic tool calling
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, // Enable automatic tool calling
                MaxTokens = config.MaxTokens,
                Temperature = config.Temperature
            };

            // 📊 TASK_INFO: Log execution settings
            _logger.LogInformation("📊 TASK_INFO: LLM Execution Settings - Agent: {AgentId}, MaxTokens: {MaxTokens}, Temperature: {Temperature}, ToolCallBehavior: {ToolBehavior}", 
                state.AgentId, executionSettings.MaxTokens, executionSettings.Temperature, "AutoInvokeKernelFunctions");

            // Create chat history starting with system prompt and user task
            var chatHistory = new ChatHistory();
            if (!string.IsNullOrEmpty(config.SystemPrompt))
            {
                // Enhanced system prompt to force tool usage
                var enhancedPrompt = config.SystemPrompt + 
                    "\n\nIMPORTANT: You MUST use the available tools.";
                
                chatHistory.AddSystemMessage(enhancedPrompt);
                _logger.LogInformation("Added enhanced system prompt: {Prompt}", enhancedPrompt);
                
                // 📊 TASK_INFO: Log system prompt details
                _logger.LogInformation("📊 TASK_INFO: System Prompt - Agent: {AgentId}, Original Length: {OriginalLength}, Enhanced Length: {EnhancedLength}, Enhancement: Tool usage enforcement", 
                    state.AgentId, config.SystemPrompt.Length, enhancedPrompt.Length);
            }
            
            // Enhanced user message to force tool usage
            var enhancedTask = task + 
                "\n\nREQUIREMENT: You must use the available tool functions.";
            
            chatHistory.AddUserMessage(enhancedTask);
            _logger.LogInformation("Added enhanced user message: {Task}", enhancedTask);

            // 📊 TASK_INFO: Log user message enhancement
            _logger.LogInformation("📊 TASK_INFO: User Message - Agent: {AgentId}, Original Task: '{OriginalTask}', Enhanced Task Length: {EnhancedLength}, Enhancement: Tool usage requirement", 
                state.AgentId, task, enhancedTask.Length);

            _logger.LogInformation("Executing ChatCompletion with AutoInvokeKernelFunctions...");
            
            // 📊 TASK_INFO: Log LLM execution start
            var executionStartTime = DateTime.UtcNow;
            _logger.LogInformation("📊 TASK_INFO: LLM Execution Start - Agent: {AgentId}, Chat History Count: {HistoryCount}, Auto Tool Calling: Enabled", 
                state.AgentId, chatHistory.Count);
            
            // Execute with automatic tool calling - LLM will call tools as needed
            var result = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings, 
                kernel);
            
            var executionDuration = DateTime.UtcNow - executionStartTime;
            var response = result.Content ?? "Task completed successfully.";
            
            // 📊 TASK_INFO: Log LLM execution result
            _logger.LogInformation("📊 TASK_INFO: LLM Execution Complete - Agent: {AgentId}, Duration: {Duration}ms, Response Length: {Length}, Response Preview: '{Response}', Tool Calls Detected: {ToolCalls}", 
                state.AgentId, executionDuration.TotalMilliseconds, response.Length, 
                response.Length > 200 ? response.Substring(0, 200) + "..." : response,
                FunctionCallContent.GetFunctionCalls(result).Any() ? "Yes" : "No");
            
            // 📊 TASK_INFO: Log tool usage analysis
            var functionCalls = FunctionCallContent.GetFunctionCalls(result).ToList();
            if (functionCalls.Any())
            {
                _logger.LogInformation("📊 TASK_INFO: Tool Usage Detected - Agent: {AgentId}, Tool Calls: {ToolCalls}", 
                    state.AgentId, string.Join(", ", functionCalls.Select(fc => $"{fc.PluginName}.{fc.FunctionName}")));
            }
            else
            {
                _logger.LogInformation("📊 TASK_INFO: No Tool Usage - Agent: {AgentId}, Direct Response Generated", state.AgentId);
            }
            
            _logger.LogInformation("Automatic LLM tool calling result: {Result}", response);
            _logger.LogDebug("Automatic LLM tool calling completed for agent {AgentId}", state.AgentId);
            
            // 📊 TASK_INFO: Log execution success summary
            _logger.LogInformation("📊 TASK_INFO: Direct Tool Execution Success - Agent: {AgentId}, Total Duration: {Duration}ms, Final Response Length: {Length}, Tools Used: {ToolsUsed}", 
                state.AgentId, executionDuration.TotalMilliseconds, response.Length, 
                functionCalls.Any() ? functionCalls.Count.ToString() : "0");
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in automatic tool calling execution for agent {AgentId}", state.AgentId);
            
            // 📊 TASK_INFO: Log execution error details
            _logger.LogError("📊 TASK_INFO: Direct Tool Execution Error - Agent: {AgentId}, Task: '{Task}', Error: {Error}, Exception Type: {ExceptionType}, Available Tools: {ToolCount}", 
                state.AgentId, task, ex.Message, ex.GetType().Name, kernel.Plugins.SelectMany(p => p).Count());
            
            throw;
        }
    }

    /// <summary>
    /// Send completion callback to parent agent using Orleans grain communication.
    /// This maintains compatibility with existing callback infrastructure.
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
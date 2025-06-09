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
        
        _logger.LogInformation("SpecializedStateMachine executing task for agent {AgentId}: {Task}", 
            state.AgentId, task);

        if (kernel == null)
        {
            var error = new InvalidOperationException("Kernel is not configured for specialized execution");
            AgentTracingService.SetError(specializedExecutionActivity, error);
            throw error;
        }

        try
        {
            // Phase 1: Sync Direct Execution Pattern - Execute with specialized tools
            using var directExecutionActivity = AgentTracingService.StartAgentActivity("ExecuteWithDirectTools", state.AgentId, task);
            directExecutionActivity?.SetTag("direct_execution.task", task.Length > 200 ? task.Substring(0, 200) + "..." : task);
            directExecutionActivity?.SetTag("direct_execution.pattern", "ToolCalling");
            directExecutionActivity?.SetTag("direct_execution.kernel_plugins", kernel.Plugins.Count);
            
            var result = await ExecuteWithDirectTools(task, kernel, state, config);
            
            directExecutionActivity?.SetTag("direct_execution.result_length", result.Length);
            directExecutionActivity?.SetTag("direct_execution.success", true);
            AgentTracingService.SetSuccess(directExecutionActivity, "Direct tool execution completed successfully");
            
            // Phase 2: Send completion callback to parent if exists
            if (!string.IsNullOrEmpty(state.ParentAgentId))
            {
                using var callbackSendActivity = AgentTracingService.StartAgentActivity("SendCompletionCallbackToParent", state.AgentId);
                callbackSendActivity?.SetTag("completion_callback.parent_id", state.ParentAgentId);
                callbackSendActivity?.SetTag("completion_callback.success", true);
                callbackSendActivity?.SetTag("completion_callback.result_length", result.Length);
                
                await SendCompletionCallback(state.ParentAgentId, result, true);
                
                AgentTracingService.SetSuccess(callbackSendActivity, $"Success callback sent to parent {state.ParentAgentId}");
            }
            else
            {
                specializedExecutionActivity?.SetTag("specialized.parent_callback", "not_needed_no_parent");
            }
            
            _logger.LogInformation("SpecializedStateMachine completed task for agent {AgentId}", state.AgentId);
            
            specializedExecutionActivity?.SetTag("specialized.result_length", result.Length);
            specializedExecutionActivity?.SetTag("specialized.completion_callback_sent", !string.IsNullOrEmpty(state.ParentAgentId));
            AgentTracingService.SetSuccess(specializedExecutionActivity, $"Specialized execution completed: {result.Length} chars result");
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SpecializedStateMachine execution failed for agent {AgentId}", state.AgentId);
            
            // Send failure callback to parent if exists
            if (!string.IsNullOrEmpty(state.ParentAgentId))
            {
                using var failureCallbackActivity = AgentTracingService.StartAgentActivity("SendFailureCallbackToParent", state.AgentId);
                failureCallbackActivity?.SetTag("failure_callback.parent_id", state.ParentAgentId);
                failureCallbackActivity?.SetTag("failure_callback.error", ex.Message);
                
                var errorMessage = $"Specialized task failed: {ex.Message}";
                await SendCompletionCallback(state.ParentAgentId, errorMessage, false);
                
                AgentTracingService.SetSuccess(failureCallbackActivity, "Failure callback sent to parent");
            }
            
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
        
        try
        {
            // Debug: Check available tools in kernel
            var availableTools = kernel.Plugins.SelectMany(p => p.Select(f => $"{p.Name}.{f.Name}")).ToList();
            _logger.LogInformation("SpecializedStateMachine available tools: {Tools}", string.Join(", ", availableTools));
            
            // Get chat completion service
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            // Configure execution settings for automatic tool calling
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, // Enable automatic tool calling
                MaxTokens = config.MaxTokens,
                Temperature = config.Temperature
            };

            // Create chat history starting with system prompt and user task
            var chatHistory = new ChatHistory();
            if (!string.IsNullOrEmpty(config.SystemPrompt))
            {
                // Enhanced system prompt to force tool usage
                var enhancedPrompt = config.SystemPrompt + 
                    "\n\nIMPORTANT: You MUST use the available tools.";
                
                chatHistory.AddSystemMessage(enhancedPrompt);
                _logger.LogInformation("Added enhanced system prompt: {Prompt}", enhancedPrompt);
            }
            
            // Enhanced user message to force tool usage
            var enhancedTask = task + 
                "\n\nREQUIREMENT: You must use the available tool functions.";
            
            chatHistory.AddUserMessage(enhancedTask);
            _logger.LogInformation("Added enhanced user message: {Task}", enhancedTask);

            _logger.LogInformation("Executing ChatCompletion with AutoInvokeKernelFunctions...");
            
            // Execute with automatic tool calling - LLM will call tools as needed
            var result = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings, 
                kernel);

            var response = result.Content ?? "Task completed successfully.";
            
            _logger.LogInformation("Automatic LLM tool calling result: {Result}", response);
            _logger.LogDebug("Automatic LLM tool calling completed for agent {AgentId}", state.AgentId);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in automatic tool calling execution for agent {AgentId}", state.AgentId);
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
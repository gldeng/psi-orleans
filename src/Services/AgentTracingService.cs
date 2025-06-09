using System.Diagnostics;

namespace PsiOrleans.Services;

/// <summary>
/// Service for managing distributed tracing across Orleans grains and agent operations
/// </summary>
public static class AgentTracingService
{
    // ActivitySource for agent operations tracing
    public static readonly ActivitySource AgentActivitySource = new("PsiOrleans.Agent");
    
    // ActivitySource for semantic kernel operations tracing
    public static readonly ActivitySource KernelActivitySource = new("PsiOrleans.Kernel");
    
    // ActivitySource for Orleans grain operations tracing
    public static readonly ActivitySource OrleansGrainActivitySource = new("PsiOrleans.Orleans.Grain");
    
    /// <summary>
    /// Start a new activity for agent task processing
    /// </summary>
    public static Activity? StartAgentActivity(string operationName, string? agentId = null, string? taskDescription = null)
    {
        // Ensure we inherit the current Activity context as parent
        var activity = AgentActivitySource.StartActivity(operationName, ActivityKind.Internal, Activity.Current?.Context ?? default);
        
        if (activity != null && !string.IsNullOrEmpty(agentId))
        {
            activity.SetTag("agent.id", agentId);
            activity.SetTag("agent.operation", operationName);
            
            if (!string.IsNullOrEmpty(taskDescription))
            {
                activity.SetTag("agent.task", taskDescription.Length > 200 ? 
                    taskDescription.Substring(0, 200) + "..." : taskDescription);
            }
        }
        
        return activity;
    }
    
    /// <summary>
    /// Start a new activity for Orleans grain method calls
    /// </summary>
    public static Activity? StartGrainMethodActivity(string grainType, string methodName, string? grainId = null)
    {
        var operationName = $"{grainType}.{methodName}";
        
        // Ensure we inherit the current Activity context (Orleans span) as parent
        var activity = OrleansGrainActivitySource.StartActivity(operationName, ActivityKind.Internal, Activity.Current?.Context ?? default);
        
        if (activity != null)
        {
            activity.SetTag("orleans.grain.type", grainType);
            activity.SetTag("orleans.grain.method", methodName);
            activity.SetTag("span.kind", "internal");
            
            if (!string.IsNullOrEmpty(grainId))
            {
                activity.SetTag("orleans.grain.id", grainId);
            }
        }
        
        return activity;
    }
    
    /// <summary>
    /// Start a new activity for kernel operations
    /// </summary>
    public static Activity? StartKernelActivity(string operationName, string? agentId = null)
    {
        // Ensure we inherit the current Activity context as parent
        var activity = KernelActivitySource.StartActivity(operationName, ActivityKind.Internal, Activity.Current?.Context ?? default);
        
        if (activity != null)
        {
            activity.SetTag("kernel.operation", operationName);
            activity.SetTag("span.kind", "internal");
            
            if (!string.IsNullOrEmpty(agentId))
            {
                activity.SetTag("agent.id", agentId);
            }
        }
        
        return activity;
    }
    
    /// <summary>
    /// Add task-specific context to an activity
    /// </summary>
    public static void AddTaskContext(Activity? activity, string taskType, string? taskId = null, string[]? dependencies = null)
    {
        if (activity == null) return;
        
        activity.SetTag("task.type", taskType);
        
        if (!string.IsNullOrEmpty(taskId))
        {
            activity.SetTag("task.id", taskId);
        }
        
        if (dependencies != null && dependencies.Length > 0)
        {
            activity.SetTag("task.dependencies", string.Join(",", dependencies));
            activity.SetTag("task.dependency_count", dependencies.Length);
        }
    }
    
    /// <summary>
    /// Mark activity as successful with optional result information
    /// </summary>
    public static void SetSuccess(Activity? activity, string? resultSummary = null)
    {
        if (activity == null) return;
        
        activity.SetTag("result.success", "True");
        activity.SetStatus(ActivityStatusCode.Ok);
        
        if (!string.IsNullOrEmpty(resultSummary))
        {
            var summary = resultSummary.Length > 500 ? 
                resultSummary.Substring(0, 500) + "..." : resultSummary;
            activity.SetTag("result.summary", summary);
        }
    }
    
    /// <summary>
    /// Mark activity as failed with error information
    /// </summary>
    public static void SetError(Activity? activity, Exception exception)
    {
        if (activity == null) return;
        
        activity.SetTag("result.success", "False");
        activity.SetTag("error.type", exception.GetType().Name);
        activity.SetTag("error.message", exception.Message);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        
        if (exception.StackTrace != null)
        {
            activity.SetTag("error.stack", exception.StackTrace);
        }
    }
    
    /// <summary>
    /// Add function call context to an activity
    /// </summary>
    public static void AddFunctionCallContext(Activity? activity, string functionName, string? input = null, string? output = null)
    {
        if (activity == null) return;
        
        activity.SetTag("function.name", functionName);
        
        if (!string.IsNullOrEmpty(input))
        {
            var inputSummary = input.Length > 300 ? input.Substring(0, 300) + "..." : input;
            activity.SetTag("function.input", inputSummary);
        }
        
        if (!string.IsNullOrEmpty(output))
        {
            var outputSummary = output.Length > 300 ? output.Substring(0, 300) + "..." : output;
            activity.SetTag("function.output", outputSummary);
        }
    }
} 
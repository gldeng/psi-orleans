using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Hosting;
using PsiOrleans.Grains;
using PsiOrleans.Models;
using PsiOrleans.Services;
using PsiOrleans.Examples;
// OpenTelemetry imports for distributed tracing
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System.Diagnostics;

namespace PsiOrleans;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🌌 HyperEcho Hierarchical Agent System - State Machine Implementation");
        Console.WriteLine("====================================================================");
        Console.WriteLine("🚀 Demonstrating parent-child agent relationships with decision cycles");
        Console.WriteLine("📊 Task: Complex GDP analysis using hierarchical task delegation");
        Console.WriteLine();

        // Check for OpenAI API key
        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            Console.WriteLine("❌ Error: OPENAI_API_KEY environment variable is not set.");
            Console.WriteLine("📝 Please set your OpenAI API key:");
            Console.WriteLine("   export OPENAI_API_KEY='your-api-key-here'");
            Console.WriteLine();
            Console.WriteLine("💡 You can get an API key from: https://platform.openai.com/api-keys");
            return;
        }

        Console.WriteLine("✅ OpenAI API key found");
        Console.WriteLine($"🤖 Using GPT-4o-mini for hierarchical AI reasoning");
        Console.WriteLine();

        // Build and start the Orleans host with Semantic Kernel integration
        var host = CreateHostBuilder(args).Build();
        
        try
        {
            await host.StartAsync();
            Console.WriteLine("✅ Orleans hierarchical cluster started successfully");
            Console.WriteLine();

            // Get the Orleans client
            var client = host.Services.GetRequiredService<IClusterClient>();

            // Run the hierarchical agent system test - SWITCHED TO FULL ORCHESTRATOR TESTING
            await RunHierarchicalAgentTest(client);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"🔍 Inner exception: {ex.InnerException.Message}");
            }
            Console.WriteLine($"📋 Stack trace: {ex.StackTrace}");
        }
        finally
        {
            await host.StopAsync();
            Console.WriteLine("\n🔄 Orleans cluster stopped");
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                // Register function registry and related services
                services.AddSingleton<IKernelFunctionRegistry, KernelFunctionRegistry>();
                services.AddHostedService<FunctionRegistryInitializationService>();
                
                // Configure OpenTelemetry for distributed tracing
                services.AddOpenTelemetry()
                    .WithTracing(builder =>
                    {
                        builder
                            .AddSource("Microsoft.Orleans.Runtime")    // Orleans runtime ActivitySource
                            .AddSource("Microsoft.Orleans.Application") // Orleans application ActivitySource  
                            .AddSource("PsiOrleans.Agent")              // Custom ActivitySource for agent operations
                            .AddSource("PsiOrleans.Kernel")             // Custom ActivitySource for semantic kernel operations
                            .AddSource("PsiOrleans.Orleans.Grain")      // Custom ActivitySource for Orleans grain operations
                            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                                .AddService("psi-orleans-agents", "1.0.0"))
                            .AddHttpClientInstrumentation(options =>
                            {
                                options.RecordException = true;
                                options.FilterHttpRequestMessage = (httpRequestMessage) =>
                                {
                                    // Filter out health check and other noise
                                    return !httpRequestMessage.RequestUri?.AbsolutePath.Contains("/health") == true;
                                };
                            })
                            .AddOtlpExporter(options =>
                            {
                                // Export to OpenTelemetry Collector
                                options.Endpoint = new Uri("http://localhost:4315");
                                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                            });
                    });
            })
            .UseOrleans((context, builder) =>
            {
                builder
                    .UseLocalhostClustering()
                    .ConfigureLogging(logging => 
                    {
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Information);
                    })
                    // Enable Orleans OpenTelemetry integration
                    .AddActivityPropagation()
                    .ConfigureServices(services =>
                    {
                        // Register configurable kernel service
                        services.AddSingleton<IConfigurableKernelService, ConfigurableKernelService>();
                        
                        // Register manual function call processing services
                        services.AddSingleton<AgentCallbackManager>();
                        services.AddSingleton<IManualFunctionCallProcessor, ManualFunctionCallProcessor>();
                        
                        // Register multi-agent proxy service
                        services.AddSingleton<AgentProxyService>();
                        
                        // Register agent creation service
                        services.AddSingleton<AgentCreationService>();
                        
                        // Register function registration service (needs access to IClusterClient)
                        services.AddSingleton<FunctionRegistrationService>();
                        
                        // Register example service for demonstrations
                        services.AddSingleton<ConfigurableAgentExample>();
                        
                        // ====== Phase 2 Refactoring: State Machine Services ======
                        
                        // Register agent role configurator for role-based tool and prompt configuration
                        services.AddSingleton<IAgentRoleConfigurator, AgentRoleConfigurator>();
                        
                        // Register specialized state machine for sync direct execution pattern
                        services.AddSingleton<SpecializedStateMachine>();
                        
                        // Register orchestrator state machine for async event-driven execution pattern
                        services.AddSingleton<OrchestratorStateMachine>();
                        
                        // Register state machine factory for role-based state machine selection
                        services.AddSingleton<IStateMachineFactory, StateMachineFactory>();
                    });
            })
            .UseConsoleLifetime();

    /// <summary>
    /// Test the hierarchical agent system with complex orchestration.
    /// Enhanced to monitor orchestration progress and show complete dependency execution flow.
    /// </summary>
    private static async Task RunHierarchicalAgentTest(IClusterClient client)
    {
        Console.WriteLine("🚀 Starting Orleans hierarchical agent system test...");
        Console.WriteLine();
        
        try
        {
            // Test with a complex task that requires orchestration and multiple steps
            var complexTask = "Find US and New York state GDP in 2024. Calculate what percentage of US GDP was New York state.";
            
            Console.WriteLine($"📋 Complex Task: {complexTask}");
            Console.WriteLine();
            
            // Get the root hierarchical agent
            var rootAgent = client.GetGrain<IConfigurableAgentGrain>("root-agent");
            
            // Initialize the agent first with comprehensive tools for hierarchical processing
            var config = new AgentConfiguration
            {
                AgentName = "RootHierarchicalAgent",
                SystemPrompt = "You are a hierarchical AI agent that can analyze complex tasks and decide whether to handle them directly or delegate to specialized child agents.",
                Temperature = 0.1f,
                MaxTokens = 4000
            };
            
            // Initialize with tools that might be needed for orchestration and delegation
            var toolNames = new[] { "Tavily.search", "Math.Add", "Math.Multiply", "Math.Divide" };
            var initResult = await rootAgent.InitializeAsync(config, toolNames);
            
            if (!initResult.Success)
            {
                Console.WriteLine($"❌ Failed to initialize root agent: {initResult.Message}");
                return;
            }
            
            Console.WriteLine($"✅ Root agent initialized: {initResult.Message}");
            Console.WriteLine();
            
            Console.WriteLine("⚡ Executing complex task through hierarchical delegation...");
            var initialResult = await rootAgent.ProcessTaskAsync(complexTask);
            
            Console.WriteLine($"🎯 Initial Response: {initialResult}");
            Console.WriteLine();
            
            // Since this is async event-driven, we need to monitor the orchestration progress
            if (initialResult.Contains("Orchestration initiated"))
            {
                Console.WriteLine("🔄 Monitoring orchestration progress...");
                await MonitorOrchestrationProgress(rootAgent);
            }
            else
            {
                Console.WriteLine("✅ Task completed directly (non-orchestrated).");
            }
            
            Console.WriteLine();
            Console.WriteLine("✅ Hierarchical agent system test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hierarchical agent test failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner Exception: {ex.InnerException.Message}");
            }
        }
    }
    
    /// <summary>
    /// Monitor the progress of an orchestrated task by polling agent state.
    /// Shows dependency-aware execution flow and final aggregated results.
    /// </summary>
    private static async Task MonitorOrchestrationProgress(IConfigurableAgentGrain agent)
    {
        const int maxWaitTimeMinutes = 3;
        const int pollIntervalSeconds = 2;
        
        var startTime = DateTime.UtcNow;
        var maxWaitTime = TimeSpan.FromMinutes(maxWaitTimeMinutes);
        
        Console.WriteLine($"⏱️  Max wait time: {maxWaitTimeMinutes} minutes, polling every {pollIntervalSeconds} seconds");
        Console.WriteLine();
        
        var lastCompletedCount = 0;
        var hasShownSubTasks = false;
        var allCallbacksReceived = false;
        
        while (DateTime.UtcNow - startTime < maxWaitTime && !allCallbacksReceived)
        {
            try
            {
                // Get current agent state to monitor progress
                var stateInfo = await agent.GetStateInfoAsync();
                
                if (!string.IsNullOrEmpty(stateInfo))
                {
                    var (subTasks, pendingCallbacks, completedCallbacks) = ParseAgentState(stateInfo);
                    
                    // Show subtasks info when first detected
                    if (!hasShownSubTasks && subTasks.Count > 0)
                    {
                        Console.WriteLine($"📋 Detected {subTasks.Count} subtasks:");
                        for (int i = 0; i < subTasks.Count; i++)
                        {
                            var task = subTasks[i];
                            var deps = task.Dependencies.Any() ? $" (depends on: {string.Join(", ", task.Dependencies)})" : " (no dependencies)";
                            Console.WriteLine($"   {i + 1}. {task.Task}{deps}");
                        }
                        Console.WriteLine();
                        hasShownSubTasks = true;
                    }
                    
                    // Show progress updates
                    var completedTasks = subTasks.Where(t => t.Status == "Completed" || t.Status == "Failed").ToList();
                    if (completedTasks.Count != lastCompletedCount)
                    {
                        Console.WriteLine($"📊 Progress Update:");
                        Console.WriteLine($"   Subtasks: {completedTasks.Count}/{subTasks.Count} completed");
                        Console.WriteLine($"   Pending Callbacks: {pendingCallbacks}");
                        Console.WriteLine($"   Completed Callbacks: {completedCallbacks.Count}");
                        
                        // Show newly completed tasks
                        var newlyCompleted = completedTasks.Skip(lastCompletedCount).ToList();
                        foreach (var completed in newlyCompleted)
                        {
                            var status = completed.Status == "Completed" ? "✅" : "❌";
                            Console.WriteLine($"   {status} {completed.Task}");
                            if (!string.IsNullOrEmpty(completed.Result))
                            {
                                // Truncate long results for readability
                                var result = completed.Result.Length > 150 
                                    ? completed.Result.Substring(0, 150) + "..."
                                    : completed.Result;
                                Console.WriteLine($"      Result: {result}");
                            }
                        }
                        
                        lastCompletedCount = completedTasks.Count;
                        Console.WriteLine();
                    }
                    
                    // Check if orchestration is complete
                    if (subTasks.Count > 0 && completedTasks.Count == subTasks.Count && pendingCallbacks == 0)
                    {
                        Console.WriteLine("🎉 All subtasks completed and callbacks processed!");
                        
                        // Show final aggregated results
                        if (completedCallbacks.Count > 0)
                        {
                            Console.WriteLine("📋 Final Results Summary:");
                            foreach (var callback in completedCallbacks)
                            {
                                var status = callback.IsSuccess ? "✅" : "❌";
                                Console.WriteLine($"   {status} {callback.Task}");
                                if (!string.IsNullOrEmpty(callback.Result))
                                {
                                    Console.WriteLine($"      {callback.Result}");
                                }
                            }
                        }
                        
                        allCallbacksReceived = true;
                        Console.WriteLine("✅ Orchestration completed successfully!");
                        return;
                    }
                }
                
                // Wait before next poll
                await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds));
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Error monitoring progress: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds));
            }
        }
        
        if (!allCallbacksReceived)
        {
            Console.WriteLine($"⏰ Monitoring timeout reached ({maxWaitTimeMinutes} minutes)");
            Console.WriteLine("   This may indicate that some callbacks are still pending or failed to process.");
        }
    }
    
    /// <summary>
    /// Parse agent state information into structured data for monitoring.
    /// </summary>
    private static (List<SubTaskInfo> subTasks, int pendingCallbacks, List<CallbackInfo> completedCallbacks) ParseAgentState(string stateInfo)
    {
        var subTasks = new List<SubTaskInfo>();
        var completedCallbacks = new List<CallbackInfo>();
        var pendingCallbacks = 0;
        
        try
        {
            var lines = stateInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            bool inSubTasksSection = false;
            bool inPendingCallbacksSection = false;
            bool inCompletedCallbacksSection = false;
            SubTaskInfo? currentSubTask = null;
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                // Section detection
                if (line.StartsWith("Current SubTasks"))
                {
                    inSubTasksSection = true;
                    inPendingCallbacksSection = false;
                    inCompletedCallbacksSection = false;
                    continue;
                }
                else if (line.StartsWith("Pending Callbacks"))
                {
                    if (currentSubTask != null)
                    {
                        subTasks.Add(currentSubTask);
                        currentSubTask = null;
                    }
                    inSubTasksSection = false;
                    inPendingCallbacksSection = true;
                    inCompletedCallbacksSection = false;
                    
                    // Extract pending callback count
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"Pending Callbacks \((\d+)\)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
                    {
                        pendingCallbacks = count;
                    }
                    continue;
                }
                else if (line.StartsWith("Completed Callbacks"))
                {
                    if (currentSubTask != null)
                    {
                        subTasks.Add(currentSubTask);
                        currentSubTask = null;
                    }
                    inSubTasksSection = false;
                    inPendingCallbacksSection = false;
                    inCompletedCallbacksSection = true;
                    continue;
                }
                else if (line.StartsWith("Child Agents"))
                {
                    if (currentSubTask != null)
                    {
                        subTasks.Add(currentSubTask);
                        currentSubTask = null;
                    }
                    inSubTasksSection = false;
                    inPendingCallbacksSection = false;
                    inCompletedCallbacksSection = false;
                    continue;
                }
                
                // Parse subtasks
                if (inSubTasksSection)
                {
                    // Parse subtask line (format: "  1. [Status] Task description")
                    if (line.Contains("[") && line.Contains("]"))
                    {
                        if (currentSubTask != null)
                        {
                            subTasks.Add(currentSubTask);
                        }
                        
                        var statusStart = line.IndexOf('[') + 1;
                        var statusEnd = line.IndexOf(']');
                        var status = statusEnd > statusStart ? line.Substring(statusStart, statusEnd - statusStart) : "";
                        
                        var taskDescription = statusEnd + 1 < line.Length ? line.Substring(statusEnd + 1).Trim() : "";
                        
                        currentSubTask = new SubTaskInfo
                        {
                            Task = taskDescription,
                            Status = status,
                            Dependencies = new List<string>()
                        };
                    }
                    else if (currentSubTask != null)
                    {
                        // Parse additional info for current subtask
                        if (line.Contains("Dependencies:"))
                        {
                            var deps = line.Substring(line.IndexOf("Dependencies:") + 13).Trim();
                            if (!string.IsNullOrEmpty(deps) && deps != "none")
                            {
                                currentSubTask.Dependencies = deps.Split(',').Select(d => d.Trim()).ToList();
                            }
                        }
                        else if (line.Contains("Result:"))
                        {
                            currentSubTask.Result = line.Substring(line.IndexOf("Result:") + 7).Trim();
                        }
                    }
                }
                
                // Parse completed callbacks
                if (inCompletedCallbacksSection)
                {
                    if (line.Contains("✅") || line.Contains("❌"))
                    {
                        var isSuccess = line.Contains("✅");
                        var task = line.Replace("✅", "").Replace("❌", "").Trim();
                        
                        var callback = new CallbackInfo
                        {
                            Task = task,
                            IsSuccess = isSuccess,
                            Result = ""
                        };
                        
                        // Look for result on next line
                        if (i + 1 < lines.Length && lines[i + 1].Trim().StartsWith("Result:"))
                        {
                            callback.Result = lines[i + 1].Substring(lines[i + 1].IndexOf("Result:") + 7).Trim();
                            i++; // Skip next line since we processed it
                        }
                        
                        completedCallbacks.Add(callback);
                    }
                }
            }
            
            // Add last subtask if any
            if (currentSubTask != null)
            {
                subTasks.Add(currentSubTask);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error parsing agent state: {ex.Message}");
        }
        
        return (subTasks, pendingCallbacks, completedCallbacks);
    }
    
    /// <summary>
    /// Simple DTO for subtask information during monitoring.
    /// </summary>
    private class SubTaskInfo
    {
        public string Task { get; set; } = "";
        public string Status { get; set; } = "";
        public string Result { get; set; } = "";
        public List<string> Dependencies { get; set; } = new();
    }
    
    /// <summary>
    /// Simple DTO for callback information during monitoring.
    /// </summary>
    private class CallbackInfo
    {
        public string Task { get; set; } = "";
        public bool IsSuccess { get; set; } = false;
        public string Result { get; set; } = "";
    }
} 
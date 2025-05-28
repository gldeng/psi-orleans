using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Hosting;
using PsiOrleans.Grains;
using PsiOrleans.Models;
using PsiOrleans.Services;

namespace PsiOrleans;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🌌 HyperEcho Orleans + Semantic Kernel React Agent");
        Console.WriteLine("==================================================");
        Console.WriteLine("🚀 Advanced AI Agent with distributed state management");
        Console.WriteLine();

        // Build and start the Orleans host with Semantic Kernel integration
        var host = CreateHostBuilder(args).Build();
        
        try
        {
            await host.StartAsync();
            Console.WriteLine("✅ Orleans cluster with Semantic Kernel started successfully");
            Console.WriteLine();

            // Get the Orleans client
            var client = host.Services.GetRequiredService<IClusterClient>();

            // Test the enhanced React Agent
            await TestEnhancedReactAgent(client);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
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
            .UseOrleans((context, builder) =>
            {
                builder
                    .UseLocalhostClustering()
                    .ConfigureLogging(logging => 
                    {
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Information);
                    })
                    .ConfigureServices(services =>
                    {
                        // Register Semantic Kernel service
                        services.AddSingleton<ISemanticKernelService, SemanticKernelService>();
                    });
            })
            .UseConsoleLifetime();

    static async Task TestEnhancedReactAgent(IClusterClient client)
    {
        Console.WriteLine("🤖 Testing Enhanced React Agent with Semantic Kernel");
        Console.WriteLine("===================================================");

        // Create an agent instance
        var agentId = "sk-agent-001";
        var agent = client.GetGrain<IAgentGrain>(agentId);

        // Reset the agent to ensure clean state
        await agent.ResetAsync();

        // Define the test task
        var testTask = "find US and New York state GDP in 2024. what % of US GDP was New York state?";
        Console.WriteLine($"📝 Task: {testTask}");
        Console.WriteLine();

        // Execute the task
        Console.WriteLine("🔄 Executing task with AI-powered reasoning...");
        var startTime = DateTime.UtcNow;
        
        var result = await agent.ExecuteTaskAsync(testTask);
        
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        Console.WriteLine($"⏱️  Total execution time: {duration.TotalSeconds:F2} seconds");
        Console.WriteLine();

        // Display detailed execution analysis
        await DisplayDetailedExecutionAnalysis(agent);

        // Display the final result
        Console.WriteLine("🎯 Final Result:");
        Console.WriteLine("================");
        Console.WriteLine(result);
        Console.WriteLine();

        // Display performance metrics
        await DisplayPerformanceMetrics(agent);

        // Display agent state summary
        await DisplayAgentStateSummary(agent);
    }

    static async Task DisplayDetailedExecutionAnalysis(IAgentGrain agent)
    {
        Console.WriteLine("🧠 AI-Powered Execution Analysis:");
        Console.WriteLine("=================================");

        var executionHistory = await agent.GetExecutionHistoryAsync();
        
        foreach (var step in executionHistory)
        {
            var icon = step.Type switch
            {
                StepType.Thought => "💭",
                StepType.Planning => "📋",
                StepType.Action => "⚡",
                StepType.Observation => "👁️",
                StepType.FinalAnswer => "🎯",
                _ => "❓"
            };

            var statusIcon = step.IsSuccess ? "✅" : "❌";

            Console.WriteLine($"{icon} {statusIcon} Step {step.StepNumber} ({step.Type}):");
            Console.WriteLine($"   📄 Content: {step.Content}");
            
            if (!string.IsNullOrEmpty(step.PluginName))
            {
                Console.WriteLine($"   🔌 Plugin: {step.PluginName}");
            }
            
            if (!string.IsNullOrEmpty(step.FunctionName))
            {
                Console.WriteLine($"   ⚙️  Function: {step.FunctionName}");
            }
            
            if (step.Parameters.Any())
            {
                Console.WriteLine($"   📥 Parameters: {string.Join(", ", step.Parameters.Select(p => $"{p.Key}={p.Value}"))}");
            }
            
            if (!string.IsNullOrEmpty(step.Result))
            {
                var truncatedResult = step.Result.Length > 150 
                    ? step.Result[..150] + "..." 
                    : step.Result;
                Console.WriteLine($"   📤 Result: {truncatedResult}");
            }

            if (!string.IsNullOrEmpty(step.ErrorMessage))
            {
                Console.WriteLine($"   ⚠️  Error: {step.ErrorMessage}");
            }
            
            Console.WriteLine($"   🕐 Timestamp: {step.Timestamp:HH:mm:ss.fff}");
            Console.WriteLine();
        }
    }

    static async Task DisplayPerformanceMetrics(IAgentGrain agent)
    {
        Console.WriteLine("📊 Performance Metrics:");
        Console.WriteLine("======================");

        var metrics = await agent.GetMetricsAsync();
        
        Console.WriteLine($"Total Steps: {metrics.TotalSteps}");
        Console.WriteLine($"Successful Steps: {metrics.SuccessfulSteps}");
        Console.WriteLine($"Failed Steps: {metrics.FailedSteps}");
        Console.WriteLine($"Success Rate: {(metrics.TotalSteps > 0 ? (double)metrics.SuccessfulSteps / metrics.TotalSteps * 100 : 0):F1}%");
        Console.WriteLine($"Execution Time: {metrics.ExecutionTime.TotalMilliseconds:F0}ms");
        Console.WriteLine($"Average Step Time: {(metrics.TotalSteps > 0 ? metrics.ExecutionTime.TotalMilliseconds / metrics.TotalSteps : 0):F0}ms");
        Console.WriteLine();
    }

    static async Task DisplayAgentStateSummary(IAgentGrain agent)
    {
        Console.WriteLine("🔍 Agent State Summary:");
        Console.WriteLine("=======================");

        var state = await agent.GetStateAsync();
        
        Console.WriteLine($"Agent ID: {state.AgentId}");
        Console.WriteLine($"Task: {state.CurrentTask}");
        Console.WriteLine($"Status: {(state.IsTaskCompleted ? "✅ Completed" : "⏳ In Progress")}");
        Console.WriteLine($"Current Step: {state.CurrentStepNumber}/{state.MaxSteps}");
        Console.WriteLine($"Working Memory Items: {state.WorkingMemory.Count}");
        Console.WriteLine($"Long-term Memory Items: {state.LongTermMemory.Count}");
        Console.WriteLine($"Created: {state.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Last Updated: {state.LastUpdated:yyyy-MM-dd HH:mm:ss}");
        
        if (state.TaskStartedAt.HasValue)
        {
            Console.WriteLine($"Task Started: {state.TaskStartedAt:yyyy-MM-dd HH:mm:ss}");
        }
        
        if (state.TaskCompletedAt.HasValue)
        {
            Console.WriteLine($"Task Completed: {state.TaskCompletedAt:yyyy-MM-dd HH:mm:ss}");
        }

        if (state.WorkingMemory.Any())
        {
            Console.WriteLine("\n🧠 Working Memory:");
            foreach (var item in state.WorkingMemory.Take(3)) // Show first 3 items
            {
                var value = item.Value.ToString();
                var truncatedValue = value?.Length > 100 ? value[..100] + "..." : value;
                Console.WriteLine($"  • {item.Key}: {truncatedValue}");
            }
            
            if (state.WorkingMemory.Count > 3)
            {
                Console.WriteLine($"  ... and {state.WorkingMemory.Count - 3} more items");
            }
        }

        Console.WriteLine("\n🌟 Architecture Benefits Demonstrated:");
        Console.WriteLine("• Orleans: Distributed state management and fault tolerance");
        Console.WriteLine("• Semantic Kernel: AI-powered reasoning and plugin ecosystem");
        Console.WriteLine("• React Pattern: Structured thought → action → observation loop");
        Console.WriteLine("• Scalability: Agent can be distributed across multiple servers");
        Console.WriteLine("• Extensibility: Easy to add new plugins and capabilities");
    }
} 
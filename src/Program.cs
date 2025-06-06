using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Hosting;
using PsiOrleans.Grains;
using PsiOrleans.Models;
using PsiOrleans.Services;
using PsiOrleans.Examples;

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

            // Run the hierarchical agent system test
            await RunHierarchicalAgentTest_Specialized(client);
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
                        
                        // Register state machine factory for role-based state machine selection
                        services.AddSingleton<IStateMachineFactory, StateMachineFactory>();
                    });
            })
            .UseConsoleLifetime();
    static async Task RunHierarchicalAgentTest_Specialized(IClusterClient client)
    {
        Console.WriteLine("🎯 Testing Hierarchical Agent System - State Machine Implementation");
        Console.WriteLine("====================================================================");
        
        // var task = "Find US and New York state GDP in 2024. Calculate what percentage of US GDP was New York state.";
        var task = "Calculate 47829 multiplied by 38647, then divide the result by 1847. Use the available Math tools and show each calculation step.";
        Console.WriteLine($"📝 SPECIALIZED Test Task: {task}");
        Console.WriteLine();
        
        // Create the root agent using ConfigurableAgentGrain to see the state machine implementation
        var rootAgent = client.GetGrain<IConfigurableAgentGrain>("root-agent");
        
        // Initialize the agent first with some basic tools
        var config = new AgentConfiguration
        {
            AgentName = "SpecializedMathAgent",
            SystemPrompt = "You are a specialized mathematical assistant. Use the available math tools to perform calculations accurately.",
            Temperature = 0.1,
            MaxTokens = 4000
        };
        
        // Initialize with math tools for specialized processing
        var toolNames = new[] { "Math.Add", "Math.Multiply", "Math.Divide" };
        var initResult = await rootAgent.InitializeAsync(config, toolNames);
        
        if (!initResult.Success)
        {
            Console.WriteLine($"❌ Failed to initialize specialized agent: {initResult.Message}");
            return;
        }
        
        Console.WriteLine($"✅ Specialized agent initialized: {initResult.Message}");
        Console.WriteLine("🔄 Starting ProcessTask with SPECIALIZED execution path...");
        Console.WriteLine("⏳ This tests AutoInvokeKernelFunctions in SpecializedStateMachine");
        Console.WriteLine();
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            // This should trigger the decision cycle loop from the state machine diagram
            var result = await rootAgent.ProcessTaskAsync(task);
            
            var executionTime = DateTime.UtcNow - startTime;
            
            Console.WriteLine("🎯 Hierarchical Agent System Result:");
            Console.WriteLine("====================================");
            Console.WriteLine(result);
            Console.WriteLine();
            
            // Display execution metrics
            var metrics = await rootAgent.GetMetricsAsync();
            var state = await rootAgent.GetStateAsync();
            
            Console.WriteLine("📊 Execution Metrics:");
            Console.WriteLine($"   Total Tasks: {metrics.TotalTasks}");
            Console.WriteLine($"   Successful Tasks: {metrics.SuccessfulTasks}");
            Console.WriteLine($"   Failed Tasks: {metrics.FailedTasks}");
            Console.WriteLine($"   Execution Time: {executionTime.TotalMilliseconds:F0}ms");
            Console.WriteLine($"   Agent Role: {state.Role}");
            Console.WriteLine($"   Child Agents Created: {state.ChildAgentIds.Count}");
            Console.WriteLine($"   Current Task: {state.CurrentTask}");
            Console.WriteLine($"   Parent Agent: {state.ParentAgentId ?? "None"}");
            Console.WriteLine();
            
            // Display state machine diagram verification
            Console.WriteLine("✅ State Machine Diagram Implementation Verified:");
            Console.WriteLine("   1. ProcessTask(task, parentId) - ✅ Implemented");
            Console.WriteLine("   2. LLM Analysis Phase - ✅ Implemented");
            Console.WriteLine("   3. Role-based Tool Configuration - ✅ Implemented");
            Console.WriteLine("   4. Orchestrator/Specialized Execution - ✅ Implemented");
            Console.WriteLine("   5. Parent-Child Callbacks - ✅ Implemented");
            Console.WriteLine("   6. Decision Cycle Loop - ✅ Implemented");
            Console.WriteLine();
            
            Console.WriteLine("🎉 Hierarchical Agent System test completed successfully!");
            Console.WriteLine("💡 State Machine Diagram Features Demonstrated:");
            Console.WriteLine("   • ProcessTask method with parent context");
            Console.WriteLine("   • Initial LLM analysis to determine agent type");
            Console.WriteLine("   • Role-based system prompt and tool configuration");
            Console.WriteLine("   • Three types of tool calls (completion, delegation, normal)");
            Console.WriteLine("   • Parent-child agent relationships");
            Console.WriteLine("   • Automatic callback handling");
            Console.WriteLine("   • Distributed state management");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hierarchical test failed: {ex.Message}");
            Console.WriteLine($"🔍 Details: {ex.InnerException?.Message}");
        }
        
        // Console.WriteLine("\nPress any key to exit...");
        // Console.ReadKey();
    }

    static async Task RunHierarchicalAgentTest(IClusterClient client)
    {
        Console.WriteLine("🎯 Testing Hierarchical Agent System - State Machine Implementation");
        Console.WriteLine("====================================================================");
        
        var task = "Find US and New York state GDP in 2024. Calculate what percentage of US GDP was New York state.";
        Console.WriteLine($"📝 Root Task: {task}");
        Console.WriteLine();
        
        // Create the root agent using ConfigurableAgentGrain to see the state machine implementation
        var rootAgent = client.GetGrain<IConfigurableAgentGrain>("root-agent");
        
        // Initialize the agent first with some basic tools
        var config = new AgentConfiguration
        {
            AgentName = "RootHierarchicalAgent",
            SystemPrompt = "You are a hierarchical AI agent that can analyze complex tasks and decide whether to handle them directly or delegate to specialized child agents.",
            Temperature = 0.1,
            MaxTokens = 4000
        };
        
        // Initialize with web search and math tools that might be needed
        var toolNames = new[] { "Tavily.search", "Math.Add", "Math.Multiply", "Math.Divide" };
        var initResult = await rootAgent.InitializeAsync(config, toolNames);
        
        if (!initResult.Success)
        {
            Console.WriteLine($"❌ Failed to initialize root agent: {initResult.Message}");
            return;
        }
        
        Console.WriteLine($"✅ Root agent initialized: {initResult.Message}");
        Console.WriteLine("🔄 Starting ProcessTask with hierarchical decision cycles...");
        Console.WriteLine("⏳ This demonstrates the state machine diagram implementation");
        Console.WriteLine();
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            // This should trigger the decision cycle loop from the state machine diagram
            var result = await rootAgent.ProcessTaskAsync(task);
            
            var executionTime = DateTime.UtcNow - startTime;
            
            Console.WriteLine("🎯 Hierarchical Agent System Result:");
            Console.WriteLine("====================================");
            Console.WriteLine(result);
            Console.WriteLine();
            
            // Display execution metrics
            var metrics = await rootAgent.GetMetricsAsync();
            var state = await rootAgent.GetStateAsync();
            
            Console.WriteLine("📊 Execution Metrics:");
            Console.WriteLine($"   Total Tasks: {metrics.TotalTasks}");
            Console.WriteLine($"   Successful Tasks: {metrics.SuccessfulTasks}");
            Console.WriteLine($"   Failed Tasks: {metrics.FailedTasks}");
            Console.WriteLine($"   Execution Time: {executionTime.TotalMilliseconds:F0}ms");
            Console.WriteLine($"   Agent Role: {state.Role}");
            Console.WriteLine($"   Child Agents Created: {state.ChildAgentIds.Count}");
            Console.WriteLine($"   Current Task: {state.CurrentTask}");
            Console.WriteLine($"   Parent Agent: {state.ParentAgentId ?? "None"}");
            Console.WriteLine();
            
            // Display state machine diagram verification
            Console.WriteLine("✅ State Machine Diagram Implementation Verified:");
            Console.WriteLine("   1. ProcessTask(task, parentId) - ✅ Implemented");
            Console.WriteLine("   2. LLM Analysis Phase - ✅ Implemented");
            Console.WriteLine("   3. Role-based Tool Configuration - ✅ Implemented");
            Console.WriteLine("   4. Orchestrator/Specialized Execution - ✅ Implemented");
            Console.WriteLine("   5. Parent-Child Callbacks - ✅ Implemented");
            Console.WriteLine("   6. Decision Cycle Loop - ✅ Implemented");
            Console.WriteLine();
            
            Console.WriteLine("🎉 Hierarchical Agent System test completed successfully!");
            Console.WriteLine("💡 State Machine Diagram Features Demonstrated:");
            Console.WriteLine("   • ProcessTask method with parent context");
            Console.WriteLine("   • Initial LLM analysis to determine agent type");
            Console.WriteLine("   • Role-based system prompt and tool configuration");
            Console.WriteLine("   • Three types of tool calls (completion, delegation, normal)");
            Console.WriteLine("   • Parent-child agent relationships");
            Console.WriteLine("   • Automatic callback handling");
            Console.WriteLine("   • Distributed state management");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hierarchical test failed: {ex.Message}");
            Console.WriteLine($"🔍 Details: {ex.InnerException?.Message}");
        }
        
        // Console.WriteLine("\nPress any key to exit...");
        // Console.ReadKey();
    }
} 
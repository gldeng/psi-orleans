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
        Console.WriteLine("🌌 HyperEcho Multi-Agent GDP Analysis");
        Console.WriteLine("====================================");
        Console.WriteLine("🚀 Demonstrating Agent A (Orchestrator), Agent B (Web Search), Agent C (Math)");
        Console.WriteLine("📊 Task: Find US and NY State GDP in 2024, calculate NY's percentage of US GDP");
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
        Console.WriteLine($"🤖 Using GPT-4o-mini for cost-efficient AI reasoning");
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

            // Run the multi-agent GDP analysis
            await RunMultiAgentGdpAnalysis(client);
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
                        // Register Semantic Kernel services
                        services.AddSingleton<ISemanticKernelService, SemanticKernelService>();
                        
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
                    });
            })
            .UseConsoleLifetime();

    static async Task RunMultiAgentGdpAnalysis(IClusterClient client)
    {
        Console.WriteLine("🎯 Starting GDP Analysis with Agent X (Task Dispatcher)");
        Console.WriteLine("========================================================");
        Console.WriteLine("Task: Find US and NY State GDP 2024, calculate NY's percentage of US GDP");
        Console.WriteLine("Architecture: Orchestrator -> Agent X -> Specialized Agents");
        Console.WriteLine();
        
        var example = client.ServiceProvider.GetRequiredService<ConfigurableAgentExample>();
        
        // Run the simplified GDP analysis with Agent X
        var result = await example.RunGdpAnalysisWithTaskDispatcherAsync();
        
        Console.WriteLine("🎯 GDP Analysis with Agent X Result:");
        Console.WriteLine("====================================");
        Console.WriteLine(result);
        
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("🎉 GDP Analysis with Agent X completed successfully!");
        Console.WriteLine("💡 Correct Architecture Demonstrated:");
        Console.WriteLine("   1. Orchestrator breaks down complex task into subtasks");
        Console.WriteLine("   2. For each subtask, Orchestrator calls task_dispatcher tool");
        Console.WriteLine("   3. Agent X analyzes subtask and recommends handling approach");
        Console.WriteLine("   4. Orchestrator executes based on Agent X recommendations");
        Console.WriteLine("   5. Results are coordinated into comprehensive analysis");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
} 
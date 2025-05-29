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
        Console.WriteLine("🌌 HyperEcho Orleans + Semantic Kernel Configurable Agents");
        Console.WriteLine("==========================================================");
        Console.WriteLine("🚀 Advanced AI Agent with distributed state management");
        Console.WriteLine("🔧 Configurable Agents - Initialize with Custom Prompts & Tools");
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

            // Present test case menu
            await PresentTestCaseMenu(client);
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
                services.AddSingleton<FunctionRegistrationService>();
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
                        
                        // Register example service for demonstrations
                        services.AddSingleton<ConfigurableAgentExample>();
                    });
            })
            .UseConsoleLifetime();

    static async Task PresentTestCaseMenu(IClusterClient client)
    {
        while (true)
        {
            Console.WriteLine("🤖 HyperEcho Configurable Agent Menu");
            Console.WriteLine("====================================");
            Console.WriteLine("🔧 Configurable Agents:");
            Console.WriteLine("  1. Data Analyst Agent (Custom Math Tools)");
            Console.WriteLine("  2. Creative Writing Agent (Text Processing Tools)");
            Console.WriteLine("  3. Research Agent (Mathematical Plugin)");
            Console.WriteLine("  4. Customer Service Agent (Support Tools)");
            Console.WriteLine("  5. Conversation Agent (Continuity Demo)");
            Console.WriteLine("  6. Hybrid Agent (Functions + Plugins)");
            Console.WriteLine("  7. View All Agent Metrics");
            Console.WriteLine("  8. Show Available Functions from Registry");
            Console.WriteLine();
            Console.WriteLine("🚀 RECOMMENDED:");
            Console.WriteLine("  9. Unified Tool Naming Example");
            Console.WriteLine();
            Console.WriteLine("  0. Exit");
            Console.WriteLine();

            Console.Write("Enter your choice: ");
            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await TestDataAnalystAgent(client);
                        break;
                    case "2":
                        await TestCreativeWritingAgent(client);
                        break;
                    case "3":
                        await TestResearchAgent(client);
                        break;
                    case "4":
                        await TestCustomerServiceAgent(client);
                        break;
                    case "5":
                        await TestConversationAgent(client);
                        break;
                    case "6":
                        await TestHybridAgent(client);
                        break;
                    case "7":
                        await ViewAllAgentMetrics(client);
                        break;
                    case "8":
                        await ShowAvailableFunctions(client);
                        break;
                    case "9":
                        await TestUnifiedToolNamingExample(client);
                        break;
                    case "0":
                        Console.WriteLine("👋 Exiting HyperEcho Agent System. Goodbye!");
                        return;
                    default:
                        Console.WriteLine("❌ Invalid choice. Please enter a valid option (0-9).");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error executing choice {choice}: {ex.Message}");
            }

            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("Press any key to return to menu...");
            Console.ReadKey();
            Console.Clear();
        }
    }

    // Configurable agent test methods
    static async Task TestDataAnalystAgent(IClusterClient client)
    {
        Console.WriteLine("🔢 Testing Configurable Data Analyst Agent");
        Console.WriteLine("==========================================");
        
        var example = client.ServiceProvider.GetRequiredService<ConfigurableAgentExample>();
        var result = await example.RunDataAnalystAgentAsync();
        
        Console.WriteLine("📊 Data Analyst Agent Result:");
        Console.WriteLine("=============================");
        Console.WriteLine(result);
    }

    static async Task TestCreativeWritingAgent(IClusterClient client)
    {
        Console.WriteLine("✍️ Testing Configurable Creative Writing Agent");
        Console.WriteLine("==============================================");
        
        var example = client.ServiceProvider.GetRequiredService<ConfigurableAgentExample>();
        var result = await example.RunCreativeWritingAgentAsync();
        
        Console.WriteLine("📝 Creative Writing Agent Result:");
        Console.WriteLine("=================================");
        Console.WriteLine(result);
    }

    static async Task TestResearchAgent(IClusterClient client)
    {
        Console.WriteLine("🔍 Testing Configurable Research Agent");
        Console.WriteLine("=====================================");
        
        var example = client.ServiceProvider.GetRequiredService<ConfigurableAgentExample>();
        var result = await example.RunResearchAgentAsync();
        
        Console.WriteLine("📚 Research Agent Result:");
        Console.WriteLine("========================");
        Console.WriteLine(result);
    }

    static async Task TestCustomerServiceAgent(IClusterClient client)
    {
        Console.WriteLine("🎧 Testing Configurable Customer Service Agent");
        Console.WriteLine("=============================================");
        
        var example = client.ServiceProvider.GetRequiredService<ConfigurableAgentExample>();
        var result = await example.RunCustomerServiceAgentAsync();
        
        Console.WriteLine("🛠️ Customer Service Agent Result:");
        Console.WriteLine("=================================");
        Console.WriteLine(result);
    }

    static async Task TestConversationAgent(IClusterClient client)
    {
        Console.WriteLine("💬 Testing Configurable Conversation Agent");
        Console.WriteLine("==========================================");
        
        var example = client.ServiceProvider.GetRequiredService<ConfigurableAgentExample>();
        var result = await example.DemonstrateConversationAsync();
        
        Console.WriteLine("🗨️ Conversation Agent Result:");
        Console.WriteLine("=============================");
        Console.WriteLine(result);
    }

    static async Task TestHybridAgent(IClusterClient client)
    {
        Console.WriteLine("🤖 Testing Configurable Hybrid Agent");
        Console.WriteLine("=====================================");
        
        var example = client.ServiceProvider.GetRequiredService<ConfigurableAgentExample>();
        var result = await example.RunHybridAgentAsync();
        
        Console.WriteLine("📊 Hybrid Agent Result:");
        Console.WriteLine("========================");
        Console.WriteLine(result);
    }

    static async Task ViewAllAgentMetrics(IClusterClient client)
    {
        Console.WriteLine("📊 All Agent Metrics");
        Console.WriteLine("===================");
        
        var example = client.ServiceProvider.GetRequiredService<ConfigurableAgentExample>();
        var result = await example.GetAgentMetricsAsync();
        
        Console.WriteLine("📈 Agent Performance Metrics:");
        Console.WriteLine("============================");
        Console.WriteLine(result);
    }

    static async Task ShowAvailableFunctions(IClusterClient client)
    {
        Console.WriteLine("📋 Showing Available Functions from Registry");
        Console.WriteLine("==========================================");
        
        var example = client.ServiceProvider.GetRequiredService<ConfigurableAgentExample>();
        var result = await example.ShowAvailableFunctionsAsync();
        
        Console.WriteLine("🔧 Registry Functions:");
        Console.WriteLine("======================");
        Console.WriteLine(result);
    }

    static async Task TestUnifiedToolNamingExample(IClusterClient client)
    {
        Console.WriteLine("🚀 Testing Unified Tool Naming Example");
        Console.WriteLine("=======================================");
        
        var example = client.ServiceProvider.GetRequiredService<ConfigurableAgentExample>();
        var result = await example.RunUnifiedToolNamingExampleAsync();
        
        Console.WriteLine("📝 Unified Tool Naming Example Result:");
        Console.WriteLine("=======================================");
        Console.WriteLine(result);
    }
} 
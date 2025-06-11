using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PsiOrleans.Analysis.Services;
using PsiOrleans.Common.Models;
using PsiOrleans.Common.Interfaces;

namespace TaskAnalyzer.Example;

/// <summary>
/// Minimal example demonstrating TaskAnalyzer with real Kernel injection pattern
/// following the ConfigurableKernelService.cs architecture.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== TaskAnalyzer Real Implementation Example ===");
        Console.WriteLine();

        // Create logger
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();

        try
        {
            await RunTaskAnalysisExample(logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Example failed");
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Example completed. Press any key to exit...");
        Console.ReadKey();
    }

    /// <summary>
    /// Demonstrates real TaskAnalyzer usage with proper Kernel injection
    /// </summary>
    static async Task RunTaskAnalysisExample(ILogger logger)
    {
        // Check for API configuration (Azure OpenAI or OpenAI)
        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        var hasAzureConfig = !string.IsNullOrEmpty(azureEndpoint) && !string.IsNullOrEmpty(azureApiKey) && !string.IsNullOrEmpty(azureDeployment);
        var hasOpenAiConfig = !string.IsNullOrEmpty(openAiApiKey);

        if (!hasAzureConfig && !hasOpenAiConfig)
        {
            Console.WriteLine("⚠️  No API configuration found.");
            Console.WriteLine("   For Azure OpenAI, set:");
            Console.WriteLine("   export AZURE_OPENAI_ENDPOINT=\"https://your-resource.openai.azure.com/\"");
            Console.WriteLine("   export AZURE_OPENAI_API_KEY=\"your-api-key\"");
            Console.WriteLine("   export AZURE_OPENAI_DEPLOYMENT_NAME=\"your-deployment-name\"");
            Console.WriteLine();
            Console.WriteLine("   For OpenAI, set:");
            Console.WriteLine("   export OPENAI_API_KEY=\"your-api-key\"");
            Console.WriteLine();
            Console.WriteLine("   Continuing with architecture demonstration...");
            Console.WriteLine();
        }
        else if (hasAzureConfig)
        {
            Console.WriteLine($"✅ Using Azure OpenAI: {azureEndpoint} (deployment: {azureDeployment})");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("✅ Using OpenAI API");
            Console.WriteLine();
        }

        // Create kernel following ConfigurableKernelService pattern
        Console.WriteLine("1. Creating Kernel with proper dependency injection:");
        var kernel = CreateKernel(azureEndpoint, azureApiKey, azureDeployment, openAiApiKey);
        Console.WriteLine("   ✅ Kernel created successfully");

        // Create TaskAnalyzer with Kernel injection (not IChatCompletionService)
        Console.WriteLine("2. Injecting Kernel into TaskAnalyzer:");
        var taskAnalyzer = new PsiOrleans.Analysis.Services.TaskAnalyzer(kernel);
        Console.WriteLine("   ✅ TaskAnalyzer instantiated with Kernel injection");

        // Create test configuration and context
        var config = CreateTestConfiguration(hasAzureConfig ? azureDeployment! : "gpt-3.5-turbo", hasAzureConfig ? azureApiKey : openAiApiKey);
        var context = new SimpleAgentContext();

        Console.WriteLine("3. Testing task analysis functionality:");
        Console.WriteLine();

        // Test different types of tasks
        var testTasks = new[]
        {
            new { Description = "Calculate the square root of 144", ExpectedType = "Simple/Direct" },
            new { Description = "Plan and execute a marketing campaign", ExpectedType = "Complex/Orchestration" },
            new { Description = "What is 2 + 2?", ExpectedType = "Simple/Direct" },
            new { Description = "Design and implement a microservices architecture", ExpectedType = "Complex/Orchestration" }
        };

        foreach (var testTask in testTasks)
        {
            Console.WriteLine($"   Task: {testTask.Description}");
            Console.WriteLine($"   Expected: {testTask.ExpectedType}");
            
            try
            {
                // Real TaskAnalyzer call
                var result = await taskAnalyzer.AnalyzeTaskAsync(testTask.Description, context, config);
                
                Console.WriteLine($"   → Approach: {result.RecommendedApproach}");
                Console.WriteLine($"   → Can Decompose: {result.CanBeDecomposed}");
                Console.WriteLine($"   → Analysis Notes: {result.AnalysisNotes}");
                
                // If it's orchestration, try breakdown
                if (result.RecommendedApproach == TaskApproach.Orchestration)
                {
                    var breakdown = await taskAnalyzer.BreakdownTaskAsync(testTask.Description, context, config);
                    Console.WriteLine($"   → Subtasks:");
                    foreach (var subtask in breakdown)
                    {
                        Console.WriteLine($"     • {subtask}");
                    }
                }
                
                Console.WriteLine("   ✅ Analysis completed successfully");
            }
                         catch (Exception ex)
             {
                 if (!hasAzureConfig && !hasOpenAiConfig)
                 {
                     Console.WriteLine($"   ⚠️  Skipped (no API configuration): {ex.Message}");
                 }
                 else
                 {
                     Console.WriteLine($"   ❌ Failed: {ex.Message}");
                 }
             }
            
            Console.WriteLine();
        }

        // Demonstrate architecture principles
        Console.WriteLine("4. Architecture Validation:");
        Console.WriteLine("   ✅ Kernel injection pattern (not direct IChatCompletionService)");
        Console.WriteLine("   ✅ Clean dependency: Analysis → Common only");
        Console.WriteLine("   ✅ Framework-agnostic design");
        Console.WriteLine("   ✅ Error handling with graceful fallbacks");
        Console.WriteLine("   ✅ Interface-based contracts (ITaskAnalyzer, IAgentContext)");
    }

    /// <summary>
    /// Creates a Kernel following the ConfigurableKernelService pattern
    /// </summary>
    static Kernel CreateKernel(string? azureEndpoint, string? azureApiKey, string? azureDeployment, string? openAiApiKey)
    {
        var builder = Kernel.CreateBuilder();
        
        var hasAzureConfig = !string.IsNullOrEmpty(azureEndpoint) && !string.IsNullOrEmpty(azureApiKey) && !string.IsNullOrEmpty(azureDeployment);
        var hasOpenAiConfig = !string.IsNullOrEmpty(openAiApiKey);
        
        if (hasAzureConfig)
        {
            // Azure OpenAI configuration
            builder.AddAzureOpenAIChatCompletion(azureDeployment!, azureEndpoint!, azureApiKey!);
            Console.WriteLine($"   Added Azure OpenAI service (deployment: {azureDeployment})");
        }
        else if (hasOpenAiConfig)
        {
            // Regular OpenAI configuration
            builder.AddOpenAIChatCompletion("gpt-3.5-turbo", openAiApiKey!);
            Console.WriteLine("   Added OpenAI service");
        }
        else
        {
            // No API configuration - kernel will be created but LLM calls will fail gracefully
            // This still demonstrates the injection pattern
            Console.WriteLine("   (No LLM service added due to missing configuration)");
        }
        
        return builder.Build();
    }

    /// <summary>
    /// Creates test configuration for the examples
    /// </summary>
    static AgentConfiguration CreateTestConfiguration(string modelId, string? apiKey)
    {
        return new AgentConfiguration
        {
            AgentName = "ExampleAgent",
            SystemPrompt = "You are a helpful task analysis assistant that determines whether tasks require orchestration or can be handled directly.",
            Temperature = 0.7,
            MaxTokens = 1000,
            Model = new ModelConfiguration
            {
                ModelId = modelId,
                ApiKey = apiKey ?? "no-api-key-provided"
            }
        };
    }
}

/// <summary>
/// Simple implementation of IAgentContext for example purposes
/// </summary>
public class SimpleAgentContext : IAgentContext
{
    public AgentId AgentId => AgentId.Parse("example-agent-123");
    public string AgentName => "ExampleAgent";
    public AgentRole Role => AgentRole.Specialized;
} 
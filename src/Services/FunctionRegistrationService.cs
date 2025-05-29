using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using PsiOrleans.Plugins;

namespace PsiOrleans.Services;

/// <summary>
/// Service responsible for registering all available functions and plugins with the registry during startup
/// </summary>
public class FunctionRegistrationService
{
    private readonly IKernelFunctionRegistry _functionRegistry;
    private readonly ILogger<FunctionRegistrationService> _logger;

    public FunctionRegistrationService(
        IKernelFunctionRegistry functionRegistry, 
        ILogger<FunctionRegistrationService> logger)
    {
        _functionRegistry = functionRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Register all available functions and plugins with the registry
    /// </summary>
    public void RegisterAllFunctionsAndPlugins()
    {
        _logger.LogInformation("Starting registration of all functions and plugins");

        // Register mathematical functions
        RegisterMathematicalFunctions();
        
        // Register text processing functions
        RegisterTextProcessingFunctions();
        
        // Register customer service functions
        RegisterCustomerServiceFunctions();
        
        // Register utility functions
        RegisterUtilityFunctions();
        
        // Register plugins
        RegisterPlugins();

        var stats = ((KernelFunctionRegistry)_functionRegistry).GetStatistics();
        _logger.LogInformation("Function registration completed: {FunctionCount} functions, {PluginCount} plugins ({TotalPluginFunctions} plugin functions)", 
            stats.FunctionCount, stats.PluginCount, stats.TotalPluginFunctions);
    }

    private void RegisterMathematicalFunctions()
    {
        _logger.LogInformation("Registering mathematical functions");

        _functionRegistry.RegisterFunction("Math.Add", 
            KernelFunctionFactory.CreateFromMethod(
                (double a, double b) => a + b,
                "Add",
                "Add two numbers together"));

        _functionRegistry.RegisterFunction("Math.Multiply", 
            KernelFunctionFactory.CreateFromMethod(
                (double a, double b) => a * b,
                "Multiply", 
                "Multiply two numbers"));

        _functionRegistry.RegisterFunction("Math.Average", 
            KernelFunctionFactory.CreateFromMethod(
                (double[] numbers) => numbers.Average(),
                "Average",
                "Calculate the average of a list of numbers"));

        _functionRegistry.RegisterFunction("Math.Sum", 
            KernelFunctionFactory.CreateFromMethod(
                (double[] numbers) => numbers.Sum(),
                "Sum",
                "Calculate the sum of a list of numbers"));

        _functionRegistry.RegisterFunction("Math.Factorial", 
            KernelFunctionFactory.CreateFromMethod(
                (int n) => n <= 1 ? 1 : Enumerable.Range(1, n).Aggregate(1, (acc, x) => acc * x),
                "Factorial",
                "Calculate factorial of a number"));
    }

    private void RegisterTextProcessingFunctions()
    {
        _logger.LogInformation("Registering text processing functions");

        _functionRegistry.RegisterFunction("Text.CountCharacters", 
            KernelFunctionFactory.CreateFromMethod(
                (string text) => text.Length,
                "CountCharacters",
                "Count the number of characters in text"));

        _functionRegistry.RegisterFunction("Text.CountWords", 
            KernelFunctionFactory.CreateFromMethod(
                (string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                "CountWords",
                "Count the number of words in text"));

        _functionRegistry.RegisterFunction("Text.ReverseWords", 
            KernelFunctionFactory.CreateFromMethod(
                (string text) => string.Join(" ", text.Split(' ').Reverse()),
                "ReverseWords",
                "Reverse the order of words in text"));

        _functionRegistry.RegisterFunction("Text.ToTitleCase", 
            KernelFunctionFactory.CreateFromMethod(
                (string text) => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower()),
                "ToTitleCase",
                "Convert text to title case"));

        _functionRegistry.RegisterFunction("Text.ToUpperCase", 
            KernelFunctionFactory.CreateFromMethod(
                (string text) => text.ToUpper(),
                "ToUpperCase",
                "Convert text to uppercase"));
    }

    private void RegisterCustomerServiceFunctions()
    {
        _logger.LogInformation("Registering customer service functions");

        _functionRegistry.RegisterFunction("CustomerService.GenerateTicketId", 
            KernelFunctionFactory.CreateFromMethod(
                (string issue) => $"TKT-{DateTime.Now:yyyyMMdd}-{issue.GetHashCode():X}",
                "GenerateTicketId",
                "Generate a unique ticket ID for customer issues"));

        _functionRegistry.RegisterFunction("CustomerService.GetResponseTime", 
            KernelFunctionFactory.CreateFromMethod(
                (string priority) => priority.ToLower() switch
                {
                    "high" => "4 hours",
                    "medium" => "24 hours", 
                    "low" => "72 hours",
                    _ => "24 hours"
                },
                "GetResponseTime",
                "Get expected response time based on priority level"));

        _functionRegistry.RegisterFunction("CustomerService.GetSupportTeam", 
            KernelFunctionFactory.CreateFromMethod(
                (string customerType) => customerType.ToLower() switch
                {
                    "premium" => "Premium Support Team",
                    "business" => "Business Support Team",
                    "standard" => "General Support Team",
                    _ => "General Support Team"
                },
                "GetSupportTeam",
                "Determine which support team should handle the customer"));
    }

    private void RegisterUtilityFunctions()
    {
        _logger.LogInformation("Registering utility functions");

        _functionRegistry.RegisterFunction("Utility.GenerateList", 
            KernelFunctionFactory.CreateFromMethod(
                (int count) => string.Join(", ", Enumerable.Range(1, count).Select(i => $"Item {i}")),
                "GenerateList",
                "Generate a numbered list with specified count"));

        _functionRegistry.RegisterFunction("Utility.GetCurrentTime", 
            KernelFunctionFactory.CreateFromMethod(
                () => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "GetCurrentTime",
                "Get the current date and time"));

        _functionRegistry.RegisterFunction("Utility.GenerateGuid", 
            KernelFunctionFactory.CreateFromMethod(
                () => Guid.NewGuid().ToString(),
                "GenerateGuid",
                "Generate a new GUID"));
    }

    private void RegisterPlugins()
    {
        _logger.LogInformation("Registering plugins");

        try
        {
            var mathPlugin = KernelPluginFactory.CreateFromType<MathematicalOperationsPlugin>();
            _functionRegistry.RegisterPlugin("MathematicalOperations", mathPlugin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register MathematicalOperationsPlugin");
        }

        // Add more plugins here as they become available
        // _functionRegistry.RegisterPlugin("WebSearch", webSearchPlugin);
        // _functionRegistry.RegisterPlugin("FileOperations", fileOperationsPlugin);
    }
} 
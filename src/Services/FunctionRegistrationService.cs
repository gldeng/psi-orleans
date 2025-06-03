using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using PsiOrleans.Plugins;
using Microsoft.SemanticKernel.Plugins.Web.Tavily;
using Orleans;
using Microsoft.Extensions.DependencyInjection;

namespace PsiOrleans.Services;

/// <summary>
/// Service responsible for registering all available functions and plugins with the registry during startup
/// </summary>
public class FunctionRegistrationService
{
    private readonly IKernelFunctionRegistry _functionRegistry;
    private readonly ILogger<FunctionRegistrationService> _logger;
    private readonly IClusterClient _clusterClient;

    public FunctionRegistrationService(
        IKernelFunctionRegistry functionRegistry, 
        ILogger<FunctionRegistrationService> logger,
        IClusterClient clusterClient)
    {
        _functionRegistry = functionRegistry;
        _logger = logger;
        _clusterClient = clusterClient;
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
        
        // Register agent proxy functions
        RegisterAgentProxyFunctions();
        
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

        _functionRegistry.RegisterFunction("Math.Divide", 
            KernelFunctionFactory.CreateFromMethod(
                (double a, double b) => b != 0 ? a / b : throw new DivideByZeroException("Cannot divide by zero"),
                "Divide",
                "Divide two numbers"));

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

    private void RegisterAgentProxyFunctions()
    {
        _logger.LogInformation("Registering agent proxy functions");

        try
        {
            // Create a logger for AgentProxyService using LoggerFactory
            var serviceProvider = _clusterClient.ServiceProvider;
            var loggerFactory = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
            var agentProxyLogger = loggerFactory.CreateLogger<AgentProxyService>();
            
            // Get AgentCreationService from the service provider
            var agentCreationService = serviceProvider.GetRequiredService<AgentCreationService>();
            
            var agentProxyService = new AgentProxyService(_clusterClient, agentProxyLogger, agentCreationService);

            _functionRegistry.RegisterFunction("AgentProxy.WebSearchAgent", 
                KernelFunctionFactory.CreateFromMethod(
                    agentProxyService.WebSearchAgentAsync,
                    "WebSearchAgent",
                    "Delegates web search tasks to the specialized Web Search Agent (Agent B). Pass natural language queries about finding web data."));

            _functionRegistry.RegisterFunction("AgentProxy.MathAgent", 
                KernelFunctionFactory.CreateFromMethod(
                    agentProxyService.MathAgentAsync,
                    "MathAgent",
                    "Delegates mathematical calculation tasks to the specialized Math Agent (Agent C). Pass natural language queries about calculations."));

            _functionRegistry.RegisterFunction("AgentProxy.CallAgent", 
                KernelFunctionFactory.CreateFromMethod(
                    agentProxyService.CallAgentAsync,
                    "CallAgent",
                    "Calls any ConfigurableAgentGrain by its ID with a natural language query"));

            _functionRegistry.RegisterFunction("AgentProxy.CheckAgentStatus", 
                KernelFunctionFactory.CreateFromMethod(
                    agentProxyService.CheckAgentStatusAsync,
                    "CheckAgentStatus",
                    "Checks if a specific agent is initialized and ready to handle requests"));
                    
            // Register agent creation functions
            _functionRegistry.RegisterFunction("AgentProxy.CreateAgent", 
                KernelFunctionFactory.CreateFromMethod(
                    agentProxyService.CreateAgentAsync,
                    "CreateAgent",
                    "Creates a new specialized agent with custom system prompt and tools"));
                    
            // Register AgentCreationService functions directly with simple names
            _functionRegistry.RegisterFunction("create_agent", 
                KernelFunctionFactory.CreateFromMethod(
                    agentCreationService.CreateAgentAsync,
                    "create_agent",
                    "Creates a new specialized agent with custom system prompt and specified tools"));
                    
            _functionRegistry.RegisterFunction("list_available_tools", 
                KernelFunctionFactory.CreateFromMethod(
                    agentCreationService.ListAvailableToolsAsync,
                    "list_available_tools",
                    "Lists all available tools that can be assigned to new agents"));
                    
            _functionRegistry.RegisterFunction("list_created_agents", 
                KernelFunctionFactory.CreateFromMethod(
                    agentCreationService.ListCreatedAgentsAsync,
                    "list_created_agents",
                    "Lists all agents that have been created by the system with their usage statistics"));
                    
            _functionRegistry.RegisterFunction("find_suitable_agent", 
                KernelFunctionFactory.CreateFromMethod(
                    agentCreationService.FindSuitableAgentAsync,
                    "find_suitable_agent",
                    "Finds existing created agents that might be suitable for handling a specific task"));
                    
            // Register call_agent function with simple name
            _functionRegistry.RegisterFunction("call_agent", 
                KernelFunctionFactory.CreateFromMethod(
                    agentProxyService.CallAgentAsync,
                    "call_agent",
                    "Calls any ConfigurableAgentGrain by its ID with a natural language query"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register AgentProxy functions");
        }
    }

    private void RegisterPlugins()
    {
        _logger.LogInformation("Registering plugins");

        // Register MathematicalOperations plugin
        try
        {
            var mathPlugin = KernelPluginFactory.CreateFromType<MathematicalOperationsPlugin>();
            _functionRegistry.RegisterPlugin("MathematicalOperations", mathPlugin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register MathematicalOperationsPlugin");
        }

        // Register Tavily web search plugin
        var tavilyApiKey = Environment.GetEnvironmentVariable("TAVILY_API_KEY");
        if (!string.IsNullOrEmpty(tavilyApiKey))
        {
            try
            {
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates
                var tavilySearch = new TavilyTextSearch(tavilyApiKey);
                
                var searchFunction = KernelFunctionFactory.CreateFromMethod(
                    async (string query) => 
                    {
                        var searchResults = await tavilySearch.SearchAsync(query);
                        var results = new List<string>();
                        
                        await foreach (var result in searchResults.Results)
                        {
                            results.Add(result);
                        }
                        
                        return string.Join("\n\n", results);
                    },
                    "Search",
                    "Search the web for information using Tavily");
                
                var tavilyPlugin = KernelPluginFactory.CreateFromFunctions("Tavily", "Tavily web search", [searchFunction]);
                _functionRegistry.RegisterPlugin("Tavily", tavilyPlugin);
#pragma warning restore SKEXP0050
                
                _logger.LogInformation("Tavily web search plugin registered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register Tavily plugin");
            }
        }
        else
        {
            _logger.LogWarning("TAVILY_API_KEY not found. Tavily web search plugin will not be available. " +
                             "Please set TAVILY_API_KEY environment variable to enable web search.");
        }
    }
} 
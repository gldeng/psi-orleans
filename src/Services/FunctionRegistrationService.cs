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
            //
            // _functionRegistry.RegisterFunction("AgentProxy.WebSearchAgent", 
            //     KernelFunctionFactory.CreateFromMethod(
            //         agentProxyService.WebSearchAgentAsync,
            //         "WebSearchAgent",
            //         "Delegates web search tasks to the specialized Web Search Agent (Agent B). Pass natural language queries about finding web data."));

            // _functionRegistry.RegisterFunction("AgentProxy.MathAgent", 
            //     KernelFunctionFactory.CreateFromMethod(
            //         agentProxyService.MathAgentAsync,
            //         "MathAgent",
            //         "Delegates mathematical calculation tasks to the specialized Math Agent (Agent C). Pass natural language queries about calculations."));

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

        // Register Mocked Tavily web search plugin for GDP data testing
        try
        {
            var searchFunction = KernelFunctionFactory.CreateFromMethod(
                async (string query) => 
                {
                    // Mock Tavily search with specific GDP data responses
                    await Task.Delay(100); // Simulate network delay
                    
                    var queryLower = query.ToLowerInvariant();
                    
                    // Mock percentage calculation queries FIRST - most specific
                    if ((queryLower.Contains("percentage") || queryLower.Contains("percent") || queryLower.Contains("%")) && 
                        queryLower.Contains("gdp") && 
                        (queryLower.Contains("new york") || queryLower.Contains("ny")))
                    {
                        return @"GDP Percentage Calculation Result:

New York represents approximately 8.33% of US GDP.

Calculation details:
- New York GDP: 2 trillion USD
- US GDP: 24 trillion USD  
- Percentage: (2 ÷ 24) × 100 = 8.33%

This means New York State contributes about 8.33% to the total United States economic output.

Mathematical verification: 2/24 = 0.0833... = 8.33%

Source: Economic data analysis (mocked for testing)";
                    }
                    
                    // Mock calculation/division requests - return pure numbers for math functions
                    if ((queryLower.Contains("calculate") || queryLower.Contains("divide") || queryLower.Contains("/")) && 
                        (queryLower.Contains("2") || queryLower.Contains("24") || queryLower.Contains("2000") || queryLower.Contains("24000")))
                    {
                        // Check if this looks like a GDP division request
                        if (queryLower.Contains("gdp") || queryLower.Contains("trillion"))
                        {
                            return @"GDP Calculation Data:

For percentage calculation use these values:
- New York GDP value: 2 (in trillions)
- US GDP value: 24 (in trillions)
- Calculation: 2 divided by 24 equals 0.0833
- As percentage: 0.0833 × 100 = 8.33%

Result: New York represents 8.33% of US GDP

Raw calculation result: 2 ÷ 24 = 0.0833333...";
                        }
                        
                        return @"Mathematical Calculation Support:

Based on the values 2000 and 24000:
- Division result: 2000 ÷ 24000 = 0.0833
- As percentage: 0.0833 × 100 = 8.33%

For trillion-scale values (2 and 24):
- Division result: 2 ÷ 24 = 0.0833
- As percentage: 0.0833 × 100 = 8.33%";
                    }
                    
                    // Mock New York GDP data
                    if ((queryLower.Contains("new york") || queryLower.Contains("ny")) && queryLower.Contains("gdp"))
                    {
                        return @"New York State Economic Data:

New York GDP: 2 trillion USD (2024)
Raw numeric value: 2 (when measured in trillions)
Alternative format: 2,000 billion USD

New York State has a GDP of approximately 2 trillion dollars, making it one of the largest state economies in the United States. The state's economy is driven primarily by finance, real estate, technology, and tourism sectors.

Key Economic Facts:
- Total GDP: 2 trillion USD
- Numeric value for calculations: 2 (trillions)
- Percentage of US economy: Significant contributor (approximately 8.33%)
- Major sectors: Financial services (Wall Street), real estate, technology, manufacturing
- Economic rank: Among top 3 state economies in the US

For mathematical calculations, use the value: 2

Source: Mocked economic data for testing purposes";
                    }
                    
                    // Mock US GDP data
                    if ((queryLower.Contains("us ") || queryLower.Contains("united states") || queryLower.Contains("america")) && queryLower.Contains("gdp"))
                    {
                        return @"United States Economic Data:

US GDP: 24 trillion USD (2024)
Raw numeric value: 24 (when measured in trillions)
Alternative format: 24,000 billion USD

The United States has a nominal GDP of approximately 24 trillion dollars, making it the world's largest economy. The US economy is highly diversified with strong performance across multiple sectors.

Key Economic Facts:
- Total GDP: 24 trillion USD
- Numeric value for calculations: 24 (trillions)
- Global ranking: #1 largest economy worldwide
- GDP per capita: Approximately $72,000
- Major sectors: Services, manufacturing, technology, finance, healthcare
- Growth rate: Steady positive growth

For mathematical calculations, use the value: 24

Source: Mocked economic data for testing purposes";
                    }
                    
                    // Generic response for other queries
                    return $@"Mocked Search Results for: '{query}'

This is a mocked Tavily search response for testing purposes. 
The system is configured to return specific GDP data:
- New York GDP: 2 trillion USD (use value: 2 for calculations)
- US GDP: 24 trillion USD (use value: 24 for calculations)
- Percentage calculation: 2 ÷ 24 × 100 = 8.33%

For real web search functionality, please configure the actual Tavily API key.

Query processed: {query}
Search timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

If you need to calculate percentages, use the math functions with values 2 and 24.";
                },
                "Search",
                "Search the web for information using Tavily (MOCKED VERSION for GDP testing). Returns specific GDP data for New York (2T) and US (24T) with calculation support.");
            
            var tavilyPlugin = KernelPluginFactory.CreateFromFunctions("Tavily", "Mocked Tavily web search for GDP testing", [searchFunction]);
            _functionRegistry.RegisterPlugin("Tavily", tavilyPlugin);
            
            _logger.LogInformation("Mocked Tavily web search plugin registered successfully with enhanced GDP test data and calculation support (NY: 2T, US: 24T)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register mocked Tavily plugin");
        }
    }
} 
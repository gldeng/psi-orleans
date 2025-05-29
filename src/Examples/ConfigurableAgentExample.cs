using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Orleans;
using PsiOrleans.Grains;
using PsiOrleans.Models;
using PsiOrleans.Plugins;
using System.ComponentModel;

namespace PsiOrleans.Examples;

/// <summary>
/// Example demonstrating how to create and use configurable agents with custom prompts and named tools
/// </summary>
public class ConfigurableAgentExample
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<ConfigurableAgentExample> _logger;

    public ConfigurableAgentExample(IClusterClient clusterClient, ILogger<ConfigurableAgentExample> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    /// <summary>
    /// Example 1: Data Analyst Agent with mathematical tools
    /// </summary>
    public async Task<string> RunDataAnalystAgentAsync()
    {
        _logger.LogInformation("🔢 Creating Data Analyst Agent with mathematical tools");

        // Specify mathematical function names from the registry
        var mathFunctionNames = new List<string>
        {
            "Math.Add",
            "Math.Multiply",
            "Math.Average",
            "Math.Sum",
            "Math.Factorial"
        };

        // Create agent configuration (behavior only)
        var config = new AgentConfiguration
        {
            SystemPrompt = @"You are a Data Analyst Agent specializing in numerical analysis and statistical calculations. 
                           You have access to mathematical functions and should use them to solve problems.
                           Always show your work and explain your calculations step by step.
                           Be precise and accurate in your analysis.",
            AgentName = "DataAnalyst",
            Temperature = 0.1,
            MaxTokens = 2000
        };

        // Get agent grain and initialize with function names
        var agent = _clusterClient.GetGrain<IConfigurableAgentGrain>("data-analyst-001");
        var initResult = await agent.InitializeAsync(config, mathFunctionNames);
        
        if (!initResult.Success)
        {
            return $"❌ Failed to initialize Data Analyst Agent: {initResult.Message}";
        }

        _logger.LogInformation("✅ {Message}", initResult.Message);

        // Execute a data analysis task
        var task = "I have the following sales data for the last 5 quarters: $125,000, $142,000, $138,500, $156,200, $147,800. Calculate the average quarterly sales, total sales, and analyze the trend.";
        var result = await agent.ExecuteTaskAsync(task);

        return result;
    }

    /// <summary>
    /// Example 2: Creative Writing Agent with text processing tools
    /// </summary>
    public async Task<string> RunCreativeWritingAgentAsync()
    {
        _logger.LogInformation("✍️ Creating Creative Writing Agent with text processing tools");

        // Specify text processing function names from the registry
        var textFunctionNames = new List<string>
        {
            "Text.CountCharacters",
            "Text.CountWords",
            "Text.ReverseWords",
            "Text.ToTitleCase",
            "Text.ToUpperCase"
        };

        var config = new AgentConfiguration
        {
            SystemPrompt = @"You are a Creative Writing Agent with a passion for storytelling and wordcraft.
                           You help users create engaging content, stories, and written works.
                           You have access to text processing tools to analyze and manipulate text.
                           Be creative, inspiring, and always encourage artistic expression.
                           Use your tools to provide insights about text structure and formatting.",
            AgentName = "CreativeWriter",
            Temperature = 0.7,
            MaxTokens = 3000
        };

        var agent = _clusterClient.GetGrain<IConfigurableAgentGrain>("creative-writer-001");
        var initResult = await agent.InitializeAsync(config, textFunctionNames);
        
        if (!initResult.Success)
        {
            return $"❌ Failed to initialize Creative Writing Agent: {initResult.Message}";
        }

        _logger.LogInformation("✅ {Message}", initResult.Message);

        var task = "Write a short story about a time traveler who discovers they can only travel to moments of great historical importance. Make it exactly 150 words, then analyze the word count and structure.";
        var result = await agent.ExecuteTaskAsync(task);

        return result;
    }

    /// <summary>
    /// Example 3: Research Agent with mathematical plugin
    /// </summary>
    public async Task<string> RunResearchAgentAsync()
    {
        _logger.LogInformation("🔍 Creating Research Agent with mathematical plugin");

        // Use plugin names from the registry
        var pluginNames = new List<string> { "MathematicalOperations" };

        var config = new AgentConfiguration
        {
            SystemPrompt = @"You are a Research Agent specialized in gathering, analyzing, and synthesizing information.
                           You have access to mathematical tools for data analysis.
                           When conducting research, be thorough, cite sources when available, and provide well-structured responses.
                           Always fact-check information and present balanced perspectives.
                           Use mathematical tools when you need to perform calculations on research data.",
            AgentName = "ResearchAgent",
            Temperature = 0.2,
            MaxTokens = 4000
        };

        var agent = _clusterClient.GetGrain<IConfigurableAgentGrain>("research-agent-001");
        var initResult = await agent.InitializeAsync(config, null, pluginNames);
        
        if (!initResult.Success)
        {
            return $"❌ Failed to initialize Research Agent: {initResult.Message}";
        }

        _logger.LogInformation("✅ {Message}", initResult.Message);

        var task = "Research the current market size of the AI industry. If you find data points, calculate the growth rate between different years and provide a comprehensive analysis.";
        var result = await agent.ExecuteTaskAsync(task);

        return result;
    }

    /// <summary>
    /// Example 4: Customer Service Agent with support tools
    /// </summary>
    public async Task<string> RunCustomerServiceAgentAsync()
    {
        _logger.LogInformation("🎧 Creating Customer Service Agent");

        // Use customer service function names from the registry
        var serviceFunctionNames = new List<string>
        {
            "CustomerService.GenerateTicketId",
            "CustomerService.GetResponseTime",
            "CustomerService.GetSupportTeam"
        };

        var config = new AgentConfiguration
        {
            SystemPrompt = @"You are a Customer Service Agent dedicated to providing excellent customer support.
                           You are empathetic, professional, and solution-oriented.
                           Always acknowledge customer concerns, provide clear next steps, and use available tools to assist.
                           Generate ticket IDs when needed and set appropriate expectations for response times.
                           Be friendly but professional in all interactions.",
            AgentName = "CustomerService",
            Temperature = 0.3,
            MaxTokens = 2500
        };

        var agent = _clusterClient.GetGrain<IConfigurableAgentGrain>("customer-service-001");
        var initResult = await agent.InitializeAsync(config, serviceFunctionNames);
        
        if (!initResult.Success)
        {
            return $"❌ Failed to initialize Customer Service Agent: {initResult.Message}";
        }

        _logger.LogInformation("✅ {Message}", initResult.Message);

        var task = "A premium customer is reporting that their account was charged twice for the same service. They seem frustrated and want immediate resolution. Help them.";
        var result = await agent.ExecuteTaskAsync(task);

        return result;
    }

    /// <summary>
    /// Demonstrate conversation continuity with an agent
    /// </summary>
    public async Task<string> DemonstrateConversationAsync()
    {
        _logger.LogInformation("💬 Demonstrating conversation continuity");

        // Create a simple conversational agent (no additional tools)
        var config = new AgentConfiguration
        {
            SystemPrompt = @"You are a friendly conversational AI assistant. 
                           Remember context from previous messages and build upon the conversation naturally.
                           Be helpful, engaging, and maintain consistency throughout the conversation.",
            AgentName = "ConversationalAgent",
            Temperature = 0.6,
            MaxTokens = 1500
        };

        var agent = _clusterClient.GetGrain<IConfigurableAgentGrain>("conversation-agent-001");
        var initResult = await agent.InitializeAsync(config); // No additional functions
        
        if (!initResult.Success)
        {
            return $"❌ Failed to initialize Conversational Agent: {initResult.Message}";
        }

        // Start a conversation
        var responses = new List<string>();
        
        var response1 = await agent.ExecuteTaskAsync("Hi! I'm planning a birthday party for my 8-year-old daughter. Do you have any suggestions?");
        responses.Add($"User: Hi! I'm planning a birthday party for my 8-year-old daughter. Do you have any suggestions?\n\nAgent: {response1}\n");

        var response2 = await agent.ContinueConversationAsync("That sounds great! She loves unicorns. Can you suggest a unicorn theme?");
        responses.Add($"User: That sounds great! She loves unicorns. Can you suggest a unicorn theme?\n\nAgent: {response2}\n");

        var response3 = await agent.ContinueConversationAsync("Perfect! How many guests would you recommend for an 8-year-old's party?");
        responses.Add($"User: Perfect! How many guests would you recommend for an 8-year-old's party?\n\nAgent: {response3}\n");

        return string.Join("\n---\n\n", responses);
    }

    /// <summary>
    /// Example 5: Multi-tool agent combining functions and plugins
    /// </summary>
    public async Task<string> RunHybridAgentAsync()
    {
        _logger.LogInformation("🔀 Creating Hybrid Agent with both functions and plugins");

        // Individual function names
        var customFunctionNames = new List<string>
        {
            "Text.ToUpperCase",
            "Utility.GenerateList"
        };

        // Plugin names
        var pluginNames = new List<string> { "MathematicalOperations" };

        var config = new AgentConfiguration
        {
            SystemPrompt = @"You are a Hybrid Agent with access to both custom functions and mathematical plugins.
                           You can handle text processing, list generation, and mathematical calculations.
                           Use the appropriate tools for each task and explain what tools you're using.",
            AgentName = "HybridAgent",
            Temperature = 0.4,
            MaxTokens = 3000
        };

        var agent = _clusterClient.GetGrain<IConfigurableAgentGrain>("hybrid-agent-001");
        var initResult = await agent.InitializeAsync(config, customFunctionNames, pluginNames);
        
        if (!initResult.Success)
        {
            return $"❌ Failed to initialize Hybrid Agent: {initResult.Message}";
        }

        _logger.LogInformation("✅ {Message}", initResult.Message);

        var task = "Create a shopping list with 5 items, convert the title to uppercase, and calculate the factorial of the number of items in the list.";
        var result = await agent.ExecuteTaskAsync(task);

        return result;
    }

    /// <summary>
    /// Example 6: Show available functions from registry
    /// </summary>
    public async Task<string> ShowAvailableFunctionsAsync()
    {
        _logger.LogInformation("📋 Showing available functions from registry");

        // Create a simple agent to access the registry
        var config = new AgentConfiguration
        {
            SystemPrompt = "You are a system agent for displaying available functions.",
            AgentName = "SystemAgent",
            Temperature = 0.1,
            MaxTokens = 1000
        };

        var agent = _clusterClient.GetGrain<IConfigurableAgentGrain>("system-agent-001");
        var initResult = await agent.InitializeAsync(config);
        
        if (!initResult.Success)
        {
            return $"❌ Failed to initialize System Agent: {initResult.Message}";
        }

        var functionNames = await agent.GetAvailableFunctionNamesAsync();
        var pluginNames = await agent.GetAvailablePluginNamesAsync();
        var allToolNames = await agent.GetAllAvailableToolNamesAsync();

        var result = "🔧 Available Functions from Registry:\n\n";
        
        result += "🚀 NEW: Unified Tool Names (RECOMMENDED):\n";
        foreach (var toolName in allToolNames)
        {
            result += $"  • {toolName}\n";
        }
        
        result += "\n" + new string('-', 50) + "\n";
        result += "📝 Legacy: Individual Functions:\n";
        foreach (var functionName in functionNames)
        {
            result += $"  • {functionName}\n";
        }

        result += "\n🔌 Legacy: Available Plugins:\n";
        foreach (var pluginName in pluginNames)
        {
            result += $"  • {pluginName}\n";
        }

        result += $"\n📊 Totals: {allToolNames.Count()} unified tools, {functionNames.Count()} functions, {pluginNames.Count()} plugins";
        
        result += "\n\n💡 Use the unified tool names for the simplest configuration!";

        return result;
    }

    /// <summary>
    /// Get metrics for all agents
    /// </summary>
    public async Task<string> GetAgentMetricsAsync()
    {
        var agentIds = new[] { 
            "data-analyst-001", 
            "creative-writer-001", 
            "research-agent-001", 
            "customer-service-001", 
            "conversation-agent-001",
            "hybrid-agent-001",
            "system-agent-001"
        };
        var metrics = new List<string>();

        foreach (var agentId in agentIds)
        {
            try
            {
                var agent = _clusterClient.GetGrain<IConfigurableAgentGrain>(agentId);
                var isInitialized = await agent.IsInitializedAsync();
                
                if (isInitialized)
                {
                    var agentMetrics = await agent.GetMetricsAsync();
                    var config = await agent.GetConfigurationAsync();
                    var tools = await agent.GetAvailableToolsAsync();
                    var successRate = agentMetrics.TotalTasks > 0 ? 
                        (double)agentMetrics.SuccessfulTasks / agentMetrics.TotalTasks * 100 : 0;

                    metrics.Add($"🤖 Agent: {config?.AgentName ?? agentId}\n" +
                              $"   🔧 Tools: {tools.Count}\n" +
                              $"   📊 Total Tasks: {agentMetrics.TotalTasks}\n" +
                              $"   ✅ Successful: {agentMetrics.SuccessfulTasks}\n" +
                              $"   ❌ Failed: {agentMetrics.FailedTasks}\n" +
                              $"   📈 Success Rate: {successRate:F1}%\n" +
                              $"   ⏱️  Total Execution Time: {agentMetrics.TotalExecutionTime.TotalSeconds:F2}s");
                }
                else
                {
                    metrics.Add($"🤖 Agent: {agentId} (Not Initialized)");
                }
            }
            catch (Exception ex)
            {
                metrics.Add($"🤖 Agent: {agentId} (Error: {ex.Message})");
            }
        }

        return string.Join("\n\n", metrics);
    }

    /// <summary>
    /// NEW: Example demonstrating the streamlined unified tool naming approach
    /// </summary>
    public async Task<string> RunUnifiedToolNamingExampleAsync()
    {
        _logger.LogInformation("🚀 Creating agent with unified tool naming (RECOMMENDED APPROACH)");

        // Single list of tools using qualified names - much simpler!
        var toolNames = new List<string>
        {
            "Math.Add",                    // Individual function
            "Math.Multiply",               // Individual function  
            "Text.CountWords",             // Individual function
            "Text.ToUpperCase",            // Individual function
            "MathematicalOperations.Add",  // Function from plugin
            "MathematicalOperations.Multiply", // Function from plugin
            "CustomerService.GenerateTicketId" // Individual function
        };

        var config = new AgentConfiguration
        {
            SystemPrompt = @"You are a Multi-Tool Agent with access to mathematical functions, text processing, and customer service tools.
                           Use the appropriate tools for each task and explain what tools you're using.
                           You can handle calculations, text analysis, and customer service scenarios.",
            AgentName = "UnifiedToolAgent",
            Temperature = 0.3,
            MaxTokens = 3000
        };

        // NEW: Use the streamlined InitializeAsync with unified tool names
        var agent = _clusterClient.GetGrain<IConfigurableAgentGrain>("unified-tool-agent-001");
        var initResult = await agent.InitializeAsync(config, toolNames);
        
        if (!initResult.Success)
        {
            return $"❌ Failed to initialize Unified Tool Agent: {initResult.Message}";
        }

        _logger.LogInformation("✅ {Message}", initResult.Message);

        var task = @"Please help me with this multi-step task:
                    1. Calculate 15 * 8 using available math functions
                    2. Count the words in this text: 'The quick brown fox jumps over the lazy dog'
                    3. Convert 'hello world' to uppercase
                    4. Generate a customer service ticket ID for 'Login Issue'
                    
                    Show all your work and explain which tools you used for each step.";

        var result = await agent.ExecuteTaskAsync(task);

        return $"🔧 Unified Tool Naming Benefits:\n" +
               $"✅ Single tool list instead of separate functions/plugins\n" +
               $"✅ Intuitive naming: 'Math.Add', 'MathematicalOperations.Multiply'\n" +
               $"✅ Mix individual functions and plugin functions seamlessly\n" +
               $"✅ Easier discovery and configuration\n\n" +
               $"📋 Tools Used: {string.Join(", ", toolNames)}\n\n" +
               $"🤖 Agent Response:\n{result}";
    }
} 
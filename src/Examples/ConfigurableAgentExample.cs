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

    /// <summary>
    /// Multi-Agent GDP Analysis: Demonstrates Agent A (Orchestrator), Agent B (Web Search), Agent C (Math)
    /// Use case: "find US and New York state GDP in 2024. what % of US GDP was New York state?"
    /// </summary>
    public async Task<string> RunMultiAgentGdpAnalysisAsync()
    {
        _logger.LogInformation("🌍 Starting Multi-Agent GDP Analysis System");
        
        var results = new List<string>();
        results.Add("🚀 Multi-Agent GDP Analysis System");
        results.Add("=====================================");
        results.Add("Task: Find US and NY State GDP in 2024, calculate NY's percentage of US GDP");
        results.Add("");

        try
        {
            // Step 1: Initialize Agent B (Web Search Agent) with Tavily search tools
            results.Add("📋 Step 1: Initializing Agent B (Web Search Expert)");
            
            // Agent B uses Tavily web search tool for real data gathering
            var webSearchToolNames = new List<string>
            {
                "Tavily.Search"
            };

            var webSearchConfig = new AgentConfiguration
            {
                SystemPrompt = @"You are Agent B, a Web Search Expert specializing in economic data gathering.
                               Your role is to search for and retrieve GDP data from reliable sources using web search.
                               Use the Tavily search tool to find current and accurate GDP information.
                               
                               When searching for GDP data:
                               - Search for official sources like Bureau of Economic Analysis, World Bank, IMF
                               - Look for the most recent and reliable data available
                               - Extract specific numeric values from your search results
                               - Always cite your sources and provide context about the data
                               
                               Format your responses clearly with:
                               - The GDP value found
                               - The source of the information  
                               - The year the data represents
                               - Any relevant context about the measurement",
                AgentName = "WebSearchExpert",
                Temperature = 0.1,
                MaxTokens = 2000
            };

            var webSearchAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>("web-search-agent");
            var webSearchInit = await webSearchAgent.InitializeAsync(webSearchConfig, webSearchToolNames);
            
            if (!webSearchInit.Success)
            {
                return $"❌ Failed to initialize Agent B: {webSearchInit.Message}";
            }
            results.Add($"✅ Agent B initialized: {webSearchInit.Message}");
            results.Add("");

            // Step 2: Initialize Agent C (Math Agent) with existing math functions
            results.Add("📋 Step 2: Initializing Agent C (Math Expert)");
            var mathToolNames = new List<string>
            {
                "Math.Add",
                "Math.Multiply", 
                "Math.Divide"
            };

            var mathConfig = new AgentConfiguration
            {
                SystemPrompt = @"You are Agent C, a Mathematical Analysis Expert specializing in economic calculations.
                               Your role is to perform precise mathematical operations on economic data.
                               You can calculate percentages, perform division, addition, and analyze GDP relationships.
                               Always show your calculations step by step and provide clear explanations.
                               Pay special attention to decimal precision and units (trillions, billions, etc.).
                               
                               To calculate percentage: (part ÷ whole) × 100
                               When working with GDP data in trillions, be careful with decimal places.",
                AgentName = "MathExpert",
                Temperature = 0.1,
                MaxTokens = 2000
            };

            var mathAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>("math-agent");
            var mathInit = await mathAgent.InitializeAsync(mathConfig, mathToolNames);
            
            if (!mathInit.Success)
            {
                return $"❌ Failed to initialize Agent C: {mathInit.Message}";
            }
            results.Add($"✅ Agent C initialized: {mathInit.Message}");
            results.Add("");

            // Step 3: Initialize Agent A (Orchestrator) with proxy functions to call other agents
            results.Add("📋 Step 3: Initializing Agent A (Task Orchestrator)");
            var orchestratorToolNames = new List<string>
            {
                "AgentProxy.WebSearchAgent",      // Proxy to Agent B
                "AgentProxy.MathAgent",           // Proxy to Agent C
                "AgentProxy.CallAgent",           // Generic agent caller
                "AgentProxy.CheckAgentStatus"     // Status checker
            };

            var orchestratorConfig = new AgentConfiguration
            {
                SystemPrompt = @"You are Agent A, the Task Orchestrator for multi-agent economic analysis.
                               Your role is to break down complex economic analysis tasks into steps and coordinate other agents.
                               
                               You have access to:
                               - WebSearchAgent function: Delegates web search tasks to Agent B (Web Search Expert)
                               - MathAgent function: Delegates mathematical calculations to Agent C (Math Expert)
                               - CallAgent function: Can call any agent by ID with natural language queries
                               - CheckAgentStatus function: Check if agents are ready
                               
                               For GDP analysis tasks:
                               1. First, use WebSearchAgent function to delegate data gathering to Agent B
                               2. Then, use MathAgent function to delegate calculations to Agent C
                               3. Coordinate the results into a comprehensive analysis
                               
                               Always explain your coordination strategy and provide clear task breakdowns.
                               Use natural language when calling other agents - they understand English queries.",
                AgentName = "TaskOrchestrator",
                Temperature = 0.2,
                MaxTokens = 4000
            };

            var orchestratorAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>("orchestrator-agent");
            var orchestratorInit = await orchestratorAgent.InitializeAsync(orchestratorConfig, orchestratorToolNames);
            
            if (!orchestratorInit.Success)
            {
                return $"❌ Failed to initialize Agent A: {orchestratorInit.Message}";
            }
            results.Add($"✅ Agent A initialized: {orchestratorInit.Message}");
            results.Add("");

            // Step 4: Execute the GDP Analysis Task
            results.Add("📋 Step 4: Executing GDP Analysis Task");
            var gdpAnalysisTask = @"Please coordinate a multi-agent analysis to find US and New York state GDP in 2024, 
                                   then calculate what percentage of US GDP was New York state.
                                   
                                   Break this down into steps:
                                   1. Use the WebSearchAgent function to ask Agent B to find US GDP for 2024
                                   2. Use the WebSearchAgent function to ask Agent B to find New York State GDP for 2024  
                                   3. Use the MathAgent function to ask Agent C to calculate the percentage: (NY GDP ÷ US GDP) × 100
                                   
                                   Coordinate the entire process and provide a comprehensive analysis.
                                   
                                   Remember: Pass natural language queries to the other agents - they understand English instructions.";

            var analysisResult = await orchestratorAgent.ExecuteTaskAsync(gdpAnalysisTask);
            results.Add("🤖 Multi-Agent Analysis Result:");
            results.Add("===============================");
            results.Add(analysisResult);

            return string.Join("\n", results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in Multi-Agent GDP Analysis");
            return $"❌ Error in Multi-Agent GDP Analysis: {ex.Message}";
        }
    }

    /// <summary>
    /// Enhanced Multi-Agent GDP Analysis: Demonstrates callable agents stored in agent state
    /// Agent A (Orchestrator) maintains its own list of callable agents and function registry
    /// Use case: "find US and New York state GDP in 2024. what % of US GDP was New York state?"
    /// </summary>
    public async Task<string> RunEnhancedMultiAgentGdpAnalysisAsync()
    {
        _logger.LogInformation("🌍 Starting Enhanced Multi-Agent GDP Analysis with Callable Agents");
        
        var results = new List<string>();
        results.Add("🚀 Enhanced Multi-Agent GDP Analysis System");
        results.Add("==========================================");
        results.Add("Task: Find US and NY State GDP in 2024, calculate NY's percentage of US GDP");
        results.Add("Enhancement: Agent A maintains its own callable agents list and function registry");
        results.Add("");

        try
        {
            // Step 1: Initialize Agent B (Web Search Agent) with Tavily search tools
            results.Add("📋 Step 1: Initializing Agent B (Web Search Expert)");
            
            var webSearchToolNames = new List<string>
            {
                "Tavily.Search"
            };

            var webSearchConfig = new AgentConfiguration
            {
                SystemPrompt = @"You are Agent B, a Web Search Expert specializing in economic data gathering.
                               Your role is to search for and retrieve GDP data from reliable sources using web search.
                               Use the Tavily search tool to find current and accurate GDP information.
                               
                               When searching for GDP data:
                               - Search for official sources like Bureau of Economic Analysis, World Bank, IMF
                               - Look for the most recent and reliable data available
                               - Extract specific numeric values from your search results
                               - Always cite your sources and provide context about the data
                               
                               Format your responses clearly with:
                               - The GDP value found
                               - The source of the information  
                               - The year the data represents
                               - Any relevant context about the measurement",
                AgentName = "WebSearchExpert",
                Temperature = 0.1,
                MaxTokens = 2000
            };

            var webSearchAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>("web-search-agent");
            var webSearchInit = await webSearchAgent.InitializeAsync(webSearchConfig, webSearchToolNames);
            
            if (!webSearchInit.Success)
            {
                return $"❌ Failed to initialize Agent B: {webSearchInit.Message}";
            }
            results.Add($"✅ Agent B initialized: {webSearchInit.Message}");
            results.Add("");

            // Step 2: Initialize Agent C (Math Agent) with existing math functions
            results.Add("📋 Step 2: Initializing Agent C (Math Expert)");
            var mathToolNames = new List<string>
            {
                "Math.Add",
                "Math.Multiply", 
                "Math.Divide"
            };

            var mathConfig = new AgentConfiguration
            {
                SystemPrompt = @"You are Agent C, a Mathematical Analysis Expert specializing in economic calculations.
                               Your role is to perform precise mathematical operations on economic data.
                               You can calculate percentages, perform division, addition, and analyze GDP relationships.
                               Always show your calculations step by step and provide clear explanations.
                               Pay special attention to decimal precision and units (trillions, billions, etc.).
                               
                               To calculate percentage: (part ÷ whole) × 100
                               When working with GDP data in trillions, be careful with decimal places.",
                AgentName = "MathExpert",
                Temperature = 0.1,
                MaxTokens = 2000
            };

            var mathAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>("math-agent");
            var mathInit = await mathAgent.InitializeAsync(mathConfig, mathToolNames);
            
            if (!mathInit.Success)
            {
                return $"❌ Failed to initialize Agent C: {mathInit.Message}";
            }
            results.Add($"✅ Agent C initialized: {mathInit.Message}");
            results.Add("");

            // Step 3: Initialize Agent A (Orchestrator) with basic tools
            results.Add("📋 Step 3: Initializing Agent A (Task Orchestrator)");
            var orchestratorToolNames = new List<string>
            {
                "Text.CountWords",  // Some basic tools for text processing
                "Text.ToUpperCase"
            };

            var orchestratorConfig = new AgentConfiguration
            {
                SystemPrompt = @"You are Agent A, the Task Orchestrator for multi-agent economic analysis.
                               Your role is to break down complex economic analysis tasks into steps and coordinate other agents.
                               
                               You have access to callable agents through your agent function registry:
                               - You can call web-search-agent for data gathering tasks
                               - You can call math-agent for mathematical calculations
                               
                               For GDP analysis tasks:
                               1. First, call the web-search-agent to gather GDP data
                               2. Then, call the math-agent to perform calculations
                               3. Coordinate the results into a comprehensive analysis
                               
                               Always explain your coordination strategy and provide clear task breakdowns.
                               Use the agent communication functions available to you.",
                AgentName = "TaskOrchestrator",
                Temperature = 0.2,
                MaxTokens = 4000
            };

            var orchestratorAgent = _clusterClient.GetGrain<IConfigurableAgentGrain>("orchestrator-agent");
            var orchestratorInit = await orchestratorAgent.InitializeAsync(orchestratorConfig, orchestratorToolNames);
            
            if (!orchestratorInit.Success)
            {
                return $"❌ Failed to initialize Agent A: {orchestratorInit.Message}";
            }
            results.Add($"✅ Agent A initialized: {orchestratorInit.Message}");
            results.Add("");

            // Step 4: Set callable agents for Agent A
            results.Add("📋 Step 4: Setting Callable Agents for Agent A");
            var callableAgents = new List<CallableAgent> 
            { 
                new CallableAgent("web-search-agent", "Web Search Agent", "Searches the web for information and answers questions using online resources"),
                new CallableAgent("math-agent", "Math Agent", "Performs mathematical calculations and solves math problems")
            };
            var setCallableResult = await orchestratorAgent.SetCallableAgentsAsync(callableAgents);
            
            if (!setCallableResult.Success)
            {
                return $"❌ Failed to set callable agents: {setCallableResult.Message}";
            }
            results.Add($"✅ {setCallableResult.Message}");
            
            // Verify callable agents
            var currentCallableAgents = await orchestratorAgent.GetCallableAgentsAsync();
            results.Add($"📋 Current callable agents: {string.Join(", ", currentCallableAgents)}");
            
            var agentFunctionNames = await orchestratorAgent.GetAvailableAgentFunctionNamesAsync();
            results.Add($"🔧 Available agent functions: {string.Join(", ", agentFunctionNames)}");
            results.Add("");

            // Step 5: Execute the GDP Analysis Task
            results.Add("📋 Step 5: Executing GDP Analysis Task with Callable Agents");
            var gdpAnalysisTask = @"Please coordinate a multi-agent analysis to find US and New York state GDP in 2024, 
                                   then calculate what percentage of US GDP was New York state.
                                   
                                   You have access to callable agents through your agent function registry:
                                   - Callweb-search-agent: for web search and data gathering
                                   - Callmath-agent: for mathematical calculations
                                   
                                   Break this down into steps:
                                   1. Use Callweb-search-agent to find US GDP for 2024
                                   2. Use Callweb-search-agent to find New York State GDP for 2024  
                                   3. Use Callmath-agent to calculate the percentage: (NY GDP ÷ US GDP) × 100
                                   
                                   Coordinate the entire process and provide a comprehensive analysis.";

            var analysisResult = await orchestratorAgent.ExecuteTaskAsync(gdpAnalysisTask);
            results.Add("🤖 Enhanced Multi-Agent Analysis Result:");
            results.Add("=======================================");
            results.Add(analysisResult);
            results.Add("");

            // Step 6: Show agent state information
            results.Add("📊 Agent State Information:");
            results.Add("===========================");
            var orchestratorState = await orchestratorAgent.GetStateAsync();
            results.Add($"🔗 Callable Agents Count: {orchestratorState.CallableAgents.Count}");
            results.Add($"🔧 Agent Functions Count: {orchestratorState.AgentFunctionRegistry?.GetAgentFunctionCount() ?? 0}");
            results.Add($"📈 Total Tasks Executed: {orchestratorState.TotalTasks}");
            results.Add($"✅ Success Rate: {orchestratorState.GetSuccessRate():F1}%");

            return string.Join("\n", results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in Enhanced Multi-Agent GDP Analysis");
            return $"❌ Error in Enhanced Multi-Agent GDP Analysis: {ex.Message}";
        }
    }
} 
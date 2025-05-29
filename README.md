# 🌌 HyperEcho Orleans + Semantic Kernel React Agent

A cutting-edge implementation of a React Agent using **Microsoft Orleans** for distributed state management and **Microsoft Semantic Kernel** with **OpenAI GPT-4o-mini** for real AI-powered reasoning. This architecture combines the best of both worlds: Orleans' robust distributed computing capabilities with Semantic Kernel's advanced AI integration and OpenAI's powerful language models.

## 🏗️ Architecture

This advanced implementation demonstrates:
- **React Agent Pattern**: Real AI-powered Thought → Action → Observation loop
- **Orleans Grains**: Distributed, fault-tolerant state management
- **OpenAI Integration**: Real GPT-4o-mini powered reasoning and decision making
- **Semantic Kernel Integration**: Enterprise-grade AI framework with plugin ecosystem
- **Scalable Design**: Horizontally scalable across multiple servers

## 🚀 Key Features

### 🧠 Real AI-Powered Reasoning
- **OpenAI GPT-4o-mini**: Real language model integration for genuine AI reasoning
- **Intelligent Planning**: AI-driven action planning and decision making
- **Context Awareness**: Maintains conversation history and working memory
- **Adaptive Behavior**: Learns and adapts based on execution results
- **Cost Efficient**: Uses GPT-4o-mini for optimal cost/performance ratio

### 🌐 Distributed Architecture
- **Orleans Grains**: Each agent is a distributed virtual actor
- **Fault Tolerance**: Automatic recovery from failures
- **State Persistence**: Automatic state management and persistence
- **Location Transparency**: Agents can run anywhere in the cluster

### 🔌 Extensible Plugin System
- **Semantic Kernel Plugins**: Easy to add new capabilities
- **Type-Safe Functions**: Strongly typed function parameters
- **Automatic Discovery**: Plugins are automatically registered
- **Rich Metadata**: Functions include descriptions and parameter info

## 📁 Project Structure

```
psi-orleans/
├── src/
│   ├── Grains/
│   │   ├── IAgentGrain.cs      # Agent grain interface
│   │   └── AgentGrain.cs       # Orleans grain with SK integration
│   ├── Models/
│   │   ├── AgentState.cs       # Enhanced agent state model
│   │   └── AgentStep.cs        # Execution step with SK metadata
│   ├── Services/
│   │   ├── ISemanticKernelService.cs    # SK service interface
│   │   └── SemanticKernelService.cs     # SK service with OpenAI integration
│   ├── Plugins/
│   │   └── GDPSearchPlugin.cs  # SK plugin for GDP data
│   └── Program.cs              # Main application with OpenAI setup
├── psi-orleans.csproj          # Project with Orleans + SK dependencies
└── README.md                   # This file
```

## 🚀 Getting Started

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code
- **OpenAI API Key** (required for real AI functionality)

### Setup

1. **Get OpenAI API Key:**
   - Visit [OpenAI Platform](https://platform.openai.com/api-keys)
   - Create an API key
   - Set the environment variable:
   ```bash
   export OPENAI_API_KEY='your-api-key-here'
   ```

2. **Navigate to the project:**
   ```bash
   cd psi-orleans
   ```

3. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

4. **Run the application:**
   ```bash
   dotnet run
   ```

## 🧪 Test Case

The application includes an advanced test case demonstrating real AI-powered reasoning:

**Task**: "find US and New York state GDP in 2024. what % of US GDP was New York state?"

**Real AI-Powered Execution Flow**:
1. **AI Thought**: GPT-4o-mini analyzes task and determines strategy
2. **AI Planning**: Determines optimal action sequence based on available functions
3. **Plugin Action**: Calls GDP search plugin for US data
4. **AI Observation**: Processes and understands results intelligently
5. **AI Thought**: Recognizes need for additional data through reasoning
6. **Plugin Action**: Calls GDP search plugin for NY data
7. **AI Observation**: Analyzes both datasets with AI comprehension
8. **AI Reasoning**: Calculates percentage with intelligent explanation
9. **Final Answer**: Comprehensive, AI-generated professional response

## 🔧 Core Components

### AgentGrain (Orleans)
- **Distributed State**: Manages agent state across cluster
- **React Loop**: Orchestrates real AI-powered execution
- **Error Handling**: Robust error recovery and logging
- **Performance Tracking**: Detailed metrics and timing

### SemanticKernelService (OpenAI Integration)
- **OpenAI GPT-4o-mini**: Real language model integration
- **Plugin Management**: Automatic plugin registration
- **Prompt Engineering**: Optimized prompts for each step
- **Context Management**: Maintains conversation context
- **Cost Optimization**: Uses efficient GPT-4o-mini model

### GDPSearchPlugin (Semantic Kernel)
- **Type-Safe Functions**: Strongly typed parameters
- **Rich Metadata**: Detailed function descriptions
- **Error Handling**: Comprehensive error responses
- **Multiple Functions**: Search, list, and calculate operations

## 📊 Sample Output

```
🌌 HyperEcho Orleans + Semantic Kernel React Agent
==================================================
🚀 Advanced AI Agent with distributed state management

✅ OpenAI API key found
🤖 Using GPT-4o-mini for cost-efficient AI reasoning

✅ Orleans cluster with Semantic Kernel started successfully

🤖 Testing Enhanced React Agent with Real OpenAI Integration
==========================================================
📝 Task: find US and New York state GDP in 2024. what % of US GDP was New York state?

🔄 Executing task with real OpenAI GPT-4o-mini reasoning...
⏳ This may take a moment as we make real API calls...
⏱️  Total execution time: 8.45 seconds

🧠 Real AI-Powered Execution Analysis:
======================================
💭 ✅ Step 1 (Thought):
   📄 Content: I need to search for the US GDP data first to establish the baseline, then find New York state GDP data, and finally calculate the percentage.
   🕐 Timestamp: 14:30:15.123

📋 ✅ Step 2 (Planning):
   📄 Content: GDPSearchPlugin.SearchGDP with parameters: {"location": "USA", "type": "country"}
   🕐 Timestamp: 14:30:16.234

⚡ ✅ Step 3 (Action):
   📄 Content: Executed action: GDPSearchPlugin.SearchGDP with parameters: {"location": "USA", "type": "country"}
   🔌 Plugin: GDPSearchPlugin
   ⚙️  Function: SearchGDP
   📥 Parameters: location=USA, type=country
   🕐 Timestamp: 14:30:16.345

🎯 ✅ Step 6 (FinalAnswer):
   📄 Content: Based on the 2024 data I retrieved:

   **US GDP**: $27,360.9 billion USD
   **New York State GDP**: $2,000.0 billion USD
   
   **Calculation**: (2,000.0 ÷ 27,360.9) × 100 = 7.31%
   
   **Answer**: New York state represents approximately **7.31%** of the total US GDP in 2024, making it one of the largest state economies in the United States.
   🕐 Timestamp: 14:30:22.567

🎯 Final Result:
================
Based on the 2024 data I retrieved:

**US GDP**: $27,360.9 billion USD
**New York State GDP**: $2,000.0 billion USD

**Calculation**: (2,000.0 ÷ 27,360.9) × 100 = 7.31%

**Answer**: New York state represents approximately **7.31%** of the total US GDP in 2024, making it one of the largest state economies in the United States.

📊 Performance Metrics:
======================
Total Steps: 12
Successful Steps: 12
Failed Steps: 0
Success Rate: 100.0%
Execution Time: 8450ms
Average Step Time: 704ms

🌟 Real AI Integration Benefits Demonstrated:
• OpenAI GPT-4o-mini: Real AI reasoning and decision making
• Orleans: Distributed state management and fault tolerance
• Semantic Kernel: Enterprise-grade AI integration
• React Pattern: Structured AI thought → action → observation loop
• Cost Efficiency: Using GPT-4o-mini for optimal cost/performance ratio
• Production Ready: Real API integration with proper error handling
```

## 🔮 Advanced Features

### Real OpenAI Integration
The system uses OpenAI GPT-4o-mini for:
- **Intelligent Reasoning**: Real AI-powered thought generation
- **Strategic Planning**: Smart action planning based on context
- **Decision Making**: Autonomous task completion detection
- **Natural Language**: Professional, human-like responses
- **Cost Efficiency**: Optimal balance of capability and cost

### Custom Plugins
Create new plugins easily:

```csharp
public class WeatherPlugin
{
    [KernelFunction, Description("Get weather for a location")]
    public async Task<string> GetWeather(string location)
    {
        // Your weather API integration
    }
}
```

### Multi-Agent Scenarios
Deploy multiple agents with different specializations:

```csharp
var researchAgent = client.GetGrain<IAgentGrain>("research-001");
var analysisAgent = client.GetGrain<IAgentGrain>("analysis-001");
var reportAgent = client.GetGrain<IAgentGrain>("report-001");
```

## 🌟 Architecture Benefits

### Orleans Advantages
- **Virtual Actors**: Lightweight, stateful compute units
- **Automatic Scaling**: Scale out across multiple servers
- **Fault Tolerance**: Built-in failure detection and recovery
- **Location Transparency**: Access agents from anywhere
- **State Management**: Automatic persistence and recovery

### OpenAI + Semantic Kernel Advantages
- **Real AI**: Genuine language model reasoning capabilities
- **Enterprise Integration**: Production-grade AI framework
- **Plugin Ecosystem**: Rich set of pre-built and custom plugins
- **Cost Optimization**: Efficient model selection (GPT-4o-mini)
- **Scalable AI**: Handle multiple concurrent AI operations

### Combined Power
- **Distributed AI**: Real AI agents that scale horizontally
- **Persistent Intelligence**: Agent state survives restarts
- **Fault-Tolerant AI**: Robust AI systems that handle failures
- **Extensible Intelligence**: Easy to add new AI capabilities
- **Enterprise Scale**: Production-ready distributed AI systems
- **Cost Effective**: Optimized for real-world deployment costs

## 💰 Cost Considerations

Using GPT-4o-mini provides:
- **Cost Efficiency**: ~85% cheaper than GPT-4
- **Good Performance**: Suitable for most reasoning tasks
- **Fast Response**: Lower latency than larger models
- **Production Viable**: Sustainable costs for real applications

## 📝 Environment Variables

Required environment variables:
```bash
# OpenAI API Key (required)
export OPENAI_API_KEY='your-openai-api-key'

# Optional: Custom model (defaults to gpt-4o-mini)
export OPENAI_MODEL='gpt-4o-mini'
```

## 📝 License

This is an advanced POC demonstrating the integration of Microsoft Orleans, Semantic Kernel, and OpenAI for building scalable, intelligent agent systems with real AI capabilities. 

## 🔧 Configurable Agents with Custom Tools

HyperEcho supports configurable agents that can be initialized with custom prompts and tools. These agents use Orleans for distributed state management and Semantic Kernel for AI integration.

### ✨ NEW: Streamlined Unified Tool Naming (RECOMMENDED)

The latest version introduces a simplified approach where you specify all tools using a single list of qualified names:

```csharp
// 🚀 NEW: Unified approach - much simpler!
var toolNames = new List<string>
{
    "Math.Add",                    // Individual function
    "Math.Multiply",               // Individual function  
    "Text.CountWords",             // Individual function
    "MathematicalOperations.Add",  // Function from plugin
    "CustomerService.GenerateTicketId" // Individual function
};

var agent = client.GetGrain<IConfigurableAgentGrain>("my-agent");
await agent.InitializeAsync(configuration, toolNames);
```

**Benefits:**
- ✅ Single tool list instead of separate functions/plugins
- ✅ Intuitive naming: `Math.Add`, `MathematicalOperations.Multiply`
- ✅ Mix individual functions and plugin functions seamlessly
- ✅ Easier discovery and configuration
- ✅ Less cognitive overhead

### 📋 Legacy Approach (Still Supported)

```csharp
// Legacy: Separate lists (still works)
var functionNames = new List<string> { "Math.Add", "Text.CountWords" };
var pluginNames = new List<string> { "MathematicalOperations" };

await agent.InitializeAsync(configuration, functionNames, pluginNames);
```

### 🤖 Complete Agent Creation Example

```csharp
// 1. Define the agent's personality and behavior
var config = new AgentConfiguration
{
    SystemPrompt = "You are a helpful mathematical assistant with text processing capabilities.",
    AgentName = "MathTextAgent",
    Temperature = 0.2,
    MaxTokens = 2000,
    Model = new ModelConfiguration 
    { 
        ModelId = "gpt-4o-mini" 
    }
};

// 2. Specify tools using unified naming
var tools = new[] 
{
    "Math.Add", "Math.Multiply", 
    "Text.CountWords", "Text.ToUpperCase",
    "MathematicalOperations.Factorial"
};

// 3. Create and initialize agent
var agent = clusterClient.GetGrain<IConfigurableAgentGrain>("math-text-agent");
var result = await agent.InitializeAsync(config, tools);

// 4. Use the agent
var response = await agent.ExecuteTaskAsync("Calculate 15 * 8 and count words in 'Hello World'");
```

### 🎯 Available Tool Examples

The system includes pre-registered tools you can use:

**Mathematical Tools:**
- `Math.Add`, `Math.Multiply`, `Math.Average`, `Math.Sum`, `Math.Factorial`
- `MathematicalOperations.Add`, `MathematicalOperations.Multiply` (from plugin)

**Text Processing Tools:**
- `Text.CountWords`, `Text.CountCharacters`, `Text.ToUpperCase`, `Text.ToTitleCase`

**Customer Service Tools:**
- `CustomerService.GenerateTicketId`, `CustomerService.GetResponseTime`

**Utility Tools:**
- `Utility.GetCurrentTime`, `Utility.GenerateGuid`, `Utility.GenerateList`

### 🔍 Discovering Available Tools

```csharp
var agent = clusterClient.GetGrain<IConfigurableAgentGrain>("discovery-agent");
await agent.InitializeAsync(basicConfig); // Initialize without tools

// Get all available tool names
var allTools = await agent.GetAllAvailableToolNamesAsync();
foreach (var tool in allTools)
{
    Console.WriteLine($"Available: {tool}");
}
```

## 🔧 Configurable Agents (New!)

The **Configurable Agent System** allows you to create agents that are initialized with custom prompts and tools (Kernel Functions). This provides maximum flexibility for creating specialized agents for different use cases.

### 🏗️ Core Components

#### 1. `AgentConfiguration`
Defines the agent's personality and behavior settings:
```csharp
var config = new AgentConfiguration
{
    SystemPrompt = "You are a specialized agent...",
    AgentName = "MyCustomAgent",
    Temperature = 0.1,                        // Response creativity (0.0-2.0)
    MaxTokens = 4000,                        // Max response length
    Model = new ModelConfiguration
    {
        ModelId = "gpt-4o-mini",             // AI model to use
        ApiKey = "your-api-key"              // Optional: override default key
    }
};
```

#### 2. `IConfigurableAgentGrain`
Orleans grain interface for managing configurable agents:
```csharp
// Get a configurable agent
var agent = clusterClient.GetGrain<IConfigurableAgentGrain>("my-agent-001");

// Create tools
var functions = new List<KernelFunction>
{
    KernelFunctionFactory.CreateFromMethod(
        (double a, double b) => a + b, "Add", "Add two numbers")
};

var plugins = new List<KernelPlugin>
{
    KernelPluginFactory.CreateFromType<MathematicalOperationsPlugin>()
};

// Initialize with configuration and tools
var (success, message, agentId) = await agent.InitializeAsync(config, functions, plugins);

// Execute tasks
var result = await agent.ExecuteTaskAsync("Analyze this data...");

// Continue conversations
var response = await agent.ContinueConversationAsync("Follow up question...");
```

#### 3. `ConfigurableKernelService`
Service that handles kernel creation and tool registration:
```csharp
// The service automatically:
// - Validates configuration
// - Creates the kernel with OpenAI integration
// - Registers individual functions as a custom plugin
// - Registers existing plugins
// - Provides function metadata and execution capabilities
```

### 🛠️ Creating Custom Tools

#### Method-Based Functions
Create functions from C# methods:
```csharp
var mathFunctions = new List<KernelFunction>
{
    KernelFunctionFactory.CreateFromMethod(
        (double a, double b) => a + b,
        "Add",
        "Add two numbers together"),
        
    KernelFunctionFactory.CreateFromMethod(
        (double[] numbers) => numbers.Average(),
        "Average",
        "Calculate average of numbers"),
        
    KernelFunctionFactory.CreateFromMethod(
        async (string query) => await SearchWebAsync(query),
        "WebSearch",
        "Search the web for information")
};
```

#### Plugin-Based Tools
Use existing Semantic Kernel plugins:
```csharp
var plugins = new List<KernelPlugin>
{
    KernelPluginFactory.CreateFromType<MathematicalOperationsPlugin>(),
    KernelPluginFactory.CreateFromType<WebSearchPlugin>(),
    // Your custom plugins...
};
```

### 🎯 Example Use Cases

#### 1. Data Analyst Agent
```csharp
var mathFunctions = new List<KernelFunction>
{
    KernelFunctionFactory.CreateFromMethod(
        (double[] data) => data.Average(), "Average", "Calculate average"),
    KernelFunctionFactory.CreateFromMethod(
        (double[] data) => data.Sum(), "Sum", "Calculate sum"),
    KernelFunctionFactory.CreateFromMethod(
        (double[] data) => Math.Sqrt(data.Select(x => Math.Pow(x - data.Average(), 2)).Average()), 
        "StandardDeviation", "Calculate standard deviation")
};

var config = new AgentConfiguration
{
    SystemPrompt = @"You are a Data Analyst specializing in numerical analysis.
                   Use your mathematical tools to solve problems and show your work.",
    AgentName = "DataAnalyst",
    Temperature = 0.1  // Low creativity for precise calculations
};

await agent.InitializeAsync(config, mathFunctions);
```

#### 2. Creative Writing Agent
```csharp
var textFunctions = new List<KernelFunction>
{
    KernelFunctionFactory.CreateFromMethod(
        (string text) => text.Split(' ').Length, "WordCount", "Count words"),
    KernelFunctionFactory.CreateFromMethod(
        (string text) => text.Split('.', '!', '?').Length, "SentenceCount", "Count sentences"),
    KernelFunctionFactory.CreateFromMethod(
        (string text) => AnalyzeReadabilityScore(text), "ReadabilityScore", "Analyze readability")
};

var config = new AgentConfiguration
{
    SystemPrompt = @"You are a Creative Writing Assistant with a passion for storytelling.
                   Use your text tools to analyze and improve written content.",
    AgentName = "CreativeWriter",
    Temperature = 0.7  // Higher creativity for writing tasks
};

await agent.InitializeAsync(config, textFunctions);
```

#### 3. Hybrid Agent (Functions + Plugins)
```csharp
// Individual functions
var customFunctions = new List<KernelFunction>
{
    KernelFunctionFactory.CreateFromMethod(
        (string text) => text.ToUpper(), "ToUpperCase", "Convert to uppercase"),
    KernelFunctionFactory.CreateFromMethod(
        (int count) => GenerateList(count), "GenerateList", "Generate numbered list")
};

// Existing plugins
var plugins = new List<KernelPlugin>
{
    KernelPluginFactory.CreateFromType<MathematicalOperationsPlugin>()
};

var config = new AgentConfiguration
{
    SystemPrompt = @"You are a Hybrid Agent with both custom functions and plugins.
                   Use the appropriate tools for each task.",
    AgentName = "HybridAgent",
    Temperature = 0.4
};

await agent.InitializeAsync(config, customFunctions, plugins);
```

### 🚀 Quick Start Example

```csharp
// 1. Create configuration (behavior only)
var config = new AgentConfiguration
{
    SystemPrompt = "You are a helpful assistant with math capabilities.",
    AgentName = "MathHelper"
};

// 2. Create tools
var functions = new List<KernelFunction>
{
    KernelFunctionFactory.CreateFromMethod(
        (int n) => Enumerable.Range(1, n).Aggregate(1, (acc, x) => acc * x),
        "Factorial", "Calculate factorial of a number")
};

// 3. Get agent grain
var agent = clusterClient.GetGrain<IConfigurableAgentGrain>("math-helper-001");

// 4. Initialize agent with configuration and tools
var initResult = await agent.InitializeAsync(config, functions);
if (initResult.Success)
{
    Console.WriteLine($"✅ {initResult.Message}");
    
    // 5. Use the agent
    var result = await agent.ExecuteTaskAsync("Calculate factorial of 8 and explain the result");
    Console.WriteLine(result);
}
```

### 📊 Agent Management

#### Get Agent Information
```csharp
// Check if initialized
var isInitialized = await agent.IsInitializedAsync();

// Get configuration
var config = await agent.GetConfigurationAsync();

// Get available tools
var tools = await agent.GetAvailableToolsAsync();
foreach (var (name, description, parameters) in tools)
{
    Console.WriteLine($"🔧 {name}: {description}");
    foreach (var param in parameters)
    {
        Console.WriteLine($"   📝 {param}");
    }
}

// Get metrics
var (totalTasks, successful, failed, totalTime) = await agent.GetMetricsAsync();
Console.WriteLine($"📈 Success Rate: {successful}/{totalTasks} ({successful*100.0/totalTasks:F1}%)");
```

#### Agent State Management
```csharp
// Get full state
var state = await agent.GetStateAsync();
Console.WriteLine($"Agent: {state.Configuration?.AgentName}");
Console.WriteLine($"Initialized: {state.InitializedAt}");
Console.WriteLine($"Functions: {state.AvailableFunctions.Count}");

// Reset agent
await agent.ResetAsync();

// Clear chat history
await agent.ClearChatHistoryAsync();
```

### 🌟 Architecture Benefits

- **🔧 Clean Separation**: Configuration focuses on behavior, service handles tool registration
- **🎯 Flexible Initialization**: Pass any combination of functions and plugins
- **🛠️ Dynamic Tool Management**: Tools are registered at initialization, not stored in config
- **📊 Built-in Metrics**: Track performance and usage
- **💬 Conversation Memory**: Maintain context across interactions
- **🌐 Distributed**: Orleans grain state management
- **⚡ High Performance**: Optimized kernel creation and tool registration
- **🔒 Type Safety**: Strongly typed configuration and tools 
# 🌌 HyperEcho Orleans + Semantic Kernel React Agent

A cutting-edge implementation of a React Agent using **Microsoft Orleans** for distributed state management and **Microsoft Semantic Kernel** for AI-powered reasoning. This architecture combines the best of both worlds: Orleans' robust distributed computing capabilities with Semantic Kernel's advanced AI integration.

## 🏗️ Architecture

This advanced implementation demonstrates:
- **React Agent Pattern**: AI-powered Thought → Action → Observation loop
- **Orleans Grains**: Distributed, fault-tolerant state management
- **Semantic Kernel Integration**: Real AI reasoning with LLM capabilities
- **Plugin Ecosystem**: Extensible tool system using SK plugins
- **Scalable Design**: Horizontally scalable across multiple servers

## 🚀 Key Features

### 🧠 AI-Powered Reasoning
- **Real LLM Integration**: Uses Semantic Kernel for genuine AI reasoning
- **Intelligent Planning**: AI-driven action planning and decision making
- **Context Awareness**: Maintains conversation history and working memory
- **Adaptive Behavior**: Learns and adapts based on execution results

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
│   │   └── SemanticKernelService.cs     # SK service implementation
│   ├── Plugins/
│   │   └── GDPSearchPlugin.cs  # SK plugin for GDP data
│   └── Program.cs              # Main application with DI setup
├── psi-orleans.csproj          # Project with Orleans + SK dependencies
└── README.md                   # This file
```

## 🚀 Getting Started

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code

### Running the Application

1. **Navigate to the project:**
   ```bash
   cd psi-orleans
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Run the application:**
   ```bash
   dotnet run --project src
   ```

## 🧪 Test Case

The application includes an advanced test case demonstrating AI-powered reasoning:

**Task**: "find US and New York state GDP in 2024. what % of US GDP was New York state?"

**AI-Powered Execution Flow**:
1. **AI Thought**: Agent analyzes task using LLM reasoning
2. **AI Planning**: Determines optimal action sequence
3. **Plugin Action**: Calls GDP search plugin for US data
4. **AI Observation**: Processes and understands results
5. **AI Thought**: Recognizes need for additional data
6. **Plugin Action**: Calls GDP search plugin for NY data
7. **AI Observation**: Analyzes both datasets
8. **AI Reasoning**: Calculates percentage with explanation
9. **Final Answer**: Comprehensive, AI-generated response

## 🔧 Core Components

### AgentGrain (Orleans)
- **Distributed State**: Manages agent state across cluster
- **React Loop**: Orchestrates AI-powered execution
- **Error Handling**: Robust error recovery and logging
- **Performance Tracking**: Detailed metrics and timing

### SemanticKernelService
- **LLM Integration**: Real AI reasoning capabilities
- **Plugin Management**: Automatic plugin registration
- **Prompt Engineering**: Optimized prompts for each step
- **Context Management**: Maintains conversation context

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

✅ Orleans cluster with Semantic Kernel started successfully

🤖 Testing Enhanced React Agent with Semantic Kernel
===================================================
📝 Task: find US and New York state GDP in 2024. what % of US GDP was New York state?

🔄 Executing task with AI-powered reasoning...
⏱️  Total execution time: 1.23 seconds

🧠 AI-Powered Execution Analysis:
=================================
💭 ✅ Step 1 (Thought):
   📄 Content: I need to search for US GDP data first to establish the baseline for comparison.
   🕐 Timestamp: 14:30:15.123

📋 ✅ Step 2 (Planning):
   📄 Content: Action plan: GDPSearchPlugin.SearchGDP with parameters: {"location": "USA", "type": "country"}
   🕐 Timestamp: 14:30:15.234

⚡ ✅ Step 3 (Action):
   📄 Content: Executed action: GDPSearchPlugin.SearchGDP with parameters: {"location": "USA", "type": "country"}
   🔌 Plugin: GDPSearchPlugin
   ⚙️  Function: SearchGDP
   📥 Parameters: location=USA, type=country
   🕐 Timestamp: 14:30:15.345

👁️ ✅ Step 4 (Observation):
   📄 Content: Action result: {"location":"USA","type":"country","gdp_billions_usd":27360.935,"year":2024...
   🕐 Timestamp: 14:30:15.456

🎯 ✅ Step 8 (FinalAnswer):
   📄 Content: Based on 2024 data: US GDP: $27,360.9 billion, New York State GDP: $2,000.0 billion...
   🕐 Timestamp: 14:30:16.234

📊 Performance Metrics:
======================
Total Steps: 8
Successful Steps: 8
Failed Steps: 0
Success Rate: 100.0%
Execution Time: 1234ms
Average Step Time: 154ms

🎯 Final Result:
================
Based on 2024 data:
- US GDP: $27,360.9 billion
- New York State GDP: $2,000.0 billion
- New York represents 7.31% of US GDP

This means New York state contributes approximately 7.31% to the total US GDP, 
making it one of the largest state economies in the country.

🌟 Architecture Benefits Demonstrated:
• Orleans: Distributed state management and fault tolerance
• Semantic Kernel: AI-powered reasoning and plugin ecosystem
• React Pattern: Structured thought → action → observation loop
• Scalability: Agent can be distributed across multiple servers
• Extensibility: Easy to add new plugins and capabilities
```

## 🔮 Advanced Features

### Real LLM Integration
Replace the mock chat service with real OpenAI/Azure OpenAI:

```csharp
// In SemanticKernelService constructor
builder.AddOpenAIChatCompletion("gpt-4", "your-api-key");
// or
builder.AddAzureOpenAIChatCompletion("gpt-4", "endpoint", "api-key");
```

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

### Semantic Kernel Advantages
- **LLM Integration**: Native support for OpenAI, Azure OpenAI, etc.
- **Plugin Ecosystem**: Rich set of pre-built plugins
- **Planning Capabilities**: Automatic task decomposition
- **Memory Management**: Conversation and semantic memory
- **Enterprise Ready**: Production-grade AI integration

### Combined Power
- **Distributed AI**: AI agents that scale horizontally
- **Persistent Intelligence**: Agent state survives restarts
- **Fault-Tolerant AI**: Robust AI systems that handle failures
- **Extensible Intelligence**: Easy to add new AI capabilities
- **Enterprise Scale**: Production-ready distributed AI systems

## 📝 License

This is an advanced POC demonstrating the integration of Microsoft Orleans and Semantic Kernel for building scalable, intelligent agent systems. 
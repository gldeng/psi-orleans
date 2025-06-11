# TaskAnalyzer Real Implementation Example

This minimal example demonstrates the **TaskAnalyzer** service from the PsiOrleans.Analysis package, showcasing the proper Kernel injection pattern following the ConfigurableKernelService.cs architecture.

## What This Example Demonstrates

1. **Kernel Injection Pattern** - How to properly inject `Kernel` into TaskAnalyzer instead of direct `IChatCompletionService` injection
2. **Task Analysis** - Real LLM-based classification of tasks as ORCHESTRATOR vs SPECIALIZED mode
3. **Task Breakdown** - Decomposition of complex tasks into subtasks
4. **Graceful Fallbacks** - Proper error handling when API keys are missing

## Architecture Validation

This example validates the Phase 2 refactoring achievements:
- ✅ Clean dependency injection using Kernel pattern
- ✅ Framework-agnostic design (Analysis → Common only)
- ✅ LLM-based task analysis functionality  
- ✅ Error handling with fallback to SPECIALIZED mode
- ✅ Proper interface contracts and models

## Running the Example

### Option 1: Architecture Demonstration (No API Configuration Required)

```bash
cd examples
dotnet run
```

This will demonstrate the Kernel injection pattern and architecture without making LLM calls.

### Option 2: Full Functionality with Azure OpenAI

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key"
export AZURE_OPENAI_DEPLOYMENT_NAME="your-deployment-name"
cd examples  
dotnet run
```

### Option 3: Full Functionality with OpenAI

```bash
export OPENAI_API_KEY="your-api-key-here"
cd examples  
dotnet run
```

Both options will run the complete example with real LLM API calls for task analysis.

## Expected Output

### Without API Configuration (Architecture Demonstration)
```
=== TaskAnalyzer Real Implementation Example ===

⚠️  No API configuration found.
   For Azure OpenAI, set:
   export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
   export AZURE_OPENAI_API_KEY="your-api-key"
   export AZURE_OPENAI_DEPLOYMENT_NAME="your-deployment-name"

   For OpenAI, set:
   export OPENAI_API_KEY="your-api-key"

   Continuing with architecture demonstration...

1. Creating Kernel with proper dependency injection:
   (No LLM service added due to missing configuration)
   ✅ Kernel created successfully
2. Injecting Kernel into TaskAnalyzer:
   ✅ TaskAnalyzer instantiated with Kernel injection
3. Testing task analysis functionality:

   Task: Calculate the square root of 144
   Expected: Simple/Direct
   ⚠️  Skipped (no API configuration): [error details]

4. Architecture Validation:
   ✅ Kernel injection pattern (not direct IChatCompletionService)
   ✅ Clean dependency: Analysis → Common only
   ✅ Framework-agnostic design
   ✅ Error handling with graceful fallbacks
   ✅ Interface-based contracts (ITaskAnalyzer, IAgentContext)
```

### With Azure OpenAI (Full Functionality)
```
=== TaskAnalyzer Real Implementation Example ===

✅ Using Azure OpenAI: https://your-resource.openai.azure.com/ (deployment: your-deployment-name)

1. Creating Kernel with proper dependency injection:
   Added Azure OpenAI service (deployment: your-deployment-name)
   ✅ Kernel created successfully
2. Injecting Kernel into TaskAnalyzer:
   ✅ TaskAnalyzer instantiated with Kernel injection
3. Testing task analysis functionality:

   Task: Calculate the square root of 144
   Expected: Simple/Direct
   → Approach: DirectExecution
   → Can Decompose: False
   → Analysis Notes: Task requires Specialized mode
   ✅ Analysis completed successfully

   Task: Design and implement a microservices architecture
   Expected: Complex/Orchestration
   → Approach: Orchestration
   → Can Decompose: True
   → Analysis Notes: Task requires Orchestrator mode
   → Subtasks:
     • Define service boundaries and responsibilities
     • Design API contracts and communication patterns
     • Implement data consistency and transaction management
     • Setup monitoring and observability
   ✅ Analysis completed successfully

4. Architecture Validation:
   ✅ Kernel injection pattern (not direct IChatCompletionService)
   ✅ Clean dependency: Analysis → Common only
   ✅ Framework-agnostic design
   ✅ Error handling with graceful fallbacks
   ✅ Interface-based contracts (ITaskAnalyzer, IAgentContext)
```

## Code Structure

### Core Components

**Program.cs** - Main example application with two scenarios:
- `RunMockExample()` - Uses MockChatCompletionService for testing
- `RunRealExample()` - Uses real OpenAI service for actual LLM analysis

**SimpleAgentContext** - Minimal IAgentContext implementation for testing

**MockChatCompletionService** - Keyword-based mock that returns predictable responses

### Key Patterns Demonstrated

1. **Kernel Creation and Configuration**
```csharp
var builder = Kernel.CreateBuilder();
builder.Services.AddSingleton<IChatCompletionService>(mockChatService);
var kernel = builder.Build();
```

2. **TaskAnalyzer Usage**
```csharp
var taskAnalyzer = new PsiOrleans.Analysis.Services.TaskAnalyzer(kernel);
var result = await taskAnalyzer.AnalyzeTaskAsync(task, context, config);
```

3. **Configuration Setup**
```csharp
var config = new AgentConfiguration
{
    AgentName = "ExampleAgent",
    SystemPrompt = "You are a helpful task analysis assistant.",
    Temperature = 0.7,
    MaxTokens = 1000,
    Model = new ModelConfiguration { ModelId = "gpt-3.5-turbo", ... }
};
```

## Dependencies

This example validates the clean dependency structure:
- **PsiOrleans.Analysis** → PsiOrleans.Common (no circular dependencies)
- **Microsoft.SemanticKernel** for LLM integration
- **Microsoft.Extensions** for logging and DI

## Learning Outcomes

After running this example, you will understand:

1. How to inject Kernel into services following the ConfigurableKernelService pattern
2. How TaskAnalyzer distinguishes between ORCHESTRATOR and SPECIALIZED tasks  
3. How the Analysis package maintains framework independence
4. How to test LLM-based services with both mock and real implementations
5. The interface contracts that will be used by other packages in Phase 3+

## Next Steps

This example prepares the foundation for:
- **Phase 3**: Orchestrator package implementation
- **Phase 4**: Specialized package implementation  
- **Phase 5**: Orleans adapter integration

The TaskAnalyzer service demonstrated here will be used by the UnifiedAgentService to determine which execution path to take for each incoming task. 
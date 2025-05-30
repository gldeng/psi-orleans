using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace PsiOrleans.Services;

/// <summary>
/// Implementation of agent function registry for managing inter-agent communication functions
/// </summary>
public class AgentFunctionRegistry : IAgentFunctionRegistry
{
    private readonly Dictionary<string, KernelFunction> _agentFunctions = new();
    private readonly ILogger<AgentFunctionRegistry> _logger;
    
    public AgentFunctionRegistry(ILogger<AgentFunctionRegistry> logger)
    {
        _logger = logger;
    }
    
    public void RegisterAgentFunction(string name, KernelFunction function)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Function name cannot be null or empty", nameof(name));
        
        if (function == null)
            throw new ArgumentNullException(nameof(function));
        
        _agentFunctions[name] = function;
        _logger.LogDebug("Registered agent function: {FunctionName}", name);
    }
    
    public KernelFunction? GetAgentFunction(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
            
        _agentFunctions.TryGetValue(name, out var function);
        return function;
    }
    
    public IEnumerable<KernelFunction> GetAgentFunctions(IEnumerable<string> names)
    {
        return names.Select(GetAgentFunction).Where(f => f != null).Cast<KernelFunction>();
    }
    
    public IEnumerable<string> GetAvailableAgentFunctionNames()
    {
        return _agentFunctions.Keys.ToList();
    }
    
    public bool HasAgentFunction(string name)
    {
        return _agentFunctions.ContainsKey(name);
    }
    
    public Dictionary<string, KernelFunction> GetAllAgentFunctions()
    {
        return new Dictionary<string, KernelFunction>(_agentFunctions);
    }
    
    public void ClearAgentFunctions()
    {
        _agentFunctions.Clear();
        _logger.LogDebug("Cleared all agent functions");
    }
    
    public int GetAgentFunctionCount()
    {
        return _agentFunctions.Count;
    }
    
    public bool RemoveAgentFunction(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
            
        var removed = _agentFunctions.Remove(name);
        if (removed)
        {
            _logger.LogInformation("Removed agent function: {FunctionName}", name);
        }
        else
        {
            _logger.LogWarning("Agent function {FunctionName} not found for removal", name);
        }
        
        return removed;
    }
} 
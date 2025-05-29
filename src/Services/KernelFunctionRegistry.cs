using Microsoft.SemanticKernel;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PsiOrleans.Services;

/// <summary>
/// Thread-safe registry implementation for managing kernel functions and plugins by name/ID
/// </summary>
public class KernelFunctionRegistry : IKernelFunctionRegistry
{
    private readonly ConcurrentDictionary<string, KernelFunction> _functions = new();
    private readonly ConcurrentDictionary<string, KernelPlugin> _plugins = new();
    private readonly ILogger<KernelFunctionRegistry> _logger;

    public KernelFunctionRegistry(ILogger<KernelFunctionRegistry> logger)
    {
        _logger = logger;
    }

    public void RegisterFunction(string name, KernelFunction function)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Function name cannot be null or empty", nameof(name));
        
        if (function == null)
            throw new ArgumentNullException(nameof(function));

        if (_functions.TryAdd(name, function))
        {
            _logger.LogInformation("Registered function: {FunctionName}", name);
        }
        else
        {
            _logger.LogWarning("Function {FunctionName} is already registered, skipping", name);
        }
    }

    public void RegisterPlugin(string name, KernelPlugin plugin)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Plugin name cannot be null or empty", nameof(name));
        
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        if (_plugins.TryAdd(name, plugin))
        {
            _logger.LogInformation("Registered plugin: {PluginName} with {FunctionCount} functions", 
                name, plugin.FunctionCount);
        }
        else
        {
            _logger.LogWarning("Plugin {PluginName} is already registered, skipping", name);
        }
    }

    public KernelFunction? GetFunction(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return _functions.TryGetValue(name, out var function) ? function : null;
    }

    public KernelPlugin? GetPlugin(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return _plugins.TryGetValue(name, out var plugin) ? plugin : null;
    }

    public IEnumerable<KernelFunction> GetFunctions(IEnumerable<string> names)
    {
        if (names == null)
            return Enumerable.Empty<KernelFunction>();

        var functions = new List<KernelFunction>();
        var notFound = new List<string>();

        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            if (_functions.TryGetValue(name, out var function))
            {
                functions.Add(function);
            }
            else
            {
                notFound.Add(name);
            }
        }

        if (notFound.Any())
        {
            _logger.LogWarning("Functions not found: {NotFoundFunctions}", string.Join(", ", notFound));
        }

        _logger.LogInformation("Retrieved {FoundCount} of {RequestedCount} functions", 
            functions.Count, names.Count());

        return functions;
    }

    public IEnumerable<KernelPlugin> GetPlugins(IEnumerable<string> names)
    {
        if (names == null)
            return Enumerable.Empty<KernelPlugin>();

        var plugins = new List<KernelPlugin>();
        var notFound = new List<string>();

        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            if (_plugins.TryGetValue(name, out var plugin))
            {
                plugins.Add(plugin);
            }
            else
            {
                notFound.Add(name);
            }
        }

        if (notFound.Any())
        {
            _logger.LogWarning("Plugins not found: {NotFoundPlugins}", string.Join(", ", notFound));
        }

        _logger.LogInformation("Retrieved {FoundCount} of {RequestedCount} plugins", 
            plugins.Count, names.Count());

        return plugins;
    }

    public IEnumerable<string> GetAvailableFunctionNames()
    {
        return _functions.Keys.ToList();
    }

    public IEnumerable<string> GetAvailablePluginNames()
    {
        return _plugins.Keys.ToList();
    }

    public bool HasFunction(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && _functions.ContainsKey(name);
    }

    public bool HasPlugin(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && _plugins.ContainsKey(name);
    }

    public KernelFunction? GetToolByQualifiedName(string qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
            return null;

        // First, try to get as individual function
        if (_functions.TryGetValue(qualifiedName, out var individualFunction))
        {
            return individualFunction;
        }

        // If not found, try to parse as plugin.function
        var parts = qualifiedName.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            var pluginName = parts[0];
            var functionName = parts[1];

            if (_plugins.TryGetValue(pluginName, out var plugin))
            {
                return plugin.TryGetFunction(functionName, out var pluginFunction) ? pluginFunction : null;
            }
        }

        return null;
    }

    public Dictionary<string, KernelFunction> GetToolsByQualifiedNames(IEnumerable<string> qualifiedNames)
    {
        var result = new Dictionary<string, KernelFunction>();
        var notFound = new List<string>();

        if (qualifiedNames == null)
            return result;

        foreach (var qualifiedName in qualifiedNames.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            var function = GetToolByQualifiedName(qualifiedName);
            if (function != null)
            {
                result[qualifiedName] = function;
            }
            else
            {
                notFound.Add(qualifiedName);
            }
        }

        if (notFound.Any())
        {
            _logger.LogWarning("Tools not found: {NotFoundTools}", string.Join(", ", notFound));
        }

        _logger.LogInformation("Retrieved {FoundCount} of {RequestedCount} tools", 
            result.Count, qualifiedNames.Count());

        return result;
    }

    public IEnumerable<string> GetAllAvailableToolNames()
    {
        var toolNames = new List<string>();

        // Add individual function names
        toolNames.AddRange(_functions.Keys);

        // Add plugin.function combinations
        foreach (var plugin in _plugins.Values)
        {
            foreach (var function in plugin)
            {
                toolNames.Add($"{plugin.Name}.{function.Name}");
            }
        }

        return toolNames.OrderBy(name => name).ToList();
    }

    public bool HasTool(string qualifiedName)
    {
        return GetToolByQualifiedName(qualifiedName) != null;
    }

    /// <summary>
    /// Get registry statistics for debugging and monitoring
    /// </summary>
    public (int FunctionCount, int PluginCount, int TotalPluginFunctions) GetStatistics()
    {
        var totalPluginFunctions = _plugins.Values.Sum(p => p.FunctionCount);
        return (_functions.Count, _plugins.Count, totalPluginFunctions);
    }

    /// <summary>
    /// Clear all registered functions and plugins (mainly for testing)
    /// </summary>
    public void Clear()
    {
        _functions.Clear();
        _plugins.Clear();
        _logger.LogInformation("Registry cleared");
    }
} 
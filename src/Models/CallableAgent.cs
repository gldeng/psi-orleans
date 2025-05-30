using Orleans;

namespace PsiOrleans.Models;

/// <summary>
/// Represents an agent that can be called by another agent
/// </summary>
[Serializable]
[GenerateSerializer]
public class CallableAgent
{
    /// <summary>
    /// Unique identifier for the agent
    /// </summary>
    [Id(0)]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable name for the agent
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of what this agent does
    /// </summary>
    [Id(2)]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Default constructor for Orleans serialization
    /// </summary>
    public CallableAgent()
    {
    }
    
    /// <summary>
    /// Initialize a new callable agent
    /// </summary>
    /// <param name="id">Unique agent identifier</param>
    /// <param name="name">Human-readable name</param>
    /// <param name="description">Description of agent capabilities</param>
    public CallableAgent(string id, string name, string description)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }
    
    /// <summary>
    /// Get the function name for calling this agent
    /// </summary>
    /// <returns>Function name in format Call_{CleanId}</returns>
    public string GetFunctionName()
    {
        return $"Call_{Id.Replace("-", "_").Replace(" ", "_")}";
    }
    
    public override string ToString()
    {
        return $"{Name} ({Id}): {Description}";
    }
    
    public override bool Equals(object? obj)
    {
        return obj is CallableAgent other && Id == other.Id;
    }
    
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
} 
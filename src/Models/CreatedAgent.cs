using System;
using System.Collections.Generic;

namespace PsiOrleans.Models;

/// <summary>
/// Metadata about a created agent for tracking and reuse
/// </summary>
public class CreatedAgent
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public List<string> Tools { get; set; } = new();
    public string Specialization { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int UsageCount { get; set; }
    public DateTime LastUsedAt { get; set; }
    
    /// <summary>
    /// Constructor for creating a new agent record
    /// </summary>
    public CreatedAgent(string agentId, string agentName, string systemPrompt, List<string> tools, string specialization = "")
    {
        AgentId = agentId;
        AgentName = agentName;
        SystemPrompt = systemPrompt;
        Tools = tools;
        Specialization = specialization;
        CreatedAt = DateTime.UtcNow;
        UsageCount = 0;
        LastUsedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Record usage of this agent
    /// </summary>
    public void RecordUsage()
    {
        UsageCount++;
        LastUsedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Check if this agent might be suitable for a given task based on its prompt and tools
    /// </summary>
    public bool MightHandleTask(string taskDescription)
    {
        var taskLower = taskDescription.ToLower();
        var promptLower = SystemPrompt.ToLower();
        var specializationLower = Specialization.ToLower();
        
        // Simple keyword matching - could be enhanced with semantic analysis
        return promptLower.Contains(taskLower.Contains("gdp") ? "gdp" : "") ||
               promptLower.Contains(taskLower.Contains("search") ? "search" : "") ||
               promptLower.Contains(taskLower.Contains("math") ? "math" : "") ||
               promptLower.Contains(taskLower.Contains("calculate") ? "calculat" : "") ||
               specializationLower.Contains(taskLower.Contains("data") ? "data" : "");
    }
    
    public override string ToString()
    {
        return $"{AgentName} ({AgentId}) - Used {UsageCount} times - Tools: {string.Join(", ", Tools)}";
    }
} 
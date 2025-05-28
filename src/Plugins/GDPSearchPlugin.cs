using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace PsiOrleans.Plugins;

/// <summary>
/// Semantic Kernel plugin for searching GDP data
/// </summary>
public class GDPSearchPlugin
{
    // Mock GDP data for 2024 - in production this would connect to real APIs
    private readonly Dictionary<string, decimal> _gdpData = new()
    {
        ["USA"] = 27360.935m, // US GDP in billions USD for 2024 (estimated)
        ["New York"] = 2000.0m, // New York state GDP in billions USD for 2024 (estimated)
        ["California"] = 3500.0m,
        ["Texas"] = 2400.0m,
        ["Florida"] = 1100.0m,
        ["Illinois"] = 900.0m,
        ["Pennsylvania"] = 850.0m
    };

    [KernelFunction, Description("Search for GDP data by location and type")]
    public async Task<string> SearchGDP(
        [Description("The location to search for (e.g., 'USA', 'New York', 'California')")] string location,
        [Description("The type of location ('country' or 'state')")] string type = "country")
    {
        await Task.Delay(150); // Simulate API call delay

        try
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return CreateErrorResponse("Location parameter is required");
            }

            // Normalize location name
            var normalizedLocation = NormalizeLocationName(location);

            if (_gdpData.TryGetValue(normalizedLocation, out var gdp))
            {
                var result = new
                {
                    location = normalizedLocation,
                    type = type.ToLower(),
                    gdp_billions_usd = gdp,
                    year = 2024,
                    currency = "USD",
                    unit = "billions",
                    source = "Mock Data Service",
                    timestamp = DateTime.UtcNow
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
            else
            {
                return CreateErrorResponse($"GDP data not found for '{location}'. Available locations: {string.Join(", ", _gdpData.Keys)}");
            }
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error executing GDP search: {ex.Message}");
        }
    }

    [KernelFunction, Description("Get list of available locations for GDP data")]
    public async Task<string> GetAvailableLocations()
    {
        await Task.Delay(50);

        var locations = _gdpData.Keys.Select(key => new
        {
            name = key,
            type = key == "USA" ? "country" : "state"
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            available_locations = locations,
            total_count = locations.Count,
            last_updated = DateTime.UtcNow
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [KernelFunction, Description("Calculate percentage of one GDP relative to another")]
    public async Task<string> CalculateGDPPercentage(
        [Description("The smaller GDP value in billions")] decimal smallerGDP,
        [Description("The larger GDP value in billions")] decimal largerGDP,
        [Description("Name of the smaller economy")] string smallerName,
        [Description("Name of the larger economy")] string largerName)
    {
        await Task.Delay(50);

        try
        {
            if (largerGDP <= 0)
            {
                return CreateErrorResponse("Larger GDP must be greater than zero");
            }

            var percentage = (smallerGDP / largerGDP) * 100;

            var result = new
            {
                calculation = new
                {
                    smaller_economy = smallerName,
                    smaller_gdp_billions = smallerGDP,
                    larger_economy = largerName,
                    larger_gdp_billions = largerGDP,
                    percentage = Math.Round(percentage, 2),
                    formula = $"({smallerGDP} / {largerGDP}) * 100"
                },
                interpretation = $"{smallerName} represents {percentage:F2}% of {largerName}'s GDP",
                timestamp = DateTime.UtcNow
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error calculating percentage: {ex.Message}");
        }
    }

    private string NormalizeLocationName(string location)
    {
        // Simple normalization - in production you'd have more sophisticated matching
        var normalized = location.Trim();
        
        // Handle common variations
        if (normalized.Equals("United States", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("US", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("United States of America", StringComparison.OrdinalIgnoreCase))
        {
            return "USA";
        }

        // Find exact match (case insensitive)
        var exactMatch = _gdpData.Keys.FirstOrDefault(k => 
            k.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        
        if (exactMatch != null)
        {
            return exactMatch;
        }

        // Return original if no match found
        return normalized;
    }

    private string CreateErrorResponse(string message)
    {
        return JsonSerializer.Serialize(new
        {
            error = true,
            message = message,
            timestamp = DateTime.UtcNow
        }, new JsonSerializerOptions { WriteIndented = true });
    }
} 
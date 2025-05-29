using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PsiOrleans.Services;

/// <summary>
/// Hosted service that initializes the function registry during application startup
/// </summary>
public class FunctionRegistryInitializationService : IHostedService
{
    private readonly FunctionRegistrationService _registrationService;
    private readonly ILogger<FunctionRegistryInitializationService> _logger;

    public FunctionRegistryInitializationService(
        FunctionRegistrationService registrationService,
        ILogger<FunctionRegistryInitializationService> logger)
    {
        _registrationService = registrationService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing function registry...");
        
        try
        {
            _registrationService.RegisterAllFunctionsAndPlugins();
            _logger.LogInformation("Function registry initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize function registry");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Function registry initialization service stopping");
        return Task.CompletedTask;
    }
} 
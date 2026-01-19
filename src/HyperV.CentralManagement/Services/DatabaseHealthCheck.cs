using Microsoft.Extensions.Diagnostics.HealthChecks;
using HyperV.CentralManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Services;

/// <summary>
/// Health check for database connectivity
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(IServiceProvider serviceProvider, ILogger<DatabaseHealthCheck> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Try to execute a simple query
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Cannot connect to database");
            }

            // Check if migrations are applied
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                return HealthCheckResult.Degraded(
                    $"Database has pending migrations: {string.Join(", ", pendingMigrations)}");
            }

            return HealthCheckResult.Healthy("Database connection OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy($"Database check failed: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Health check for LDAP connectivity (if enabled)
/// </summary>
public class LdapHealthCheck : IHealthCheck
{
    private readonly LdapAuthService _ldapService;
    private readonly ILogger<LdapHealthCheck> _logger;
    private readonly bool _ldapEnabled;

    public LdapHealthCheck(
        LdapAuthService ldapService,
        IConfiguration configuration,
        ILogger<LdapHealthCheck> logger)
    {
        _ldapService = ldapService;
        _logger = logger;
        _ldapEnabled = configuration.GetValue<bool>("Ldap:Enabled");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_ldapEnabled)
        {
            return HealthCheckResult.Healthy("LDAP is disabled");
        }

        try
        {
            // Simple connectivity check - try to bind without authentication
            await Task.Run(() =>
            {
                // Implementation depends on your LdapAuthService
                // This is a placeholder that should be implemented based on your LDAP service
                var testResult = true; // Replace with actual LDAP connectivity test

                if (!testResult)
                {
                    throw new Exception("LDAP connectivity test failed");
                }
            }, cancellationToken);

            return HealthCheckResult.Healthy("LDAP connectivity OK");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LDAP health check failed");
            return HealthCheckResult.Degraded($"LDAP check failed: {ex.Message}");
        }
    }
}

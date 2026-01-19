using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HyperV.CentralManagement.Services;

public class InitialAdminOptions
{
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "admin";
}

public class DatabaseSeedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly InitialAdminOptions _options;

    public DatabaseSeedService(IServiceProvider serviceProvider, IOptions<InitialAdminOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();

        await db.Database.MigrateAsync(cancellationToken);

        if (!await db.UserAccounts.AnyAsync(cancellationToken))
        {
            db.UserAccounts.Add(new UserAccount
            {
                Username = _options.Username,
                PasswordHash = hasher.Hash(_options.Password),
                Role = "Admin"
            });

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

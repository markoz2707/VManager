using global::HyperV.Contracts.Interfaces.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace VManager.Provider.HyperV;

public static class HyperVServiceRegistration
{
    public static IServiceCollection AddHyperVProvider(this IServiceCollection services)
    {
        services.AddSingleton<IVmProvider, HyperVVmProvider>();
        services.AddSingleton<IHostProvider, HyperVHostProvider>();
        services.AddSingleton<IMetricsProvider, HyperVMetricsProvider>();
        services.AddSingleton<IMigrationProvider, HyperVMigrationProvider>();
        services.AddSingleton<INetworkProvider, HyperVNetworkProvider>();
        services.AddSingleton<IStorageProvider, HyperVStorageProvider>();
        services.AddSingleton<IContainerProvider, HyperVContainerProvider>();
        services.AddSingleton<IGuestAgentProvider, HyperVGuestAgentProvider>();
        return services;
    }
}

using HyperV.Contracts.Interfaces.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace VManager.Provider.KVM;

public static class KvmServiceRegistration
{
    public static IServiceCollection AddKvmProvider(this IServiceCollection services)
    {
        services.AddSingleton<IVmProvider, KvmVmProvider>();
        services.AddSingleton<IHostProvider, KvmHostProvider>();
        services.AddSingleton<IMetricsProvider, KvmMetricsProvider>();
        services.AddSingleton<IMigrationProvider, KvmMigrationProvider>();
        services.AddSingleton<INetworkProvider, KvmNetworkProvider>();
        services.AddSingleton<IStorageProvider, KvmStorageProvider>();
        services.AddSingleton<IContainerProvider, KvmContainerProvider>();
        services.AddSingleton<IGuestAgentProvider, KvmGuestAgentProvider>();
        services.AddSingleton<IEventLogProvider, KvmEventLogProvider>();
        services.AddSingleton<IBackupProvider, KvmBackupProvider>();
        return services;
    }
}

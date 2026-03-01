using Spectre.Console.Cli;
using HyperV.LocalShell.Commands.Vm;
using HyperV.LocalShell.Commands.Network;
using HyperV.LocalShell.Commands.Storage;
using HyperV.LocalShell.Commands.Host;
using HyperV.LocalShell.Commands.Hardware;
using HyperV.LocalShell.Commands.Perf;

// HyperV Local Shell (hvsh) - ESXi SSH equivalent CLI
// Usage: hvsh <category> <command> [options]

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("hvsh");
    config.SetApplicationVersion("1.0.0");

    config.AddExample("vm", "list");
    config.AddExample("vm", "power", "MyVM", "--on");
    config.AddExample("vm", "info", "MyVM", "--metrics");
    config.AddExample("vm", "snapshot", "MyVM", "--list");
    config.AddExample("network", "list");
    config.AddExample("storage", "datastore");
    config.AddExample("system", "info");
    config.AddExample("perf", "top");

    // VM Commands
    config.AddBranch("vm", vm =>
    {
        vm.SetDescription("Virtual machine operations");

        vm.AddCommand<VmListCommand>("list")
            .WithDescription("List all virtual machines")
            .WithExample("vm", "list")
            .WithExample("vm", "list", "--running")
            .WithExample("vm", "list", "--wmi");

        vm.AddCommand<VmPowerCommand>("power")
            .WithDescription("Control VM power state")
            .WithExample("vm", "power", "MyVM", "--on")
            .WithExample("vm", "power", "MyVM", "--off")
            .WithExample("vm", "power", "MyVM", "--shutdown");

        vm.AddCommand<VmInfoCommand>("info")
            .WithDescription("Show VM details and properties")
            .WithExample("vm", "info", "MyVM")
            .WithExample("vm", "info", "MyVM", "--metrics");

        vm.AddCommand<VmSnapshotCommand>("snapshot")
            .WithDescription("Manage VM snapshots")
            .WithExample("vm", "snapshot", "MyVM", "--list")
            .WithExample("vm", "snapshot", "MyVM", "--create", "--name", "Backup")
            .WithExample("vm", "snapshot", "MyVM", "--revert", "--name", "Backup");
    });

    // Network Commands
    config.AddBranch("network", network =>
    {
        network.SetDescription("Network and virtual switch operations");

        network.AddCommand<NetworkListCommand>("list")
            .WithDescription("List all networks and virtual switches")
            .WithExample("network", "list")
            .WithExample("network", "list", "--wmi");

        network.AddCommand<NetworkAdapterCommand>("adapter")
            .WithDescription("List physical network adapters")
            .WithExample("network", "adapter");
    });

    // Storage Commands
    config.AddBranch("storage", storage =>
    {
        storage.SetDescription("Storage and VHD operations");

        storage.AddCommand<StorageDeviceCommand>("device")
            .WithDescription("List storage devices")
            .WithExample("storage", "device");

        storage.AddCommand<StorageDatastoreCommand>("datastore")
            .WithDescription("List available storage locations")
            .WithExample("storage", "datastore")
            .WithExample("storage", "datastore", "--min-gb", "100");

        storage.AddCommand<StorageDiskCommand>("disk")
            .WithDescription("Show physical disk performance")
            .WithExample("storage", "disk");
    });

    // System Commands
    config.AddBranch("system", system =>
    {
        system.SetDescription("System and host operations");

        system.AddCommand<SystemInfoCommand>("info")
            .WithDescription("Show system information")
            .WithExample("system", "info");

        system.AddCommand<SystemHealthCommand>("health")
            .WithDescription("Check agent service health")
            .WithExample("system", "health");

        system.AddCommand<SystemServiceCommand>("service")
            .WithDescription("Manage Hyper-V related services")
            .WithExample("system", "service", "--list")
            .WithExample("system", "service", "vmcompute")
            .WithExample("system", "service", "vmcompute", "--restart");
    });

    // Hardware Commands
    config.AddBranch("hardware", hardware =>
    {
        hardware.SetDescription("Hardware information");

        hardware.AddCommand<HardwareInfoCommand>("info")
            .WithDescription("Show hardware information")
            .WithExample("hardware", "info");

        hardware.AddCommand<HardwareCpuCommand>("cpu")
            .WithDescription("Show CPU information and usage")
            .WithExample("hardware", "cpu");

        hardware.AddCommand<HardwareMemoryCommand>("memory")
            .WithDescription("Show memory information and usage")
            .WithExample("hardware", "memory");
    });

    // Performance Commands
    config.AddBranch("perf", perf =>
    {
        perf.SetDescription("Performance monitoring (esxtop equivalent)");

        perf.AddCommand<PerfTopCommand>("top")
            .WithDescription("Interactive performance monitor")
            .WithExample("perf", "top")
            .WithExample("perf", "top", "--interval", "5")
            .WithExample("perf", "top", "--count", "10");

        perf.AddCommand<PerfStatsCommand>("stats")
            .WithDescription("Show VM performance statistics")
            .WithExample("perf", "stats", "MyVM")
            .WithExample("perf", "stats", "MyVM", "--watch");
    });
});

return app.Run(args);

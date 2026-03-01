using System.Diagnostics;
using System.Text.Json;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;

namespace VManager.Provider.KVM;

public class KvmContainerProvider : IContainerProvider
{
    private readonly ILogger<KvmContainerProvider> _logger;
    private readonly string _containerRuntime;

    public KvmContainerProvider(ILogger<KvmContainerProvider> logger)
    {
        _logger = logger;
        _containerRuntime = DetectContainerRuntime();
    }

    public async Task<List<ContainerSummaryDto>> ListContainersAsync()
    {
        var result = new List<ContainerSummaryDto>();
        try
        {
            var output = await RunCommandAsync($"{_containerRuntime} ps -a --format \"{{{{.ID}}}}|{{{{.Names}}}}|{{{{.State}}}}|{{{{.Image}}}}\"");
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Trim().Split('|');
                if (parts.Length >= 4)
                {
                    result.Add(new ContainerSummaryDto
                    {
                        Id = parts[0],
                        Name = parts[1],
                        State = parts[2],
                        Image = parts[3],
                        Backend = _containerRuntime == "podman" ? "Podman" : "Docker"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list containers via {Runtime}", _containerRuntime);
        }
        return result;
    }

    public async Task<ContainerDetailsDto?> GetContainerAsync(string containerId)
    {
        try
        {
            var output = await RunCommandAsync($"{_containerRuntime} inspect {containerId} --format \"{{{{.Id}}}}|{{{{.Name}}}}|{{{{.State.Status}}}}|{{{{.Config.Image}}}}|{{{{.HostConfig.NanoCpus}}}}|{{{{.HostConfig.Memory}}}}\"");
            var parts = output.Trim().Split('|');
            if (parts.Length >= 4)
            {
                long.TryParse(parts.Length > 5 ? parts[5] : "0", out var memBytes);
                return new ContainerDetailsDto
                {
                    Id = parts[0],
                    Name = parts[1].TrimStart('/'),
                    State = parts[2],
                    Image = parts[3],
                    MemoryMB = memBytes / 1024 / 1024,
                    Backend = _containerRuntime == "podman" ? "Podman" : "Docker"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect container {Id}", containerId);
        }
        return null;
    }

    public async Task<string> CreateContainerAsync(CreateContainerSpec spec)
    {
        var args = $"run -d --name {spec.Name}";
        if (spec.MemoryMB > 0) args += $" --memory {spec.MemoryMB}m";
        if (spec.CpuCount > 0) args += $" --cpus {spec.CpuCount}";
        args += $" {spec.Image}";

        var output = await RunCommandAsync($"{_containerRuntime} {args}");
        return output.Trim();
    }

    public async Task DeleteContainerAsync(string containerId)
    {
        await RunCommandAsync($"{_containerRuntime} rm -f {containerId}");
    }

    public async Task StartContainerAsync(string containerId)
    {
        await RunCommandAsync($"{_containerRuntime} start {containerId}");
    }

    public async Task StopContainerAsync(string containerId)
    {
        await RunCommandAsync($"{_containerRuntime} stop {containerId}");
    }

    public async Task PauseContainerAsync(string containerId)
    {
        await RunCommandAsync($"{_containerRuntime} pause {containerId}");
    }

    public async Task ResumeContainerAsync(string containerId)
    {
        await RunCommandAsync($"{_containerRuntime} unpause {containerId}");
    }

    public async Task TerminateContainerAsync(string containerId)
    {
        await RunCommandAsync($"{_containerRuntime} kill {containerId}");
    }

    private static string DetectContainerRuntime()
    {
        try
        {
            var psi = new ProcessStartInfo("podman", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
            if (proc?.ExitCode == 0) return "podman";
        }
        catch { }

        return "docker";
    }

    private static async Task<string> RunCommandAsync(string command)
    {
        var parts = command.Split(' ', 2);
        var psi = new ProcessStartInfo(parts[0], parts.Length > 1 ? parts[1] : "")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {parts[0]}");
        var output = await proc.StandardOutput.ReadToEndAsync();
        var error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"{parts[0]} failed: {error}");

        return output;
    }
}

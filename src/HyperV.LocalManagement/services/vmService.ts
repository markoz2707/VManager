import { fetchApi, ApiError } from './baseService';
import { VirtualMachine, VmSnapshot, StorageDevice, StorageController, VmStatus, VmEnvironment, AppHealthStatus } from '../types';

const mapVmStatus = (apiStatus: string | undefined): VmStatus => {
    if (!apiStatus) return VmStatus.UNKNOWN;
    switch (apiStatus.toLowerCase()) {
        case 'running': return VmStatus.RUNNING;
        case 'stopped': return VmStatus.STOPPED;
        case 'paused': return VmStatus.PAUSED;
        case 'saved': return VmStatus.SAVED;
        case 'saving': return VmStatus.SAVING;
        case 'stopping': return VmStatus.STOPPING;
        case 'starting': return VmStatus.STARTING;
        case 'pausing': return VmStatus.PAUSING;
        case 'resuming': return VmStatus.RESUMING;
        case 'suspended': return VmStatus.SUSPENDED;
        case 'off': return VmStatus.STOPPED;
        default: return VmStatus.UNKNOWN;
    }
};

export const getVms = async (page: number = 1, pageSize: number = 50): Promise<{ WMI: VirtualMachine[], HCS: VirtualMachine[], totalCount?: number, hasMore?: boolean }> => {
    const data = await fetchApi(`/vms?page=${page}&pageSize=${pageSize}`);

    // Handle paginated response (new format)
    if (data.items) {
        const vms = (data.items || []).map((vm: any) => ({
            id: vm.Id || vm.id || vm.ID,
            name: vm.Name || vm.name || vm.ID || vm.id,
            status: mapVmStatus(vm.State || vm.state),
            environment: 'WMI' as VmEnvironment,
            cpuCount: vm.Processors || vm.processors,
            memoryMB: vm.Memory || vm.memory
        }));
        return { WMI: vms, HCS: [], totalCount: data.totalCount, hasMore: data.hasMore };
    }

    // Legacy format
    const wmiData = data.wmi || data.WMI || {};
    const hcsData = data.hcs || data.HCS || {};

    return {
        WMI: (wmiData.VMs || wmiData.vms || []).map((vm: any) => ({
            id: vm.Id || vm.id || vm.ID,
            name: vm.Name || vm.name || vm.ID || vm.id,
            status: mapVmStatus(vm.State || vm.state),
            environment: 'WMI' as VmEnvironment,
            cpuCount: vm.Processors || vm.processors,
            memoryMB: vm.Memory || vm.memory
        })),
        HCS: (hcsData.VMs || []).map((vm: any) => ({
            id: vm.Id || vm.id,
            name: vm.Name || vm.name,
            status: mapVmStatus(vm.State || vm.state),
            environment: 'HCS' as VmEnvironment,
            cpuCount: vm.Processors || vm.processors,
            memoryMB: vm.Memory || vm.memory
        }))
    };
};

export const getVmProperties = async (vmName: string): Promise<{
    memoryMB: number | null, 
    cpuCount: number | null,
    enableDynamicMemory?: boolean,
    minMemoryMB?: number,
    maxMemoryMB?: number
}> => {
    try {
        const data = await fetchApi(`/vms/${encodeURIComponent(vmName)}/properties`);
        return {
            memoryMB: data.properties?.memory ?? null,
            cpuCount: data.properties?.processors ?? null,
            enableDynamicMemory: data.properties?.enableDynamicMemory ?? false,
            minMemoryMB: data.properties?.minMemoryMB,
            maxMemoryMB: data.properties?.maxMemoryMB,
        };
    } catch (error) {
        console.error(`Failed to get properties for VM ${vmName}:`, error);
        return { memoryMB: null, cpuCount: null };
    }
};

const performVmAction = async (vmName: string, action: string): Promise<any> => {
    const response = await fetchApi(`/vms/${encodeURIComponent(vmName)}/${action}`, { method: 'POST' });
    if (response && response.jobId) {
        await pollJobCompletion(response.jobId, action);
    }
    return response;
};

export const startVm = async (vmName: string): Promise<void> => { await performVmAction(vmName, 'start'); };
export const stopVm = async (vmName: string): Promise<void> => { await performVmAction(vmName, 'stop'); };
export const pauseVm = async (vmName: string): Promise<void> => { await performVmAction(vmName, 'pause'); };
export const resumeVm = async (vmName: string): Promise<void> => { await performVmAction(vmName, 'resume'); };
export const shutdownVm = async (vmName: string): Promise<void> => { await performVmAction(vmName, 'shutdown'); };
export const terminateVm = async (vmName: string): Promise<void> => { await performVmAction(vmName, 'terminate'); };
export const saveVm = async (vmName: string): Promise<void> => { await performVmAction(vmName, 'save'); };

export const migrateVm = async (vmName: string, destinationHost: string, live: boolean, storage: boolean): Promise<{ jobId: string }> => {
    return fetchApi(`/vms/${encodeURIComponent(vmName)}/migrate`, {
        method: 'POST',
        body: JSON.stringify({ destinationHost, live, storage })
    });
};

export const getVmAppHealth = async (vmName: string): Promise<AppHealthStatus> => {
    return fetchApi(`/vms/${encodeURIComponent(vmName)}/apphealth`);
};

export const migrateVmStorage = async (vmName: string, destinationPath: string): Promise<{ jobId: string }> => {
    return fetchApi(`/vms/${encodeURIComponent(vmName)}/migrate-storage`, {
        method: 'POST',
        body: JSON.stringify({ destinationPath })
    });
};

export const getVmConsoleInfo = async (vmName: string): Promise<{
    vmId: string;
    vmName: string;
    state: string;
    rdpHost: string;
    rdpPort: number;
    protocol: string;
}> => {
    return fetchApi(`/vms/${encodeURIComponent(vmName)}/console`);
};

export const getVmConsoleRdpUrl = (vmName: string): string => {
    const baseUrl = (window as any).__API_BASE_URL__ || '';
    return `${baseUrl}/api/v1/vms/${encodeURIComponent(vmName)}/console/rdp`;
};

export const copyFileToGuest = async (vmName: string, sourcePath: string, destPath: string, overwrite: boolean = false): Promise<{ jobId: string }> => {
    return fetchApi(`/vms/${encodeURIComponent(vmName)}/guestfilecopy`, {
        method: 'POST',
        body: JSON.stringify({ sourcePath, destPath, overwrite })
    });
};

export const getVmSnapshots = async (vmName: string): Promise<VmSnapshot[]> => {
    try {
        const snapshots = await fetchApi(`/vms/${encodeURIComponent(vmName)}/snapshots`);
        return snapshots.Snapshots || snapshots || [];
    } catch (error) {
        console.error(`Failed to get snapshots for VM ${vmName}:`, error);
        return [];
    }
};

export const createVmSnapshot = async (vmName: string, name: string, description?: string): Promise<VmSnapshot> => {
    const response = await fetchApi(`/vms/${encodeURIComponent(vmName)}/snapshots`, {
        method: 'POST',
        body: JSON.stringify({ SnapshotName: name, Notes: description })
    });
    if (response && response.jobId) {
        await pollJobCompletion(response.jobId, 'create snapshot');
    }
    return response;
};

export const deleteVmSnapshot = async (vmName: string, snapshotId: string): Promise<void> => {
    const response = await fetchApi(`/vms/${encodeURIComponent(vmName)}/snapshots/${encodeURIComponent(snapshotId)}`, {
        method: 'DELETE'
    });
    if (response && response.jobId) {
        await pollJobCompletion(response.jobId, 'delete snapshot');
    }
};

export const revertToSnapshot = async (vmName: string, snapshotId: string): Promise<void> => {
    const response = await fetchApi(`/vms/${encodeURIComponent(vmName)}/snapshots/${encodeURIComponent(snapshotId)}/revert`, {
        method: 'POST'
    });
    if (response && response.jobId) {
        await pollJobCompletion(response.jobId, 'revert snapshot');
    }
};

async function pollJobCompletion(jobId: string, operation: string, maxAttempts = 60): Promise<void> {
    let attempts = 0;
    while (attempts < maxAttempts) {
        try {
            const jobDetails = await fetchApi(`/jobs/${encodeURIComponent(jobId)}`);
            const status = jobDetails.status || jobDetails.JobState;
            
            if (status === 'Completed' || status === 7) return;
            else if (status === 'Exception' || status === 10 || status === 'Failed' || status === 8 || status === 9) {
                throw new Error(`Job failed: ${jobDetails.ErrorDescription || 'Unknown error'}`);
            }
            await new Promise(resolve => setTimeout(resolve, 1000));
            attempts++;
        } catch (pollError) {
            if (attempts >= maxAttempts - 1) throw pollError;
            await new Promise(resolve => setTimeout(resolve, 1000));
            attempts++;
        }
    }
    throw new Error(`${operation} job timed out`);
}

export const getVmStorageDevices = async (vmName: string): Promise<StorageDevice[]> => {
    return fetchApi(`/vms/${encodeURIComponent(vmName)}/storage/devices`);
};

export const addStorageDevice = async (vmName: string, device: {
    deviceType: string;
    path?: string;
    readOnly?: boolean;
    controllerId: string;
    controllerLocation?: number;
}): Promise<void> => {
    await fetchApi(`/vms/${encodeURIComponent(vmName)}/storage/devices`, {
        method: 'POST',
        body: JSON.stringify(device)
    });
};

export const removeStorageDevice = async (vmName: string, deviceId: string): Promise<void> => {
    await fetchApi(`/vms/${encodeURIComponent(vmName)}/storage/devices/${encodeURIComponent(deviceId)}`, {
        method: 'DELETE'
    });
};

export const getVmStorageControllers = async (vmName: string): Promise<StorageController[]> => {
    return fetchApi(`/vms/${encodeURIComponent(vmName)}/storage/controllers`);
};

export const createVm = async (payload: {
    id: string;
    name: string;
    memoryMB: number;
    cpuCount: number;
    diskSizeGB: number;
    mode: string;
    generation: number;
    secureBoot: boolean;
    switchName: string;
    notes?: string;
    vhdPath?: string;
}): Promise<any> => {
    const apiPayload = {
        Id: payload.id,
        Name: payload.name,
        Mode: payload.mode === 'WMI' ? 1 : 0,
        MemoryMB: payload.memoryMB,
        CpuCount: payload.cpuCount,
        DiskSizeGB: payload.diskSizeGB,
        Generation: payload.generation,
        SecureBoot: payload.secureBoot,
        VhdPath: payload.vhdPath,
        SwitchName: payload.switchName,
        Notes: payload.notes,
    };
    return fetchApi('/vms', {
        method: 'POST',
        body: JSON.stringify(apiPayload)
    });
};

export const configureVm = async (vmName: string, config: {
    memoryMB?: number;
    cpuCount?: number;
    enableDynamicMemory?: boolean;
    minMemoryMB?: number;
    maxMemoryMB?: number;
}): Promise<void> => {
    const response = await fetchApi(`/vms/${encodeURIComponent(vmName)}/configure`, {
        method: 'POST',
        body: JSON.stringify(config)
    });
    if (response && response.jobId) {
        await pollJobCompletion(response.jobId, 'configure');
    }
};
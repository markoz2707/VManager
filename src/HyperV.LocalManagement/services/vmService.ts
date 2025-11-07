import { fetchApi, ApiError } from './baseService';
import { VirtualMachine, VmSnapshot, StorageDevice, StorageController, VmStatus, VmEnvironment } from '../types';

// Helper to map API status strings to VmStatus enum
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
        case 'off': return VmStatus.STOPPED; // Assuming 'Off' maps to Stopped
        default: return VmStatus.UNKNOWN;
    }
};

export const getVms = async (): Promise<{ WMI: VirtualMachine[], HCS: VirtualMachine[] }> => {
    const data = await fetchApi('/vms');
    console.log('VM API Response:', data); // Debug log
    
    // API zwraca lowercase klucze: 'hcs' i 'wmi' z strukturą {Count, VMs, Backend}
    // Upewniamy się, że pobieramy dane nawet jeśli klucze są w innej wielkości liter lub brakuje niektórych pól
    const wmiData = data.wmi || data.WMI || {};
    const hcsData = data.hcs || data.HCS || {};
    
    return {
        WMI: (wmiData.VMs || wmiData.vms || []).map((vm: any) => ({
            id: vm.Id || vm.id || vm.ID,
            name: vm.Name || vm.name || vm.ID || vm.id, // Fallback to ID if name is missing
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

export const getVmProperties = async (vmName: string): Promise<{memoryMB: number | null, cpuCount: number | null}> => {
    try {
        const data = await fetchApi(`/vms/${encodeURIComponent(vmName)}/properties`);
        console.log(`VM Properties for ${vmName}:`, data); // Debug log
        return {
            memoryMB: data.properties?.memory ?? null, // API returns lowercase
            cpuCount: data.properties?.processors ?? null // API returns lowercase
        };
    } catch (error) {
        if (error instanceof ApiError && error.details && error.details.errors) {
            console.error(`WMI Errors for VM properties ${vmName}:`, error.details.errors);
        }
        console.error(`Failed to get properties for VM ${vmName}:`, error);
        return { memoryMB: null, cpuCount: null }; // Return nulls on error
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

// Add methods for editing VM properties if they exist in the API
// export const editVmProperties = async (vmName: string, config: { memoryMB?: number; cpuCount?: number; }): Promise<void> => {
//     await fetchApi(`/vms/${encodeURIComponent(vmName)}/configure`, {
//         method: 'POST',
//         body: JSON.stringify(config)
//     });
// };

export const getVmSnapshots = async (vmName: string): Promise<VmSnapshot[]> => {
    try {
        const snapshots = await fetchApi(`/vms/${encodeURIComponent(vmName)}/snapshots`);
        return snapshots.Snapshots || snapshots || [];
    } catch (error) {
        if (error instanceof ApiError && error.details && error.details.errors) {
            console.error(`WMI Errors for VM snapshots ${vmName}:`, error.details.errors);
        }
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

// Unified job polling helper (add at end of file)
async function pollJobCompletion(jobId: string, operation: string, maxAttempts = 60): Promise<void> {
    let attempts = 0;
    while (attempts < maxAttempts) {
        try {
            const jobDetails = await fetchApi(`/jobs/${encodeURIComponent(jobId)}`);
            const status = jobDetails.status || jobDetails.JobState;
            
            if (status === 'Completed' || status === 7) {
                console.log(`${operation} job completed successfully`);
                return;
            } else if (status === 'Exception' || status === 10 || status === 'Failed' || status === 8 || status === 9) {
                const errorDetails = jobDetails.ErrorDescription || jobDetails.errors || 'Unknown job error';
                console.error(`${operation} job failed:`, errorDetails);
                if (jobDetails.errors) {
                    console.error('WMI Errors:', jobDetails.errors);
                }
                throw new Error(`Job failed: ${errorDetails}`);
            }
            
            await new Promise(resolve => setTimeout(resolve, 1000));
            attempts++;
        } catch (pollError) {
            console.warn(`Error polling ${operation} job ${jobId}:`, pollError);
            if (attempts >= maxAttempts - 1) throw pollError;
            await new Promise(resolve => setTimeout(resolve, 1000));
            attempts++;
        }
    }
    throw new Error(`${operation} job timed out after ${maxAttempts} seconds`);
}

export const getVmStorageDevices = async (vmName: string): Promise<StorageDevice[]> => {
    return fetchApi(`/vms/${encodeURIComponent(vmName)}/storage/devices`);
};

export const addStorageDevice = async (vmName: string, device: {
    deviceType: string;
    path: string;
    readOnly?: boolean;
    controllerId: string;
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
    Id: string;
    Name: string;
    MemoryMB: number;
    CpuCount: number;
    DiskSizeGB: number;
    Mode: string;
}): Promise<any> => {
    const response = await fetchApi('/vms', {
        method: 'POST',
        body: JSON.stringify(payload)
    });
    if (response && response.jobId) {
        await pollJobCompletion(response.jobId, 'create');
    }
    return response;
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
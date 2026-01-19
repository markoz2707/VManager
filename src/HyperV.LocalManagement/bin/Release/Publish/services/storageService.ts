import { fetchApi, ApiError } from './baseService';
import { VhdMetadata, VirtualDiskChanges, VhdSetInformation, StorageQoSPolicy, StorageDeviceInfo, StorageLocation } from '../types';

export const createVhd = async (path: string, sizeGB: number, format: 'VHD' | 'VHDX' = 'VHDX', type: 'Fixed' | 'Dynamic' = 'Dynamic'): Promise<void> => {
    await fetchApi('/storage/vhd', {
        method: 'POST',
        body: JSON.stringify({
            Path: path,
            MaxInternalSize: sizeGB * 1024 * 1024 * 1024,
            Format: format,
            Type: type
        })
    });
};

export const attachVhd = async (vmName: string, vhdPath: string): Promise<void> => {
    await fetchApi(`/storage/vhd/attach?vmName=${encodeURIComponent(vmName)}&vhdPath=${encodeURIComponent(vhdPath)}`, {
        method: 'PUT'
    });
};

export const detachVhd = async (vmName: string, vhdPath: string): Promise<void> => {
    await fetchApi(`/storage/vhd/detach?vmName=${encodeURIComponent(vmName)}&vhdPath=${encodeURIComponent(vhdPath)}`, {
        method: 'PUT'
    });
};

export const resizeVhd = async (vhdPath: string, newSizeGB: number): Promise<void> => {
    await fetchApi('/storage/vhd/resize', {
        method: 'PUT',
        body: JSON.stringify({
            Path: vhdPath,
            MaxInternalSize: newSizeGB * 1024 * 1024 * 1024
        })
    });
};

export const getVhdMetadata = async (vhdPath: string): Promise<VhdMetadata> => {
    return fetchApi(`/storage/vhd/metadata?vhdPath=${encodeURIComponent(vhdPath)}`);
};

export const updateVhdMetadata = async (vhdPath: string, metadata: {
    name?: string;
    description?: string;
    source?: string;
}): Promise<void> => {
    await fetchApi(`/storage/vhd/metadata?vhdPath=${encodeURIComponent(vhdPath)}`, {
        method: 'PUT',
        body: JSON.stringify(metadata)
    });
};

export const compactVhd = async (vhdPath: string): Promise<string> => {
    const response = await fetchApi('/storage/vhd/compact', {
        method: 'POST',
        body: JSON.stringify({
            Path: vhdPath,
            Mode: 'Quick'
        })
    });
    return response.jobId;
};

export const convertVhdFormat = async (sourcePath: string, destinationPath: string, targetFormat: 'VHD' | 'VHDX'): Promise<string> => {
    const response = await fetchApi('/storage/vhd/convert', {
        method: 'POST',
        body: JSON.stringify({
            SourcePath: sourcePath,
            DestinationPath: destinationPath,
            TargetFormat: targetFormat
        })
    });
    return response.jobId;
};

export const convertToVhdSet = async (sourcePath: string, destinationPath: string): Promise<void> => {
    await fetchApi('/storage/vhdset/convert', {
        method: 'POST',
        body: JSON.stringify({
            SourcePath: sourcePath,
            DestinationPath: destinationPath
        })
    });
};

export const getVhdSetInfo = async (vhdSetPath: string): Promise<VhdSetInformation> => {
    return fetchApi(`/storage/vhdset/${encodeURIComponent(vhdSetPath)}/info`);
};

export const createVhdSetSnapshot = async (vhdSetPath: string, name: string, description?: string): Promise<void> => {
    await fetchApi(`/storage/vhdset/${encodeURIComponent(vhdSetPath)}/snapshots`, {
        method: 'POST',
        body: JSON.stringify({
            Name: name,
            Description: description
        })
    });
};

export const enableChangeTracking = async (vhdPath: string): Promise<string> => {
    const response = await fetchApi(`/storage/vhd/${encodeURIComponent(vhdPath)}/tracking/enable`, {
        method: 'POST'
    });
    return response.trackingId;
};

export const getVirtualDiskChanges = async (vhdPath: string, trackingId: string): Promise<VirtualDiskChanges> => {
    return fetchApi(`/storage/vhd/${encodeURIComponent(vhdPath)}/changes?changeTrackingId=${encodeURIComponent(trackingId)}`);
};

// Storage QoS
export const createQoSPolicy = async (policyId: string, maxIops: number, maxBandwidth: number, description?: string): Promise<{ policyId: string }> => {
    return fetchApi('/qos', {
        method: 'POST',
        body: JSON.stringify({ policyId, maxIops, maxBandwidth, description })
    });
};

export const getQoSPolicies = async (): Promise<StorageQoSPolicy[]> => {
    // Assuming GET /qos lists all policies
    try {
        return await fetchApi('/qos');
    } catch (e) {
        console.warn("Could not list QoS policies, API might not support it.", e);
        return [];
    }
};

export const deleteQoSPolicy = async (policyId: string): Promise<void> => {
    await fetchApi(`/qos/${encodeURIComponent(policyId)}`, { method: 'DELETE' });
};

export const applyQoSPolicyToVm = async (vmName: string, policyId: string): Promise<void> => {
    await fetchApi(`/vms/${encodeURIComponent(vmName)}/qos/${encodeURIComponent(policyId)}`, { method: 'POST' });
};

// New storage device functions
export const listStorageDevices = async (): Promise<StorageDeviceInfo[]> => {
    try {
        return await fetchApi('/storage/devices');
    } catch (e) {
        console.warn("Could not list storage devices, API might not support it.", e);
        return [];
    }
};

export const getSuitableVhdLocations = async (minGb?: number): Promise<StorageLocation[]> => {
    try {
        const url = minGb ? `/storage/locations?minGb=${minGb}` : '/storage/locations';
        return await fetchApi(url);
    } catch (e) {
        console.warn("Could not list suitable VHD locations, API might not support it.", e);
        return [];
    }
};

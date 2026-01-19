import { fetchApi, ApiError } from './baseService';
import { Container, VmEnvironment } from '../types';

export const getContainers = async (): Promise<{ HCS: Container[], WMI: Container[] }> => {
    const data = await fetchApi('/containers');
    const hcsData = data.HcsContainers || data.hcsContainers || data.HCS || data.hcs || [];
    const wmiData = data.WmiContainers || data.wmiContainers || data.WMI || data.wmi || [];
    
    return {
        HCS: hcsData.map((container: any) => ({
            id: container.Id || container.id,
            name: container.Name || container.name,
            state: container.State || container.state,
            image: container.Image || container.image,
            memoryMB: container.MemoryMB || container.memoryMB,
            cpuCount: container.CpuCount || container.cpuCount,
            networkMode: container.NetworkMode || container.networkMode,
            createdAt: container.CreatedAt || container.createdAt,
            startedAt: container.StartedAt || container.startedAt
        })),
        WMI: wmiData.map((container: any) => ({
            id: container.Id || container.id,
            name: container.Name || container.name,
            state: container.State || container.state,
            image: container.Image || container.image,
            memoryMB: container.MemoryMB || container.memoryMB,
            cpuCount: container.CpuCount || container.cpuCount,
            networkMode: container.NetworkMode || container.networkMode,
            createdAt: container.CreatedAt || container.createdAt,
            startedAt: container.StartedAt || container.startedAt
        }))
    };
};

export const getContainer = async (containerId: string): Promise<Container> => {
    return fetchApi(`/containers/${encodeURIComponent(containerId)}`);
};

export const createContainer = async (container: {
    name: string;
    image: string;
    memoryMB: number;
    cpuCount: number;
    storageSizeGB: number;
    environment: Record<string, string>;
    portMappings: Record<number, number>;
    volumeMounts: Record<string, string>;
    environmentType: VmEnvironment;
}): Promise<Container> => {
    return fetchApi('/containers', {
        method: 'POST',
        body: JSON.stringify({
            Id: `container-${crypto.randomUUID()}`,
            Name: container.name,
            Image: container.image,
            MemoryMB: container.memoryMB,
            CpuCount: container.cpuCount,
            StorageSizeGB: container.storageSizeGB,
            Environment: container.environment,
            PortMappings: container.portMappings,
            VolumeMounts: container.volumeMounts,
            Mode: container.environmentType === 'HCS' ? 0 : 1
        })
    });
};

const performContainerAction = async (containerId: string, action: string): Promise<void> => {
    await fetchApi(`/containers/${encodeURIComponent(containerId)}/${action}`, { method: 'POST' });
};

export const startContainer = (containerId: string): Promise<void> => performContainerAction(containerId, 'start');
export const stopContainer = (containerId: string): Promise<void> => performContainerAction(containerId, 'stop');
export const pauseContainer = (containerId: string): Promise<void> => performContainerAction(containerId, 'pause');
export const resumeContainer = (containerId: string): Promise<void> => performContainerAction(containerId, 'resume');
export const terminateContainer = (containerId: string): Promise<void> => performContainerAction(containerId, 'terminate');

export const deleteContainer = async (containerId: string): Promise<void> => {
    await fetchApi(`/containers/${encodeURIComponent(containerId)}`, { method: 'DELETE' });
};
// Import wszystkich nowych serwisów
import * as vmService from './vmService';
import * as containerService from './containerService';
import * as storageService from './storageService';
import * as networkService from './networkService';
import * as jobService from './jobService';
import * as healthService from './healthService';
import { fetchApi, ApiError } from './baseService';
import { VirtualMachine, VmStatus, VirtualNetwork, NetworkType, ServiceInfo, VmEnvironment, Container } from '../types';

const BASE_URL = '/api/v1';

// Eksportuj wszystkie serwisy
export {
    vmService,
    containerService,
    storageService,
    networkService,
    jobService,
    healthService
};

// Export additional network functions
export const getPhysicalAdapters = networkService.getPhysicalAdapters;

// Zachowaj kompatybilność wsteczną z istniejącymi metodami
export const getVms = vmService.getVms;
export const getVmProperties = vmService.getVmProperties;
export const startVm = vmService.startVm;
export const stopVm = vmService.stopVm;
export const pauseVm = vmService.pauseVm;
export const resumeVm = vmService.resumeVm;
export const shutdownVm = vmService.shutdownVm;
export const terminateVm = vmService.terminateVm;
export const saveVm = vmService.saveVm;
export const configureVm = vmService.configureVm;
export const getVmSnapshots = vmService.getVmSnapshots;
export const createVmSnapshot = vmService.createVmSnapshot;
export const deleteVmSnapshot = vmService.deleteVmSnapshot;
export const revertToSnapshot = vmService.revertToSnapshot;
export const createVm = async (data: {
    Name: string;
    MemoryMB: number;
    CpuCount: number;
    DiskSizeGB: number;
}, environment: VmEnvironment): Promise<VirtualMachine> => {
    const payload = {
        ...data,
        Id: `vm-${crypto.randomUUID()}`,
        Mode: environment,
    };

    const response = await vmService.createVm(payload);

    return {
        id: payload.Id,
        name: payload.Name,
        status: VmStatus.STOPPED,
        environment: environment,
    };
};

export const getNetworks = async (): Promise<{ WMI: VirtualNetwork[], HCS: VirtualNetwork[] }> => {
    try {
        // Próbujemy pobrać sieci z API - może być endpoint /networks
        const data = await fetchApi('/networks');
        console.log('Network API Response:', data); // Debug log

        // Handle different possible response formats
        const wmiData = data.WMI || data.wmi || [];
        const hcsData = data.HCS || data.hcs || [];

        return {
            WMI: wmiData.map((net: any) => ({
                id: net.Id || net.id,
                name: net.Name || net.name,
                type: net.Type || net.type || NetworkType.INTERNAL,
                subnet: net.Subnet || net.subnet || 'N/A',
                environment: 'WMI' as VmEnvironment
            })),
            HCS: hcsData.map((net: any) => ({
                id: net.Id || net.id,
                name: net.Name || net.name,
                type: net.Type || net.type || NetworkType.NAT,
                subnet: net.Subnet || net.subnet || 'N/A',
                environment: 'HCS' as VmEnvironment
            }))
        };
    } catch (error) {
        console.warn('Network API not available, returning empty lists:', error);
        return { WMI: [], HCS: [] };
    }
};

export const createWmiNetwork = async (payload: {
    name: string;
    type: NetworkType;
    notes?: string;
}): Promise<VirtualNetwork> => {
    // Filter out unsupported types for WMI
    if (payload.type === NetworkType.NAT) {
        throw new Error('NAT type is not supported for WMI switches. Use HCS instead.');
    }
    const params = new URLSearchParams({
        name: payload.name,
        type: payload.type,
        ...(payload.notes && { notes: payload.notes })
    });

    const response = await fetchApi(`/networks/wmi?${params}`, { method: 'POST' });

    return {
        id: response.id,
        name: payload.name,
        type: payload.type,
        subnet: 'N/A', // WMI switches don't have subnet concept
        environment: 'WMI'
    };
};

export const createHcsNatNetwork = async (name: string, prefix: string): Promise<VirtualNetwork> => {
    const id = await networkService.createNatNetwork(name, prefix);
    return {
        id: id,
        name: name,
        type: NetworkType.NAT,
        subnet: prefix,
        environment: 'HCS'
    };
};

export const updateWmiNetwork = async (id: string, payload: { name?: string; notes?: string }): Promise<void> => {
    const params = new URLSearchParams();
    if (payload.name) params.append('name', payload.name);
    if (payload.notes) params.append('notes', payload.notes);

    await fetchApi(`/networks/wmi/${id}?${params}`, { method: 'PUT' });
};

export const deleteNetwork = async (id: string, environment: VmEnvironment): Promise<void> => {
    const params = new URLSearchParams({ environment });
    await fetchApi(`/networks/${id}?${params}`, { method: 'DELETE' });
};

export const getServiceInfo = healthService.getServiceInfo;

// Dashboard stats - używa nowych serwisów
export const getDashboardStats = async (): Promise<{
    runningVms: number;
    totalVms: number;
    totalNetworks: number;
    totalMemoryAssigned: number
}> => {
    const vmsData = await getVms();
    const vms = [...vmsData.WMI, ...vmsData.HCS];

    let networks: VirtualNetwork[] = [];
    try {
        const networksData = await getNetworks();
        networks = [...networksData.WMI, ...networksData.HCS];
    } catch (e) {
        console.warn("Could not fetch networks for dashboard stats:", e);
    }

    const runningVms = vms.filter(vm => vm.status === VmStatus.RUNNING).length;
    const totalVms = vms.length;
    const totalNetworks = networks.length;

    // Fetch all properties in parallel for performance
    const propertiesPromises = vms.map(vm =>
        getVmProperties(vm.name).catch(() => ({ memoryMB: 0, cpuCount: 0 }))
    );
    const properties = await Promise.all(propertiesPromises);
    const totalMemoryAssigned = properties.reduce((acc, prop) => acc + prop.memoryMB, 0);

    return { runningVms, totalVms, totalNetworks, totalMemoryAssigned };
};

export const getHostDetails = async () => {
    return await fetchApi('/host/details');
};
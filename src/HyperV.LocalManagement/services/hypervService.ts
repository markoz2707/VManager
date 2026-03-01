import * as vmService from './vmService';
import * as containerService from './containerService';
import * as storageService from './storageService';
import * as networkService from './networkService';
import * as jobService from './jobService';
import * as healthService from './healthService';
import * as replicationService from './replicationService';
import * as metricsService from './metricsService';
import * as hostService from './hostService';
import * as authService from './authService';

import { fetchApi, ApiError } from './baseService';
import { VirtualMachine, VmStatus, VirtualNetwork, NetworkType, ServiceInfo, VmEnvironment, HostDetails, Stats } from '../types';

export {
    vmService,
    containerService,
    storageService,
    networkService,
    jobService,
    healthService,
    replicationService,
    metricsService,
    hostService,
    authService
};

export const getPhysicalAdapters = networkService.getPhysicalAdapters;
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
export const getServiceInfo = healthService.getServiceInfo;

export const getHostDetails = async (): Promise<HostDetails> => {
    const [hardware, system] = await Promise.all([
        hostService.getHardwareInfo(),
        hostService.getSystemInfo()
    ]);
    
    const uptimeSeconds = (new Date().getTime() - new Date(system.lastBootUpTime).getTime()) / 1000;
    const uptimeDays = Math.floor(uptimeSeconds / 86400);
    const uptimeHours = Math.floor((uptimeSeconds % 86400) / 3600);
    const uptimeMins = Math.floor((uptimeSeconds % 3600) / 60);

    return {
        hostname: system.osName.split(' ')[0],
        manufacturer: hardware.manufacturer,
        model: hardware.model,
        cpuInfo: '28 CPUs x 2.4 GHz',
        totalMemoryGB: hardware.totalPhysicalMemory / (1024 * 1024 * 1024),
        serialNumber: hardware.biosSerialNumber,
        biosVersion: hardware.biosVersion,
        uptime: `${uptimeDays} Days, ${uptimeHours} Hours, ${uptimeMins} Minutes`,
        version: `${system.osVersion} (Build ${system.osBuildNumber})`
    };
};

export const getDashboardStats = async (): Promise<Stats> => {
    const results = await Promise.allSettled([
        vmService.getVms(),
        getNetworks(),
        hostService.getPerformanceSummary(),
        storageService.listStorageDevices(),
        hostService.getSystemInfo(),
    ]);
    
    const vmsData = results[0].status === 'fulfilled' ? results[0].value : { WMI: [], HCS: [] };
    const networks = results[1].status === 'fulfilled' ? results[1].value : { WMI: [], HCS: [] };
    const performance = results[2].status === 'fulfilled' ? results[2].value : { cpuUsagePercent: 0 };
    const storageDevices = results[3].status === 'fulfilled' ? results[3].value : [];
    const systemInfo = results[4].status === 'fulfilled' ? results[4].value : { totalVisibleMemorySize: 0 };

    const allVms = [...(vmsData.WMI || []), ...(vmsData.HCS || [])];
    const totalVms = allVms.length;
    const runningVms = allVms.filter(v => v.status === VmStatus.RUNNING).length;

    const vmPropsPromises = allVms.map(vm =>
        vmService.getVmProperties(vm.name).catch(() => ({ memoryMB: 0 }))
    );
    const vmPropsResults = await Promise.allSettled(vmPropsPromises);
    const totalMemoryAssignedGB = vmPropsResults
        .filter(r => r.status === 'fulfilled')
        .reduce((sum, r) => sum + ((r as PromiseFulfilledResult<any>).value.memoryMB || 0), 0) / 1024;
    
    const totalNetworks = (networks.WMI?.length || 0) + (networks.HCS?.length || 0);

    const storageCapacityBytes = storageDevices.reduce((sum, dev) => sum + dev.size, 0);
    const storageUsedBytes = storageDevices.reduce((sum, dev) => sum + dev.usedSpace, 0);

    return {
        runningVms,
        totalVms,
        totalNetworks,
        totalMemoryAssignedGB,
        totalMemoryCapacityGB: systemInfo.totalVisibleMemorySize / (1024 * 1024),
        cpuUsagePercent: performance.cpuUsagePercent,
        storageUsedTB: storageCapacityBytes > 0 ? storageUsedBytes / (1024**4) : 0,
        storageCapacityTB: storageCapacityBytes / (1024**4),
    };
};

export const getNetworks = async (): Promise<{ WMI: VirtualNetwork[], HCS: VirtualNetwork[] }> => {
    try {
        const data = await fetchApi('/networks');
        const wmiData = data.WMI || data.wmi || [];
        const hcsData = data.HCS || data.hcs || [];
        
        return {
            WMI: wmiData.map((net: any) => ({
                id: net.Id || net.id,
                name: net.Name || net.name,
                type: net.Type || net.type || NetworkType.INTERNAL,
                subnet: net.Subnet || net.subnet || 'N/A',
                environment: 'WMI' as VmEnvironment,
                notes: net.Notes || net.notes,
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
    externalAdapterName?: string;
    allowManagementOS?: boolean;
}): Promise<VirtualNetwork> => {
    if (payload.type === NetworkType.NAT) {
        throw new Error('NAT type is not supported for WMI switches. Use HCS instead.');
    }
    const params: Record<string, string> = {
        name: payload.name,
        type: payload.type,
    };

    if (payload.notes) params.notes = payload.notes;
    if (payload.type === NetworkType.EXTERNAL) {
        if (payload.externalAdapterName) params.externalAdapterName = payload.externalAdapterName;
        if (payload.allowManagementOS) params.allowManagementOS = String(payload.allowManagementOS);
    }
    
    const response = await fetchApi(`/networks/wmi?${new URLSearchParams(params)}`, { method: 'POST' });
    
    return {
        id: response.id,
        name: payload.name,
        type: payload.type,
        subnet: 'N/A',
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
    await fetchApi(`/networks/wmi/${encodeURIComponent(id)}?${params.toString()}`, { method: 'PUT' });
};
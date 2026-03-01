import { fetchApi } from './baseService';
import { HostHardware, HostSystem, HostPerformance, HostTask, HostUsageSummary, HostCapabilities } from '../types';

export const getHardwareInfo = async (): Promise<HostHardware> => {
    return fetchApi('/host/hardware');
};

export const getSystemInfo = async (): Promise<HostSystem> => {
    return fetchApi('/host/system');
};

export const getPerformanceSummary = async (): Promise<HostPerformance> => {
    return fetchApi('/host/performance');
};

export const getHostUsageSummary = async (): Promise<HostUsageSummary> => {
    return fetchApi('/host/metrics/usage');
};

export const getRecentTasks = async (limit: number = 10): Promise<HostTask[]> => {
    return fetchApi(`/host/tasks?limit=${limit}`);
};

export const getCapabilities = async (): Promise<HostCapabilities> => {
    return fetchApi('/host/capabilities');
};
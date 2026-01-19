import { fetchApi } from './baseService';
import { Metric, VmUsageSummary } from '../types';

export const getDiscreteMetricsForVm = async (vmName: string): Promise<Metric[]> => {
    return fetchApi(`/vms/${encodeURIComponent(vmName)}/metrics/discrete`);
};

export const getVmUsageSummary = async (vmName: string): Promise<VmUsageSummary> => {
    return fetchApi(`/vms/${encodeURIComponent(vmName)}/metrics/usage`);
};

export const getMetricsForResourcePool = async (resourceType: string, poolId: string, subType?: string): Promise<Metric[]> => {
    const params = new URLSearchParams({ resourceType, poolId });
    if (subType) {
        params.append('subType', subType);
    }
    return fetchApi(`/vms/metrics/pool?${params.toString()}`);
};
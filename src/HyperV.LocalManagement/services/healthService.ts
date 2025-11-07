import { fetchApi } from './baseService';
import { ServiceInfo } from '../types';

export const getHealthStatus = async (): Promise<{
    status: string;
    timestamp: string;
    version: string;
    services: {
        hcs: string;
        wmi: string;
        hcn: string;
    };
}> => {
    return fetchApi('/service/health');
};

export const getServiceInfo = async (): Promise<ServiceInfo> => {
    return fetchApi('/service/info');
};
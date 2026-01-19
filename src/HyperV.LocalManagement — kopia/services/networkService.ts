import { fetchApi, ApiError } from './baseService';
import { VirtualNetwork } from '../types';

export const createNatNetwork = async (name: string, prefix: string = '192.168.100.0/24'): Promise<string> => {
    const response = await fetchApi(`/networks/nat?name=${encodeURIComponent(name)}&prefix=${encodeURIComponent(prefix)}`, {
        method: 'POST'
    });
    return response.id;
};

export const deleteNetwork = async (networkId: string): Promise<void> => {
    await fetchApi(`/networks/${encodeURIComponent(networkId)}`, {
        method: 'DELETE'
    });
};

export const getNetworkProperties = async (networkId: string): Promise<VirtualNetwork> => {
    return fetchApi(`/networks/${encodeURIComponent(networkId)}/properties`);
};

export const createNetworkEndpoint = async (networkId: string, name: string, ipAddress?: string): Promise<string> => {
    const url = `/networks/${encodeURIComponent(networkId)}/endpoints?name=${encodeURIComponent(name)}` + 
                (ipAddress ? `&ipAddress=${encodeURIComponent(ipAddress)}` : '');
    const response = await fetchApi(url, {
        method: 'POST'
    });
    return response.id;
};

export const deleteNetworkEndpoint = async (endpointId: string): Promise<void> => {
    await fetchApi(`/networks/endpoints/${encodeURIComponent(endpointId)}`, {
        method: 'DELETE'
    });
};

export const getEndpointProperties = async (endpointId: string): Promise<{
    id: string;
    name: string;
    ipAddress: string;
    macAddress: string;
    networkId: string;
    state: string;
}> => {
    return fetchApi(`/networks/endpoints/${encodeURIComponent(endpointId)}/properties`);
};

export const getPhysicalAdapters = async (): Promise<{
    name: string;
    guid: string;
    pnpDeviceId: string;
}[]> => {
    return fetchApi('/networks/adapters');
};
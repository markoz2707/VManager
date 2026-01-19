import { fetchApi, ApiError } from './baseService';
import { VirtualNetwork, FibreChannelSan } from '../types';

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

export const connectVmToSwitch = async (vmName: string, switchName: string): Promise<void> => {
    await fetchApi(`/vms/${encodeURIComponent(vmName)}/connect/${encodeURIComponent(switchName)}`, {
        method: 'POST'
    });
};


// Fibre Channel
export const createFibreChannelSan = async (sanName: string, wwpnArray: string[], wwnnArray: string[], notes?: string): Promise<{ poolId: string }> => {
    return fetchApi('/fibrechannel/san', {
        method: 'POST',
        body: JSON.stringify({ sanName, wwpnArray, wwnnArray, notes })
    });
};

export const getFibreChannelSans = async (): Promise<FibreChannelSan[]> => {
    // Assuming a GET /fibrechannel/san endpoint exists to list all SANs
    try {
        return await fetchApi('/fibrechannel/san');
    } catch (e) {
        console.warn("Could not list Fibre Channel SANs, API might not support it.", e);
        return [];
    }
};

export const deleteFibreChannelSan = async (poolId: string): Promise<void> => {
    await fetchApi(`/fibrechannel/san/${encodeURIComponent(poolId)}`, { method: 'DELETE' });
};

export const addFibreChannelPortToVm = async (vmName: string, sanPoolId: string, wwpn: string, wwnn: string): Promise<{ portId: string }> => {
    return fetchApi(`/vms/${encodeURIComponent(vmName)}/fibrechannel/port`, {
        method: 'POST',
        body: JSON.stringify({ sanPoolId, wwpn, wwnn })
    });
};

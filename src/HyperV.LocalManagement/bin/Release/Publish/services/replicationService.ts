import { fetchApi } from './baseService';
import { ReplicationRelationship, FailoverMode } from '../types';

export const getReplicationRelationships = async (vmName?: string): Promise<ReplicationRelationship[]> => {
    const url = vmName ? `/replication/${encodeURIComponent(vmName)}/state` : '/replication/relationships';
    const result = await fetchApi(url);
    // The API might return a single object for a specific VM or an array for all.
    return Array.isArray(result) ? result : [result];
};

export const createReplicationRelationship = async (sourceVm: string, targetHost: string, authMode?: string): Promise<{ relationshipId: string }> => {
    return fetchApi('/replication/relationships', {
        method: 'POST',
        body: JSON.stringify({ sourceVm, targetHost, authMode })
    });
};

export const startReplication = async (vmName: string): Promise<{ jobId: string }> => {
    return fetchApi(`/replication/${encodeURIComponent(vmName)}/start`, { method: 'POST' });
};

export const initiateFailover = async (vmName: string, mode: FailoverMode): Promise<{ jobId: string }> => {
    return fetchApi(`/replication/${encodeURIComponent(vmName)}/failover`, {
        method: 'POST',
        body: JSON.stringify({ mode })
    });
};

export const reverseReplication = async (vmName: string): Promise<void> => {
    await fetchApi(`/replication/${encodeURIComponent(vmName)}/reverse`, { method: 'PUT' });
};

export const updateReplicationAuthorization = async (relationshipId: string, authMode: string, credentials?: object): Promise<void> => {
    await fetchApi(`/replication/${encodeURIComponent(relationshipId)}/authorization`, {
        method: 'PUT',
        body: JSON.stringify({ authMode, credentials })
    });
};

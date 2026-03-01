import { apiFetch } from './apiClient';
import type { ClusterDto, ClusterNodeDto } from '../types/api';

export async function getClusters(): Promise<ClusterDto[]> {
  return apiFetch<ClusterDto[]>('/api/clusters');
}

export async function getCluster(id: string): Promise<ClusterDto> {
  return apiFetch<ClusterDto>(`/api/clusters/${id}`);
}

export async function createCluster(name: string, description?: string, datacenterId?: string): Promise<ClusterDto> {
  return apiFetch<ClusterDto>('/api/clusters', {
    method: 'POST',
    body: JSON.stringify({ name, description: description || null, datacenterId: datacenterId || null }),
  });
}

export async function addNodeToCluster(clusterId: string, agentHostId: string): Promise<ClusterNodeDto> {
  return apiFetch<ClusterNodeDto>(`/api/clusters/${clusterId}/nodes`, {
    method: 'POST',
    body: JSON.stringify({ agentHostId }),
  });
}

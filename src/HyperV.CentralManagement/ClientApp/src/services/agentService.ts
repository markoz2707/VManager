import { apiFetch } from './apiClient';
import type { AgentHostDto } from '../types/api';

export async function getAgents(): Promise<AgentHostDto[]> {
  return apiFetch<AgentHostDto[]>('/api/agents');
}

export async function getAgent(id: string): Promise<AgentHostDto> {
  return apiFetch<AgentHostDto>(`/api/agents/${id}`);
}

export async function createAgent(
  hostname: string,
  apiBaseUrl: string,
  ipAddress?: string,
  hostType?: string,
  datacenterId?: string
): Promise<AgentHostDto> {
  return apiFetch<AgentHostDto>('/api/agents', {
    method: 'POST',
    body: JSON.stringify({
      hostname,
      apiBaseUrl,
      ipAddress: ipAddress || null,
      hostType: hostType || null,
      datacenterId: datacenterId || null,
    }),
  });
}

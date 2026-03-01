import { apiFetch } from './apiClient';
import type { DatacenterDto } from '../types/api';

export async function getDatacenters(): Promise<DatacenterDto[]> {
  return apiFetch<DatacenterDto[]>('/api/datacenters');
}

export async function createDatacenter(name: string, description?: string): Promise<DatacenterDto> {
  return apiFetch<DatacenterDto>('/api/datacenters', {
    method: 'POST',
    body: JSON.stringify({ name, description: description || null }),
  });
}

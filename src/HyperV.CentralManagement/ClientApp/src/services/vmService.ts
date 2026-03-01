import { apiFetch } from './apiClient';
import type { VmInventoryDto, VmFolderDto } from '../types/api';

export async function getAllVms(search?: string): Promise<VmInventoryDto[]> {
  const params = search ? `?search=${encodeURIComponent(search)}` : '';
  return apiFetch<VmInventoryDto[]>(`/api/v1/vms${params}`);
}

export async function getVmsByAgent(agentId: string): Promise<VmInventoryDto[]> {
  return apiFetch<VmInventoryDto[]>(`/api/v1/vms/agent/${agentId}`);
}

export async function getVm(id: string): Promise<VmInventoryDto> {
  return apiFetch<VmInventoryDto>(`/api/v1/vms/${id}`);
}

export async function powerOperation(
  vmId: string,
  operation: 'start' | 'stop' | 'shutdown' | 'pause' | 'resume'
): Promise<void> {
  return apiFetch<void>(`/api/v1/vms/${vmId}/power`, {
    method: 'POST',
    body: JSON.stringify({ operation }),
  });
}

export async function getFolders(): Promise<VmFolderDto[]> {
  return apiFetch<VmFolderDto[]>('/api/v1/vms/folders');
}

export async function getStatistics(): Promise<{
  total: number;
  running: number;
  stopped: number;
  saved: number;
}> {
  return apiFetch('/api/v1/vms/statistics');
}

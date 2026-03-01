import { fetchApi, getAuthHeaders } from './baseService';

export interface BackupInfo {
  id: string;
  vmName: string;
  createdUtc: string;
  sizeBytes: number;
  backupPath: string;
  hypervisorType: string;
  includesSnapshots: boolean;
}

export interface BackupResult {
  backupId: string;
  success: boolean;
  message: string;
  sizeBytes: number;
}

export interface RestoreResult {
  success: boolean;
  restoredVmName: string | null;
  message: string;
}

class BackupService {
  async listBackups(vmName?: string): Promise<BackupInfo[]> {
    const params = vmName ? `?vmName=${encodeURIComponent(vmName)}` : '';
    return await fetchApi(`/backups${params}`) || [];
  }

  async getBackup(id: string): Promise<BackupInfo> {
    return await fetchApi(`/backups/${id}`);
  }

  async backupVm(vmName: string, destinationPath: string, includeSnapshots: boolean = false): Promise<BackupResult> {
    return await fetchApi(`/vms/${encodeURIComponent(vmName)}/backup`, {
      method: 'POST',
      body: JSON.stringify({ destinationPath, includeSnapshots })
    });
  }

  async restoreBackup(backupId: string, newVmName?: string): Promise<RestoreResult> {
    return await fetchApi(`/backups/${backupId}/restore`, {
      method: 'POST',
      body: JSON.stringify({ newVmName })
    });
  }

  async deleteBackup(id: string): Promise<void> {
    await fetchApi(`/backups/${id}`, {
      method: 'DELETE'
    });
  }
}

export const backupService = new BackupService();

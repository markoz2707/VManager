import React, { useCallback, useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
import { Card } from '../components/Card';
import { Spinner } from '../components/Spinner';
import { OutletContextType } from '../App';
import { backupService, BackupInfo } from '../services/backupService';
import { PlusIcon, RefreshIcon, TrashIcon } from '../components/Icons';

const formatSize = (bytes: number): string => {
  if (bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  const value = bytes / Math.pow(1024, i);
  return `${value.toFixed(i > 1 ? 2 : 0)} ${units[i]}`;
};

const formatDateTime = (iso: string): string => {
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
};

export const BackupPage: React.FC = () => {
  const { addNotification } = useOutletContext<OutletContextType>();

  const [backups, setBackups] = useState<BackupInfo[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isBackupOpen, setIsBackupOpen] = useState(false);
  const [isRestoreOpen, setIsRestoreOpen] = useState(false);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [backupForm, setBackupForm] = useState({
    vmName: '',
    destinationPath: '',
    includeSnapshots: false,
  });

  const [restoreBackupId, setRestoreBackupId] = useState<string | null>(null);
  const [restoreForm, setRestoreForm] = useState({ newVmName: '' });

  const fetchData = useCallback(async () => {
    setIsLoading(true);
    try {
      const data = await backupService.listBackups();
      setBackups(data);
    } catch (err: any) {
      addNotification('error', `Failed to load backups: ${err.message}`);
    } finally {
      setIsLoading(false);
    }
  }, [addNotification]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleBackup = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    try {
      const result = await backupService.backupVm(
        backupForm.vmName,
        backupForm.destinationPath,
        backupForm.includeSnapshots
      );
      if (result.success) {
        addNotification('success', `Backup created successfully. Size: ${formatSize(result.sizeBytes)}`);
      } else {
        addNotification('error', `Backup failed: ${result.message}`);
      }
      setIsBackupOpen(false);
      setBackupForm({ vmName: '', destinationPath: '', includeSnapshots: false });
      fetchData();
    } catch (err: any) {
      addNotification('error', `Backup failed: ${err.message}`);
    } finally {
      setIsSubmitting(false);
    }
  };

  const openRestoreModal = (backup: BackupInfo) => {
    setRestoreBackupId(backup.id);
    setRestoreForm({ newVmName: '' });
    setIsRestoreOpen(true);
  };

  const handleRestore = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!restoreBackupId) return;
    setIsSubmitting(true);
    try {
      const result = await backupService.restoreBackup(
        restoreBackupId,
        restoreForm.newVmName || undefined
      );
      if (result.success) {
        addNotification(
          'success',
          `Backup restored successfully${result.restoredVmName ? ` as "${result.restoredVmName}"` : ''}.`
        );
      } else {
        addNotification('error', `Restore failed: ${result.message}`);
      }
      setIsRestoreOpen(false);
      setRestoreBackupId(null);
      fetchData();
    } catch (err: any) {
      addNotification('error', `Restore failed: ${err.message}`);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await backupService.deleteBackup(id);
      addNotification('success', 'Backup deleted.');
      setDeleteConfirmId(null);
      fetchData();
    } catch (err: any) {
      addNotification('error', `Failed to delete backup: ${err.message}`);
    }
  };

  return (
    <div className="flex flex-col h-full">
      <header className="p-4 bg-panel-bg border-b border-panel-border flex items-center justify-between flex-shrink-0">
        <h1 className="text-lg font-semibold text-gray-800">Backup & Restore</h1>
        <div className="flex items-center space-x-1">
          <Button variant="toolbar" onClick={() => setIsBackupOpen(true)} leftIcon={<PlusIcon />}>
            Backup VM
          </Button>
          <Button variant="toolbar" onClick={fetchData} leftIcon={<RefreshIcon />}>
            Refresh
          </Button>
        </div>
      </header>

      <main className="flex-1 overflow-y-auto p-4">
        <Card>
          {isLoading ? (
            <div className="p-6 flex justify-center">
              <Spinner />
            </div>
          ) : backups.length === 0 ? (
            <div className="p-6 text-center text-gray-500">
              No backups found. Click "Backup VM" to create one.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead className="bg-gray-100 border-b border-panel-border">
                  <tr>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">VM Name</th>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Date</th>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Size</th>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Hypervisor</th>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Snapshots</th>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Path</th>
                    <th className="px-4 py-2 text-right font-semibold text-gray-600">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {backups.map(backup => (
                    <tr key={backup.id} className="hover:bg-gray-50">
                      <td className="px-4 py-2 font-medium text-gray-800">{backup.vmName}</td>
                      <td className="px-4 py-2 text-gray-600 text-xs">{formatDateTime(backup.createdUtc)}</td>
                      <td className="px-4 py-2 text-gray-600">{formatSize(backup.sizeBytes)}</td>
                      <td className="px-4 py-2 text-gray-600">
                        <span className="inline-block px-2 py-0.5 text-xs bg-indigo-100 text-indigo-800 rounded">
                          {backup.hypervisorType}
                        </span>
                      </td>
                      <td className="px-4 py-2 text-gray-600 text-center">
                        {backup.includesSnapshots ? (
                          <span className="inline-block px-2 py-0.5 text-xs bg-green-100 text-green-800 rounded">
                            Yes
                          </span>
                        ) : (
                          <span className="text-gray-400">No</span>
                        )}
                      </td>
                      <td className="px-4 py-2 text-gray-600 text-xs font-mono max-w-xs truncate" title={backup.backupPath}>
                        {backup.backupPath}
                      </td>
                      <td className="px-4 py-2 text-right space-x-1 whitespace-nowrap">
                        <Button size="sm" variant="ghost" onClick={() => openRestoreModal(backup)}>
                          Restore
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => setDeleteConfirmId(backup.id)}
                          title="Delete backup"
                        >
                          <TrashIcon className="w-4 h-4 text-red-500" />
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      </main>

      {/* Backup VM Modal */}
      <Modal isOpen={isBackupOpen} onClose={() => setIsBackupOpen(false)} title="Backup Virtual Machine">
        <form onSubmit={handleBackup} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700">VM Name</label>
            <input
              type="text"
              required
              className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-primary-500 focus:border-primary-500"
              value={backupForm.vmName}
              onChange={e => setBackupForm({ ...backupForm, vmName: e.target.value })}
              placeholder="e.g. WebServer-01"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700">Destination Path</label>
            <input
              type="text"
              required
              className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-primary-500 focus:border-primary-500"
              value={backupForm.destinationPath}
              onChange={e => setBackupForm({ ...backupForm, destinationPath: e.target.value })}
              placeholder="D:\Backups\VMs"
            />
          </div>

          <div className="flex items-center">
            <input
              type="checkbox"
              id="includeSnapshots"
              className="h-4 w-4 text-blue-600 border-gray-300 rounded focus:ring-primary-500"
              checked={backupForm.includeSnapshots}
              onChange={e => setBackupForm({ ...backupForm, includeSnapshots: e.target.checked })}
            />
            <label htmlFor="includeSnapshots" className="ml-2 text-sm text-gray-700">
              Include snapshots in backup
            </label>
          </div>

          <div className="mt-6 flex justify-end space-x-3 pt-4 border-t border-gray-200">
            <Button type="button" variant="secondary" onClick={() => setIsBackupOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Backing up...' : 'Start Backup'}
            </Button>
          </div>
        </form>
      </Modal>

      {/* Restore Backup Modal */}
      <Modal isOpen={isRestoreOpen} onClose={() => setIsRestoreOpen(false)} title="Restore Backup">
        <form onSubmit={handleRestore} className="space-y-4">
          <p className="text-sm text-gray-600">
            Restore will recreate the virtual machine from this backup. You can optionally specify a new name
            for the restored VM.
          </p>

          <div>
            <label className="block text-sm font-medium text-gray-700">New VM Name (optional)</label>
            <input
              type="text"
              className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-primary-500 focus:border-primary-500"
              value={restoreForm.newVmName}
              onChange={e => setRestoreForm({ newVmName: e.target.value })}
              placeholder="Leave blank to use original name"
            />
          </div>

          <div className="mt-6 flex justify-end space-x-3 pt-4 border-t border-gray-200">
            <Button type="button" variant="secondary" onClick={() => setIsRestoreOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Restoring...' : 'Restore'}
            </Button>
          </div>
        </form>
      </Modal>

      {/* Delete Confirmation Modal */}
      <Modal
        isOpen={deleteConfirmId !== null}
        onClose={() => setDeleteConfirmId(null)}
        title="Confirm Delete"
      >
        <p className="text-sm text-gray-700 mb-6">
          Are you sure you want to delete this backup? This action cannot be undone and the backup data
          will be permanently removed.
        </p>
        <div className="flex justify-end space-x-3">
          <Button variant="secondary" onClick={() => setDeleteConfirmId(null)}>
            Cancel
          </Button>
          <Button
            variant="primary"
            onClick={() => deleteConfirmId && handleDelete(deleteConfirmId)}
          >
            Delete
          </Button>
        </div>
      </Modal>
    </div>
  );
};

export default BackupPage;

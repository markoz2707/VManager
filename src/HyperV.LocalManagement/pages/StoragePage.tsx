import React, { useCallback, useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
import { Spinner } from '../components/Spinner';
import { OutletContextType } from '../App';
import * as storage from '../services/storageService';
import * as jobs from '../services/jobService';
import { StorageJob } from '../types';

export const StoragePage: React.FC = () => {
  const { addNotification } = useOutletContext<OutletContextType>();

  // Create VHD form state
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState({ path: '', sizeGB: '20', format: 'VHDX' as 'VHD' | 'VHDX', type: 'Dynamic' as 'Fixed' | 'Dynamic' });

  // Attach/Detach form state
  const [isAttachOpen, setIsAttachOpen] = useState(false);
  const [attachForm, setAttachForm] = useState({ vmName: '', vhdPath: '' });
  const [isDetachOpen, setIsDetachOpen] = useState(false);
  const [detachForm, setDetachForm] = useState({ vmName: '', vhdPath: '' });

  // Resize form state
  const [isResizeOpen, setIsResizeOpen] = useState(false);
  const [resizeForm, setResizeForm] = useState({ vhdPath: '', newSizeGB: '40' });

  // Jobs
  const [jobsList, setJobsList] = useState<StorageJob[]>([]);
  const [isLoadingJobs, setIsLoadingJobs] = useState(false);

  const refreshJobs = useCallback(async () => {
    setIsLoadingJobs(true);
    try {
      const j = await jobs.getStorageJobs();
      setJobsList(j);
    } catch (err: any) {
      addNotification('error', `Failed to load storage jobs: ${err.message}`);
    } finally {
      setIsLoadingJobs(false);
    }
  }, [addNotification]);

  useEffect(() => { refreshJobs(); }, [refreshJobs]);

  const onCreateVhd = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await storage.createVhd(createForm.path, parseInt(createForm.sizeGB, 10), createForm.format, createForm.type);
      addNotification('success', 'VHD created successfully.');
      setIsCreateOpen(false);
      setCreateForm({ path: '', sizeGB: '20', format: 'VHDX', type: 'Dynamic' });
      refreshJobs();
    } catch (err: any) {
      addNotification('error', `Failed to create VHD: ${err.message}`);
    }
  };

  const onAttach = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await storage.attachVhd(attachForm.vmName, attachForm.vhdPath);
      addNotification('success', 'VHD attached successfully.');
      setIsAttachOpen(false);
      setAttachForm({ vmName: '', vhdPath: '' });
    } catch (err: any) {
      addNotification('error', `Failed to attach VHD: ${err.message}`);
    }
  };

  const onDetach = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await storage.detachVhd(detachForm.vmName, detachForm.vhdPath);
      addNotification('success', 'VHD detached successfully.');
      setIsDetachOpen(false);
      setDetachForm({ vmName: '', vhdPath: '' });
    } catch (err: any) {
      addNotification('error', `Failed to detach VHD: ${err.message}`);
    }
  };

  const onResize = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await storage.resizeVhd(resizeForm.vhdPath, parseInt(resizeForm.newSizeGB, 10));
      addNotification('success', 'VHD resize requested successfully.');
      setIsResizeOpen(false);
      setResizeForm({ vhdPath: '', newSizeGB: '40' });
      refreshJobs();
    } catch (err: any) {
      addNotification('error', `Failed to resize VHD: ${err.message}`);
    }
  };

  const cancelJob = async (jobId: string) => {
    try {
      await jobs.cancelStorageJob(jobId);
      addNotification('success', 'Job cancelled successfully.');
      refreshJobs();
    } catch (err: any) {
      addNotification('error', `Failed to cancel job: ${err.message}`);
    }
  };

  const deleteJob = async (jobId: string) => {
    try {
      await jobs.deleteStorageJob(jobId);
      addNotification('success', 'Job deleted successfully.');
      refreshJobs();
    } catch (err: any) {
      addNotification('error', `Failed to delete job: ${err.message}`);
    }
  };

  return (
    <div className="p-6 space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-3xl font-bold text-white">Storage</h1>
        <div className="space-x-2">
          <Button onClick={() => setIsCreateOpen(true)}>Create VHD</Button>
          <Button variant="secondary" onClick={() => setIsAttachOpen(true)}>Attach VHD</Button>
          <Button variant="secondary" onClick={() => setIsDetachOpen(true)}>Detach VHD</Button>
          <Button variant="secondary" onClick={() => setIsResizeOpen(true)}>Resize VHD</Button>
        </div>
      </div>

      <div className="bg-gray-800 rounded-lg shadow-md overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-700 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-white">Storage Jobs</h2>
          <Button size="sm" variant="ghost" onClick={refreshJobs}>Refresh</Button>
        </div>
        {isLoadingJobs ? (
          <div className="p-6 flex justify-center"><Spinner /></div>
        ) : jobsList.length === 0 ? (
          <div className="p-6 text-gray-400">No storage jobs.</div>
        ) : (
          <table className="min-w-full">
            <thead className="bg-gray-700">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">ID</th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Operation</th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">State</th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Progress</th>
                <th className="px-6 py-3 text-right text-xs font-medium text-gray-300 uppercase tracking-wider">Actions</th>
              </tr>
            </thead>
            <tbody className="bg-gray-800 divide-y divide-gray-700">
              {jobsList.map(j => (
                <tr key={j.jobId} className="hover:bg-gray-700/50">
                  <td className="px-6 py-3 text-gray-200 font-mono text-xs">{j.jobId}</td>
                  <td className="px-6 py-3 text-gray-200">{j.operationType}</td>
                  <td className="px-6 py-3 text-gray-200">{j.state}</td>
                  <td className="px-6 py-3 text-gray-200">{Math.round(j.percentComplete)}%</td>
                  <td className="px-6 py-3 text-right space-x-2">
                    <Button size="sm" variant="ghost" onClick={() => cancelJob(j.jobId)}>Cancel</Button>
                    <Button size="sm" variant="ghost" onClick={() => deleteJob(j.jobId)}>Delete</Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Create VHD Modal */}
      <Modal isOpen={isCreateOpen} onClose={() => setIsCreateOpen(false)} title="Create Virtual Hard Disk">
        <form onSubmit={onCreateVhd} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-300">Path</label>
            <input id="create-vhd-path" placeholder="C:\\VMs\\disk.vhdx" className="mt-1 w-full bg-gray-700 border border-gray-600 rounded-md py-2 px-3 text-white" value={createForm.path} onChange={e => setCreateForm({ ...createForm, path: e.target.value })} required />
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div>
              <label className="block text-sm font-medium text-gray-300">Size (GB)</label>
              <input id="create-vhd-size" type="number" min={1} placeholder="20" className="mt-1 w-full bg-gray-700 border border-gray-600 rounded-md py-2 px-3 text-white" value={createForm.sizeGB} onChange={e => setCreateForm({ ...createForm, sizeGB: e.target.value })} required />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-300">Format</label>
              <select id="create-vhd-format" title="Disk format" className="mt-1 w-full bg-gray-700 border border-gray-600 rounded-md py-2 px-3 text-white" value={createForm.format} onChange={e => setCreateForm({ ...createForm, format: e.target.value as any })}>
                <option value="VHDX">VHDX</option>
                <option value="VHD">VHD</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-300">Type</label>
              <select id="create-vhd-type" title="Disk type" className="mt-1 w-full bg-gray-700 border border-gray-600 rounded-md py-2 px-3 text-white" value={createForm.type} onChange={e => setCreateForm({ ...createForm, type: e.target.value as any })}>
                <option value="Dynamic">Dynamic</option>
                <option value="Fixed">Fixed</option>
              </select>
            </div>
          </div>
          <div className="flex justify-end space-x-2">
            <Button type="button" variant="secondary" onClick={() => setIsCreateOpen(false)}>Cancel</Button>
            <Button type="submit">Create</Button>
          </div>
        </form>
      </Modal>

      {/* Attach VHD Modal */}
      <Modal isOpen={isAttachOpen} onClose={() => setIsAttachOpen(false)} title="Attach VHD to VM">
        <form onSubmit={onAttach} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-300">VM Name</label>
            <input id="attach-vm-name" placeholder="MyVM" className="mt-1 w-full bg-gray-700 border border-gray-600 rounded-md py-2 px-3 text-white" value={attachForm.vmName} onChange={e => setAttachForm({ ...attachForm, vmName: e.target.value })} required />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-300">VHD Path</label>
            <input id="attach-vhd-path" placeholder="C:\\VMs\\disk.vhdx" className="mt-1 w-full bg-gray-700 border border-gray-600 rounded-md py-2 px-3 text-white" value={attachForm.vhdPath} onChange={e => setAttachForm({ ...attachForm, vhdPath: e.target.value })} required />
          </div>
          <div className="flex justify-end space-x-2">
            <Button type="button" variant="secondary" onClick={() => setIsAttachOpen(false)}>Cancel</Button>
            <Button type="submit">Attach</Button>
          </div>
        </form>
      </Modal>

      {/* Detach VHD Modal */}
      <Modal isOpen={isDetachOpen} onClose={() => setIsDetachOpen(false)} title="Detach VHD from VM">
        <form onSubmit={onDetach} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-300">VM Name</label>
            <input id="detach-vm-name" placeholder="MyVM" className="mt-1 w-full bg-gray-700 border border-gray-600 rounded-md py-2 px-3 text-white" value={detachForm.vmName} onChange={e => setDetachForm({ ...detachForm, vmName: e.target.value })} required />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-300">VHD Path</label>
            <input id="detach-vhd-path" placeholder="C:\\VMs\\disk.vhdx" className="mt-1 w-full bg-gray-700 border border-gray-600 rounded-md py-2 px-3 text-white" value={detachForm.vhdPath} onChange={e => setDetachForm({ ...detachForm, vhdPath: e.target.value })} required />
          </div>
          <div className="flex justify-end space-x-2">
            <Button type="button" variant="secondary" onClick={() => setIsDetachOpen(false)}>Cancel</Button>
            <Button type="submit">Detach</Button>
          </div>
        </form>
      </Modal>

      {/* Resize VHD Modal */}
      <Modal isOpen={isResizeOpen} onClose={() => setIsResizeOpen(false)} title="Resize VHD">
        <form onSubmit={onResize} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-300">VHD Path</label>
            <input id="resize-vhd-path" placeholder="C:\\VMs\\disk.vhdx" className="mt-1 w-full bg-gray-700 border border-gray-600 rounded-md py-2 px-3 text-white" value={resizeForm.vhdPath} onChange={e => setResizeForm({ ...resizeForm, vhdPath: e.target.value })} required />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-300">New Size (GB)</label>
            <input id="resize-vhd-size" type="number" min={1} placeholder="40" className="mt-1 w-full bg-gray-700 border border-gray-600 rounded-md py-2 px-3 text-white" value={resizeForm.newSizeGB} onChange={e => setResizeForm({ ...resizeForm, newSizeGB: e.target.value })} required />
          </div>
          <div className="flex justify-end space-x-2">
            <Button type="button" variant="secondary" onClick={() => setIsResizeOpen(false)}>Cancel</Button>
            <Button type="submit">Resize</Button>
          </div>
        </form>
      </Modal>
    </div>
  );
};

export default StoragePage;

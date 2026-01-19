import React, { useCallback, useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
import { Spinner } from '../components/Spinner';
import { OutletContextType } from '../App';
import * as storage from '../services/storageService';
import * as jobs from '../services/jobService';
import { StorageJob } from '../types';
import { StorageIcon, PlusIcon, TrashIcon, ChevronRightIcon } from '../components/Icons';

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
    <div className="p-6 space-y-6 animate-fade-in">
      <div className="flex justify-between items-center">
        <h1 className="text-3xl font-bold text-white">Storage Management</h1>
        <div className="space-x-2">
          <Button onClick={() => setIsCreateOpen(true)} leftIcon={<PlusIcon />}>Create VHD</Button>
          <Button variant="secondary" onClick={() => setIsAttachOpen(true)}>Attach VHD</Button>
          <Button variant="secondary" onClick={() => setIsDetachOpen(true)}>Detach VHD</Button>
          <Button variant="secondary" onClick={() => setIsResizeOpen(true)}>Resize VHD</Button>
        </div>
      </div>

      <div className="glass-panel overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-700/50 flex items-center justify-between bg-slate-800/30">
          <div className="flex items-center">
            <StorageIcon className="w-5 h-5 text-blue-400 mr-2" />
            <h2 className="text-lg font-semibold text-white">Storage Jobs</h2>
          </div>
          <Button size="sm" variant="ghost" onClick={refreshJobs}>Refresh</Button>
        </div>
        {isLoadingJobs ? (
          <div className="p-6 flex justify-center"><Spinner /></div>
        ) : jobsList.length === 0 ? (
          <div className="p-12 text-center text-slate-500 flex flex-col items-center">
            <StorageIcon className="w-12 h-12 mb-3 opacity-20" />
            <p>No active storage jobs.</p>
          </div>
        ) : (
          <table className="min-w-full">
            <thead className="bg-slate-800/50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-400 uppercase tracking-wider">ID</th>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-400 uppercase tracking-wider">Operation</th>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-400 uppercase tracking-wider">State</th>
                <th className="px-6 py-3 text-left text-xs font-medium text-slate-400 uppercase tracking-wider">Progress</th>
                <th className="px-6 py-3 text-right text-xs font-medium text-slate-400 uppercase tracking-wider">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-700/50">
              {jobsList.map(j => (
                <tr key={j.jobId} className="hover:bg-slate-700/30 transition-colors">
                  <td className="px-6 py-4 whitespace-nowrap text-slate-400 font-mono text-xs">{j.jobId.substring(0, 8)}...</td>
                  <td className="px-6 py-4 whitespace-nowrap text-white font-medium">{j.operationType}</td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <span className={`px-2 py-1 rounded-full text-xs font-medium ${j.state === 'Running' ? 'bg-blue-500/20 text-blue-400' :
                        j.state === 'Completed' ? 'bg-green-500/20 text-green-400' :
                          j.state === 'Failed' ? 'bg-red-500/20 text-red-400' :
                            'bg-slate-700 text-slate-300'
                      }`}>
                      {j.state}
                    </span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="w-full bg-slate-700 rounded-full h-2.5">
                      <div className="bg-blue-600 h-2.5 rounded-full transition-all duration-500" style={{ width: `${j.percentComplete}%` }}></div>
                    </div>
                    <span className="text-xs text-slate-400 mt-1 inline-block">{Math.round(j.percentComplete)}%</span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-right space-x-2">
                    <Button size="sm" variant="ghost" onClick={() => cancelJob(j.jobId)} disabled={j.state !== 'Running'}>Cancel</Button>
                    <Button size="sm" variant="ghost" onClick={() => deleteJob(j.jobId)}>
                      <TrashIcon className="h-4 w-4 text-slate-400 hover:text-red-400" />
                    </Button>
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
            <input id="create-vhd-path" placeholder="C:\\VMs\\disk.vhdx" className="mt-1 w-full bg-slate-800 border border-slate-600 rounded-md py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" value={createForm.path} onChange={e => setCreateForm({ ...createForm, path: e.target.value })} required />
            <p className="text-xs text-slate-500 mt-1">Full path to the new VHD file</p>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div>
              <label className="block text-sm font-medium text-gray-300">Size (GB)</label>
              <input id="create-vhd-size" type="number" min={1} placeholder="20" className="mt-1 w-full bg-slate-800 border border-slate-600 rounded-md py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" value={createForm.sizeGB} onChange={e => setCreateForm({ ...createForm, sizeGB: e.target.value })} required />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-300">Format</label>
              <select id="create-vhd-format" title="Disk format" className="mt-1 w-full bg-slate-800 border border-slate-600 rounded-md py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" value={createForm.format} onChange={e => setCreateForm({ ...createForm, format: e.target.value as any })}>
                <option value="VHDX">VHDX</option>
                <option value="VHD">VHD</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-300">Type</label>
              <select id="create-vhd-type" title="Disk type" className="mt-1 w-full bg-slate-800 border border-slate-600 rounded-md py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" value={createForm.type} onChange={e => setCreateForm({ ...createForm, type: e.target.value as any })}>
                <option value="Dynamic">Dynamic</option>
                <option value="Fixed">Fixed</option>
              </select>
            </div>
          </div>
          <div className="flex justify-end space-x-2 pt-4">
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
            <input id="attach-vm-name" placeholder="MyVM" className="mt-1 w-full bg-slate-800 border border-slate-600 rounded-md py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" value={attachForm.vmName} onChange={e => setAttachForm({ ...attachForm, vmName: e.target.value })} required />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-300">VHD Path</label>
            <input id="attach-vhd-path" placeholder="C:\\VMs\\disk.vhdx" className="mt-1 w-full bg-slate-800 border border-slate-600 rounded-md py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" value={attachForm.vhdPath} onChange={e => setAttachForm({ ...attachForm, vhdPath: e.target.value })} required />
          </div>
          <div className="flex justify-end space-x-2 pt-4">
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
            <input id="detach-vm-name" placeholder="MyVM" className="mt-1 w-full bg-slate-800 border border-slate-600 rounded-md py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" value={detachForm.vmName} onChange={e => setDetachForm({ ...detachForm, vmName: e.target.value })} required />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-300">VHD Path</label>
            <input id="detach-vhd-path" placeholder="C:\\VMs\\disk.vhdx" className="mt-1 w-full bg-slate-800 border border-slate-600 rounded-md py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" value={detachForm.vhdPath} onChange={e => setDetachForm({ ...detachForm, vhdPath: e.target.value })} required />
          </div>
          <div className="flex justify-end space-x-2 pt-4">
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
            <input id="resize-vhd-path" placeholder="C:\\VMs\\disk.vhdx" className="mt-1 w-full bg-slate-800 border border-slate-600 rounded-md py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" value={resizeForm.vhdPath} onChange={e => setResizeForm({ ...resizeForm, vhdPath: e.target.value })} required />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-300">New Size (GB)</label>
            <input id="resize-vhd-size" type="number" min={1} placeholder="40" className="mt-1 w-full bg-slate-800 border border-slate-600 rounded-md py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" value={resizeForm.newSizeGB} onChange={e => setResizeForm({ ...resizeForm, newSizeGB: e.target.value })} required />
          </div>
          <div className="flex justify-end space-x-2 pt-4">
            <Button type="button" variant="secondary" onClick={() => setIsResizeOpen(false)}>Cancel</Button>
            <Button type="submit">Resize</Button>
          </div>
        </form>
      </Modal>
    </div>
  );
};

export default StoragePage;

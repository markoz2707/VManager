import React, { useCallback, useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
import { Card } from '../components/Card';
import { Spinner } from '../components/Spinner';
import { OutletContextType } from '../App';
// Fix: Import all services through the hypervService barrel file for consistency.
import * as api from '../services/hypervService';
import { StorageJob, StorageQoSPolicy, VirtualMachine } from '../types';
import { RefreshIcon, PlusIcon } from '../components/Icons';
import { Tabs } from '../components/Tabs';
import { useHostContext } from '../hooks/useHostContext';

export const StoragePage: React.FC = () => {
  const { addNotification } = useOutletContext<OutletContextType>();
  const { isHyperV, isKVM, capabilities } = useHostContext();

  const [activeTab, setActiveTab] = useState<'VHD Management' | 'Storage QoS'>('VHD Management');
  const [isLoading, setIsLoading] = useState(true);

  // VHD state
  const [isCreateVhdOpen, setIsCreateVhdOpen] = useState(false);
  const [createVhdForm, setCreateVhdForm] = useState({ path: '', sizeGB: '20', format: 'VHDX' as 'VHD' | 'VHDX', type: 'Dynamic' as 'Fixed' | 'Dynamic' });
  const [jobsList, setJobsList] = useState<StorageJob[]>([]);
  
  // QoS state
  const [qosPolicies, setQosPolicies] = useState<StorageQoSPolicy[]>([]);
  const [vms, setVms] = useState<VirtualMachine[]>([]);
  const [isCreateQosOpen, setIsCreateQosOpen] = useState(false);
  const [isApplyQosOpen, setIsApplyQosOpen] = useState(false);
  const [createQosForm, setCreateQosForm] = useState({ policyId: '', maxIops: '1000', maxBandwidth: '100' });
  const [applyQosForm, setApplyQosForm] = useState({ vmName: '', policyId: '' });

  const fetchData = useCallback(async () => {
    setIsLoading(true);
    try {
      // Fix: Corrected service calls to use the unified 'api' import. vmService is correctly accessed from the hypervService export.
      const [j, p, vmsData] = await Promise.all([
        api.jobService.getStorageJobs(),
        api.storageService.getQoSPolicies(),
        api.vmService.getVms()
      ]);
      setJobsList(j);
      setQosPolicies(p);
      setVms(vmsData.WMI);
    } catch (err: any) {
      addNotification('error', `Failed to load storage data: ${err.message}`);
    } finally {
      setIsLoading(false);
    }
  }, [addNotification]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const onCreateVhd = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.storageService.createVhd(createVhdForm.path, parseInt(createVhdForm.sizeGB), createVhdForm.format, createVhdForm.type);
      addNotification('success', 'VHD created successfully.');
      setIsCreateVhdOpen(false);
      fetchData();
    } catch (err: any) { addNotification('error', `Failed to create VHD: ${err.message}`); }
  };
  
  const onCreateQos = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
        await api.storageService.createQoSPolicy(createQosForm.policyId, parseInt(createQosForm.maxIops), parseInt(createQosForm.maxBandwidth));
        addNotification('success', 'QoS Policy created.');
        setIsCreateQosOpen(false);
        fetchData();
    } catch (err: any) { addNotification('error', `Failed to create QoS policy: ${err.message}`); }
  }
  
  const onApplyQos = async (e: React.FormEvent) => {
      e.preventDefault();
      try {
          await api.storageService.applyQoSPolicyToVm(applyQosForm.vmName, applyQosForm.policyId);
          addNotification('success', 'QoS policy applied.');
          setIsApplyQosOpen(false);
      } catch (err: any) { addNotification('error', `Failed to apply QoS policy: ${err.message}`); }
  }

  const openApplyQosModal = (policyId: string) => {
      setApplyQosForm({ vmName: vms.length > 0 ? vms[0].name : '', policyId });
      setIsApplyQosOpen(true);
  }

  // VHD Operations state
  const [isResizeVhdOpen, setIsResizeVhdOpen] = useState(false);
  const [isConvertVhdOpen, setIsConvertVhdOpen] = useState(false);
  const [isVhdInfoOpen, setIsVhdInfoOpen] = useState(false);
  const [resizeVhdForm, setResizeVhdForm] = useState({ path: '', newSizeGB: '50' });
  const [convertVhdForm, setConvertVhdForm] = useState({ sourcePath: '', destinationPath: '', targetFormat: 'VHDX' as 'VHD' | 'VHDX' });
  const [vhdInfoPath, setVhdInfoPath] = useState('');
  const [vhdInfo, setVhdInfo] = useState<any>(null);
  const [isVhdOperationLoading, setIsVhdOperationLoading] = useState(false);

  const onResizeVhd = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsVhdOperationLoading(true);
    try {
      await api.storageService.resizeVhd(resizeVhdForm.path, parseInt(resizeVhdForm.newSizeGB));
      addNotification('success', 'VHD resized successfully.');
      setIsResizeVhdOpen(false);
      fetchData();
    } catch (err: any) { addNotification('error', `Failed to resize VHD: ${err.message}`); }
    finally { setIsVhdOperationLoading(false); }
  };

  const onConvertVhd = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsVhdOperationLoading(true);
    try {
      const jobId = await api.storageService.convertVhdFormat(convertVhdForm.sourcePath, convertVhdForm.destinationPath, convertVhdForm.targetFormat);
      addNotification('success', `VHD conversion started (Job: ${jobId}).`);
      setIsConvertVhdOpen(false);
      fetchData();
    } catch (err: any) { addNotification('error', `Failed to convert VHD: ${err.message}`); }
    finally { setIsVhdOperationLoading(false); }
  };

  const onCompactVhd = async (path: string) => {
    try {
      const jobId = await api.storageService.compactVhd(path);
      addNotification('success', `VHD compact started (Job: ${jobId}).`);
      fetchData();
    } catch (err: any) { addNotification('error', `Failed to compact VHD: ${err.message}`); }
  };

  const onGetVhdInfo = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsVhdOperationLoading(true);
    setVhdInfo(null);
    try {
      const info = await api.storageService.getVhdMetadata(vhdInfoPath);
      setVhdInfo(info);
    } catch (err: any) { addNotification('error', `Failed to get VHD info: ${err.message}`); }
    finally { setIsVhdOperationLoading(false); }
  };

  const renderVhdManagement = () => (
    <div className="space-y-4">
      <div className="flex space-x-2 mb-4">
        <Button size="sm" variant="secondary" onClick={() => setIsResizeVhdOpen(true)}>Resize VHD</Button>
        <Button size="sm" variant="secondary" onClick={() => setIsConvertVhdOpen(true)}>Convert VHD</Button>
        <Button size="sm" variant="secondary" onClick={() => setIsVhdInfoOpen(true)}>VHD Info</Button>
      </div>
      <Card title="Storage Jobs">
          {jobsList.length === 0 ? <p className="text-sm text-gray-500">No storage jobs.</p> :
              <table className="min-w-full text-sm">
                  <thead className="bg-gray-100"><tr><th className="px-3 py-2 text-left">ID</th><th className="px-3 py-2 text-left">Operation</th><th className="px-3 py-2 text-left">State</th><th className="px-3 py-2 text-left">Progress</th></tr></thead>
                  <tbody className="divide-y divide-gray-200">
                      {jobsList.map(j => <tr key={j.jobId} className="hover:bg-gray-50"><td className="px-3 py-2 font-mono text-xs">{j.jobId}</td><td className="px-3 py-2">{j.operationType}</td><td className="px-3 py-2">{j.state}</td><td className="px-3 py-2">{j.percentComplete}%</td></tr>)}
                  </tbody>
              </table>
          }
      </Card>
    </div>
  );

  const renderQosManagement = () => (
    <Card title="Storage QoS Policies">
        {qosPolicies.length === 0 ? <p>No QoS policies found.</p> :
            <table className="min-w-full text-sm">
                <thead><tr><th>Policy ID</th><th>Max IOPS</th><th>Max Bandwidth</th><th>Actions</th></tr></thead>
                <tbody>
                    {qosPolicies.map(p => <tr key={p.policyId}><td>{p.policyId}</td><td>{p.maxIops}</td><td>{p.maxBandwidth}</td><td><Button size="sm" onClick={() => openApplyQosModal(p.policyId)}>Apply to VM</Button></td></tr>)}
                </tbody>
            </table>
        }
    </Card>
  );


  return (
    <div className="flex flex-col h-full">
      <header className="p-4 bg-panel-bg border-b border-panel-border flex items-center justify-between flex-shrink-0">
        <h1 className="text-lg font-semibold text-gray-800">Storage</h1>
        <div className="flex items-center space-x-1">
            {activeTab === 'VHD Management' && <Button onClick={() => setIsCreateVhdOpen(true)} leftIcon={<PlusIcon/>}>Create {isKVM ? 'Disk' : 'VHD'}</Button>}
            {activeTab === 'Storage QoS' && isHyperV && <Button onClick={() => setIsCreateQosOpen(true)} leftIcon={<PlusIcon/>}>Create QoS Policy</Button>}
            <Button variant="toolbar" onClick={fetchData} leftIcon={<RefreshIcon />}>Refresh</Button>
        </div>
      </header>

      <main className="flex-1 overflow-y-auto p-4">
        {isHyperV ? (
            <Tabs tabs={['VHD Management', 'Storage QoS']} activeTab={activeTab} onTabClick={(tab) => setActiveTab(tab as 'VHD Management' | 'Storage QoS')} />
        ) : null}
        {isLoading ? <Spinner/> : (activeTab === 'VHD Management' || isKVM ? renderVhdManagement() : renderQosManagement())}
      </main>
      
      <Modal isOpen={isApplyQosOpen} onClose={() => setIsApplyQosOpen(false)} title={`Apply QoS Policy ${applyQosForm.policyId}`}>
        <form onSubmit={onApplyQos} className="space-y-4">
             <select className="mt-1 block w-full" value={applyQosForm.vmName} onChange={e => setApplyQosForm({...applyQosForm, vmName: e.target.value})}>
                {vms.map(vm => <option key={vm.id} value={vm.name}>{vm.name}</option>)}
            </select>
            <div className="mt-6 flex justify-end"><Button type="submit">Apply Policy</Button></div>
        </form>
      </Modal>

      <Modal isOpen={isCreateVhdOpen} onClose={() => setIsCreateVhdOpen(false)} title={isKVM ? 'Create Virtual Disk' : 'Create Virtual Hard Disk'}>
        <form onSubmit={onCreateVhd} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700">File Path</label>
            <input type="text" required className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={createVhdForm.path} onChange={e => setCreateVhdForm({...createVhdForm, path: e.target.value})} placeholder="C:\Hyper-V\Disks\disk1.vhdx" />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Size (GB)</label>
            <input type="number" required min="1" className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={createVhdForm.sizeGB} onChange={e => setCreateVhdForm({...createVhdForm, sizeGB: e.target.value})} />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Format</label>
            <select className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={createVhdForm.format} onChange={e => setCreateVhdForm({...createVhdForm, format: e.target.value as any})}>
              {isKVM ? (
                <>
                  <option value="qcow2">QCOW2 (recommended)</option>
                  <option value="raw">RAW</option>
                </>
              ) : (
                <>
                  <option value="VHDX">VHDX (recommended)</option>
                  <option value="VHD">VHD (legacy)</option>
                </>
              )}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Type</label>
            <select className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={createVhdForm.type} onChange={e => setCreateVhdForm({...createVhdForm, type: e.target.value as 'Fixed' | 'Dynamic'})}>
              <option value="Dynamic">Dynamic (grows as needed)</option>
              <option value="Fixed">Fixed (pre-allocated)</option>
            </select>
          </div>
          <div className="mt-6 flex justify-end space-x-2">
            <Button variant="ghost" onClick={() => setIsCreateVhdOpen(false)}>Cancel</Button>
            <Button type="submit">Create VHD</Button>
          </div>
        </form>
      </Modal>

      <Modal isOpen={isResizeVhdOpen} onClose={() => setIsResizeVhdOpen(false)} title="Resize Virtual Hard Disk">
        <form onSubmit={onResizeVhd} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700">VHD Path</label>
            <input type="text" required className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={resizeVhdForm.path} onChange={e => setResizeVhdForm({...resizeVhdForm, path: e.target.value})} placeholder="C:\Hyper-V\Disks\disk1.vhdx" />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">New Size (GB)</label>
            <input type="number" required min="1" className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={resizeVhdForm.newSizeGB} onChange={e => setResizeVhdForm({...resizeVhdForm, newSizeGB: e.target.value})} />
          </div>
          <div className="mt-6 flex justify-end space-x-2">
            <Button variant="ghost" onClick={() => setIsResizeVhdOpen(false)}>Cancel</Button>
            <Button type="submit" disabled={isVhdOperationLoading}>Resize</Button>
          </div>
        </form>
      </Modal>

      <Modal isOpen={isConvertVhdOpen} onClose={() => setIsConvertVhdOpen(false)} title="Convert Virtual Hard Disk">
        <form onSubmit={onConvertVhd} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700">Source Path</label>
            <input type="text" required className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={convertVhdForm.sourcePath} onChange={e => setConvertVhdForm({...convertVhdForm, sourcePath: e.target.value})} placeholder="C:\Hyper-V\Disks\disk1.vhd" />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Destination Path</label>
            <input type="text" required className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={convertVhdForm.destinationPath} onChange={e => setConvertVhdForm({...convertVhdForm, destinationPath: e.target.value})} placeholder="C:\Hyper-V\Disks\disk1.vhdx" />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Target Format</label>
            <select className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={convertVhdForm.targetFormat} onChange={e => setConvertVhdForm({...convertVhdForm, targetFormat: e.target.value as any})}>
              {isKVM ? (
                <>
                  <option value="qcow2">QCOW2</option>
                  <option value="raw">RAW</option>
                  <option value="vmdk">VMDK</option>
                </>
              ) : (
                <>
                  <option value="VHDX">VHDX</option>
                  <option value="VHD">VHD</option>
                </>
              )}
            </select>
          </div>
          <div className="mt-6 flex justify-end space-x-2">
            <Button variant="ghost" onClick={() => setIsConvertVhdOpen(false)}>Cancel</Button>
            <Button type="submit" disabled={isVhdOperationLoading}>Convert</Button>
          </div>
        </form>
      </Modal>

      <Modal isOpen={isVhdInfoOpen} onClose={() => { setIsVhdInfoOpen(false); setVhdInfo(null); }} title="VHD Information">
        <form onSubmit={onGetVhdInfo} className="space-y-4">
          <div className="flex space-x-2">
            <input type="text" required className="flex-1 border border-gray-300 rounded px-3 py-2 text-sm" value={vhdInfoPath} onChange={e => setVhdInfoPath(e.target.value)} placeholder="C:\Hyper-V\Disks\disk1.vhdx" />
            <Button type="submit" disabled={isVhdOperationLoading}>Get Info</Button>
          </div>
        </form>
        {vhdInfo && (
          <div className="mt-4 border border-gray-200 rounded-md p-3 bg-gray-50">
            <pre className="text-xs text-gray-700 overflow-auto max-h-64">{JSON.stringify(vhdInfo, null, 2)}</pre>
          </div>
        )}
      </Modal>

      <Modal isOpen={isCreateQosOpen} onClose={() => setIsCreateQosOpen(false)} title="Create QoS Policy">
        <form onSubmit={onCreateQos} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700">Policy ID</label>
            <input type="text" required className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={createQosForm.policyId} onChange={e => setCreateQosForm({...createQosForm, policyId: e.target.value})} placeholder="e.g. high-performance" />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Max IOPS</label>
            <input type="number" required min="0" className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={createQosForm.maxIops} onChange={e => setCreateQosForm({...createQosForm, maxIops: e.target.value})} />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Max Bandwidth (MB/s)</label>
            <input type="number" required min="0" className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={createQosForm.maxBandwidth} onChange={e => setCreateQosForm({...createQosForm, maxBandwidth: e.target.value})} />
          </div>
          <div className="mt-6 flex justify-end space-x-2">
            <Button variant="ghost" onClick={() => setIsCreateQosOpen(false)}>Cancel</Button>
            <Button type="submit">Create Policy</Button>
          </div>
        </form>
      </Modal>
    </div>
  );
};

export default StoragePage;
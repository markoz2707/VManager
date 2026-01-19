import React, { useCallback, useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
import { Card } from '../components/Card';
import { Spinner } from '../components/Spinner';
import { OutletContextType } from '../App';
import * as api from '../services/hypervService';
import { ReplicationRelationship, FailoverMode, VirtualMachine } from '../types';
import { PlusIcon, RefreshIcon, ActionsIcon } from '../components/Icons';

export const ReplicationPage: React.FC = () => {
  const { addNotification } = useOutletContext<OutletContextType>();
  const [relationships, setRelationships] = useState<ReplicationRelationship[]>([]);
  const [vms, setVms] = useState<VirtualMachine[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isFailoverModalOpen, setIsFailoverModalOpen] = useState(false);
  
  const [createForm, setCreateForm] = useState({ sourceVm: '', targetHost: '', authMode: 'Kerberos' });
  const [failoverVm, setFailoverVm] = useState<ReplicationRelationship | null>(null);
  const [failoverMode, setFailoverMode] = useState<FailoverMode>('Planned');

  const fetchData = useCallback(async () => {
    setIsLoading(true);
    try {
      const [rels, vmsData] = await Promise.all([
        api.replicationService.getReplicationRelationships(),
        api.vmService.getVms()
      ]);
      setRelationships(rels.filter(r => r && r.relationshipId));
      setVms(vmsData.WMI);
    } catch (err: any) {
      addNotification('error', `Failed to load replication data: ${err.message}`);
    } finally {
      setIsLoading(false);
    }
  }, [addNotification]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.replicationService.createReplicationRelationship(createForm.sourceVm, createForm.targetHost, createForm.authMode);
      addNotification('success', 'Replication relationship created successfully.');
      setIsCreateModalOpen(false);
      setCreateForm({ sourceVm: '', targetHost: '', authMode: 'Kerberos' });
      fetchData();
    } catch (err: any) {
      addNotification('error', `Failed to create relationship: ${err.message}`);
    }
  };

  const handleStartReplication = async (vmName: string) => {
    try {
      await api.replicationService.startReplication(vmName);
      addNotification('success', `Replication started for ${vmName}.`);
      fetchData();
    } catch (err: any) {
      addNotification('error', `Failed to start replication: ${err.message}`);
    }
  };
  
  const handleFailover = async (e: React.FormEvent) => {
      e.preventDefault();
      if (!failoverVm) return;
      try {
          await api.replicationService.initiateFailover(failoverVm.sourceVm, failoverMode);
          addNotification('success', `Failover initiated for ${failoverVm.sourceVm}.`);
          setIsFailoverModalOpen(false);
          setFailoverVm(null);
          fetchData();
      } catch (err: any) {
          addNotification('error', `Failover failed: ${err.message}`);
      }
  };
  
  const openFailoverModal = (rel: ReplicationRelationship) => {
      setFailoverVm(rel);
      setIsFailoverModalOpen(true);
  };

  return (
    <div className="flex flex-col h-full">
      <header className="p-4 bg-panel-bg border-b border-panel-border flex items-center justify-between flex-shrink-0">
        <h1 className="text-lg font-semibold text-gray-800">VM Replication</h1>
        <div className="flex items-center space-x-1">
          <Button variant="toolbar" onClick={() => setIsCreateModalOpen(true)} leftIcon={<PlusIcon />}>Create Relationship</Button>
          <Button variant="toolbar" onClick={fetchData} leftIcon={<RefreshIcon />}>Refresh</Button>
        </div>
      </header>

      <main className="flex-1 overflow-y-auto p-4">
        <Card>
          {isLoading ? (
            <div className="p-6 flex justify-center"><Spinner /></div>
          ) : relationships.length === 0 ? (
            <div className="p-6 text-center text-gray-500">No replication relationships found.</div>
          ) : (
            <table className="min-w-full text-sm">
              <thead className="bg-gray-100 border-b border-panel-border">
                <tr>
                  <th className="px-4 py-2 text-left font-semibold text-gray-600">Source VM</th>
                  <th className="px-4 py-2 text-left font-semibold text-gray-600">Target Host</th>
                  <th className="px-4 py-2 text-left font-semibold text-gray-600">State</th>
                  <th className="px-4 py-2 text-left font-semibold text-gray-600">Health</th>
                  <th className="px-4 py-2 text-left font-semibold text-gray-600">Mode</th>
                  <th className="px-4 py-2 text-right font-semibold text-gray-600">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {relationships.map(rel => (
                  <tr key={rel.relationshipId} className="hover:bg-gray-50">
                    <td className="px-4 py-2 font-medium text-gray-800">{rel.sourceVm}</td>
                    <td className="px-4 py-2 text-gray-600">{rel.targetHost}</td>
                    <td className="px-4 py-2 text-gray-600">{rel.state}</td>
                    <td className="px-4 py-2 text-gray-600">{rel.health || 'N/A'}</td>
                    <td className="px-4 py-2 text-gray-600">{rel.mode || 'N/A'}</td>
                    <td className="px-4 py-2 text-right space-x-1">
                      <Button size="sm" variant="ghost" onClick={() => handleStartReplication(rel.sourceVm)}>Start</Button>
                      <Button size="sm" variant="ghost" onClick={() => openFailoverModal(rel)}>Failover</Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Card>
      </main>

      <Modal isOpen={isCreateModalOpen} onClose={() => setIsCreateModalOpen(false)} title="Create Replication Relationship">
        <form onSubmit={handleCreate} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700">Source VM</label>
            <select className="mt-1 block w-full bg-white border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-primary-500 focus:border-primary-500" value={createForm.sourceVm} onChange={e => setCreateForm({ ...createForm, sourceVm: e.target.value })} required>
              <option value="">Select a VM</option>
              {vms.map(vm => <option key={vm.id} value={vm.name}>{vm.name}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Target Host</label>
            <input placeholder="hyperv-replica.domain.local" className="mt-1 block w-full bg-white border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-primary-500 focus:border-primary-500" value={createForm.targetHost} onChange={e => setCreateForm({ ...createForm, targetHost: e.target.value })} required />
          </div>
           <div className="mt-6 flex justify-end space-x-3 pt-4 border-t border-gray-200">
            <Button type="button" variant="secondary" onClick={() => setIsCreateModalOpen(false)}>Cancel</Button>
            <Button type="submit">Create</Button>
          </div>
        </form>
      </Modal>
      
      <Modal isOpen={isFailoverModalOpen} onClose={() => setIsFailoverModalOpen(false)} title={`Initiate Failover for ${failoverVm?.sourceVm}`}>
        <form onSubmit={handleFailover} className="space-y-4">
            <p>Select the type of failover to perform.</p>
             <div>
                <label className="block text-sm font-medium text-gray-700">Failover Mode</label>
                <select className="mt-1 block w-full bg-white border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-primary-500 focus:border-primary-500" value={failoverMode} onChange={e => setFailoverMode(e.target.value as FailoverMode)}>
                    <option value="Planned">Planned Failover</option>
                    <option value="Test">Test Failover</option>
                    <option value="Live">Live Failover</option>
                </select>
            </div>
            <div className="mt-6 flex justify-end space-x-3 pt-4 border-t border-gray-200">
                <Button type="button" variant="secondary" onClick={() => setIsFailoverModalOpen(false)}>Cancel</Button>
                <Button type="submit">Initiate Failover</Button>
            </div>
        </form>
      </Modal>
    </div>
  );
};

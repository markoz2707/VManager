import React, { useState, useEffect, useCallback } from 'react';
import { useOutletContext } from 'react-router-dom';
import { VirtualMachine, VmStatus } from '../types';
import * as api from '../services/hypervService';
import { VmSnapshot } from '../types';
import { Spinner } from '../components/Spinner';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
import { PlayIcon, StopIcon, PauseIcon, PlusIcon, TrashIcon, ResumeIcon } from '../components/Icons';
import { OutletContextType } from '../App';

type VmAction = 'start' | 'stop' | 'pause' | 'resume' | 'shutdown' | 'terminate' | 'save';

const statusColors: Record<string, string> = {
    [VmStatus.RUNNING]: 'bg-green-500',
    [VmStatus.STOPPED]: 'bg-red-500',
    [VmStatus.PAUSED]: 'bg-yellow-500',
    [VmStatus.SAVED]: 'bg-blue-500',
    [VmStatus.SAVING]: 'bg-blue-500 animate-pulse-fast',
    [VmStatus.STARTING]: 'bg-orange-500 animate-pulse',
    [VmStatus.STOPPING]: 'bg-orange-500 animate-pulse',
    [VmStatus.PAUSING]: 'bg-yellow-600 animate-pulse',
    [VmStatus.RESUMING]: 'bg-green-600 animate-pulse',
    [VmStatus.SUSPENDED]: 'bg-gray-400',
    [VmStatus.UNKNOWN]: 'bg-gray-500',
};

const VmCard: React.FC<{
    vm: VirtualMachine;
    onAction: (name: string, action: VmAction) => void,
    isBusy: boolean;
    onOpenSnapshots: (vmName: string) => void;
}> = ({ vm, onAction, isBusy, onOpenSnapshots }) => {

    const renderActionButton = () => {
        if (vm.status === VmStatus.RUNNING) {
            return (
                <Button size="sm" variant="ghost" disabled={isBusy} onClick={() => onAction(vm.name, 'pause')}>
                    <PauseIcon className="h-4 w-4" />
                </Button>
            );
        }
        if (vm.status === VmStatus.PAUSED || vm.status === VmStatus.SUSPENDED) {
            return (
                <Button size="sm" variant="ghost" disabled={isBusy} onClick={() => onAction(vm.name, 'resume')}>
                    <ResumeIcon className="h-4 w-4" />
                </Button>
            );
        }
        return (
            <Button size="sm" variant="ghost" disabled>
                <PauseIcon className="h-4 w-4" />
            </Button>
        );
    }

    const canStart = vm.status === VmStatus.STOPPED || vm.status === VmStatus.SAVED;
    const canStop = vm.status === VmStatus.RUNNING;
    const canSave = vm.status === VmStatus.RUNNING || vm.status === VmStatus.PAUSED;

    return (
        <div className="bg-gray-800 rounded-lg shadow-md p-4 flex flex-col justify-between transition-transform transform hover:scale-105">
            <div>
                <div className="flex justify-between items-start">
                    <h3 className="text-lg font-bold text-white">{vm.name}</h3>
                    <div className="flex items-center">
                        <span className={`h-3 w-3 rounded-full mr-2 ${statusColors[vm.status] || 'bg-gray-500'}`}></span>
                        <span className="text-sm font-medium text-gray-300">{vm.status || 'Unknown'}</span>
                    </div>
                </div>
                <div className="text-sm text-gray-400 mt-2 grid grid-cols-2 gap-2">
                    {vm.cpuCount === undefined ? <Spinner size="sm" /> : (
                      <>
                        <span>CPUs: <span className="font-semibold text-gray-200">{vm.cpuCount}</span></span>
                        <span>Memory: <span className="font-semibold text-gray-200">{vm.memoryMB} MB</span></span>
                      </>
                    )}
                </div>
            </div>
            <div className="mt-4 flex space-x-2">
                <Button size="sm" variant="ghost" disabled={isBusy || !canStart} onClick={() => onAction(vm.name, 'start')}>
                    <PlayIcon className="h-4 w-4" />
                </Button>
                <Button size="sm" variant="ghost" disabled={isBusy || !canStop} onClick={() => onAction(vm.name, 'stop')}>
                    <StopIcon className="h-4 w-4" />
                </Button>
                {renderActionButton()}
                <Button size="sm" variant="ghost" disabled={isBusy || !canSave} onClick={() => onAction(vm.name, 'save')}>
                    💾
                </Button>
                <Button size="sm" variant="ghost" disabled={isBusy} onClick={() => onOpenSnapshots(vm.name)} title="Snapshots">
                    📸
                </Button>
            </div>
        </div>
    );
};

export const VirtualMachinesPage = () => {
    const { addNotification } = useOutletContext<OutletContextType>();
    const [vms, setVms] = useState<VirtualMachine[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [busyVm, setBusyVm] = useState<string | null>(null);
    const [selectedVmForSnapshots, setSelectedVmForSnapshots] = useState<string | null>(null);
    const [snapshots, setSnapshots] = useState<VmSnapshot[]>([]);
    const [snapshotName, setSnapshotName] = useState('');
    const [snapshotDescription, setSnapshotDescription] = useState('');
    const [isLoadingSnapshots, setIsLoadingSnapshots] = useState(false);

    const [newVm, setNewVm] = useState({ Name: '', CpuCount: '2', MemoryMB: '2048', DiskSizeGB: '20' });

    const fetchVms = useCallback(async () => {
        try {
            const vmList = await api.getVms();
            console.log('Fetched VMs:', vmList);
            
            // VMs are only in WMI environment, HCS is for containers
            const wmiVms = vmList.WMI || [];
            setVms(wmiVms);
            
            // Fetch properties for each VM in parallel (but limit concurrent requests)
            const vmPropsPromises = wmiVms.map(async (vm) => {
                try {
                    const props = await api.getVmProperties(vm.name);
                    return { ...vm, ...props };
                } catch (err: any) {
                    console.error(`Failed to get properties for ${vm.name}:`, err);
                    return vm; // Return original VM if properties fail
                }
            });

            const vmsWithProps = await Promise.allSettled(vmPropsPromises);
            const finalVms = vmsWithProps.map((result, index) => 
                result.status === 'fulfilled' ? result.value : wmiVms[index]
            );
            
            setVms(finalVms);
        } catch (err: any) {
            addNotification('error', `Failed to load VMs: ${err.message}`);
            console.error('VM fetch error:', err);
        } finally {
            setIsLoading(false);
        }
    }, [addNotification]);

    useEffect(() => {
        setIsLoading(true);
        fetchVms();
    }, [fetchVms]);

    const handleAction = async (name: string, action: VmAction) => {
        setBusyVm(name);
        const actionMap = {
            start: api.startVm,
            stop: api.stopVm,
            pause: api.pauseVm,
            resume: api.resumeVm,
            shutdown: api.shutdownVm,
            terminate: api.terminateVm,
            save: api.saveVm,
        };

        try {
            await actionMap[action](name);
            addNotification('success', `VM action '${action}' completed successfully.`);
            setTimeout(fetchVms, 1000);
        } catch (err: any) {
            addNotification('error', `Action failed: ${err.message}`);
        } finally {
            setBusyVm(null);
        }
    };

    const handleCreateVm = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await api.createVm({ 
                Name: newVm.Name, 
                CpuCount: parseInt(newVm.CpuCount, 10), 
                MemoryMB: parseInt(newVm.MemoryMB, 10),
                DiskSizeGB: parseInt(newVm.DiskSizeGB, 10)
            }, 'WMI');
            addNotification('success', `VM '${newVm.Name}' created successfully.`);
            setIsModalOpen(false);
            setNewVm({ Name: '', CpuCount: '2', MemoryMB: '2048', DiskSizeGB: '20' });
            fetchVms();
        } catch (err: any) {
            addNotification('error', `Failed to create VM: ${err.message}`);
        }
    };

    const loadSnapshots = async (vmName: string) => {
        setIsLoadingSnapshots(true);
        try {
            const snapshotList = await api.getVmSnapshots(vmName);
            setSnapshots(snapshotList);
        } catch (err: any) {
            addNotification('error', `Failed to load snapshots: ${err.message}`);
            setSnapshots([]);
        } finally {
            setIsLoadingSnapshots(false);
        }
    };

    const handleCreateSnapshot = async () => {
        if (!selectedVmForSnapshots || !snapshotName.trim()) return;
        
        try {
            await api.createVmSnapshot(selectedVmForSnapshots, snapshotName.trim(), snapshotDescription.trim() || undefined);
            addNotification('success', `Snapshot '${snapshotName}' created successfully.`);
            setSnapshotName('');
            setSnapshotDescription('');
            await loadSnapshots(selectedVmForSnapshots);
        } catch (err: any) {
            addNotification('error', `Failed to create snapshot: ${err.message}`);
        }
    };

    const handleDeleteSnapshot = async (snapshotId: string) => {
        if (!selectedVmForSnapshots) return;
        
        try {
            await api.deleteVmSnapshot(selectedVmForSnapshots, snapshotId);
            addNotification('success', 'Snapshot deleted successfully.');
            await loadSnapshots(selectedVmForSnapshots);
        } catch (err: any) {
            addNotification('error', `Failed to delete snapshot: ${err.message}`);
        }
    };

    const handleRevertToSnapshot = async (snapshotId: string) => {
        if (!selectedVmForSnapshots) return;
        
        try {
            await api.revertToSnapshot(selectedVmForSnapshots, snapshotId);
            addNotification('success', 'VM reverted to snapshot successfully.');
            setSelectedVmForSnapshots(null);
            fetchVms();
        } catch (err: any) {
            addNotification('error', `Failed to revert to snapshot: ${err.message}`);
        }
    };

    const openSnapshotsModal = (vmName: string) => {
        setSelectedVmForSnapshots(vmName);
        loadSnapshots(vmName);
    };

    const closeSnapshotsModal = () => {
        setSelectedVmForSnapshots(null);
        setSnapshots([]);
        setSnapshotName('');
        setSnapshotDescription('');
    };

    return (
        <div className="p-6">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-3xl font-bold text-white">Virtual Machines</h1>
                <Button onClick={() => setIsModalOpen(true)} leftIcon={<PlusIcon />}>Create VM</Button>
            </div>

            {isLoading ? <div className="flex justify-center items-center h-96"><Spinner /></div> : (
                vms.length > 0 ? (
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
                        {vms.map(vm => <VmCard
                            key={vm.id}
                            vm={vm}
                            onAction={handleAction}
                            isBusy={busyVm === vm.name}
                            onOpenSnapshots={openSnapshotsModal}
                        />)}
                    </div>
                ) : (
                    <div className="text-center py-12 text-gray-500">
                        <p>No virtual machines found. Create your first VM to get started.</p>
                    </div>
                )
            )}

            <Modal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} title="Create New Virtual Machine">
                <form onSubmit={handleCreateVm}>
                    <div className="space-y-4">
                        <div>
                            <label htmlFor="vm-name" className="block text-sm font-medium text-gray-300">VM Name</label>
                            <input type="text" id="vm-name" value={newVm.Name} onChange={e => setNewVm({ ...newVm, Name: e.target.value })} className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500" required />
                        </div>
                        <div>
                            <label htmlFor="cpu-count" className="block text-sm font-medium text-gray-300">CPU Count</label>
                            <input type="number" id="cpu-count" min="1" max="16" value={newVm.CpuCount} onChange={e => setNewVm({ ...newVm, CpuCount: e.target.value })} className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500" required />
                        </div>
                        <div>
                            <label htmlFor="memory-mb" className="block text-sm font-medium text-gray-300">Memory (MB)</label>
                            <input type="number" id="memory-mb" step="512" min="512" value={newVm.MemoryMB} onChange={e => setNewVm({ ...newVm, MemoryMB: e.target.value })} className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500" required />
                        </div>
                        <div>
                            <label htmlFor="disk-size-gb" className="block text-sm font-medium text-gray-300">Disk Size (GB)</label>
                            <input type="number" id="disk-size-gb" step="10" min="10" value={newVm.DiskSizeGB} onChange={e => setNewVm({ ...newVm, DiskSizeGB: e.target.value })} className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500" required />
                        </div>
                    </div>
                    <div className="mt-6 flex justify-end space-x-3">
                        <Button type="button" variant="secondary" onClick={() => setIsModalOpen(false)}>Cancel</Button>
                        <Button type="submit">Create</Button>
                    </div>
                </form>
            </Modal>

            {/* Snapshots Modal */}
            <Modal
                isOpen={selectedVmForSnapshots !== null}
                onClose={closeSnapshotsModal}
                title={`Snapshots - ${selectedVmForSnapshots}`}
            >
                <div className="space-y-6">
                    {/* Create Snapshot Section */}
                    <div className="border-b border-gray-600 pb-4">
                        <h3 className="text-lg font-medium text-white mb-3">Create New Snapshot</h3>
                        <div className="space-y-3">
                            <div>
                                <label htmlFor="snapshot-name" className="block text-sm font-medium text-gray-300">Snapshot Name</label>
                                <input
                                    type="text"
                                    id="snapshot-name"
                                    value={snapshotName}
                                    onChange={e => setSnapshotName(e.target.value)}
                                    className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500"
                                    placeholder="Enter snapshot name..."
                                />
                            </div>
                            <div>
                                <label htmlFor="snapshot-description" className="block text-sm font-medium text-gray-300">Description (Optional)</label>
                                <textarea
                                    id="snapshot-description"
                                    value={snapshotDescription}
                                    onChange={e => setSnapshotDescription(e.target.value)}
                                    className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500"
                                    rows={3}
                                    placeholder="Optional description..."
                                />
                            </div>
                            <Button onClick={handleCreateSnapshot} disabled={!snapshotName.trim()}>
                                Create Snapshot
                            </Button>
                        </div>
                    </div>

                    {/* Snapshots List */}
                    <div>
                        <h3 className="text-lg font-medium text-white mb-3">Existing Snapshots</h3>
                        {isLoadingSnapshots ? (
                            <div className="flex justify-center py-8">
                                <Spinner />
                            </div>
                        ) : snapshots.length > 0 ? (
                            <div className="space-y-3">
                                {snapshots.map((snapshot) => (
                                    <div key={snapshot.id} className="bg-gray-700 rounded-lg p-4">
                                        <div className="flex justify-between items-start">
                                            <div>
                                                <h4 className="font-medium text-white">{snapshot.name}</h4>
                                                <p className="text-sm text-gray-300 mt-1">
                                                    Created: {new Date(snapshot.creationTime).toLocaleString()}
                                                </p>
                                                {snapshot.type && (
                                                    <p className="text-xs text-gray-400 mt-1">Type: {snapshot.type}</p>
                                                )}
                                            </div>
                                            <div className="flex space-x-2">
                                                <Button
                                                    size="sm"
                                                    variant="ghost"
                                                    onClick={() => handleRevertToSnapshot(snapshot.id)}
                                                    title="Revert to this snapshot"
                                                >
                                                    ↩️ Revert
                                                </Button>
                                                <Button
                                                    size="sm"
                                                    variant="ghost"
                                                    onClick={() => handleDeleteSnapshot(snapshot.id)}
                                                    className="text-red-400 hover:text-red-300"
                                                    title="Delete this snapshot"
                                                >
                                                    🗑️ Delete
                                                </Button>
                                            </div>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        ) : (
                            <p className="text-gray-400 text-center py-8">No snapshots found for this VM.</p>
                        )}
                    </div>
                </div>
            </Modal>
        </div>
    );
};
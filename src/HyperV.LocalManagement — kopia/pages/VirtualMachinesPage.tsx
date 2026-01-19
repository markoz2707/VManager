import React, { useState, useEffect, useCallback } from 'react';
import { useOutletContext, useParams, useNavigate } from 'react-router-dom';
import { VirtualMachine, VmStatus } from '../types';
import * as api from '../services/hypervService';
import { VmSnapshot } from '../types';
import { Spinner } from '../components/Spinner';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
import { Tabs } from '../components/Tabs';
import { PlayIcon, StopIcon, PauseIcon, PlusIcon, TrashIcon, ResumeIcon, VmIcon, ChipIcon, StorageIcon, NetworkIcon } from '../components/Icons';
import { OutletContextType } from '../App';
import { Gauge } from '../components/Widgets/Gauge';

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

// --- Components ---

const VmCard: React.FC<{
    vm: VirtualMachine;
    onAction: (name: string, action: VmAction) => void,
    isBusy: boolean;
    onOpenSnapshots: (vmName: string) => void;
    onClick: () => void;
}> = ({ vm, onAction, isBusy, onOpenSnapshots, onClick }) => {

    const renderActionButton = () => {
        if (vm.status === VmStatus.RUNNING) {
            return (
                <Button size="sm" variant="ghost" disabled={isBusy} onClick={(e) => { e.stopPropagation(); onAction(vm.name, 'pause'); }}>
                    <PauseIcon className="h-4 w-4" />
                </Button>
            );
        }
        if (vm.status === VmStatus.PAUSED || vm.status === VmStatus.SUSPENDED) {
            return (
                <Button size="sm" variant="ghost" disabled={isBusy} onClick={(e) => { e.stopPropagation(); onAction(vm.name, 'resume'); }}>
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
        <div
            className="glass-panel p-4 flex flex-col justify-between transition-all transform hover:scale-[1.02] hover:bg-slate-800/60 cursor-pointer group"
            onClick={onClick}
        >
            <div>
                <div className="flex justify-between items-start">
                    <div className="flex items-center">
                        <div className={`p-2 rounded-lg mr-3 ${vm.status === VmStatus.RUNNING ? 'bg-green-500/20' : 'bg-slate-700/50'}`}>
                            <VmIcon className={`h-6 w-6 ${vm.status === VmStatus.RUNNING ? 'text-green-400' : 'text-slate-400'}`} />
                        </div>
                        <div>
                            <h3 className="text-lg font-bold text-white group-hover:text-blue-400 transition-colors">{vm.name}</h3>
                            <div className="flex items-center mt-1">
                                <span className={`h-2 w-2 rounded-full mr-2 ${statusColors[vm.status] || 'bg-gray-500'}`}></span>
                                <span className="text-xs font-medium text-slate-400 uppercase tracking-wide">{vm.status || 'Unknown'}</span>
                            </div>
                        </div>
                    </div>
                </div>
                <div className="mt-4 grid grid-cols-2 gap-2">
                    <div className="bg-slate-900/50 p-2 rounded flex items-center">
                        <ChipIcon className="w-4 h-4 text-slate-500 mr-2" />
                        <span className="text-sm text-slate-300">{vm.cpuCount || '-'} vCPU</span>
                    </div>
                    <div className="bg-slate-900/50 p-2 rounded flex items-center">
                        <div className="w-4 h-4 text-slate-500 mr-2 text-xs font-bold border border-slate-500 rounded flex items-center justify-center">M</div>
                        <span className="text-sm text-slate-300">{vm.memoryMB ? Math.round(vm.memoryMB / 1024 * 10) / 10 + ' GB' : '-'}</span>
                    </div>
                </div>
            </div>
            <div className="mt-4 flex space-x-2 border-t border-slate-700/50 pt-3">
                <Button size="sm" variant="ghost" disabled={isBusy || !canStart} onClick={(e) => { e.stopPropagation(); onAction(vm.name, 'start'); }} title="Start">
                    <PlayIcon className="h-4 w-4 text-green-400" />
                </Button>
                <Button size="sm" variant="ghost" disabled={isBusy || !canStop} onClick={(e) => { e.stopPropagation(); onAction(vm.name, 'stop'); }} title="Stop">
                    <StopIcon className="h-4 w-4 text-red-400" />
                </Button>
                {renderActionButton()}
            </div>
        </div>
    );
};

const VmDetail: React.FC<{ vmName: string }> = ({ vmName }) => {
    const [activeTab, setActiveTab] = useState('Summary');
    const [vm, setVm] = useState<VirtualMachine | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const { addNotification } = useOutletContext<OutletContextType>();

    useEffect(() => {
        const loadVm = async () => {
            try {
                // In a real app we might have a getVm(id) but here we filter from list or get properties
                // For now let's fetch properties which acts like a detail fetch
                const props = await api.getVmProperties(vmName);
                // We need the base VM info too, but getVmProperties might return enough
                // Let's construct a VM object
                setVm({
                    id: vmName, // ID might be different but name is key for WMI
                    name: vmName,
                    status: props.state as VmStatus, // Map state to status
                    memoryMB: props.memory,
                    cpuCount: props.processors,
                    environment: 'WMI' // Assuming WMI for now
                });
            } catch (err: any) {
                addNotification('error', `Failed to load VM details: ${err.message}`);
            } finally {
                setIsLoading(false);
            }
        };
        loadVm();
    }, [vmName, addNotification]);

    if (isLoading) return <div className="flex justify-center py-20"><Spinner /></div>;
    if (!vm) return <div className="text-center py-20 text-slate-400">VM not found</div>;

    return (
        <div className="animate-fade-in">
            <div className="flex items-center mb-6">
                <div className={`p-3 rounded-xl mr-4 ${vm.status === VmStatus.RUNNING ? 'bg-green-500/20' : 'bg-slate-700/50'}`}>
                    <VmIcon className={`h-8 w-8 ${vm.status === VmStatus.RUNNING ? 'text-green-400' : 'text-slate-400'}`} />
                </div>
                <div>
                    <h1 className="text-2xl font-bold text-white">{vm.name}</h1>
                    <div className="flex items-center mt-1 space-x-3">
                        <span className={`px-2 py-0.5 rounded text-xs font-medium uppercase ${vm.status === VmStatus.RUNNING ? 'bg-green-500/20 text-green-400' : 'bg-slate-700 text-slate-400'
                            }`}>
                            {vm.status}
                        </span>
                        <span className="text-slate-500 text-sm">|</span>
                        <span className="text-slate-400 text-sm">WMI Backend</span>
                    </div>
                </div>
            </div>

            <Tabs
                tabs={['Summary', 'Monitor', 'Configure']}
                activeTab={activeTab}
                onTabClick={setActiveTab}
            />

            <div className="mt-6">
                {activeTab === 'Summary' && (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                        <div className="glass-panel p-6">
                            <h3 className="text-lg font-medium text-white mb-4 border-b border-slate-700 pb-2">General</h3>
                            <div className="space-y-3">
                                <div className="flex justify-between">
                                    <span className="text-slate-400">Guest OS</span>
                                    <span className="text-white">Windows 10 (Detected)</span>
                                </div>
                                <div className="flex justify-between">
                                    <span className="text-slate-400">DNS Name</span>
                                    <span className="text-white">{vm.name}.local</span>
                                </div>
                                <div className="flex justify-between">
                                    <span className="text-slate-400">IP Addresses</span>
                                    <span className="text-white">192.168.1.105</span>
                                </div>
                            </div>
                        </div>
                        <div className="glass-panel p-6">
                            <h3 className="text-lg font-medium text-white mb-4 border-b border-slate-700 pb-2">Hardware</h3>
                            <div className="space-y-3">
                                <div className="flex justify-between items-center">
                                    <span className="text-slate-400 flex items-center"><ChipIcon className="w-4 h-4 mr-2" /> CPU</span>
                                    <span className="text-white">{vm.cpuCount} vCPUs</span>
                                </div>
                                <div className="flex justify-between items-center">
                                    <span className="text-slate-400 flex items-center"><div className="w-4 h-4 mr-2 border rounded text-[10px] flex items-center justify-center">M</div> Memory</span>
                                    <span className="text-white">{vm.memoryMB} MB</span>
                                </div>
                                <div className="flex justify-between items-center">
                                    <span className="text-slate-400 flex items-center"><StorageIcon className="w-4 h-4 mr-2" /> Storage</span>
                                    <span className="text-white">60 GB</span>
                                </div>
                            </div>
                        </div>
                    </div>
                )}
                {activeTab === 'Monitor' && (
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                        <div className="glass-panel p-6 flex flex-col items-center">
                            <Gauge value={15} label="CPU Usage" />
                        </div>
                        <div className="glass-panel p-6 flex flex-col items-center">
                            <Gauge value={45} label="Memory Usage" />
                        </div>
                    </div>
                )}
                {activeTab === 'Configure' && (
                    <div className="glass-panel p-6 text-center text-slate-400">
                        Configuration options coming soon...
                    </div>
                )}
            </div>
        </div>
    );
};

export const VirtualMachinesPage = () => {
    const { id } = useParams();
    const navigate = useNavigate();
    const { addNotification } = useOutletContext<OutletContextType>();
    const [vms, setVms] = useState<VirtualMachine[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [busyVm, setBusyVm] = useState<string | null>(null);
    const [newVm, setNewVm] = useState({ Name: '', CpuCount: '2', MemoryMB: '2048', DiskSizeGB: '20' });

    const fetchVms = useCallback(async () => {
        try {
            const vmList = await api.getVms();
            const wmiVms = vmList.WMI || [];
            setVms(wmiVms);

            const vmPropsPromises = wmiVms.map(async (vm) => {
                try {
                    const props = await api.getVmProperties(vm.name);
                    return { ...vm, ...props };
                } catch (err: any) {
                    return vm;
                }
            });

            const vmsWithProps = await Promise.allSettled(vmPropsPromises);
            const finalVms = vmsWithProps.map((result, index) =>
                result.status === 'fulfilled' ? result.value : wmiVms[index]
            );

            setVms(finalVms);
        } catch (err: any) {
            addNotification('error', `Failed to load VMs: ${err.message}`);
        } finally {
            setIsLoading(false);
        }
    }, [addNotification]);

    useEffect(() => {
        if (!id) {
            setIsLoading(true);
            fetchVms();
        }
    }, [fetchVms, id]);

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

    if (id) {
        return <VmDetail vmName={id} />;
    }

    return (
        <div className="p-6 animate-fade-in">
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
                            onOpenSnapshots={() => { }}
                            onClick={() => navigate(`/vms/${vm.name}`)}
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
                            <input type="text" id="vm-name" value={newVm.Name} onChange={e => setNewVm({ ...newVm, Name: e.target.value })} className="mt-1 block w-full bg-slate-800 border border-slate-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" required />
                        </div>
                        <div>
                            <label htmlFor="cpu-count" className="block text-sm font-medium text-gray-300">CPU Count</label>
                            <input type="number" id="cpu-count" min="1" max="16" value={newVm.CpuCount} onChange={e => setNewVm({ ...newVm, CpuCount: e.target.value })} className="mt-1 block w-full bg-slate-800 border border-slate-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" required />
                        </div>
                        <div>
                            <label htmlFor="memory-mb" className="block text-sm font-medium text-gray-300">Memory (MB)</label>
                            <input type="number" id="memory-mb" step="512" min="512" value={newVm.MemoryMB} onChange={e => setNewVm({ ...newVm, MemoryMB: e.target.value })} className="mt-1 block w-full bg-slate-800 border border-slate-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" required />
                        </div>
                        <div>
                            <label htmlFor="disk-size-gb" className="block text-sm font-medium text-gray-300">Disk Size (GB)</label>
                            <input type="number" id="disk-size-gb" step="10" min="10" value={newVm.DiskSizeGB} onChange={e => setNewVm({ ...newVm, DiskSizeGB: e.target.value })} className="mt-1 block w-full bg-slate-800 border border-slate-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" required />
                        </div>
                    </div>
                    <div className="mt-6 flex justify-end space-x-3">
                        <Button type="button" variant="secondary" onClick={() => setIsModalOpen(false)}>Cancel</Button>
                        <Button type="submit">Create</Button>
                    </div>
                </form>
            </Modal>
        </div>
    );
};
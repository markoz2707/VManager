import React, { useState, useEffect, useCallback } from 'react';
import { useOutletContext } from 'react-router-dom';
import { VirtualMachine, VmStatus, VmSnapshot, AppHealthStatus } from '../types';
import * as api from '../services/hypervService';
import { Spinner } from '../components/Spinner';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
// DO add comment above each fix.
// Fix: Removed non-existent icon exports (MigrateIcon, HealthCheckIcon, FileCopyIcon) from the imports.
import { PlayIcon, StopIcon, PlusIcon, TrashIcon, SettingsIcon, RefreshIcon, ActionsIcon, SnapshotIcon } from '../components/Icons';
import { OutletContextType } from '../App';
import { CreateVmWizard } from '../components/CreateVmWizard';
import { VmSettingsModal } from '../components/VmSettingsModal';
import { useSignalR } from '../hooks/useSignalR';
import { useHostContext } from '../hooks/useHostContext';

type VmAction = 'start' | 'stop' | 'pause' | 'resume' | 'shutdown' | 'terminate' | 'save';

const statusColors: Record<string, string> = {
    [VmStatus.RUNNING]: 'text-green-600',
    [VmStatus.STOPPED]: 'text-red-600',
    [VmStatus.PAUSED]: 'text-yellow-600',
    [VmStatus.SAVED]: 'text-blue-600',
    [VmStatus.SAVING]: 'text-blue-500 animate-pulse-fast',
    [VmStatus.STARTING]: 'text-orange-500 animate-pulse',
    [VmStatus.STOPPING]: 'text-orange-500 animate-pulse',
    [VmStatus.PAUSING]: 'text-yellow-500 animate-pulse',
    [VmStatus.RESUMING]: 'text-green-500 animate-pulse',
    [VmStatus.SUSPENDED]: 'text-gray-500',
    [VmStatus.UNKNOWN]: 'text-gray-400',
};

const ActionDropdown: React.FC<{ vm: VirtualMachine, onAction: (name: string, action: VmAction) => void, onOpenModal: (type: 'snapshot' | 'config' | 'migrate' | 'health' | 'filecopy' | 'storage-migrate' | 'console', vm: VirtualMachine) => void, isBusy: boolean, isHyperV: boolean }> =
({ vm, onAction, onOpenModal, isBusy, isHyperV }) => {
    const [isOpen, setIsOpen] = useState(false);

    const handleActionClick = (action: VmAction) => {
        onAction(vm.name, action);
        setIsOpen(false);
    }
    const handleModalClick = (type: 'snapshot' | 'config' | 'migrate' | 'health' | 'filecopy' | 'storage-migrate' | 'console') => {
        onOpenModal(type, vm);
        setIsOpen(false);
    }

    return (
        <div className="relative inline-block text-left">
            <div>
                <Button variant="ghost" size="md" onClick={() => setIsOpen(!isOpen)} disabled={isBusy}><ActionsIcon className="h-5 w-5"/></Button>
            </div>
            {isOpen && (
                 <div className="origin-top-right absolute right-0 mt-2 w-56 rounded-md shadow-lg bg-white ring-1 ring-black ring-opacity-5 z-10">
                    <div className="py-1" role="menu" aria-orientation="vertical">
                        <a href="#" onClick={() => handleModalClick('console')} className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">Open Console</a>
                        <a href="#" onClick={() => handleModalClick('migrate')} className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">Migrate</a>
                        {isHyperV && <a href="#" onClick={() => handleModalClick('storage-migrate')} className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">Migrate Storage</a>}
                        {isHyperV && <a href="#" onClick={() => handleModalClick('health')} className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">App Health</a>}
                        <a href="#" onClick={() => handleModalClick('filecopy')} className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">Copy File to Guest</a>
                        <div className="border-t my-1"></div>
                        <a href="#" onClick={() => handleActionClick('pause')} className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">Pause</a>
                        <a href="#" onClick={() => handleActionClick('resume')} className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">Resume</a>
                        <a href="#" onClick={() => handleActionClick('save')} className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">Save State</a>
                        <div className="border-t my-1"></div>
                        <a href="#" onClick={() => handleActionClick('shutdown')} className="block px-4 py-2 text-sm text-red-600 hover:bg-red-50" role="menuitem">Shutdown Guest</a>
                        <a href="#" onClick={() => handleActionClick('terminate')} className="block px-4 py-2 text-sm text-red-600 hover:bg-red-50" role="menuitem">Terminate</a>
                    </div>
                </div>
            )}
        </div>
    );
};


export const VirtualMachinesPage = () => {
    const { addNotification } = useOutletContext<OutletContextType>();
    const { isHyperV, isKVM, capabilities } = useHostContext();
    const [vms, setVms] = useState<VirtualMachine[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [isWizardOpen, setIsWizardOpen] = useState(false);
    const [busyVm, setBusyVm] = useState<string | null>(null);
    const [selectedVm, setSelectedVm] = useState<VirtualMachine | null>(null);
    const [selectedVmIds, setSelectedVmIds] = useState<Set<string>>(new Set());
    const [isBulkAction, setIsBulkAction] = useState(false);
    
    // Modals states
    const [activeModal, setActiveModal] = useState<'snapshot' | 'config' | 'migrate' | 'health' | 'filecopy' | 'storage-migrate' | 'console' | null>(null);
    
    // Snapshot state
    const [snapshots, setSnapshots] = useState<VmSnapshot[]>([]);
    const [snapshotName, setSnapshotName] = useState('');
    const [isLoadingSnapshots, setIsLoadingSnapshots] = useState(false);

    // Migrate VM state
    const [migrateForm, setMigrateForm] = useState({ destinationHost: '', live: true, storage: false });
    
    // App Health state
    const [appHealth, setAppHealth] = useState<AppHealthStatus | null>(null);
    
    // File Copy state
    const [fileCopyForm, setFileCopyForm] = useState({ sourcePath: '', destPath: '', overwrite: false });

    // Storage Migration state
    const [storageMigrateForm, setStorageMigrateForm] = useState({ destinationPath: '' });

    // Console state
    const [consoleInfo, setConsoleInfo] = useState<{ vmId: string; vmName: string; state: string; rdpHost: string; rdpPort: number; protocol: string } | null>(null);


    const fetchVms = useCallback(async () => {
        setIsLoading(true);
        try {
            const vmList = await api.getVms();
            const wmiVms = vmList.WMI || [];
            
            setVms(wmiVms);

            const vmPropsPromises = wmiVms.map(vm =>
                api.getVmProperties(vm.name).then(props => ({ ...vm, ...props }))
                .catch(err => {
                    console.error(`Failed to get properties for ${vm.name}:`, err);
                    return vm;
                })
            );

            const vmsWithProps = await Promise.all(vmPropsPromises);
            setVms(vmsWithProps);
        } catch (err: any) {
            addNotification('error', `Failed to load VMs: ${err.message}`);
        } finally {
            setIsLoading(false);
        }
    }, [addNotification]);

    useEffect(() => {
        fetchVms();
    }, [fetchVms]);

    // Real-time updates via SignalR
    useSignalR({
        groups: ['vm-events'],
        onVmStateChanged: () => fetchVms(),
    });

    const handleAction = async (name: string, action: VmAction) => {
        setBusyVm(name);
        const actionMap = {
            start: api.startVm, stop: api.stopVm, pause: api.pauseVm, resume: api.resumeVm,
            shutdown: api.shutdownVm, terminate: api.terminateVm, save: api.saveVm,
        };

        try {
            await actionMap[action](name);
            addNotification('success', `VM action '${action}' initiated for ${name}.`);
            setTimeout(fetchVms, 2000); // Refresh after a delay
        } catch (err: any) {
            addNotification('error', `Action failed: ${err.message}`);
        } finally {
            setBusyVm(null);
        }
    };

    const toggleVmSelection = (vmId: string) => {
        setSelectedVmIds(prev => {
            const next = new Set(prev);
            if (next.has(vmId)) next.delete(vmId);
            else next.add(vmId);
            return next;
        });
    };

    const toggleSelectAll = () => {
        if (selectedVmIds.size === vms.length) {
            setSelectedVmIds(new Set());
        } else {
            setSelectedVmIds(new Set(vms.map(vm => vm.id)));
        }
    };

    const handleBulkAction = async (action: VmAction) => {
        const selectedVms = vms.filter(vm => selectedVmIds.has(vm.id));
        if (selectedVms.length === 0) return;

        setIsBulkAction(true);
        const actionMap = {
            start: api.startVm, stop: api.stopVm, pause: api.pauseVm, resume: api.resumeVm,
            shutdown: api.shutdownVm, terminate: api.terminateVm, save: api.saveVm,
        };

        const results = await Promise.allSettled(
            selectedVms.map(vm => actionMap[action](vm.name))
        );

        const succeeded = results.filter(r => r.status === 'fulfilled').length;
        const failed = results.filter(r => r.status === 'rejected').length;

        if (succeeded > 0) addNotification('success', `Bulk ${action}: ${succeeded} VMs succeeded.`);
        if (failed > 0) addNotification('error', `Bulk ${action}: ${failed} VMs failed.`);

        setSelectedVmIds(new Set());
        setIsBulkAction(false);
        setTimeout(fetchVms, 2000);
    };

    const handleCreationComplete = () => {
        setIsWizardOpen(false);
        fetchVms();
    };
    
    const handleOpenModal = async (type: 'snapshot' | 'config' | 'migrate' | 'health' | 'filecopy' | 'storage-migrate' | 'console', vm: VirtualMachine) => {
        setSelectedVm(vm);
        setActiveModal(type);
        if (type === 'snapshot') {
            setIsLoadingSnapshots(true);
            try {
                const snapshotList = await api.getVmSnapshots(vm.name);
                setSnapshots(snapshotList);
            } catch(e) { /* error handled in api */ }
            finally { setIsLoadingSnapshots(false); }
        }
        if (type === 'health') {
            setAppHealth(null);
            try {
                const health = await api.vmService.getVmAppHealth(vm.name);
                setAppHealth(health);
            } catch (err: any) {
                addNotification('error', `Failed to get app health: ${err.message}`);
            }
        }
        if (type === 'console') {
            setConsoleInfo(null);
            try {
                const info = await api.vmService.getVmConsoleInfo(vm.name);
                setConsoleInfo(info);
            } catch (err: any) {
                addNotification('error', `Failed to get console info: ${err.message}`);
            }
        }
    };

    const handleCloseModal = () => {
        setActiveModal(null);
        setSelectedVm(null);
    };

    const handleConfigureVm = async (vmName: string, config: { cpuCount: number; memoryMB: number; newName: string; }) => {
        setBusyVm(vmName);
        try {
            await api.configureVm(vmName, {
                cpuCount: config.cpuCount,
                memoryMB: config.memoryMB
            });

            if (config.newName && config.newName !== vmName) {
                addNotification('info', `VM renaming from "${vmName}" to "${config.newName}" is a visual placeholder. API support is not implemented.`);
            }

            addNotification('success', `VM ${vmName} configuration saved successfully.`);
            handleCloseModal();
            setTimeout(fetchVms, 1000); // Refresh list
        } catch (err: any) {
            addNotification('error', `Failed to configure VM: ${err.message}`);
        } finally {
            setBusyVm(null);
        }
    };

    const handleCreateSnapshot = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!selectedVm || !snapshotName.trim()) return;

        setIsLoadingSnapshots(true);
        try {
            await api.createVmSnapshot(selectedVm.name, snapshotName);
            addNotification('success', `Snapshot '${snapshotName}' created for ${selectedVm.name}.`);
            setSnapshotName('');
            // Refresh snapshots
            const snapshotList = await api.getVmSnapshots(selectedVm.name);
            setSnapshots(snapshotList);
        } catch (err: any) {
            addNotification('error', `Failed to create snapshot: ${err.message}`);
        } finally {
            setIsLoadingSnapshots(false);
        }
    };

    const handleDeleteSnapshot = async (snapshotId: string) => {
        if (!selectedVm) return;
        
        setIsLoadingSnapshots(true);
        try {
            await api.deleteVmSnapshot(selectedVm.name, snapshotId);
            addNotification('success', 'Snapshot deleted.');
            // Refresh
            const snapshotList = await api.getVmSnapshots(selectedVm.name);
            setSnapshots(snapshotList);
        } catch (err: any) {
            addNotification('error', `Failed to delete snapshot: ${err.message}`);
        } finally {
            setIsLoadingSnapshots(false);
        }
    };
    
    const handleMigrate = async (e: React.FormEvent) => {
        e.preventDefault();
        if(!selectedVm) return;
        try {
            await api.vmService.migrateVm(selectedVm.name, migrateForm.destinationHost, migrateForm.live, migrateForm.storage);
            addNotification('success', `VM migration for ${selectedVm.name} initiated.`);
            handleCloseModal();
        } catch (err: any) {
            addNotification('error', `Migration failed: ${err.message}`);
        }
    };

    const handleFileCopy = async (e: React.FormEvent) => {
        e.preventDefault();
        if(!selectedVm) return;
        try {
            await api.vmService.copyFileToGuest(selectedVm.name, fileCopyForm.sourcePath, fileCopyForm.destPath, fileCopyForm.overwrite);
            addNotification('success', `File copy for ${selectedVm.name} initiated.`);
            handleCloseModal();
        } catch (err: any) {
            addNotification('error', `File copy failed: ${err.message}`);
        }
    };

    const handleStorageMigrate = async (e: React.FormEvent) => {
        e.preventDefault();
        if(!selectedVm) return;
        try {
            await api.vmService.migrateVmStorage(selectedVm.name, storageMigrateForm.destinationPath);
            addNotification('success', `Storage migration for ${selectedVm.name} initiated.`);
            handleCloseModal();
        } catch (err: any) {
            addNotification('error', `Storage migration failed: ${err.message}`);
        }
    };

    const handleDownloadRdp = () => {
        if (!selectedVm) return;
        const url = api.vmService.getVmConsoleRdpUrl(selectedVm.name);
        window.open(url, '_blank');
    };

    return (
        <div className="flex flex-col h-full">
            <header className="p-4 bg-panel-bg border-b border-panel-border flex items-center justify-between flex-shrink-0">
                 <div className="flex items-center space-x-3">
                    <h1 className="text-lg font-semibold text-gray-800">Virtual Machines</h1>
                    {selectedVmIds.size > 0 && (
                        <span className="text-sm text-gray-500 bg-gray-200 px-2 py-0.5 rounded-full">
                            {selectedVmIds.size} selected
                        </span>
                    )}
                 </div>
                 <div className="flex items-center space-x-1">
                    {selectedVmIds.size > 0 && (
                        <>
                            <Button variant="toolbar" size="md" onClick={() => handleBulkAction('start')} disabled={isBulkAction} leftIcon={<PlayIcon className="h-4 w-4" />}>Start All</Button>
                            <Button variant="toolbar" size="md" onClick={() => handleBulkAction('stop')} disabled={isBulkAction} leftIcon={<StopIcon className="h-4 w-4" />}>Stop All</Button>
                            <Button variant="toolbar" size="md" onClick={() => handleBulkAction('pause')} disabled={isBulkAction}>Pause All</Button>
                            <div className="w-px h-6 bg-gray-300 mx-1"></div>
                        </>
                    )}
                    <Button variant="toolbar" size="md" onClick={() => setIsWizardOpen(true)} leftIcon={<PlusIcon />}>Create VM</Button>
                    <Button variant="toolbar" size="md" onClick={fetchVms} leftIcon={<RefreshIcon/>}>Refresh</Button>
                 </div>
            </header>
            
            <main className="flex-1 overflow-y-auto">
                {isLoading && vms.length === 0 ? <div className="flex justify-center items-center h-full"><Spinner /></div> : (
                    vms.length > 0 ? (
                        <table className="min-w-full text-sm">
                            <thead className="bg-gray-100 border-b border-panel-border">
                                <tr>
                                    <th className="px-2 py-2 w-8">
                                        <input
                                            type="checkbox"
                                            checked={selectedVmIds.size === vms.length && vms.length > 0}
                                            onChange={toggleSelectAll}
                                            className="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
                                            style={{ colorScheme: 'light' }}
                                        />
                                    </th>
                                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Name</th>
                                    <th className="px-4 py-2 text-left font-semibold text-gray-600">State</th>
                                    <th className="px-4 py-2 text-left font-semibold text-gray-600">CPUs</th>
                                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Memory (MB)</th>
                                    <th className="px-4 py-2 text-right font-semibold text-gray-600">Actions</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-gray-200 align-middle">
                                {vms.map(vm => (
                                    <tr key={vm.id} className={`hover:bg-gray-50 ${selectedVmIds.has(vm.id) ? 'bg-primary-50' : ''}`}>
                                        <td className="px-2 py-2">
                                            <input
                                                type="checkbox"
                                                checked={selectedVmIds.has(vm.id)}
                                                onChange={() => toggleVmSelection(vm.id)}
                                                className="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
                                                style={{ colorScheme: 'light' }}
                                            />
                                        </td>
                                        <td className="px-4 py-2 font-medium text-gray-800">{vm.name}</td>
                                        <td className="px-4 py-2">
                                            <div className="flex items-center">
                                                <div className={`w-2.5 h-2.5 rounded-full mr-2 ${statusColors[vm.status]?.replace('text-', 'bg-')}`}></div>
                                                <span className={`${statusColors[vm.status]}`}>{vm.status}</span>
                                            </div>
                                        </td>
                                        <td className="px-4 py-2 text-gray-600">{vm.cpuCount ?? <Spinner size="sm" className="inline-block"/>}</td>
                                        <td className="px-4 py-2 text-gray-600">{vm.memoryMB ?? <Spinner size="sm" className="inline-block"/>}</td>
                                        <td className="px-4 py-2 text-right">
                                            <div className="flex items-center justify-end space-x-1">
                                                <Button variant="ghost" size="md" title="Start" disabled={busyVm === vm.name || vm.status !== VmStatus.STOPPED} onClick={() => handleAction(vm.name, 'start')}><PlayIcon className="h-5 w-5"/></Button>
                                                <Button variant="ghost" size="md" title="Stop" disabled={busyVm === vm.name || vm.status !== VmStatus.RUNNING} onClick={() => handleAction(vm.name, 'stop')}><StopIcon className="h-5 w-5"/></Button>
                                                <Button variant="ghost" size="md" title="Snapshots" disabled={busyVm === vm.name} onClick={() => handleOpenModal('snapshot', vm)}><SnapshotIcon className="h-5 w-5"/></Button>
                                                <Button variant="ghost" size="md" title="Configure" disabled={busyVm === vm.name || vm.status !== VmStatus.STOPPED} onClick={() => handleOpenModal('config', vm)}><SettingsIcon className="h-5 w-5"/></Button>
                                                <ActionDropdown vm={vm} onAction={handleAction} onOpenModal={handleOpenModal} isBusy={busyVm === vm.name} isHyperV={isHyperV} />
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    ) : (
                         <div className="text-center py-12 text-gray-500">
                            <p>No virtual machines found. Create your first VM to get started.</p>
                        </div>
                    )
                )}
            </main>
            
            {isWizardOpen && <CreateVmWizard isOpen={isWizardOpen} onClose={() => setIsWizardOpen(false)} onComplete={handleCreationComplete} />}

            <VmSettingsModal 
                isOpen={activeModal === 'config'}
                onClose={handleCloseModal}
                vm={selectedVm}
                onSave={handleConfigureVm}
                isSaving={busyVm === selectedVm?.name}
            />

            <Modal isOpen={activeModal === 'snapshot'} onClose={handleCloseModal} title={`Snapshots - ${selectedVm?.name}`}>
                <div className="space-y-4">
                    <form onSubmit={handleCreateSnapshot} className="flex items-end space-x-2">
                        <div className="flex-grow">
                            <label htmlFor="snapshot-name" className="block text-sm font-medium text-gray-700">New Snapshot Name</label>
                            <input
                                id="snapshot-name"
                                type="text"
                                value={snapshotName}
                                onChange={e => setSnapshotName(e.target.value)}
                                className="mt-1 block w-full bg-white border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-primary-500 focus:border-primary-500"
                                placeholder="e.g., Before update"
                                required
                            />
                        </div>
                        <Button type="submit" disabled={isLoadingSnapshots || !snapshotName.trim()}>
                            {isLoadingSnapshots ? <Spinner size="sm" /> : 'Create'}
                        </Button>
                    </form>
                    <div className="border-t border-gray-200 pt-4">
                        <h4 className="text-md font-semibold text-gray-800 mb-2">Existing Snapshots</h4>
                        {isLoadingSnapshots && !snapshots.length ? <Spinner /> : (
                            snapshots.length > 0 ? (
                                <ul className="divide-y divide-gray-200 max-h-60 overflow-y-auto">
                                    {snapshots.map(snap => (
                                        <li key={snap.id} className="py-2 flex justify-between items-center">
                                            <div>
                                                <p className="font-medium text-gray-900">{snap.name}</p>
                                                <p className="text-xs text-gray-500">Created: {new Date(snap.creationTime).toLocaleString()}</p>
                                            </div>
                                            <Button variant="danger" size="sm" onClick={() => handleDeleteSnapshot(snap.id)} disabled={isLoadingSnapshots}>
                                                <TrashIcon className="h-4 w-4" />
                                            </Button>
                                        </li>
                                    ))}
                                </ul>
                            ) : (
                                <p className="text-sm text-gray-500">No snapshots found for this VM.</p>
                            )
                        )}
                    </div>
                </div>
            </Modal>

            <Modal isOpen={activeModal === 'migrate'} onClose={handleCloseModal} title={`Migrate VM - ${selectedVm?.name}`}>
                <form onSubmit={handleMigrate} className="space-y-4">
                    <input className="mt-1 block w-full bg-white border border-gray-300 rounded-md shadow-sm py-2 px-3" value={migrateForm.destinationHost} onChange={e => setMigrateForm({...migrateForm, destinationHost: e.target.value})} placeholder="Destination Host" required />
                    <div className="flex items-center"><input type="checkbox" checked={migrateForm.live} onChange={e => setMigrateForm({...migrateForm, live: e.target.checked})} className="h-4 w-4" /><label className="ml-2">Live Migration</label></div>
                    <div className="flex items-center"><input type="checkbox" checked={migrateForm.storage} onChange={e => setMigrateForm({...migrateForm, storage: e.target.checked})} className="h-4 w-4" /><label className="ml-2">Migrate Storage</label></div>
                    <div className="mt-6 flex justify-end"><Button type="submit">Start Migration</Button></div>
                </form>
            </Modal>
            
            <Modal isOpen={activeModal === 'health'} onClose={handleCloseModal} title={`App Health - ${selectedVm?.name}`}>
                {!appHealth ? <Spinner/> : <div><p><strong>Status:</strong> {appHealth.status}</p><p><strong>App Status:</strong> {appHealth.appStatus}</p></div>}
            </Modal>
            
            <Modal isOpen={activeModal === 'filecopy'} onClose={handleCloseModal} title={`Copy File to Guest - ${selectedVm?.name}`}>
                 <form onSubmit={handleFileCopy} className="space-y-4">
                    <input className="mt-1 block w-full bg-white border border-gray-300 rounded-md shadow-sm py-2 px-3" value={fileCopyForm.sourcePath} onChange={e => setFileCopyForm({...fileCopyForm, sourcePath: e.target.value})} placeholder="Source Path (on host)" required />
                    <input className="mt-1 block w-full bg-white border border-gray-300 rounded-md shadow-sm py-2 px-3" value={fileCopyForm.destPath} onChange={e => setFileCopyForm({...fileCopyForm, destPath: e.target.value})} placeholder="Destination Path (in guest)" required />
                    <div className="flex items-center"><input type="checkbox" checked={fileCopyForm.overwrite} onChange={e => setFileCopyForm({...fileCopyForm, overwrite: e.target.checked})} className="h-4 w-4" /><label className="ml-2">Overwrite if exists</label></div>
                    <div className="mt-6 flex justify-end"><Button type="submit">Copy File</Button></div>
                </form>
            </Modal>

            <Modal isOpen={activeModal === 'storage-migrate'} onClose={handleCloseModal} title={`Migrate Storage - ${selectedVm?.name}`}>
                <form onSubmit={handleStorageMigrate} className="space-y-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-700">Destination Path</label>
                        <input className="mt-1 block w-full bg-white border border-gray-300 rounded-md shadow-sm py-2 px-3" value={storageMigrateForm.destinationPath} onChange={e => setStorageMigrateForm({...storageMigrateForm, destinationPath: e.target.value})} placeholder="e.g. D:\Hyper-V\VMs" required />
                        <p className="mt-1 text-xs text-gray-500">VM virtual hard disks will be moved to this location without downtime.</p>
                    </div>
                    <div className="mt-6 flex justify-end"><Button type="submit">Start Storage Migration</Button></div>
                </form>
            </Modal>

            <Modal isOpen={activeModal === 'console'} onClose={handleCloseModal} title={`Console - ${selectedVm?.name}`}>
                <div className="space-y-4">
                    {!consoleInfo ? <Spinner/> : (
                        <div className="space-y-3">
                            <div className="bg-gray-50 rounded-lg p-4 space-y-2">
                                <p><strong>VM:</strong> {consoleInfo.vmName}</p>
                                <p><strong>State:</strong> {consoleInfo.state}</p>
                                <p><strong>Protocol:</strong> {consoleInfo.protocol}</p>
                                <p><strong>Host:</strong> {consoleInfo.rdpHost}:{consoleInfo.rdpPort}</p>
                            </div>
                            {isHyperV ? (
                                <>
                                    <div className="flex space-x-2">
                                        <Button onClick={handleDownloadRdp}>Download .rdp File</Button>
                                    </div>
                                    <p className="text-xs text-gray-500">Open the downloaded .rdp file with Remote Desktop Connection to access the VM console.</p>
                                </>
                            ) : (
                                <>
                                    <div className="flex space-x-2">
                                        <Button onClick={() => window.open(`vnc://${consoleInfo.rdpHost}:${consoleInfo.rdpPort}`, '_blank')}>
                                            Open {capabilities?.consoleType || 'VNC'} Console
                                        </Button>
                                    </div>
                                    <p className="text-xs text-gray-500">
                                        Connect using a {capabilities?.consoleType || 'VNC'} client to {consoleInfo.rdpHost}:{consoleInfo.rdpPort} to access the VM console.
                                    </p>
                                </>
                            )}
                        </div>
                    )}
                </div>
            </Modal>
        </div>
    );
};
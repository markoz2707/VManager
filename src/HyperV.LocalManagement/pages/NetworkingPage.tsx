import React, { useEffect, useState, useCallback } from 'react';
import { fetchApi } from '../services/baseService';
import { useOutletContext } from 'react-router-dom';
import { VirtualNetwork, VmEnvironment, NetworkType, PhysicalAdapterInfo, VirtualMachine, FibreChannelSan, VlanConfiguration, VlanOperationMode } from '../types';
import * as api from '../services/hypervService';
import * as networkApi from '../services/networkService';
import { Spinner } from '../components/Spinner';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
import { PlusIcon, TrashIcon, RefreshIcon, SettingsIcon, ChipIcon, ConnectIcon } from '../components/Icons';
import { OutletContextType } from '../App';
import { Tabs } from '../components/Tabs';

export const NetworkingPage = () => {
    const { addNotification } = useOutletContext<OutletContextType>();

    // Data states
    const [allNetworks, setAllNetworks] = useState<{ WMI: VirtualNetwork[], HCS: VirtualNetwork[] }>({ WMI: [], HCS: [] });
    const [physicalAdapters, setPhysicalAdapters] = useState<PhysicalAdapterInfo[]>([]);
    const [vms, setVms] = useState<VirtualMachine[]>([]);
    const [sans, setSans] = useState<FibreChannelSan[]>([]);

    // UI states
    const [isLoading, setIsLoading] = useState(true);
    const [activeMainTab, setActiveMainTab] = useState<'Virtual Switches' | 'Fibre Channel'>('Virtual Switches');
    const [activeSubTab, setActiveSubTab] = useState<VmEnvironment>('WMI');

    // Modals
    const [isCreateSwitchOpen, setIsCreateSwitchOpen] = useState(false);
    const [isCreateSanOpen, setIsCreateSanOpen] = useState(false);
    const [isConnectVmOpen, setIsConnectVmOpen] = useState(false);
    const [isVlanConfigOpen, setIsVlanConfigOpen] = useState(false);
    
    // Form data
    const [wmiFormData, setWmiFormData] = useState({ name: '', type: NetworkType.INTERNAL, externalAdapterName: '', allowManagementOS: false });
    const [hcsFormData, setHcsFormData] = useState({ name: '', prefix: '192.168.100.0/24' });
    const [sanForm, setSanForm] = useState({ sanName: '', wwpnArray: '', wwnnArray: '' });
    const [connectVmForm, setConnectVmForm] = useState({ vmName: '', switchName: '' });
    const [vlanForm, setVlanForm] = useState({ vmName: '', vlanId: 1, operationMode: VlanOperationMode.ACCESS, nativeVlanId: 1, trunkVlanIds: '' });
    const [currentVlanConfig, setCurrentVlanConfig] = useState<VlanConfiguration | null>(null);

    const fetchData = useCallback(async () => {
        setIsLoading(true);
        try {
            const [netData, adapters, vmsData, sansData] = await Promise.all([
                api.getNetworks(),
                networkApi.getPhysicalAdapters(),
                api.vmService.getVms(),
                networkApi.getFibreChannelSans()
            ]);
            setAllNetworks(netData);
            setPhysicalAdapters(adapters);
            setVms(vmsData.WMI);
            setSans(sansData);
        } catch (err: any) {
            addNotification('error', `Failed to load network data: ${err.message}`);
        } finally {
            setIsLoading(false);
        }
    }, [addNotification]);

    useEffect(() => {
        fetchData();
    }, [fetchData]);

    const handleCreateSwitch = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            if (activeSubTab === 'WMI') {
                await api.createWmiNetwork({ name: wmiFormData.name, type: wmiFormData.type, externalAdapterName: wmiFormData.externalAdapterName, allowManagementOS: wmiFormData.allowManagementOS });
            } else {
                await api.createHcsNatNetwork(hcsFormData.name, hcsFormData.prefix);
            }
            addNotification('success', 'Virtual switch created.');
            setIsCreateSwitchOpen(false);
            fetchData();
        } catch (err: any) { addNotification('error', `Failed to create switch: ${err.message}`); }
    };

    const handleCreateSan = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await networkApi.createFibreChannelSan(sanForm.sanName, sanForm.wwpnArray.split(','), sanForm.wwnnArray.split(','));
            addNotification('success', 'Fibre Channel SAN created.');
            setIsCreateSanOpen(false);
            fetchData();
        } catch(err: any) { addNotification('error', `Failed to create SAN: ${err.message}`); }
    };

    const handleConnectVm = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await networkApi.connectVmToSwitch(connectVmForm.vmName, connectVmForm.switchName);
            addNotification('success', `Connected ${connectVmForm.vmName} to ${connectVmForm.switchName}.`);
            setIsConnectVmOpen(false);
        } catch(err: any) { addNotification('error', `Failed to connect VM: ${err.message}`); }
    };

    const openConnectVmModal = (switchName: string) => {
        setConnectVmForm({ vmName: vms.length > 0 ? vms[0].name : '', switchName });
        setIsConnectVmOpen(true);
    }

    const openVlanConfigModal = async () => {
        setVlanForm({ vmName: vms.length > 0 ? vms[0].name : '', vlanId: 1, operationMode: VlanOperationMode.ACCESS, nativeVlanId: 1, trunkVlanIds: '' });
        setCurrentVlanConfig(null);
        setIsVlanConfigOpen(true);
    };

    const handleLoadVlan = async () => {
        if (!vlanForm.vmName) return;
        try {
            const config = await networkApi.getVlanConfiguration(vlanForm.vmName);
            setCurrentVlanConfig(config);
            setVlanForm(prev => ({
                ...prev,
                vlanId: config.vlanId || 1,
                operationMode: config.operationMode || VlanOperationMode.ACCESS,
                nativeVlanId: config.nativeVlanId || 1,
                trunkVlanIds: config.trunkVlanIds?.join(',') || ''
            }));
        } catch (err: any) {
            addNotification('error', `Failed to load VLAN config: ${err.message}`);
        }
    };

    const handleSetVlan = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            const trunkVlanIds = vlanForm.operationMode === VlanOperationMode.TRUNK && vlanForm.trunkVlanIds
                ? vlanForm.trunkVlanIds.split(',').map(s => parseInt(s.trim())).filter(n => !isNaN(n))
                : undefined;
            await networkApi.setVlanConfiguration(
                vlanForm.vmName,
                vlanForm.vlanId,
                vlanForm.operationMode,
                vlanForm.operationMode === VlanOperationMode.TRUNK ? vlanForm.nativeVlanId : undefined,
                trunkVlanIds
            );
            addNotification('success', `VLAN ${vlanForm.vlanId} configured on VM '${vlanForm.vmName}'.`);
            setIsVlanConfigOpen(false);
        } catch (err: any) {
            addNotification('error', `Failed to set VLAN: ${err.message}`);
        }
    };

    const handleRemoveVlan = async () => {
        if (!vlanForm.vmName) return;
        try {
            await networkApi.removeVlanConfiguration(vlanForm.vmName);
            addNotification('success', `VLAN removed from VM '${vlanForm.vmName}'.`);
            setIsVlanConfigOpen(false);
        } catch (err: any) {
            addNotification('error', `Failed to remove VLAN: ${err.message}`);
        }
    };
    
    const displayedNetworks = allNetworks[activeSubTab] || [];

    const renderVirtualSwitches = () => (
        <>
            <Tabs tabs={['WMI', 'HCS']} activeTab={activeSubTab} onTabClick={(tab) => setActiveSubTab(tab as VmEnvironment)} />
            <div className="bg-panel-bg border border-panel-border shadow-sm overflow-hidden">
                <table className="min-w-full text-sm">
                     <thead className="bg-gray-100 border-b border-panel-border">
                        <tr>
                            <th className="px-4 py-2 text-left">Name</th>
                            <th className="px-4 py-2 text-left">Type</th>
                            <th className="px-4 py-2 text-left">Subnet</th>
                            <th className="px-4 py-2 text-right">Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {displayedNetworks.map(net => (
                            <tr key={net.id}>
                                <td className="px-4 py-2">{net.name}</td>
                                <td className="px-4 py-2">{net.type}</td>
                                <td className="px-4 py-2">{net.subnet}</td>
                                <td className="px-4 py-2 text-right">
                                    <Button variant="ghost" size="md" onClick={() => openConnectVmModal(net.name)} title="Connect VM"><ConnectIcon className="h-5 w-5"/></Button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </>
    );

    const renderFibreChannel = () => (
        <div className="bg-panel-bg border border-panel-border shadow-sm overflow-hidden">
            <table className="min-w-full text-sm">
                <thead className="bg-gray-100 border-b border-panel-border">
                    <tr><th className="px-4 py-2 text-left">SAN Name</th><th className="px-4 py-2 text-left">Pool ID</th><th className="px-4 py-2 text-left">WWPNs</th></tr>
                </thead>
                <tbody>
                    {sans.map(san => <tr key={san.poolId}><td className="px-4 py-2">{san.name}</td><td className="px-4 py-2">{san.poolId}</td><td className="px-4 py-2">{san.wwpnArray.join(', ')}</td></tr>)}
                </tbody>
            </table>
        </div>
    );

    return (
        <div className="flex flex-col h-full">
            <header className="p-4 bg-panel-bg border-b border-panel-border flex items-center justify-between">
                <h1 className="text-lg font-semibold">Networking</h1>
                <div>
                    {activeMainTab === 'Virtual Switches' && <Button onClick={() => setIsCreateSwitchOpen(true)} leftIcon={<PlusIcon/>}>Create Switch</Button>}
                    {activeMainTab === 'Virtual Switches' && <Button variant="toolbar" onClick={openVlanConfigModal} leftIcon={<SettingsIcon/>}>VLAN Config</Button>}
                    {activeMainTab === 'Fibre Channel' && <Button onClick={() => setIsCreateSanOpen(true)} leftIcon={<PlusIcon/>}>Create SAN</Button>}
                    <Button variant="toolbar" onClick={fetchData} leftIcon={<RefreshIcon/>}>Refresh</Button>
                </div>
            </header>
            
            <main className="flex-1 overflow-y-auto p-4">
                {/* Fix: Corrected the onTabClick handler to satisfy TypeScript's type checking by casting the tab string to the expected union type. */}
                <Tabs tabs={['Virtual Switches', 'Fibre Channel']} activeTab={activeMainTab} onTabClick={(tab) => setActiveMainTab(tab as 'Virtual Switches' | 'Fibre Channel')} />
                {isLoading ? <Spinner/> : (activeMainTab === 'Virtual Switches' ? renderVirtualSwitches() : renderFibreChannel())}
            </main>
            
            <Modal isOpen={isConnectVmOpen} onClose={() => setIsConnectVmOpen(false)} title={`Connect VM to ${connectVmForm.switchName}`}>
                <form onSubmit={handleConnectVm} className="space-y-4">
                    <select className="mt-1 block w-full" value={connectVmForm.vmName} onChange={e => setConnectVmForm({...connectVmForm, vmName: e.target.value})}>
                        {vms.map(vm => <option key={vm.id} value={vm.name}>{vm.name}</option>)}
                    </select>
                    <div className="mt-6 flex justify-end"><Button type="submit">Connect</Button></div>
                </form>
            </Modal>
            <Modal isOpen={isCreateSwitchOpen} onClose={() => setIsCreateSwitchOpen(false)} title={activeSubTab === 'WMI' ? 'Create Virtual Switch (WMI)' : 'Create NAT Network (HCS)'}>
                <form onSubmit={handleCreateSwitch} className="space-y-4">
                    {activeSubTab === 'WMI' ? (
                        <>
                            <div>
                                <label className="block text-sm font-medium text-gray-700">Name</label>
                                <input type="text" required className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={wmiFormData.name} onChange={e => setWmiFormData({...wmiFormData, name: e.target.value})} placeholder="e.g. vSwitch-Internal" />
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-gray-700">Type</label>
                                <select className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={wmiFormData.type} onChange={e => setWmiFormData({...wmiFormData, type: e.target.value as NetworkType})}>
                                    <option value={NetworkType.INTERNAL}>Internal</option>
                                    <option value={NetworkType.EXTERNAL}>External</option>
                                    <option value={NetworkType.PRIVATE}>Private</option>
                                </select>
                            </div>
                            {wmiFormData.type === NetworkType.EXTERNAL && (
                                <>
                                    <div>
                                        <label className="block text-sm font-medium text-gray-700">Physical Adapter</label>
                                        <select className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={wmiFormData.externalAdapterName} onChange={e => setWmiFormData({...wmiFormData, externalAdapterName: e.target.value})}>
                                            <option value="">-- Select Adapter --</option>
                                            {physicalAdapters.map(a => <option key={a.guid} value={a.name}>{a.name}</option>)}
                                        </select>
                                    </div>
                                    <div className="flex items-center">
                                        <input type="checkbox" id="allowMgmt" className="mr-2" checked={wmiFormData.allowManagementOS} onChange={e => setWmiFormData({...wmiFormData, allowManagementOS: e.target.checked})} />
                                        <label htmlFor="allowMgmt" className="text-sm text-gray-700">Allow management OS to share this network adapter</label>
                                    </div>
                                </>
                            )}
                        </>
                    ) : (
                        <>
                            <div>
                                <label className="block text-sm font-medium text-gray-700">Network Name</label>
                                <input type="text" required className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={hcsFormData.name} onChange={e => setHcsFormData({...hcsFormData, name: e.target.value})} placeholder="e.g. NatNetwork1" />
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-gray-700">Subnet Prefix</label>
                                <input type="text" required className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={hcsFormData.prefix} onChange={e => setHcsFormData({...hcsFormData, prefix: e.target.value})} placeholder="192.168.100.0/24" />
                            </div>
                        </>
                    )}
                    <div className="mt-6 flex justify-end space-x-2">
                        <Button variant="ghost" onClick={() => setIsCreateSwitchOpen(false)}>Cancel</Button>
                        <Button type="submit">Create</Button>
                    </div>
                </form>
            </Modal>

            <Modal isOpen={isCreateSanOpen} onClose={() => setIsCreateSanOpen(false)} title="Create Fibre Channel SAN">
                <form onSubmit={handleCreateSan} className="space-y-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-700">SAN Name</label>
                        <input type="text" required className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={sanForm.sanName} onChange={e => setSanForm({...sanForm, sanName: e.target.value})} placeholder="e.g. SAN-Fabric-A" />
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-700">WWPNs (comma-separated)</label>
                        <input type="text" required className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={sanForm.wwpnArray} onChange={e => setSanForm({...sanForm, wwpnArray: e.target.value})} placeholder="10:00:00:00:00:00:00:01,10:00:00:00:00:00:00:02" />
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-700">WWNNs (comma-separated)</label>
                        <input type="text" required className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={sanForm.wwnnArray} onChange={e => setSanForm({...sanForm, wwnnArray: e.target.value})} placeholder="20:00:00:00:00:00:00:01,20:00:00:00:00:00:00:02" />
                    </div>
                    <div className="mt-6 flex justify-end space-x-2">
                        <Button variant="ghost" onClick={() => setIsCreateSanOpen(false)}>Cancel</Button>
                        <Button type="submit">Create SAN</Button>
                    </div>
                </form>
            </Modal>

            <Modal isOpen={isVlanConfigOpen} onClose={() => setIsVlanConfigOpen(false)} title="VLAN Configuration">
                <form onSubmit={handleSetVlan} className="space-y-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-700">Virtual Machine</label>
                        <div className="flex space-x-2">
                            <select className="mt-1 flex-1 border border-gray-300 rounded px-3 py-2 text-sm" value={vlanForm.vmName} onChange={e => setVlanForm({...vlanForm, vmName: e.target.value})}>
                                {vms.map(vm => <option key={vm.id} value={vm.name}>{vm.name}</option>)}
                            </select>
                            <Button variant="toolbar" type="button" onClick={handleLoadVlan}>Load</Button>
                        </div>
                    </div>
                    {currentVlanConfig && (
                        <div className="bg-gray-50 rounded p-3 text-sm">
                            <p><strong>Current:</strong> VLAN {currentVlanConfig.vlanId} ({currentVlanConfig.operationModeName})</p>
                            {currentVlanConfig.nativeVlanId && <p><strong>Native VLAN:</strong> {currentVlanConfig.nativeVlanId}</p>}
                            {currentVlanConfig.trunkVlanIds && currentVlanConfig.trunkVlanIds.length > 0 && <p><strong>Trunk VLANs:</strong> {currentVlanConfig.trunkVlanIds.join(', ')}</p>}
                        </div>
                    )}
                    <div>
                        <label className="block text-sm font-medium text-gray-700">VLAN ID</label>
                        <input type="number" min={1} max={4094} required className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={vlanForm.vlanId} onChange={e => setVlanForm({...vlanForm, vlanId: parseInt(e.target.value) || 1})} />
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-700">Mode</label>
                        <select className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={vlanForm.operationMode} onChange={e => setVlanForm({...vlanForm, operationMode: parseInt(e.target.value) as VlanOperationMode})}>
                            <option value={VlanOperationMode.ACCESS}>Access</option>
                            <option value={VlanOperationMode.TRUNK}>Trunk</option>
                            <option value={VlanOperationMode.PRIVATE}>Private</option>
                        </select>
                    </div>
                    {vlanForm.operationMode === VlanOperationMode.TRUNK && (
                        <>
                            <div>
                                <label className="block text-sm font-medium text-gray-700">Native VLAN ID</label>
                                <input type="number" min={1} max={4094} className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={vlanForm.nativeVlanId} onChange={e => setVlanForm({...vlanForm, nativeVlanId: parseInt(e.target.value) || 1})} />
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-gray-700">Trunk VLAN IDs (comma-separated)</label>
                                <input type="text" className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm" value={vlanForm.trunkVlanIds} onChange={e => setVlanForm({...vlanForm, trunkVlanIds: e.target.value})} placeholder="10,20,30" />
                            </div>
                        </>
                    )}
                    <div className="mt-6 flex justify-between">
                        <Button variant="danger" type="button" onClick={handleRemoveVlan}>Remove VLAN</Button>
                        <div className="flex space-x-2">
                            <Button variant="ghost" type="button" onClick={() => setIsVlanConfigOpen(false)}>Cancel</Button>
                            <Button type="submit">Apply VLAN</Button>
                        </div>
                    </div>
                </form>
            </Modal>
        </div>
    );
};
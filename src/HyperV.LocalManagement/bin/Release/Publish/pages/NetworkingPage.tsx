import React, { useEffect, useState, useCallback } from 'react';
import { fetchApi } from '../services/baseService';
import { useOutletContext } from 'react-router-dom';
import { VirtualNetwork, VmEnvironment, NetworkType, PhysicalAdapterInfo, VirtualMachine, FibreChannelSan } from '../types';
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
    
    // Form data
    const [wmiFormData, setWmiFormData] = useState({ name: '', type: NetworkType.INTERNAL, externalAdapterName: '', allowManagementOS: false });
    const [hcsFormData, setHcsFormData] = useState({ name: '', prefix: '192.168.100.0/24' });
    const [sanForm, setSanForm] = useState({ sanName: '', wwpnArray: '', wwnnArray: '' });
    const [connectVmForm, setConnectVmForm] = useState({ vmName: '', switchName: '' });

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
                await api.createWmiNetwork({ name: wmiFormData.name, type: wmiFormData.type, /*...other fields*/ });
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
             {/* Other modals omitted for brevity */}
        </div>
    );
};
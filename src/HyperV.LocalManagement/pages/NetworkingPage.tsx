import React, { useEffect, useState, useCallback } from 'react';
import { fetchApi } from '../services/baseService';
import { fetchApi } from '../services/hypervService';
import { fetchApi } from '../services/hypervService';
import { useOutletContext } from 'react-router-dom';
import { VirtualNetwork, VmEnvironment, NetworkType, PhysicalAdapterInfo } from '../types';
import * as api from '../services/hypervService';
import * as networkApi from '../services/networkService';
import { Spinner } from '../components/Spinner';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
import { PlusIcon, TrashIcon } from '../components/Icons';
import { OutletContextType } from '../App';
import { Tabs } from '../components/Tabs';

export const NetworkingPage = () => {
    const [allNetworks, setAllNetworks] = useState<{ WMI: VirtualNetwork[], HCS: VirtualNetwork[] }>({ WMI: [], HCS: [] });
    const [extensions, setExtensions] = useState<any[]>([]);
    const [selectedSwitch, setSelectedSwitch] = useState<string>('');
    const [isExtensionModalOpen, setIsExtensionModalOpen] = useState(false);

    const loadExtensions = useCallback(async (networkId: string) => {
        try {
            const response = await fetchApi(`/networks/${networkId}/extensions`);
            setExtensions(response);
        } catch (err: any) {
            addNotification('error', `Failed to load extensions for network ${networkId}: ${err.message}`);
        }
    }, [addNotification]);

    const handleToggleExtension = async (networkId: string, extensionName: string, enabled: boolean) => {
        try {
            await fetchApi(`/networks/${networkId}/extensions/${encodeURIComponent(extensionName)}/enable?enabled=${enabled}`, {
                method: 'PUT'
            });
            addNotification('success', `Extension ${enabled ? 'enabled' : 'disabled'} successfully`);
            loadExtensions(networkId);
        } catch (err: any) {
            addNotification('error', `Failed to toggle extension: ${err.message}`);
        }
    };
    const [allNetworks, setAllNetworks] = useState<{ WMI: VirtualNetwork[], HCS: VirtualNetwork[] }>({ WMI: [], HCS: [] });
    const [extensions, setExtensions] = useState<any[]>([]);
    const [selectedSwitch, setSelectedSwitch] = useState<string>('');
    const [isExtensionModalOpen, setIsExtensionModalOpen] = useState(false);
    const [activeTab, setActiveTab] = useState<VmEnvironment>('WMI');
    const [isLoading, setIsLoading] = useState(true);
    const { addNotification } = useOutletContext<OutletContextType>();
    const [isModalOpen, setIsModalOpen] = useState(false);

    const [extensions, setExtensions] = useState<any[]>([]);
    const [selectedSwitch, setSelectedSwitch] = useState<string>('');
    const [isExtensionModalOpen, setIsExtensionModalOpen] = useState(false);

    const [wmiFormData, setWmiFormData] = useState({ name: '', type: NetworkType.INTERNAL, externalAdapterName: '', allowManagementOS: false });
    const [hcsFormData, setHcsFormData] = useState({ name: '', prefix: '192.168.100.0/24' });
    const [physicalAdapters, setPhysicalAdapters] = useState<PhysicalAdapterInfo[]>([]);

    // Edit modal state
    const [isEditOpen, setIsEditOpen] = useState(false);
    const [editFormData, setEditFormData] = useState<{ id: string; name: string; notes: string }>({ id: '', name: '', notes: '' });

    const fetchNetworks = useCallback(async () => {
        setIsLoading(true);
        try {
            const data = await api.getNetworks();
            // Load extensions for WMI networks
            const networksWithExtensions = { ...data };
            for (const net of networksWithExtensions.WMI) {
                try {
                    const extResponse = await fetchApi(`/networks/${net.id}/extensions`);
                    net.extensions = extResponse;
                    net.supportsTrunk = await fetchApi(`/networks/${net.id}/trunk`);
                } catch (err) {
                    net.extensions = [];
                    net.supportsTrunk = false;
                }
            }
            setAllNetworks(networksWithExtensions);
            // Fetch physical adapters for WMI External switch creation
            const adapters = await networkApi.getPhysicalAdapters();
            setPhysicalAdapters(adapters);
        } catch (err: any) {
            addNotification('error', `Failed to load networks: ${err.message}`);
        } finally {
            setIsLoading(false);
        }
    }, [addNotification]);

    const loadExtensions = useCallback(async (networkId: string) => {
        try {
            const response = await fetchApi(`/networks/${encodeURIComponent(networkId)}/extensions`);
            setExtensions(response || []);
        } catch (err: any) {
            addNotification('error', `Failed to load extensions for network ${networkId}: ${err.message}`);
            setExtensions([]);
        }
    }, [addNotification]);

    const handleToggleExtension = async (networkId: string, extensionName: string, currentEnabled: boolean) => {
        try {
            await fetchApi(`/networks/${encodeURIComponent(networkId)}/extensions/${encodeURIComponent(extensionName)}/enable?enabled=${!currentEnabled}`, {
                method: 'PUT'
            });
            addNotification('success', `Extension ${!currentEnabled ? 'enabled' : 'disabled'} successfully`);
            // Reload extensions
            loadExtensions(networkId);
        } catch (err: any) {
            addNotification('error', `Failed to toggle extension: ${err.message}`);
        }
    };

    const loadExtensions = useCallback(async (networkId: string) => {
        try {
            const response = await fetchApi(`/networks/${networkId}/extensions`);
            setExtensions(response);
        } catch (err: any) {
            addNotification('error', `Failed to load extensions for network ${networkId}: ${err.message}`);
        }
    }, [addNotification]);

    const handleToggleExtension = async (networkId: string, extensionName: string, enabled: boolean) => {
        try {
            await fetchApi(`/networks/${networkId}/extensions/${encodeURIComponent(extensionName)}/enable?enabled=${enabled}`, {
                method: 'PUT'
            });
            addNotification('success', `Extension ${enabled ? 'enabled' : 'disabled'} successfully`);
            // Reload extensions or update local state
            loadExtensions(networkId);
        } catch (err: any) {
            addNotification('error', `Failed to toggle extension: ${err.message}`);
        }
    };

    useEffect(() => {
        fetchNetworks();
    }, [fetchNetworks]);

    const handleOpenCreateModal = () => {
        setWmiFormData({ name: '', type: NetworkType.INTERNAL, externalAdapterName: '', allowManagementOS: false });
        setHcsFormData({ name: '', prefix: '192.168.100.0/24' });
        setIsModalOpen(true);
    };
    
    const handleCloseModal = () => setIsModalOpen(false);
    
    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            if (activeTab === 'WMI') {
                const wmiPayload = {
                    name: wmiFormData.name,
                    type: wmiFormData.type,
                    notes: '', // Optional
                    externalAdapterName: wmiFormData.type === NetworkType.EXTERNAL ? wmiFormData.externalAdapterName : undefined,
                    allowManagementOS: wmiFormData.type === NetworkType.EXTERNAL ? wmiFormData.allowManagementOS : false,
                };
                await api.createWmiNetwork(wmiPayload);
                addNotification('success', `WMI Network '${wmiFormData.name}' created successfully.`);
            } else {
                await api.createHcsNatNetwork(hcsFormData.name, hcsFormData.prefix);
                addNotification('success', `HCS NAT Network '${hcsFormData.name}' created successfully.`);
            }
            handleCloseModal();
            fetchNetworks();
        } catch (err: any) {
            addNotification('error', `Failed to create network: ${err.message}`);
        }
    };

    const handleDelete = async (network: VirtualNetwork) => {
        if(window.confirm(`Are you sure you want to delete the ${network.environment} network "${network.name}"?`)){
            try {
                await api.deleteNetwork(network.id, network.environment);
                addNotification('success', `Network '${network.name}' deleted successfully.`);
                fetchNetworks();
            } catch(err: any) {
                 addNotification('error', `Failed to delete network: ${err.message}`);
            }
        }
    }

    const handleOpenEdit = (net: VirtualNetwork) => {
        if (net.environment === 'WMI') {
            setEditFormData({ id: net.id, name: net.name, notes: net.notes ?? '' });
            setIsEditOpen(true);
        } else {
            addNotification('info', 'Editing HCS networks is not supported.');
        }
    };

    const handleCloseEdit = () => setIsEditOpen(false);

    const handleSubmitEdit = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await api.updateWmiNetwork(editFormData.id, { name: editFormData.name, notes: editFormData.notes });
            addNotification('success', `WMI Network '${editFormData.name}' updated successfully.`);
            handleCloseEdit();
            fetchNetworks();
        } catch (err: any) {
            addNotification('error', `Failed to update network: ${err.message}`);
        }
    };

    const displayedNetworks = allNetworks[activeTab] || [];

    useEffect(() => {
        if (selectedSwitch) {
            loadExtensions(selectedSwitch);
        }
    }, [selectedSwitch]);

    useEffect(() => {
        if (selectedSwitch) {
            loadExtensions(selectedSwitch);
        }
    }, [selectedSwitch]);

    const renderWmiModal = () => (
        <>
            <div>
                <label htmlFor="wmi-net-name" className="block text-sm font-medium text-gray-300">Switch Name</label>
                <input type="text" id="wmi-net-name" value={wmiFormData.name} onChange={e => setWmiFormData({...wmiFormData, name: e.target.value})} className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500" required />
            </div>
            <div>
                <label htmlFor="wmi-net-type" className="block text-sm font-medium text-gray-300">Switch Type</label>
                <select id="wmi-net-type" value={wmiFormData.type} onChange={e => setWmiFormData({...wmiFormData, type: e.target.value as NetworkType})} className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500">
                    <option value={NetworkType.INTERNAL}>Internal</option>
                    <option value={NetworkType.PRIVATE}>Private</option>
                    <option value={NetworkType.EXTERNAL}>External</option>
                    <option value={NetworkType.EXTERNAL_ONLY}>External Only</option>
                </select>
            </div>
            {wmiFormData.type === NetworkType.EXTERNAL && (
                <>
                    <div>
                        <label htmlFor="external-adapter" className="block text-sm font-medium text-gray-300">Physical Adapter</label>
                        <select id="external-adapter" value={wmiFormData.externalAdapterName} onChange={e => setWmiFormData({...wmiFormData, externalAdapterName: e.target.value})} className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500">
                            <option value="">Select adapter...</option>
                            {physicalAdapters.map(adapter => (
                                <option key={adapter.guid || adapter.pnpDeviceId} value={adapter.name}>
                                    {adapter.name} ({adapter.guid || adapter.pnpDeviceId})
                                </option>
                            ))}
                        </select>
                    </div>
                    <div className="flex items-center">
                        <input id="allow-management-os" type="checkbox" checked={wmiFormData.allowManagementOS} onChange={e => setWmiFormData({...wmiFormData, allowManagementOS: e.target.checked})} className="rounded border-gray-300 text-primary-600 focus:ring-primary-500" />
                        <label htmlFor="allow-management-os" className="ml-2 block text-sm text-gray-300">Allow management OS access (adds internal port)</label>
                    </div>
                </>
            )}
            <div className="text-sm text-gray-400">
                <p><strong>Internal:</strong> Virtual machines can communicate with each other and the host</p>
                <p><strong>Private:</strong> Virtual machines can only communicate with each other</p>
                <p><strong>External:</strong> Virtual machines can communicate with physical network through selected adapter</p>
                <p><strong>External Only:</strong> Virtual machines can communicate with physical network, but host cannot access VMs</p>
            </div>
        </>
    );

    const renderHcsModal = () => (
         <>
            <div>
                <label htmlFor="hcs-net-name" className="block text-sm font-medium text-gray-300">Name</label>
                <input type="text" id="hcs-net-name" value={hcsFormData.name} onChange={e => setHcsFormData({...hcsFormData, name: e.target.value})} className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500" required />
            </div>
            <div>
                <label htmlFor="hcs-net-prefix" className="block text-sm font-medium text-gray-300">Subnet Prefix</label>
                <input type="text" id="hcs-net-prefix" value={hcsFormData.prefix} onChange={e => setHcsFormData({...hcsFormData, prefix: e.target.value})} className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500" required />
            </div>
        </>
    );

    return (
        <div className="p-6">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-3xl font-bold text-white">Virtual Networks</h1>
                <Button onClick={handleOpenCreateModal} leftIcon={<PlusIcon />}>Create {activeTab} Network</Button>
            </div>
            
            {/* FIX: The onTabClick prop expects a function of type (tab: string) => void, but setActiveTab expects a VmEnvironment. Create a new function that casts the tab string to VmEnvironment before calling setActiveTab. */}
            <Tabs tabs={['WMI', 'HCS']} activeTab={activeTab} onTabClick={(tab) => setActiveTab(tab as VmEnvironment)} />
            
            {isLoading ? <div className="flex justify-center items-center h-full"><Spinner /></div> : (
                <div className="bg-gray-800 rounded-lg shadow-md overflow-hidden">
                    <table className="min-w-full">
                        <thead className="bg-gray-700">
                            <tr>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Name</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Type</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Subnet</th>
                                <th className="px-6 py-3 text-right text-xs font-medium text-gray-300 uppercase tracking-wider">Actions</th>
                            </tr>
                        </thead>
                        {displayedNetworks.length > 0 ? (
                           <tbody className="bg-gray-800 divide-y divide-gray-700">
                                {displayedNetworks.map(net => (
                                    <tr key={net.id} className="hover:bg-gray-700/50">
                                        <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-white">{net.name}</td>
                                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-300">{net.type}</td>
                                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-300">{net.subnet}</td>
                                        <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium space-x-2">
                                            <Button variant="secondary" size="sm" onClick={() => handleOpenEdit(net)}>Edit</Button>
                                            <Button variant="secondary" size="sm" onClick={() => handleDelete(net)}>
                                                <TrashIcon className="h-4 w-4" />
                                            </Button>
                                            <Button variant="secondary" size="sm" onClick={() => setSelectedSwitch(net.id)}>Extensions</Button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        ) : (
                           <tbody>
                                <tr>
                                    <td colSpan={4} className="text-center py-12 text-gray-500">
                                        No networks found in the {activeTab} environment.
                                    </td>
                                </tr>
                           </tbody>
                        )}
                    </table>
                </div>
            )}
             <Modal isOpen={isModalOpen} onClose={handleCloseModal} title={`Create ${activeTab} Network`}>
                 <form onSubmit={handleSubmit} className="space-y-4">
                     {activeTab === 'WMI' ? renderWmiModal() : renderHcsModal()}
                     <div className="mt-6 flex justify-end space-x-3 pt-4">
                         <Button type="button" variant="secondary" onClick={handleCloseModal}>Cancel</Button>
                         <Button type="submit">Create</Button>
                     </div>
                 </form>
             </Modal>

             {/* Extensions Modal */}
             <Modal isOpen={isExtensionModalOpen} onClose={() => setIsExtensionModalOpen(false)} title={`Manage Extensions for ${selectedSwitch}`}>
                 <div className="space-y-4">
                     <label className="block text-sm font-medium text-gray-300">Select Switch</label>
                     <select
                         value={selectedSwitch}
                         onChange={(e) => setSelectedSwitch(e.target.value)}
                         className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white"
                         aria-label="Select switch for extension management"
                     >
                         <option value="">Select a switch...</option>
                         {displayedNetworks.map(net => (
                             <option key={net.id} value={net.id}>{net.name} ({net.type})</option>
                         ))}
                     </select>
                     
                     {extensions.length > 0 && (
                         <div className="space-y-2">
                             <label className="block text-sm font-medium text-gray-300">Extensions</label>
                             {extensions.map((ext: any) => (
                                 <div key={ext.name} className="flex items-center justify-between p-2 bg-gray-700 rounded">
                                     <span className="text-sm text-white">{ext.name} - {ext.type} ({ext.vendor})</span>
                                     <button
                                         onClick={() => handleToggleExtension(selectedSwitch, ext.name, !ext.enabled)}
                                         className={`px-3 py-1 rounded text-sm ${ext.enabled ? 'bg-red-600 hover:bg-red-700' : 'bg-green-600 hover:bg-green-700'} text-white`}
                                     >
                                         {ext.enabled ? 'Disable' : 'Enable'}
                                     </button>
                                 </div>
                             ))}
                         </div>
                     )}
                     
                     {selectedSwitch && (
                         <div className={`mt-4 p-3 rounded ${displayedNetworks.find(n => n.id === selectedSwitch)?.supportsTrunk ? 'bg-green-700 text-white' : 'bg-yellow-700 text-black'}`}>
                             <label className="block text-sm font-medium mb-2">Trunk Mode</label>
                             <span className="inline-block px-2 py-1 rounded text-sm">
                                 {displayedNetworks.find(n => n.id === selectedSwitch)?.supportsTrunk ? 'Supported' : 'Not Supported'}
                             </span>
                         </div>
                     )}
                 </div>
             </Modal>

             {/* Extensions Modal */}
             <Modal isOpen={isExtensionModalOpen} onClose={() => setIsExtensionModalOpen(false)} title={`Manage Extensions for ${selectedSwitch}`}>
                 <div className="space-y-4">
                     <label className="block text-sm font-medium text-gray-300">Select Switch</label>
                     <select value={selectedSwitch} onChange={(e) => setSelectedSwitch(e.target.value)} className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white">
                         <option value="">Select a switch...</option>
                         {displayedNetworks.map(net => (
                             <option key={net.id} value={net.id}>{net.name} ({net.type})</option>
                         ))}
                     </select>
                     
                     {extensions.length > 0 && (
                         <div className="space-y-2">
                             <label className="block text-sm font-medium text-gray-300">Extensions</label>
                             {extensions.map((ext: any) => (
                                 <div key={ext.name} className="flex items-center justify-between p-2 bg-gray-700 rounded">
                                     <span className="text-sm text-white">{ext.name} - {ext.type} ({ext.vendor})</span>
                                     <button
                                         onClick={() => handleToggleExtension(selectedSwitch, ext.name, !ext.enabled)}
                                         className={`px-3 py-1 rounded text-sm ${ext.enabled ? 'bg-red-600 hover:bg-red-700' : 'bg-green-600 hover:bg-green-700'} text-white`}
                                     >
                                         {ext.enabled ? 'Disable' : 'Enable'}
                                     </button>
                                 </div>
                             ))}
                         </div>
                     )}
                     
                     <div className={`mt-4 p-3 rounded ${displayedNetworks.find(n => n.id === selectedSwitch)?.supportsTrunk ? 'bg-green-700 text-white' : 'bg-yellow-700 text-black'}`}>
                         <label className="block text-sm font-medium mb-2">Trunk Mode</label>
                         <span className="inline-block px-2 py-1 rounded text-sm">
                             {displayedNetworks.find(n => n.id === selectedSwitch)?.supportsTrunk ? 'Supported' : 'Not Supported'}
                         </span>
                     </div>
                 </div>
             </Modal>

            {/* Edit modal for WMI networks */}
            <Modal isOpen={isEditOpen} onClose={handleCloseEdit} title="Edit WMI Network">
                <form onSubmit={handleSubmitEdit} className="space-y-4">
                    <div>
                        <label htmlFor="edit-net-name" className="block text-sm font-medium text-gray-300">Name</label>
                        <input
                            type="text"
                            id="edit-net-name"
                            value={editFormData.name}
                            onChange={e => setEditFormData({ ...editFormData, name: e.target.value })}
                            className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500"
                            required
                        />
                    </div>
                    <div>
                        <label htmlFor="edit-net-notes" className="block text-sm font-medium text-gray-300">Notes</label>
                        <textarea
                            id="edit-net-notes"
                            value={editFormData.notes}
                            onChange={e => setEditFormData({ ...editFormData, notes: e.target.value })}
                            className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500"
                            rows={3}
                        />
                    </div>
                    <div className="mt-6 flex justify-end space-x-3 pt-4">
                        <Button type="button" variant="secondary" onClick={handleCloseEdit}>Cancel</Button>
                        <Button type="submit">Save</Button>
                    </div>
                </form>
            </Modal>

            {/* Extensions Management Section */}
            <div className="mt-8">
                <div className="flex justify-between items-center mb-4">
                    <h2 className="text-xl font-semibold text-white">Switch Extensions</h2>
                </div>
                
                {displayedNetworks.map(network => (
                    <div key={network.id} className="bg-gray-800 rounded-lg p-4 mb-4">
                        <h3 className="text-lg font-medium text-white mb-2">{network.name} ({network.type})</h3>
                        
                        <div className="mt-4">
                            <label className="block text-sm font-medium text-gray-300 mb-2">Extensions</label>
                            <div className="space-y-2">
                                {network.extensions?.map((ext: any) => (
                                    <div key={ext.name} className="flex items-center justify-between p-2 bg-gray-700 rounded">
                                        <span className="text-sm text-white">{ext.name} - {ext.type}</span>
                                        <button
                                            onClick={() => handleToggleExtension(network.id, ext.name, !ext.enabled)}
                                            className={`px-3 py-1 rounded text-sm ${ext.enabled ? 'bg-red-600 hover:bg-red-700' : 'bg-green-600 hover:bg-green-700'} text-white`}
                                        >
                                            {ext.enabled ? 'Disable' : 'Enable'}
                                        </button>
                                    </div>
                                ))}
                            </div>
                        </div>

                        <div className="mt-4 p-3 bg-gray-700 rounded">
                            <label className="block text-sm font-medium text-gray-300 mb-2">Trunk Mode Support</label>
                            <span className={`inline-block px-2 py-1 rounded text-sm ${network.supportsTrunk ? 'bg-green-500 text-white' : 'bg-yellow-500 text-black'}`}>
                                {network.supportsTrunk ? 'Supported' : 'Not Supported'}
                            </span>
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
};
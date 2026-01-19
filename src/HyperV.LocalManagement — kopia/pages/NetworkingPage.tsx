import React, { useEffect, useState, useCallback } from 'react';
import { useOutletContext } from 'react-router-dom';
import { VirtualNetwork, VmEnvironment, NetworkType, PhysicalAdapterInfo } from '../types';
import * as api from '../services/hypervService';
import * as networkApi from '../services/networkService';
import { fetchApi } from '../services/baseService';
import { Spinner } from '../components/Spinner';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
import { PlusIcon, TrashIcon, NetworkIcon } from '../components/Icons';
import { OutletContextType } from '../App';
import { Tabs } from '../components/Tabs';

export const NetworkingPage = () => {
    const [allNetworks, setAllNetworks] = useState<{ WMI: VirtualNetwork[], HCS: VirtualNetwork[] }>({ WMI: [], HCS: [] });
    const [extensions, setExtensions] = useState<any[]>([]);
    const [selectedSwitch, setSelectedSwitch] = useState<string>('');
    const [isExtensionModalOpen, setIsExtensionModalOpen] = useState(false);
    const [activeTab, setActiveTab] = useState<VmEnvironment>('WMI');
    const [isLoading, setIsLoading] = useState(true);
    const { addNotification } = useOutletContext<OutletContextType>();
    const [isModalOpen, setIsModalOpen] = useState(false);

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

    useEffect(() => {
        fetchNetworks();
    }, [fetchNetworks]);

    useEffect(() => {
        if (selectedSwitch) {
            loadExtensions(selectedSwitch);
        }
    }, [selectedSwitch, loadExtensions]);

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
        if (window.confirm(`Are you sure you want to delete the ${network.environment} network "${network.name}"?`)) {
            try {
                await api.deleteNetwork(network.id, network.environment);
                addNotification('success', `Network '${network.name}' deleted successfully.`);
                fetchNetworks();
            } catch (err: any) {
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

    const renderWmiModal = () => (
        <>
            <div>
                <label htmlFor="wmi-net-name" className="block text-sm font-medium text-gray-300">Switch Name</label>
                <input type="text" id="wmi-net-name" value={wmiFormData.name} onChange={e => setWmiFormData({ ...wmiFormData, name: e.target.value })} className="mt-1 block w-full bg-slate-800 border border-slate-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" required />
            </div>
            <div>
                <label htmlFor="wmi-net-type" className="block text-sm font-medium text-gray-300">Switch Type</label>
                <select id="wmi-net-type" value={wmiFormData.type} onChange={e => setWmiFormData({ ...wmiFormData, type: e.target.value as NetworkType })} className="mt-1 block w-full bg-slate-800 border border-slate-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500">
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
                        <select id="external-adapter" value={wmiFormData.externalAdapterName} onChange={e => setWmiFormData({ ...wmiFormData, externalAdapterName: e.target.value })} className="mt-1 block w-full bg-slate-800 border border-slate-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500">
                            <option value="">Select adapter...</option>
                            {physicalAdapters.map(adapter => (
                                <option key={adapter.guid || adapter.pnpDeviceId} value={adapter.name}>
                                    {adapter.name} ({adapter.guid || adapter.pnpDeviceId})
                                </option>
                            ))}
                        </select>
                    </div>
                    <div className="flex items-center">
                        <input id="allow-management-os" type="checkbox" checked={wmiFormData.allowManagementOS} onChange={e => setWmiFormData({ ...wmiFormData, allowManagementOS: e.target.checked })} className="rounded border-gray-600 bg-slate-800 text-blue-600 focus:ring-blue-500" />
                        <label htmlFor="allow-management-os" className="ml-2 block text-sm text-gray-300">Allow management OS access (adds internal port)</label>
                    </div>
                </>
            )}
            <div className="text-sm text-gray-400 bg-slate-800/50 p-3 rounded">
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
                <input type="text" id="hcs-net-name" value={hcsFormData.name} onChange={e => setHcsFormData({ ...hcsFormData, name: e.target.value })} className="mt-1 block w-full bg-slate-800 border border-slate-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" required />
            </div>
            <div>
                <label htmlFor="hcs-net-prefix" className="block text-sm font-medium text-gray-300">Subnet Prefix</label>
                <input type="text" id="hcs-net-prefix" value={hcsFormData.prefix} onChange={e => setHcsFormData({ ...hcsFormData, prefix: e.target.value })} className="mt-1 block w-full bg-slate-800 border border-slate-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500" required />
            </div>
        </>
    );

    return (
        <div className="p-6 animate-fade-in">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-3xl font-bold text-white">Virtual Networks</h1>
                <Button onClick={handleOpenCreateModal} leftIcon={<PlusIcon />}>Create {activeTab} Network</Button>
            </div>

            <Tabs tabs={['WMI', 'HCS']} activeTab={activeTab} onTabClick={(tab) => setActiveTab(tab as VmEnvironment)} />

            {isLoading ? <div className="flex justify-center items-center h-full"><Spinner /></div> : (
                <div className="glass-panel overflow-hidden">
                    <table className="min-w-full">
                        <thead className="bg-slate-800/50">
                            <tr>
                                <th className="px-6 py-3 text-left text-xs font-medium text-slate-400 uppercase tracking-wider">Name</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-slate-400 uppercase tracking-wider">Type</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-slate-400 uppercase tracking-wider">Subnet</th>
                                <th className="px-6 py-3 text-right text-xs font-medium text-slate-400 uppercase tracking-wider">Actions</th>
                            </tr>
                        </thead>
                        {displayedNetworks.length > 0 ? (
                            <tbody className="divide-y divide-slate-700/50">
                                {displayedNetworks.map(net => (
                                    <tr key={net.id} className="hover:bg-slate-700/30 transition-colors">
                                        <td className="px-6 py-4 whitespace-nowrap">
                                            <div className="flex items-center">
                                                <div className="p-2 rounded bg-blue-500/10 mr-3">
                                                    <NetworkIcon className="w-5 h-5 text-blue-400" />
                                                </div>
                                                <span className="text-sm font-medium text-white">{net.name}</span>
                                            </div>
                                        </td>
                                        <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-300">{net.type}</td>
                                        <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-300 font-mono">{net.subnet}</td>
                                        <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium space-x-2">
                                            <Button variant="secondary" size="sm" onClick={() => handleOpenEdit(net)}>Edit</Button>
                                            <Button variant="secondary" size="sm" onClick={() => handleDelete(net)}>
                                                <TrashIcon className="h-4 w-4" />
                                            </Button>
                                            <Button variant="secondary" size="sm" onClick={() => { setSelectedSwitch(net.id); setIsExtensionModalOpen(true); }}>Extensions</Button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        ) : (
                            <tbody>
                                <tr>
                                    <td colSpan={4} className="text-center py-12 text-slate-500">
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
            <Modal isOpen={isExtensionModalOpen} onClose={() => setIsExtensionModalOpen(false)} title={`Manage Extensions`}>
                <div className="space-y-4">
                    <label className="block text-sm font-medium text-gray-300">Select Switch</label>
                    <select
                        value={selectedSwitch}
                        onChange={(e) => setSelectedSwitch(e.target.value)}
                        className="mt-1 block w-full bg-slate-800 border border-slate-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500"
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
                                <div key={ext.name} className="flex items-center justify-between p-3 bg-slate-800 rounded border border-slate-700">
                                    <span className="text-sm text-white">{ext.name}</span>
                                    <button
                                        onClick={() => handleToggleExtension(selectedSwitch, ext.name, ext.enabled)}
                                        className={`px-3 py-1 rounded text-xs font-medium ${ext.enabled ? 'bg-red-500/20 text-red-400 hover:bg-red-500/30' : 'bg-green-500/20 text-green-400 hover:bg-green-500/30'}`}
                                    >
                                        {ext.enabled ? 'Disable' : 'Enable'}
                                    </button>
                                </div>
                            ))}
                        </div>
                    )}

                    {selectedSwitch && (
                        <div className={`mt-4 p-3 rounded border ${displayedNetworks.find(n => n.id === selectedSwitch)?.supportsTrunk ? 'bg-green-500/10 border-green-500/30 text-green-400' : 'bg-yellow-500/10 border-yellow-500/30 text-yellow-400'}`}>
                            <div className="flex items-center justify-between">
                                <span className="text-sm font-medium">Trunk Mode Support</span>
                                <span className="text-xs font-bold uppercase">
                                    {displayedNetworks.find(n => n.id === selectedSwitch)?.supportsTrunk ? 'Yes' : 'No'}
                                </span>
                            </div>
                        </div>
                    )}
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
                            className="mt-1 block w-full bg-slate-800 border border-slate-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                            required
                        />
                    </div>
                    <div>
                        <label htmlFor="edit-net-notes" className="block text-sm font-medium text-gray-300">Notes</label>
                        <textarea
                            id="edit-net-notes"
                            value={editFormData.notes}
                            onChange={e => setEditFormData({ ...editFormData, notes: e.target.value })}
                            className="mt-1 block w-full bg-slate-800 border border-slate-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                            rows={3}
                        />
                    </div>
                    <div className="mt-6 flex justify-end space-x-3 pt-4">
                        <Button type="button" variant="secondary" onClick={handleCloseEdit}>Cancel</Button>
                        <Button type="submit">Save</Button>
                    </div>
                </form>
            </Modal>
        </div>
    );
};
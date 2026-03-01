
import React, { useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { ServiceInfo } from '../types';
import { getServiceInfo } from '../services/hypervService';
import { Spinner } from '../components/Spinner';
import { Card } from '../components/Card';
import { OutletContextType } from '../App';
import { useHostContext } from '../hooks/useHostContext';

export const HypervisorPage = () => {
    const [info, setInfo] = useState<ServiceInfo | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const { addNotification } = useOutletContext<OutletContextType>();
    const { capabilities } = useHostContext();

    useEffect(() => {
        getServiceInfo()
            .then(setInfo)
            .catch(err => addNotification('error', `Failed to load hypervisor info: ${err.message}`))
            .finally(() => setIsLoading(false));
    }, [addNotification]);

    return (
        <div className="flex flex-col h-full">
            <header className="p-4 bg-panel-bg border-b border-panel-border flex items-center justify-between flex-shrink-0">
                <div className="flex items-center space-x-3">
                    <h1 className="text-lg font-semibold text-gray-800">Hypervisor Agent</h1>
                    {capabilities && (
                        <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${capabilities.hypervisorType === 'KVM' ? 'bg-orange-100 text-orange-700' : 'bg-blue-100 text-blue-700'}`}>
                            {capabilities.hypervisorType}
                        </span>
                    )}
                </div>
            </header>

            <main className="flex-1 overflow-y-auto p-4">
                {isLoading ? <div className="flex justify-center items-center h-full"><Spinner /></div> : info ? (
                    <div className="space-y-4">
                        <Card title="Agent Information">
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                               <div>
                                    <h4 className="font-bold text-lg text-gray-800">{info.name}</h4>
                                    <p className="text-sm text-gray-500">Version: {info.version}</p>
                                    {capabilities && <p className="text-sm text-gray-500">Hypervisor: {capabilities.hypervisorType}</p>}
                               </div>
                               <p className="text-gray-600 md:col-span-2">{info.description}</p>
                            </div>
                        </Card>
                        {capabilities && (
                            <Card title="Host Capabilities">
                                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                                    <div className="bg-gray-100 p-3 rounded-md">
                                        <div className="text-xs text-gray-500 uppercase">Console Type</div>
                                        <div className="text-sm font-semibold text-gray-800">{capabilities.consoleType}</div>
                                    </div>
                                    <div className="bg-gray-100 p-3 rounded-md">
                                        <div className="text-xs text-gray-500 uppercase">Disk Formats</div>
                                        <div className="text-sm font-semibold text-gray-800">{capabilities.supportedDiskFormats.join(', ')}</div>
                                    </div>
                                    <div className="bg-gray-100 p-3 rounded-md">
                                        <div className="text-xs text-gray-500 uppercase">Live Migration</div>
                                        <div className={`text-sm font-semibold ${capabilities.supportsLiveMigration ? 'text-green-700' : 'text-gray-400'}`}>{capabilities.supportsLiveMigration ? 'Supported' : 'Not Available'}</div>
                                    </div>
                                    <div className="bg-gray-100 p-3 rounded-md">
                                        <div className="text-xs text-gray-500 uppercase">Dynamic Memory</div>
                                        <div className={`text-sm font-semibold ${capabilities.supportsDynamicMemory ? 'text-green-700' : 'text-gray-400'}`}>{capabilities.supportsDynamicMemory ? 'Supported' : 'Not Available'}</div>
                                    </div>
                                    <div className="bg-gray-100 p-3 rounded-md">
                                        <div className="text-xs text-gray-500 uppercase">Replication</div>
                                        <div className={`text-sm font-semibold ${capabilities.supportsReplication ? 'text-green-700' : 'text-gray-400'}`}>{capabilities.supportsReplication ? 'Supported' : 'Not Available'}</div>
                                    </div>
                                    <div className="bg-gray-100 p-3 rounded-md">
                                        <div className="text-xs text-gray-500 uppercase">Fibre Channel</div>
                                        <div className={`text-sm font-semibold ${capabilities.supportsFibreChannel ? 'text-green-700' : 'text-gray-400'}`}>{capabilities.supportsFibreChannel ? 'Supported' : 'Not Available'}</div>
                                    </div>
                                </div>
                            </Card>
                        )}
                        <Card title="Service Capabilities">
                            <ul className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2 text-gray-700">
                               {info.capabilities.map((cap, index) => (
                                   <li key={index} className="bg-gray-100 p-2 rounded-md text-sm">{cap}</li>
                               ))}
                            </ul>
                        </Card>
                        <Card title="API Endpoints">
                             <div className="space-y-2">
                               {Object.entries(info.endpoints).map(([key, value]) => (
                                   <div key={key} className="flex items-center bg-gray-100 p-2 rounded-md">
                                       <span className="font-mono text-sm text-primary-700 font-semibold capitalize mr-4">{key}</span>
                                       <span className="font-mono text-sm text-gray-600">{value}</span>
                                   </div>
                               ))}
                            </div>
                        </Card>
                    </div>
                ) : <div className="p-6 text-center text-gray-500">Could not load hypervisor agent information.</div>}
            </main>
        </div>
    );
};
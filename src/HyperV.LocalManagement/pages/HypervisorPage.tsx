
import React, { useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { ServiceInfo } from '../types';
import { getServiceInfo } from '../services/hypervService';
import { Spinner } from '../components/Spinner';
import { Card } from '../components/Card';
import { OutletContextType } from '../App';

export const HypervisorPage = () => {
    const [info, setInfo] = useState<ServiceInfo | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const { addNotification } = useOutletContext<OutletContextType>();

    useEffect(() => {
        getServiceInfo()
            .then(setInfo)
            .catch(err => addNotification('error', `Failed to load hypervisor info: ${err.message}`))
            .finally(() => setIsLoading(false));
    }, [addNotification]);

    return (
        <div className="p-6">
            <h1 className="text-3xl font-bold mb-6 text-white">Hypervisor Agent</h1>
            {isLoading ? <div className="flex justify-center items-center h-full"><Spinner /></div> : info ? (
                <div className="space-y-6">
                    <Card title="Agent Information">
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                           <div>
                                <h4 className="font-bold text-lg text-white">{info.name}</h4>
                                <p className="text-sm text-gray-400">Version: {info.version}</p>
                           </div>
                           <p className="text-gray-300 md:col-span-2">{info.description}</p>
                        </div>
                    </Card>
                    <Card title="Capabilities">
                        <ul className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3 list-disc list-inside text-gray-300">
                           {info.capabilities.map((cap, index) => (
                               <li key={index} className="bg-gray-700/50 p-2 rounded-md">{cap}</li>
                           ))}
                        </ul>
                    </Card>
                    <Card title="API Endpoints">
                         <div className="space-y-2">
                           {Object.entries(info.endpoints).map(([key, value]) => (
                               <div key={key} className="flex items-center bg-gray-900 p-2 rounded-md">
                                   <span className="font-mono text-sm text-primary-400 capitalize mr-4">{key}</span>
                                   <span className="font-mono text-sm text-gray-300">{value}</span>
                               </div>
                           ))}
                        </div>
                    </Card>
                </div>
            ) : <div className="p-6 text-center text-gray-400">Could not load hypervisor agent information.</div>}
        </div>
    );
};

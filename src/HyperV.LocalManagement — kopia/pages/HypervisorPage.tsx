import React, { useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { ServiceInfo } from '../types';
import { getServiceInfo } from '../services/hypervService';
import { Spinner } from '../components/Spinner';
import { OutletContextType } from '../App';
import { ServerIcon, ChipIcon, NetworkIcon } from '../components/Icons';

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
        <div className="p-6 animate-fade-in">
            <h1 className="text-3xl font-bold mb-6 text-white">Hypervisor Agent</h1>
            {isLoading ? <div className="flex justify-center items-center h-full"><Spinner /></div> : info ? (
                <div className="space-y-6">
                    <div className="glass-panel p-6">
                        <div className="flex items-start">
                            <div className="p-4 bg-blue-500/20 rounded-xl mr-4">
                                <ServerIcon className="w-8 h-8 text-blue-400" />
                            </div>
                            <div>
                                <h2 className="text-xl font-bold text-white">{info.name}</h2>
                                <p className="text-sm text-slate-400 mt-1">Version: <span className="font-mono text-white">{info.version}</span></p>
                                <p className="text-slate-300 mt-4 max-w-2xl">{info.description}</p>
                            </div>
                        </div>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                        <div className="glass-panel p-6">
                            <div className="flex items-center mb-4">
                                <ChipIcon className="w-5 h-5 text-purple-400 mr-2" />
                                <h3 className="text-lg font-semibold text-white">Capabilities</h3>
                            </div>
                            <ul className="space-y-2">
                                {info.capabilities.map((cap, index) => (
                                    <li key={index} className="bg-slate-800/50 p-3 rounded-lg flex items-center text-slate-300 text-sm border border-slate-700/50">
                                        <div className="w-1.5 h-1.5 rounded-full bg-purple-500 mr-3"></div>
                                        {cap}
                                    </li>
                                ))}
                            </ul>
                        </div>

                        <div className="glass-panel p-6">
                            <div className="flex items-center mb-4">
                                <NetworkIcon className="w-5 h-5 text-green-400 mr-2" />
                                <h3 className="text-lg font-semibold text-white">API Endpoints</h3>
                            </div>
                            <div className="space-y-2">
                                {Object.entries(info.endpoints).map(([key, value]) => (
                                    <div key={key} className="flex items-center justify-between bg-slate-800/50 p-3 rounded-lg border border-slate-700/50">
                                        <span className="font-mono text-xs text-green-400 uppercase tracking-wider">{key}</span>
                                        <span className="font-mono text-xs text-slate-400">{value}</span>
                                    </div>
                                ))}
                            </div>
                        </div>
                    </div>
                </div>
            ) : <div className="p-12 text-center text-slate-500">Could not load hypervisor agent information.</div>}
        </div>
    );
};

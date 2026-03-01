import React, { useState } from 'react';
import type { Host } from '../../../types';
import { ChevronDownIcon, ChevronRightIcon } from '../../../components/icons/Icons';
import { StorageInterfacesView } from './config/StorageInterfacesView';
import { StorageDevicesView } from './config/StorageDevicesView';

interface HostConfigureTabProps {
  host: Host;
}

const configMenu = [
    {
        category: 'Storage',
        items: ['Storage Interfaces', 'Storage Devices']
    },
    {
        category: 'Networking',
        items: ['Virtual switches', 'Virtual adapters', 'Physical adapters']
    },
    {
        category: 'Virtual Machines',
        items: ['VM Startup/Shutdown', 'Default VM Generation', 'Swap File Location']
    },
    {
        category: 'System',
        items: ['Licensing', 'Time Configuration', 'Authentication Services', 'Certificate', 'Power Management', 'Advanced System Settings', 'Firewall', 'Services']
    },
    {
        category: 'Hardware',
        items: ['Overview', 'Graphics', 'PCI Devices', 'Firmware']
    },
];

export const HostConfigureTab: React.FC<HostConfigureTabProps> = ({ host }) => {
    const [selectedItem, setSelectedItem] = useState('Storage Interfaces');
    const [expandedCategories, setExpandedCategories] = useState<Set<string>>(new Set(['Storage', 'Networking', 'Virtual Machines', 'System', 'Hardware']));

    const toggleCategory = (category: string) => {
        setExpandedCategories(prev => {
            const newSet = new Set(prev);
            if (newSet.has(category)) {
                newSet.delete(category);
            } else {
                newSet.add(category);
            }
            return newSet;
        });
    };

    const renderContent = () => {
        switch (selectedItem) {
            case 'Storage Interfaces':
                return <StorageInterfacesView host={host} />;
            case 'Storage Devices':
                return <StorageDevicesView host={host} />;
            default:
                return (
                    <div className="flex-grow p-8 bg-white border border-gray-200 rounded-md">
                        <h2 className="text-xl font-semibold text-gray-700">{selectedItem}</h2>
                        <p className="mt-4 text-gray-500">This configuration section is coming soon.</p>
                    </div>
                );
        }
    };

    return (
        <div className="flex gap-4">
            <aside className="w-64 flex-shrink-0">
                <div className="bg-white border border-gray-200 rounded-md p-2 space-y-1">
                    {configMenu.map(({ category, items }) => (
                        <div key={category}>
                            <button onClick={() => toggleCategory(category)} className="w-full flex items-center justify-between text-left text-sm font-semibold text-gray-700 py-1 px-2 hover:bg-gray-100 rounded">
                                <span>{category}</span>
                                {expandedCategories.has(category) ? <ChevronDownIcon className="w-4 h-4" /> : <ChevronRightIcon className="w-4 h-4" />}
                            </button>
                            {expandedCategories.has(category) && (
                                <div className="pl-2 mt-1">
                                    {items.map(item => (
                                        <button
                                            key={item}
                                            onClick={() => setSelectedItem(item)}
                                            className={`w-full text-left block text-sm py-1 px-3 rounded ${selectedItem === item ? 'bg-blue-100 text-blue-800 font-semibold' : 'hover:bg-gray-100'}`}
                                        >
                                            {item}
                                        </button>
                                    ))}
                                </div>
                            )}
                        </div>
                    ))}
                </div>
            </aside>
            <main className="flex-grow">
                {renderContent()}
            </main>
        </div>
    );
};

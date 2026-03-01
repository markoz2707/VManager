import React, { useState } from 'react';
import type { Host, StorageInterface } from '../../../../types';
import { ArrowsRightLeftIcon, ChevronDownIcon, FilterIcon } from '../../../../components/icons/Icons';

interface StorageInterfacesViewProps {
  host: Host;
}

const TableHeader: React.FC<{ label: string }> = ({ label }) => (
  <th scope="col" className="px-4 py-3 text-left text-xs font-medium text-gray-600 uppercase tracking-wider whitespace-nowrap">
    <div className="flex items-center justify-between">
      <span>{label}</span>
      <FilterIcon className="w-4 h-4 text-gray-400" />
    </div>
  </th>
);

export const StorageInterfacesView: React.FC<StorageInterfacesViewProps> = ({ host: _host }) => {
  const [interfaces] = useState<StorageInterface[]>([]);
  const [selectedInterfaces, setSelectedInterfaces] = useState<Set<string>>(new Set());

  const handleSelectAll = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.checked) {
      setSelectedInterfaces(new Set(interfaces.map(i => i.id)));
    } else {
      setSelectedInterfaces(new Set());
    }
  };

  const handleSelect = (id: string) => {
    setSelectedInterfaces(prev => {
      const newSet = new Set(prev);
      if (newSet.has(id)) {
        newSet.delete(id);
      } else {
        newSet.add(id);
      }
      return newSet;
    });
  };

  return (
    <div className="bg-white border border-gray-200 rounded-md">
      <div className="p-4">
        <h2 className="text-xl font-light text-gray-800">Storage Interfaces</h2>
      </div>

      <div className="px-4 py-2 border-y border-gray-200 flex items-center space-x-4">
        <button className="flex items-center text-blue-600 text-sm font-semibold">
          ADD SOFTWARE ADAPTER <ChevronDownIcon className="w-4 h-4 ml-1" />
        </button>
        <button className="text-blue-600 text-sm font-semibold">REFRESH</button>
        <button className="text-blue-600 text-sm font-semibold">RESCAN STORAGE</button>
      </div>

      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-200">
          <thead className="bg-gray-50">
            <tr>
              <th scope="col" className="px-4 py-3">
                <input type="checkbox" className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                  checked={selectedInterfaces.size > 0 && selectedInterfaces.size === interfaces.length}
                  onChange={handleSelectAll}
                />
              </th>
              <TableHeader label="Adapter" />
              <TableHeader label="Model" />
              <TableHeader label="Type" />
              <TableHeader label="Status" />
              <TableHeader label="Identifier" />
              <TableHeader label="Targets" />
              <TableHeader label="Devices" />
              <TableHeader label="Paths" />
            </tr>
          </thead>
          <tbody className="bg-white divide-y divide-gray-200">
            {interfaces.length === 0 ? (
              <tr><td colSpan={9} className="text-center py-8 text-gray-500">Coming soon - requires agent proxy</td></tr>
            ) : (
              interfaces.map(item => (
                <tr key={item.id} className="text-sm text-gray-700 hover:bg-gray-50">
                  <td className="px-4 py-2">
                    <input type="checkbox" className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                      checked={selectedInterfaces.has(item.id)}
                      onChange={() => handleSelect(item.id)}
                    />
                  </td>
                  <td className="px-4 py-2 whitespace-nowrap">
                    <div className="flex items-center">
                      <ArrowsRightLeftIcon className="w-4 h-4 text-gray-400 mr-2" />
                      {item.adapter}
                    </div>
                  </td>
                  <td className="px-4 py-2">{item.model}</td>
                  <td className="px-4 py-2 whitespace-nowrap">{item.type}</td>
                  <td className="px-4 py-2 whitespace-nowrap">{item.status}</td>
                  <td className="px-4 py-2 whitespace-pre-line text-xs">{item.identifier || '--'}</td>
                  <td className="px-4 py-2 text-right">{item.targets}</td>
                  <td className="px-4 py-2 text-right">{item.devices}</td>
                  <td className="px-4 py-2 text-right">{item.paths}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="flex justify-between items-center p-4 border-t border-gray-200">
        <div>
          <button className="px-3 py-1.5 border border-gray-300 rounded-sm text-sm font-semibold text-gray-700 hover:bg-gray-100 mr-2">Manage Columns</button>
          <button className="px-3 py-1.5 border border-gray-300 rounded-sm text-sm font-semibold text-gray-700 hover:bg-gray-100">Export</button>
        </div>
        <div className="text-sm text-gray-500">
          {interfaces.length} items
        </div>
      </div>
    </div>
  );
};

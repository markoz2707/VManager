import React, { useState } from 'react';
import type { Host, StorageDevice } from '../../../../types';
import { FilterIcon, ChevronDownIcon } from '../../../../components/icons/Icons';

interface StorageDevicesViewProps {
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

export const StorageDevicesView: React.FC<StorageDevicesViewProps> = ({ host: _host }) => {
  const [devices] = useState<StorageDevice[]>([]);
  const [selectedDeviceIds, setSelectedDeviceIds] = useState<Set<string>>(new Set());

  const handleSelectAll = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.checked) {
      setSelectedDeviceIds(new Set(devices.map(d => d.id)));
    } else {
      setSelectedDeviceIds(new Set());
    }
  };

  const isAllSelected = selectedDeviceIds.size > 0 && selectedDeviceIds.size === devices.length;

  return (
    <div className="bg-white border border-gray-200 rounded-md">
      <div className="p-4">
        <h2 className="text-xl font-light text-gray-800">Storage Devices</h2>
      </div>

      <div className="px-4 py-2 border-y border-gray-200 flex items-center flex-wrap gap-x-4 gap-y-2 text-sm">
        <button className="font-semibold text-blue-600">REFRESH</button>
      </div>

      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-200">
          <thead className="bg-gray-50">
            <tr>
              <th scope="col" className="px-4 py-3">
                <input type="checkbox" className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                  checked={isAllSelected}
                  onChange={handleSelectAll}
                />
              </th>
              <TableHeader label="Name" />
              <TableHeader label="LUN" />
              <TableHeader label="Type" />
              <TableHeader label="Capacity" />
              <TableHeader label="Storage Volume" />
              <TableHeader label="Operational State" />
              <TableHeader label="Drive Type" />
              <TableHeader label="Transport" />
            </tr>
          </thead>
          <tbody className="bg-white divide-y divide-gray-200">
            {devices.length === 0 ? (
              <tr><td colSpan={9} className="text-center py-8 text-gray-500">Coming soon - requires agent proxy</td></tr>
            ) : null}
          </tbody>
        </table>
      </div>

      <div className="flex justify-between items-center px-4 py-2 border-t border-gray-200">
        <div>
          <button className="px-3 py-1.5 border border-gray-300 rounded-sm text-sm font-semibold text-gray-700 hover:bg-gray-100 mr-2">Manage Columns</button>
          <button className="px-3 py-1.5 border border-gray-300 rounded-sm text-sm font-semibold text-gray-700 hover:bg-gray-100 flex items-center">Export <ChevronDownIcon className="w-4 h-4 ml-1" /></button>
        </div>
        <div className="text-sm text-gray-500">{devices.length} items</div>
      </div>
    </div>
  );
};

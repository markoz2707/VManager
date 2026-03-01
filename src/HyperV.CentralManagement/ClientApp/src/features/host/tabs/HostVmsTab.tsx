import React from 'react';
import type { VirtualMachine, VmStatus } from '../../../types';
import { VmRow } from '../components/VmRow';
import { PlusCircleIcon } from '../../../components/icons/Icons';

interface HostVmsTabProps {
  vms: VirtualMachine[];
  onUpdateVmStatus: (vmId: string, status: VmStatus) => void;
  onOpenCreateModal: () => void;
}

export const HostVmsTab: React.FC<HostVmsTabProps> = ({ vms, onUpdateVmStatus, onOpenCreateModal }) => {
  const TABLE_HEADERS = ["Name", "Status", "Host", "CPU Usage", "Memory Usage", "Provisioned", "Used", "Actions"];

  return (
    <div className="bg-white border border-gray-200 rounded">
        <div className="flex justify-between items-center p-4 border-b">
            <h3 className="text-lg font-semibold text-gray-800">Virtual Machines ({vms.length})</h3>
            <button
            onClick={onOpenCreateModal}
            className="flex items-center px-3 py-1.5 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 transition-colors font-semibold shadow-sm"
            >
            <PlusCircleIcon className="w-5 h-5 mr-2" />
            Create VM
            </button>
        </div>
        <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
                <tr>
                {TABLE_HEADERS.map(header => (
                    <th key={header} scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    {header}
                    </th>
                ))}
                </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
                {vms.map(vm => (
                    <VmRow key={vm.id} vm={vm} onUpdateStatus={onUpdateVmStatus} />
                ))}
            </tbody>
            </table>
        </div>
       {vms.length === 0 && (
            <div className="text-center py-12">
                <p className="text-gray-500">No virtual machines found.</p>
            </div>
        )}
    </div>
  );
};

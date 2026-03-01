import React, { useState } from 'react';
import type { VirtualMachine, VmStatus } from '../../../types';
import { VmStatus as VmStatusEnum } from '../../../types';
import { VmRowControls } from './VmRowControls';
import { WindowsIcon, LinuxIcon } from '../../../components/icons/Icons';

interface VmRowProps {
  vm: VirtualMachine;
  onUpdateStatus: (vmId: string, status: VmStatus) => void;
}

const statusStyles: { [key in VmStatus]: { text: string; dot: string } } = {
  [VmStatusEnum.Running]: { text: 'text-green-800', dot: 'bg-green-500' },
  [VmStatusEnum.PoweredOn]: { text: 'text-green-800', dot: 'bg-green-500' },
  [VmStatusEnum.Stopped]: { text: 'text-red-800', dot: 'bg-red-500' },
  [VmStatusEnum.Saved]: { text: 'text-yellow-800', dot: 'bg-yellow-500' },
  [VmStatusEnum.Creating]: { text: 'text-blue-800', dot: 'bg-blue-500' },
};

export const VmRow: React.FC<VmRowProps> = ({ vm, onUpdateStatus }) => {
  const [isUpdating, setIsUpdating] = useState(false);
  const style = statusStyles[vm.status];

  const handleStatusUpdate = async (status: VmStatus) => {
    setIsUpdating(true);
    await onUpdateStatus(vm.id, status);
    setIsUpdating(false);
  };

  return (
    <tr className={`text-sm text-gray-700 ${isUpdating ? 'opacity-50' : ''}`}>
      <td className="px-6 py-4 whitespace-nowrap">
        <div className="flex items-center">
            {vm.os === 'windows' || vm.os === 'windows-other' ? <WindowsIcon className="w-5 h-5 mr-2 text-blue-500" /> : <LinuxIcon className="w-5 h-5 mr-2 text-yellow-500" />}
            <span className="font-medium">{vm.name}</span>
        </div>
      </td>
      <td className="px-6 py-4 whitespace-nowrap">
        <div className="flex items-center">
          <span className={`w-2.5 h-2.5 mr-2 rounded-full ${style.dot}`}></span>
          <span className={style.text}>{vm.status}</span>
        </div>
      </td>
      <td className="px-6 py-4 whitespace-nowrap text-gray-500">{vm.hostName}</td>
      <td className="px-6 py-4 whitespace-nowrap text-gray-500">{vm.cpuUsage.toFixed(1)} %</td>
      <td className="px-6 py-4 whitespace-nowrap text-gray-500">{vm.memoryUsage.toFixed(1)} GB / {vm.totalMemory} GB</td>
      <td className="px-6 py-4 whitespace-nowrap text-gray-500">{vm.provisionedSpaceGB} GB</td>
      <td className="px-6 py-4 whitespace-nowrap text-gray-500">{vm.usedSpaceGB} GB</td>
      <td className="px-6 py-4 whitespace-nowrap">
        <VmRowControls status={vm.status} onAction={handleStatusUpdate} />
      </td>
    </tr>
  );
};

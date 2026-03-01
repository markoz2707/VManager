import React from 'react';
import type { VirtualMachine, VmStatus } from '../../types';
import { VmStatus as VmStatusEnum } from '../../types';
import {
    WindowsIcon, LinuxIcon, ChevronDownIcon,
    CameraIcon, PlayIcon,
    StopIcon, PauseIcon, WebConsoleIcon,
    CogIcon, GridLayoutIcon,
} from '../../components/icons/Icons';
import { VmSummaryTab } from './tabs/VmSummaryTab';
import { VmMonitorTab } from './tabs/VmMonitorTab';
import { VmConfigureTab } from './tabs/VmConfigureTab';
import { VmSnapshotsTab } from './tabs/VmSnapshotsTab';

interface VmViewProps {
  vm: VirtualMachine;
  onUpdateVmStatus: (vmId: string, status: VmStatus) => void;
}

export const VmView: React.FC<VmViewProps> = ({ vm, onUpdateVmStatus }) => {
  const [activeTab, setActiveTab] = React.useState('Summary');

  if (!vm) {
    return null;
  }

  const tabs = ['Summary', 'Monitor', 'Settings', 'Permissions', 'Snapshots', 'Updates'];

  const isRunning = vm.status === VmStatusEnum.Running || vm.status === VmStatusEnum.PoweredOn;
  const isStopped = vm.status === VmStatusEnum.Stopped;

  return (
    <div className="space-y-4">
        <div className="flex items-center justify-between">
            <div className="flex items-center space-x-4">
                 <h2 className="text-2xl font-light text-gray-700 flex items-center">
                    {vm.os === 'windows' || vm.os === 'windows-other' ? <WindowsIcon className="w-6 h-6 mr-2 text-blue-500"/> : <LinuxIcon className="w-6 h-6 mr-2 text-yellow-500" />}
                    {vm.name}
                </h2>
                <div className="flex items-center text-gray-400 space-x-1">
                    <button disabled={!isStopped} title="Start" className="p-1 hover:text-green-500 disabled:opacity-50"><PlayIcon className="w-5 h-5"/></button>
                    <button disabled={!isRunning} title="Stop" className="p-1 hover:text-red-500 disabled:opacity-50"><StopIcon className="w-5 h-5"/></button>
                    <button disabled={!isRunning} title="Pause" className="p-1 hover:text-yellow-500 disabled:opacity-50"><PauseIcon className="w-5 h-5"/></button>
                    <button title="Launch Web Console" className="p-1 hover:text-blue-500"><WebConsoleIcon className="w-5 h-5"/></button>
                    <button title="Take Snapshot" className="p-1 hover:text-blue-500"><CameraIcon className="w-5 h-5"/></button>
                </div>
            </div>
            <div className="flex items-center space-x-2">
                <button className="text-gray-400 hover:text-gray-600"><GridLayoutIcon className="w-5 h-5" /></button>
                <button className="text-gray-400 hover:text-gray-600"><CogIcon className="w-5 h-5" /></button>
                <span className="text-gray-300">|</span>
                <button className="text-sm font-semibold text-blue-600 flex items-center">ACTIONS <ChevronDownIcon className="w-4 h-4 ml-1" /></button>
            </div>
        </div>

        <div className="border-b border-gray-200">
            <nav className="-mb-px flex space-x-6 overflow-x-auto">
            {tabs.map(tab => (
                <button
                    key={tab}
                    onClick={() => setActiveTab(tab)}
                    className={`py-2 px-1 border-b-2 text-sm font-medium whitespace-nowrap ${activeTab === tab ? 'border-blue-500 text-blue-600' : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'}`}
                >
                    {tab}
                </button>
            ))}
            </nav>
        </div>

        <div>
            {activeTab === 'Summary' && <VmSummaryTab vm={vm} />}
            {activeTab === 'Monitor' && <VmMonitorTab vm={vm} />}
            {activeTab === 'Settings' && <VmConfigureTab vm={vm} />}
            {activeTab === 'Snapshots' && <VmSnapshotsTab vm={vm} />}
            {['Permissions', 'Updates'].includes(activeTab) && (
                 <div className="text-center py-16 text-gray-500">
                    <p>{activeTab} view is not implemented.</p>
                </div>
            )}
        </div>
    </div>
  );
};

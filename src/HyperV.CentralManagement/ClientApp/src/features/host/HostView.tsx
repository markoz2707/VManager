import React, { useState, useEffect } from 'react';
import type { Host, VirtualMachine, VmStatus } from '../../types';
import { HostSummaryTab } from './tabs/HostSummaryTab';
import { HostVmsTab } from './tabs/HostVmsTab';
import { HostMonitorTab } from './tabs/HostMonitorTab';
import { HostConfigureTab } from './tabs/HostConfigureTab';
import * as metricsService from '../../services/metricsService';

interface HostViewProps {
  host: Host | null | undefined;
  vms: VirtualMachine[];
  onUpdateVmStatus: (vmId: string, status: VmStatus) => void;
  onOpenCreateModal: () => void;
}

export const HostView: React.FC<HostViewProps> = ({ host, vms, onUpdateVmStatus, onOpenCreateModal }) => {
  const [activeTab, setActiveTab] = useState('Summary');
  const [usageHistory, setUsageHistory] = useState<any[]>([]);
  const [isHistoryLoading, setIsHistoryLoading] = useState(false);

  useEffect(() => {
    const fetchHistory = async () => {
      if (activeTab === 'Monitor' && host) {
        setIsHistoryLoading(true);
        try {
          const history = await metricsService.getHostUsageHistory(host.id);
          setUsageHistory(history);
        } catch (error) {
          console.error('Failed to fetch usage history:', error);
        } finally {
          setIsHistoryLoading(false);
        }
      }
    };
    fetchHistory();
  }, [activeTab, host]);

  if (!host) {
    return <div className="bg-white border border-gray-200 rounded p-6 text-center text-gray-500">Select a host to view details.</div>;
  }

  const tabs = ['Summary', 'Monitor', 'Configure', 'Permissions', 'VMs', 'Storage Volumes', 'Networks', 'Updates'];

  return (
    <div className="space-y-4">
        <div className="flex items-center justify-between">
            <h2 className="text-2xl font-light text-gray-700">{host.name}</h2>
            <div className="flex items-center space-x-2">
                <span className="text-gray-500">|</span>
                <button className="text-sm font-semibold text-blue-600">ACTIONS</button>
            </div>
        </div>

        <div className="border-b border-gray-200">
            <nav className="-mb-px flex space-x-6">
            {tabs.map(tab => (
                <button
                    key={tab}
                    onClick={() => setActiveTab(tab)}
                    className={`py-2 px-1 border-b-2 text-sm font-medium ${activeTab === tab ? 'border-blue-500 text-blue-600' : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'}`}
                >
                    {tab}
                </button>
            ))}
            </nav>
        </div>

        <div>
            {activeTab === 'Summary' && <HostSummaryTab host={host} />}
            {activeTab === 'VMs' && <HostVmsTab vms={vms} onUpdateVmStatus={onUpdateVmStatus} onOpenCreateModal={onOpenCreateModal} />}
            {activeTab === 'Configure' && <HostConfigureTab host={host} />}
            {activeTab === 'Monitor' && (
                isHistoryLoading ? (
                    <div className="text-center py-16 text-gray-500">Loading performance data...</div>
                ) : (
                    <HostMonitorTab data={usageHistory} />
                )
            )}
            {!['Summary', 'VMs', 'Monitor', 'Configure'].includes(activeTab) && (
                <div className="text-center py-16 text-gray-500">
                    <p>{activeTab} view is not implemented.</p>
                </div>
            )}
        </div>
    </div>
  );
};

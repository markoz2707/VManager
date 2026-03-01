import React, { useEffect, useState, useCallback } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Card } from '../components/Card';
import { Spinner } from '../components/Spinner';
import { Modal } from '../components/Modal';
import { getDashboardStats, getHostDetails, jobService, hostService } from '../services/hypervService';
import { Button } from '../components/Button';
import { ShutdownIcon, RebootIcon, RefreshIcon, ActionsIcon, PlusIcon, CheckCircleIcon } from '../components/Icons';
import { OutletContextType } from '../App';
import { HostDetails, HostPerformance, StorageJob, Stats } from '../types';
import { CreateVmWizard } from '../components/CreateVmWizard';
import { Alert } from '../components/Alert';
import { useSignalR } from '../hooks/useSignalR';
import { useHostContext } from '../hooks/useHostContext';

const ResourceBar = ({ title, used, capacity, unit, percent, colorClass }: { title: string, used: number, capacity: number, unit: string, percent: number, colorClass: string }) => (
    <div>
        <div className="flex justify-between items-baseline mb-1">
            <span className="text-sm font-semibold text-gray-600">{title}</span>
            <span className="text-xs text-gray-500">
                <span className="font-bold text-gray-800">{percent}%</span> FREE: {(capacity - used).toFixed(2)} {unit}
            </span>
        </div>
        <div className="w-full bg-gray-200 rounded-full h-2.5">
            <div className={`${colorClass} h-2.5 rounded-full`} style={{ width: `${percent}%` }}></div>
        </div>
        <div className="flex justify-between text-xs text-gray-500 mt-1">
            <span>USED: {used.toFixed(2)} {unit}</span>
            <span>CAPACITY: {capacity.toFixed(2)} {unit}</span>
        </div>
    </div>
);

const InfoRow = ({ label, value }: { label: string, value: React.ReactNode }) => (
    <div className="flex justify-between py-1.5 border-b border-gray-200">
        <dt className="text-sm text-gray-600">{label}</dt>
        <dd className="text-sm font-medium text-gray-800 text-right">{value}</dd>
    </div>
);

export const HostPage = () => {
  const [stats, setStats] = useState<Stats | null>(null);
  const [hostDetails, setHostDetails] = useState<HostDetails | null>(null);
  const [jobs, setJobs] = useState<StorageJob[]>([]);
  const [performance, setPerformance] = useState<HostPerformance | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isWizardOpen, setIsWizardOpen] = useState(false);
  const [confirmAction, setConfirmAction] = useState<'shutdown' | 'reboot' | null>(null);
  const [isActionBusy, setIsActionBusy] = useState(false);
  const { addNotification } = useOutletContext<OutletContextType>();
  const { capabilities } = useHostContext();

  const fetchData = useCallback(async () => {
    setIsLoading(true);
    try {
        const [statsData, detailsData, jobsData, perfData] = await Promise.all([
            getDashboardStats(),
            getHostDetails(),
            jobService.getStorageJobs().catch(() => []),
            hostService.getPerformanceSummary().catch(() => null)
        ]);
        setStats(statsData);
        setHostDetails(detailsData);
        setPerformance(perfData);
        setJobs(jobsData.sort((a, b) => {
            if (!a.completionTime) return 1;
            if (!b.completionTime) return -1;
            return new Date(b.completionTime).getTime() - new Date(a.completionTime).getTime();
        }));
    } catch (err: any) {
        addNotification('error', `Failed to load host data: ${err.message}`);
    } finally {
        setIsLoading(false);
    }
  }, [addNotification]);
  
  const handleCreationComplete = () => {
    setIsWizardOpen(false);
    fetchData();
  };

  const handleHostPowerAction = async () => {
    if (!confirmAction) return;
    setIsActionBusy(true);
    try {
        if (confirmAction === 'shutdown') {
            await hostService.shutdownHost();
            addNotification('success', 'Host shutdown initiated.');
        } else {
            await hostService.rebootHost();
            addNotification('success', 'Host reboot initiated.');
        }
    } catch (err: any) {
        addNotification('error', `Failed to ${confirmAction} host: ${err.message}`);
    } finally {
        setIsActionBusy(false);
        setConfirmAction(null);
    }
  };

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Real-time updates via SignalR
  useSignalR({
    groups: ['vm-events', 'metrics'],
    onVmStateChanged: () => fetchData(),
    onMetricsUpdate: () => fetchData(),
  });

  if (isLoading || !stats || !hostDetails) {
    return <div className="flex justify-center items-center h-full"><Spinner /></div>;
  }

  return (
    <div className="flex flex-col h-full">
      <header className="p-4 bg-panel-bg border-b border-panel-border flex items-center justify-between flex-shrink-0">
        <div className="flex items-center space-x-3">
            <h1 className="text-lg font-semibold text-gray-800">
                {hostDetails.hostname}
            </h1>
            {capabilities && (
                <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${capabilities.hypervisorType === 'KVM' ? 'bg-orange-100 text-orange-700' : 'bg-blue-100 text-blue-700'}`}>
                    {capabilities.hypervisorType}
                </span>
            )}
        </div>
        <div className="flex items-center space-x-1">
            <Button variant="toolbar" size="md" onClick={() => setIsWizardOpen(true)} leftIcon={<PlusIcon className="h-4 w-4"/>}>Create/Register VM</Button>
            <Button variant="toolbar" size="md" leftIcon={<ShutdownIcon className="h-4 w-4"/>} onClick={() => setConfirmAction('shutdown')}>Shut down</Button>
            <Button variant="toolbar" size="md" leftIcon={<RebootIcon className="h-4 w-4"/>} onClick={() => setConfirmAction('reboot')}>Reboot</Button>
            <Button variant="toolbar" size="md" leftIcon={<RefreshIcon className="h-4 w-4"/>} onClick={fetchData}>Refresh</Button>
            <Button variant="toolbar" size="md" leftIcon={<ActionsIcon className="h-4 w-4"/>}>Actions</Button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto p-4">
        <main className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="bg-panel-bg border border-panel-border rounded-sm p-4 shadow-sm">
                    <ResourceBar
                        title="CPU"
                        used={performance ? (performance.cpuUsagePercent / 100) * (hostDetails.totalMemoryGB > 0 ? 67.2 : 67.2) : 0}
                        capacity={67.2}
                        unit="GHz"
                        percent={performance ? performance.cpuUsagePercent : stats.cpuUsagePercent}
                        colorClass="bg-green-500"
                    />
                </div>
                 <div className="bg-panel-bg border border-panel-border rounded-sm p-4 shadow-sm">
                     <ResourceBar 
                        title="MEMORY" 
                        used={stats.totalMemoryAssignedGB}
                        capacity={stats.totalMemoryCapacityGB} 
                        unit="GB" 
                        percent={Math.round((stats.totalMemoryAssignedGB / stats.totalMemoryCapacityGB) * 100)} 
                        colorClass="bg-blue-500"
                    />
                </div>
                 <div className="bg-panel-bg border border-panel-border rounded-sm p-4 shadow-sm">
                     <ResourceBar 
                        title="STORAGE" 
                        used={stats.storageUsedTB}
                        capacity={stats.storageCapacityTB}
                        unit="TB" 
                        percent={Math.round((stats.storageUsedTB / stats.storageCapacityTB) * 100)}
                        colorClass="bg-purple-500"
                    />
                </div>
            </div>

            {capabilities && (
                <Alert type="info">
                    {capabilities.hypervisorType === 'KVM'
                        ? 'This host is managed by libvirt/KVM. VM operations use the QEMU/KVM hypervisor.'
                        : 'This host is managed by Hyper-V. Actions may be performed automatically by the Hyper-V subsystem in the background.'}
                </Alert>
            )}
            
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                <Card title="Hardware">
                    <dl>
                        <InfoRow label="Manufacturer" value={hostDetails.manufacturer} />
                        <InfoRow label="Model" value={hostDetails.model} />
                        <InfoRow label="CPU" value={hostDetails.cpuInfo} />
                        <InfoRow label="Memory" value={`${hostDetails.totalMemoryGB.toFixed(2)} GB`} />
                    </dl>
                </Card>
                <Card title="System Information">
                    <dl>
                       <InfoRow label="Serial number" value={hostDetails.serialNumber} />
                       <InfoRow label="BIOS version" value={hostDetails.biosVersion} />
                       <InfoRow label="Uptime" value={hostDetails.uptime} />
                       <InfoRow label="Version" value={hostDetails.version} />
                    </dl>
                </Card>
            </div>

            <Card title="Performance summary">
                <div className="space-y-4">
                    <div>
                        <div className="flex justify-between text-sm mb-1">
                            <span className="font-medium text-gray-600">CPU Usage</span>
                            <span className="font-bold text-gray-800">{stats.cpuUsagePercent}%</span>
                        </div>
                        <div className="w-full bg-gray-200 rounded-full h-3">
                            <div className={`h-3 rounded-full transition-all ${stats.cpuUsagePercent > 80 ? 'bg-red-500' : stats.cpuUsagePercent > 50 ? 'bg-yellow-500' : 'bg-green-500'}`} style={{ width: `${stats.cpuUsagePercent}%` }} />
                        </div>
                    </div>
                    <div>
                        <div className="flex justify-between text-sm mb-1">
                            <span className="font-medium text-gray-600">Memory Usage</span>
                            <span className="font-bold text-gray-800">{stats.totalMemoryCapacityGB > 0 ? Math.round((stats.totalMemoryAssignedGB / stats.totalMemoryCapacityGB) * 100) : 0}%</span>
                        </div>
                        <div className="w-full bg-gray-200 rounded-full h-3">
                            <div className="h-3 rounded-full bg-blue-500 transition-all" style={{ width: `${stats.totalMemoryCapacityGB > 0 ? Math.round((stats.totalMemoryAssignedGB / stats.totalMemoryCapacityGB) * 100) : 0}%` }} />
                        </div>
                        <div className="flex justify-between text-xs text-gray-500 mt-1">
                            <span>{stats.totalMemoryAssignedGB.toFixed(1)} GB assigned</span>
                            <span>{stats.totalMemoryCapacityGB.toFixed(1)} GB total</span>
                        </div>
                    </div>
                    <div>
                        <div className="flex justify-between text-sm mb-1">
                            <span className="font-medium text-gray-600">Storage Usage</span>
                            <span className="font-bold text-gray-800">{stats.storageCapacityTB > 0 ? Math.round((stats.storageUsedTB / stats.storageCapacityTB) * 100) : 0}%</span>
                        </div>
                        <div className="w-full bg-gray-200 rounded-full h-3">
                            <div className={`h-3 rounded-full transition-all ${stats.storageCapacityTB > 0 && (stats.storageUsedTB / stats.storageCapacityTB) > 0.85 ? 'bg-red-500' : 'bg-purple-500'}`} style={{ width: `${stats.storageCapacityTB > 0 ? Math.round((stats.storageUsedTB / stats.storageCapacityTB) * 100) : 0}%` }} />
                        </div>
                        <div className="flex justify-between text-xs text-gray-500 mt-1">
                            <span>{stats.storageUsedTB.toFixed(2)} TB used</span>
                            <span>{stats.storageCapacityTB.toFixed(2)} TB total</span>
                        </div>
                    </div>
                    {performance && Object.keys(performance.storageUsagePercent || {}).length > 0 && (
                        <div>
                            <h4 className="text-sm font-medium text-gray-600 mb-2">Storage per Drive</h4>
                            <div className="space-y-2">
                                {Object.entries(performance.storageUsagePercent).map(([drive, pct]) => (
                                    <div key={drive}>
                                        <div className="flex justify-between text-xs mb-0.5">
                                            <span className="text-gray-600">{drive}</span>
                                            <span className="font-medium text-gray-700">{Math.round(pct)}%</span>
                                        </div>
                                        <div className="w-full bg-gray-200 rounded-full h-2">
                                            <div className={`h-2 rounded-full ${pct > 90 ? 'bg-red-500' : pct > 70 ? 'bg-yellow-500' : 'bg-indigo-400'}`} style={{ width: `${Math.round(pct)}%` }} />
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </div>
                    )}
                    <div className="grid grid-cols-3 gap-4 pt-2 border-t border-gray-200">
                        <div className="text-center">
                            <div className="text-2xl font-bold text-gray-800">{stats.runningVms}</div>
                            <div className="text-xs text-gray-500">Running VMs</div>
                        </div>
                        <div className="text-center">
                            <div className="text-2xl font-bold text-gray-800">{stats.totalVms}</div>
                            <div className="text-xs text-gray-500">Total VMs</div>
                        </div>
                        <div className="text-center">
                            <div className="text-2xl font-bold text-gray-800">{stats.totalNetworks}</div>
                            <div className="text-xs text-gray-500">Networks</div>
                        </div>
                    </div>
                </div>
            </Card>

            <Card title="Recent tasks">
                {jobs.length > 0 ? (
                    <div className="overflow-x-auto">
                        <table className="w-full text-sm">
                            <thead className="border-b border-panel-border">
                                <tr>
                                    <th className="py-2 px-3 text-left font-semibold text-gray-600 whitespace-nowrap">Task</th>
                                    <th className="py-2 px-3 text-left font-semibold text-gray-600 whitespace-nowrap">Target</th>
                                    <th className="py-2 px-3 text-left font-semibold text-gray-600 whitespace-nowrap">Initiator</th>
                                    <th className="py-2 px-3 text-left font-semibold text-gray-600 whitespace-nowrap">Queued</th>
                                    <th className="py-2 px-3 text-left font-semibold text-gray-600 whitespace-nowrap">Started</th>
                                    <th className="py-2 px-3 text-left font-semibold text-gray-600 whitespace-nowrap">Result</th>
                                </tr>
                            </thead>
                            <tbody>
                                {jobs.slice(0, 10).map(job => (
                                    <tr key={job.jobId} className="border-b border-gray-200 last:border-b-0 hover:bg-gray-50">
                                        <td className="py-2 px-3 text-gray-700">{job.operationType || 'Storage Operation'}</td>
                                        <td className="py-2 px-3 text-gray-700">N/A</td>
                                        <td className="py-2 px-3 text-gray-700">root</td>
                                        <td className="py-2 px-3 text-gray-700">{job.completionTime ? new Date(job.completionTime).toLocaleTimeString() : 'Pending'}</td>
                                        <td className="py-2 px-3 text-gray-700">{job.completionTime ? new Date(job.completionTime).toLocaleString() : 'Pending'}</td>
                                        <td className="py-2 px-3 text-gray-700">
                                            {job.status === 'Completed' ? 
                                                <span className="text-green-600 flex items-center">
                                                    <CheckCircleIcon className="h-4 w-4 mr-1" />
                                                    Completed
                                                </span> 
                                                : job.status}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : (
                    <div className="text-center py-4 text-gray-500">No recent tasks.</div>
                )}
            </Card>
        </main>
      </div>
      {isWizardOpen && <CreateVmWizard isOpen={isWizardOpen} onClose={() => setIsWizardOpen(false)} onComplete={handleCreationComplete} />}

      <Modal isOpen={confirmAction !== null} onClose={() => setConfirmAction(null)} title={`Confirm ${confirmAction === 'shutdown' ? 'Shutdown' : 'Reboot'}`}>
        <div className="space-y-4">
            <p className="text-sm text-gray-700">
                Are you sure you want to <strong>{confirmAction}</strong> this host?
                {confirmAction === 'shutdown'
                    ? ' All running VMs will be stopped and the host will power off.'
                    : ' All running VMs will be stopped and the host will restart.'}
            </p>
            <div className="flex justify-end space-x-2">
                <Button variant="toolbar" onClick={() => setConfirmAction(null)} disabled={isActionBusy}>Cancel</Button>
                <Button variant="danger" onClick={handleHostPowerAction} disabled={isActionBusy}>
                    {isActionBusy ? <Spinner size="sm" /> : `Yes, ${confirmAction === 'shutdown' ? 'Shut down' : 'Reboot'}`}
                </Button>
            </div>
        </div>
      </Modal>
    </div>
  );
};
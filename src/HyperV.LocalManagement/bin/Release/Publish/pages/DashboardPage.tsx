import React, { useEffect, useState, useCallback } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Card } from '../components/Card';
import { Spinner } from '../components/Spinner';
import { getDashboardStats, getHostDetails, jobService } from '../services/hypervService';
import { Button } from '../components/Button';
// DO add comment above each fix.
// Fix: Added missing CheckCircleIcon to the imports from Icons component.
import { ShutdownIcon, RebootIcon, RefreshIcon, ActionsIcon, PlusIcon, CheckCircleIcon } from '../components/Icons';
import { OutletContextType } from '../App';
import { HostDetails, StorageJob, Stats } from '../types';
import { CreateVmWizard } from '../components/CreateVmWizard';
import { Alert } from '../components/Alert';

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
  const [isLoading, setIsLoading] = useState(true);
  const [isWizardOpen, setIsWizardOpen] = useState(false);
  const { addNotification } = useOutletContext<OutletContextType>();

  const fetchData = useCallback(async () => {
    setIsLoading(true);
    try {
        const [statsData, detailsData, jobsData] = await Promise.all([
            getDashboardStats(),
            getHostDetails(),
            jobService.getStorageJobs().catch(() => [])
        ]);
        setStats(statsData);
        setHostDetails(detailsData);
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

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  if (isLoading || !stats || !hostDetails) {
    return <div className="flex justify-center items-center h-full"><Spinner /></div>;
  }

  return (
    <div className="flex flex-col h-full">
      <header className="p-4 bg-panel-bg border-b border-panel-border flex items-center justify-between flex-shrink-0">
        <h1 className="text-lg font-semibold text-gray-800">
            {hostDetails.hostname}
        </h1>
        <div className="flex items-center space-x-1">
            <Button variant="toolbar" size="md" onClick={() => setIsWizardOpen(true)} leftIcon={<PlusIcon className="h-4 w-4"/>}>Create/Register VM</Button>
            <Button variant="toolbar" size="md" leftIcon={<ShutdownIcon className="h-4 w-4"/>}>Shut down</Button>
            <Button variant="toolbar" size="md" leftIcon={<RebootIcon className="h-4 w-4"/>}>Reboot</Button>
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
                        used={((stats.totalMemoryCapacityGB * 1024 * 0.11) / (2.4 * 28))} 
                        capacity={2.4*28}
                        unit="GHz" 
                        percent={stats.cpuUsagePercent} 
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

            <Alert type="info">
                This host is being managed by a Hyper-V Cluster. Actions may be performed automatically by Hyper-V subsystem in a background.
            </Alert>
            
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

            <Card title="Performance summary last hour" className="h-72">
                <div className="h-full flex items-center justify-center text-gray-400">
                    Performance Chart Placeholder
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
    </div>
  );
};
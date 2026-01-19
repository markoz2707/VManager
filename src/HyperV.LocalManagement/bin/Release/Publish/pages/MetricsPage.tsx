import React, { useCallback, useEffect, useState, useRef } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { Spinner } from '../components/Spinner';
import { OutletContextType } from '../App';
import * as api from '../services/hypervService';
import { VmUsageSummary, HostUsageSummary, VirtualMachine, VmStatus } from '../types';
import { 
    ChipIcon, 
    MemoryChipIcon, 
    HardDiskIconSimple, 
    NetworkIcon, 
    ScsiControllerIcon, 
    RefreshIcon,
    HostIcon
} from '../components/Icons';

type MetricScope = 'host' | 'vm';

const UsageBar = ({ label, percent, value, color }: { label: string, percent: number, value: string, color: string }) => (
    <div className="mb-4">
        <div className="flex justify-between mb-1">
            <span className="text-sm font-medium text-gray-700">{label}</span>
            <span className="text-sm font-bold text-gray-900">{value} ({Math.round(percent)}%)</span>
        </div>
        <div className="w-full bg-gray-200 rounded-full h-2.5">
            <div 
                className={`${color} h-2.5 rounded-full transition-all duration-500`} 
                style={{ width: `${Math.min(percent, 100)}%` }}
            ></div>
        </div>
    </div>
);

const MetricTable = ({ title, icon, headers, rows }: { title: string, icon: React.ReactNode, headers: string[], rows: any[][] }) => (
    <Card title={<div className="flex items-center gap-2">{icon} {title}</div>}>
        <div className="overflow-x-auto">
            <table className="min-w-full text-xs">
                <thead>
                    <tr className="border-b border-gray-200">
                        {headers.map(h => <th key={h} className="text-left py-2 font-semibold text-gray-500 uppercase">{h}</th>)}
                    </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                    {rows.length > 0 ? rows.map((row, idx) => (
                        <tr key={idx} className="hover:bg-gray-50">
                            {row.map((cell, cidx) => <td key={cidx} className="py-2 text-gray-700 font-mono">{cell}</td>)}
                        </tr>
                    )) : (
                        <tr><td colSpan={headers.length} className="py-4 text-center text-gray-400">No active device metrics</td></tr>
                    )}
                </tbody>
            </table>
        </div>
    </Card>
);

const formatBytes = (bytes: number, decimals = 2) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
};

const formatBits = (bits: number) => {
    if (bits === 0) return '0 bps';
    const sizes = ['bps', 'Kbps', 'Mbps', 'Gbps'];
    const i = Math.floor(Math.log(bits) / Math.log(1000));
    return parseFloat((bits / Math.pow(1000, i)).toFixed(2)) + ' ' + sizes[i];
};

export const MetricsPage: React.FC = () => {
    const { addNotification } = useOutletContext<OutletContextType>();
    const [scope, setScope] = useState<MetricScope>('vm');
    const [vms, setVms] = useState<VirtualMachine[]>([]);
    const [selectedVm, setSelectedVm] = useState<string>('');
    const [vmUsage, setVmUsage] = useState<VmUsageSummary | null>(null);
    const [hostUsage, setHostUsage] = useState<HostUsageSummary | null>(null);
    const [isLoading, setIsLoading] = useState(false);
    const [isPolling, setIsPolling] = useState(false);
    const pollingInterval = useRef<number | null>(null);

    const fetchVms = useCallback(async () => {
        setIsLoading(true);
        try {
            const vmsData = await api.vmService.getVms();
            const allVms = [...vmsData.WMI, ...vmsData.HCS];
            setVms(allVms);
            if (allVms.length > 0 && !selectedVm) {
                const running = allVms.find(v => v.status === VmStatus.RUNNING);
                setSelectedVm(running ? running.name : allVms[0].name);
            }
        } catch (err: any) {
            addNotification('error', `Failed to load VMs: ${err.message}`);
        } finally {
            setIsLoading(false);
        }
    }, [addNotification, selectedVm]);

    const fetchAllMetrics = useCallback(async () => {
        try {
            if (scope === 'vm' && selectedVm) {
                const data = await api.metricsService.getVmUsageSummary(selectedVm);
                setVmUsage(data);
            } else if (scope === 'host') {
                const data = await api.hostService.getHostUsageSummary();
                setHostUsage(data);
            }
        } catch (err: any) {
            if (!isPolling) {
                addNotification('error', `Failed to fetch metrics: ${err.message}`);
            }
        }
    }, [scope, selectedVm, isPolling, addNotification]);

    useEffect(() => {
        fetchVms();
    }, [fetchVms]);

    // Polling logic
    useEffect(() => {
        if (isPolling) {
            fetchAllMetrics();
            pollingInterval.current = window.setInterval(fetchAllMetrics, 5000);
        } else {
            if (pollingInterval.current) {
                clearInterval(pollingInterval.current);
                pollingInterval.current = null;
            }
        }
        return () => {
            if (pollingInterval.current) clearInterval(pollingInterval.current);
        };
    }, [isPolling, fetchAllMetrics]);

    const togglePolling = () => {
        if (!isPolling) fetchAllMetrics();
        setIsPolling(!isPolling);
    };

    const getCpuColor = (p: number) => p > 90 ? 'bg-red-500' : p > 70 ? 'bg-yellow-500' : 'bg-green-500';

    const renderVmMetrics = () => {
        if (!vmUsage) return (
            <div className="p-12 text-center bg-white border border-dashed border-gray-300 rounded-lg text-gray-500">
                Select a running VM and start Live View to see resource usage metrics.
                <p className="text-xs mt-2 italic">Note: Metric collection must be enabled on the Hyper-V host for this VM.</p>
            </div>
        );

        return (
            <div className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <Card title={<div className="flex items-center gap-2"><ChipIcon className="h-5 w-5"/> VM Processor Usage</div>}>
                        <UsageBar 
                            label="Hypervisor CPU" 
                            percent={vmUsage.cpu.usagePercent} 
                            value={`${vmUsage.cpu.usagePercent}%`} 
                            color={getCpuColor(vmUsage.cpu.usagePercent)}
                        />
                        <div className="text-xs text-gray-500 flex justify-between">
                            <span>Guest Avg: {vmUsage.cpu.guestAverageUsage}%</span>
                            <span>Last Sample: {new Date(vmUsage.timestamp).toLocaleTimeString()}</span>
                        </div>
                    </Card>
                    <Card title={<div className="flex items-center gap-2"><MemoryChipIcon className="h-5 w-5"/> VM Memory Utilization</div>}>
                        <UsageBar 
                            label="Memory Pressure" 
                            percent={vmUsage.memory.usagePercent} 
                            value={`${vmUsage.memory.demandMB} MB`} 
                            color={getCpuColor(vmUsage.memory.usagePercent)}
                        />
                        <div className="text-xs text-gray-500 flex justify-between">
                            <span>Assigned: {vmUsage.memory.assignedMB} MB</span>
                            <span>Status: <span className={vmUsage.memory.status === 'Healthy' ? 'text-green-600' : 'text-yellow-600'}>{vmUsage.memory.status}</span></span>
                        </div>
                    </Card>
                </div>

                <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                    <MetricTable 
                        title="Virtual Hard Disks (I/O)" 
                        icon={<HardDiskIconSimple className="h-5 w-5 text-gray-600"/>}
                        headers={['Disk', 'Read IOPS', 'Write IOPS', 'Throughput', 'Latency']}
                        rows={vmUsage.disks.map(d => [
                            d.name,
                            d.readIops,
                            d.writeIops,
                            `${formatBytes(d.throughputBytesPerSec)}/s`,
                            `${d.latencyMs}ms`
                        ])}
                    />
                    <MetricTable 
                        title="Network Adapters (Traffic)" 
                        icon={<NetworkIcon className="h-5 w-5 text-gray-600"/>}
                        headers={['Adapter', 'Sent', 'Received', 'Dropped']}
                        rows={vmUsage.networks.map(n => [
                            n.adapterName,
                            `${formatBytes(n.bytesSentPerSec)}/s`,
                            `${formatBytes(n.bytesReceivedPerSec)}/s`,
                            n.packetsDropped
                        ])}
                    />
                </div>

                <MetricTable 
                    title="VM Storage Controllers" 
                    icon={<ScsiControllerIcon className="h-5 w-5 text-gray-600"/>}
                    headers={['Controller', 'Queue Depth', 'Total Throughput', 'Error Count']}
                    rows={vmUsage.storageAdapters.map(s => [
                        s.name,
                        s.queueDepth,
                        `${formatBytes(s.throughputBytesPerSec)}/s`,
                        s.errorsCount
                    ])}
                />
            </div>
        );
    };

    const renderHostMetrics = () => {
        if (!hostUsage) return (
            <div className="p-12 text-center bg-white border border-dashed border-gray-300 rounded-lg text-gray-500">
                Start Live View to see physical host resource metrics.
            </div>
        );

        return (
            <div className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <Card title={<div className="flex items-center gap-2"><ChipIcon className="h-5 w-5"/> Physical CPU Load</div>}>
                        <UsageBar 
                            label="Host CPU" 
                            percent={hostUsage.cpu.usagePercent} 
                            value={`${hostUsage.cpu.usagePercent}%`} 
                            color={getCpuColor(hostUsage.cpu.usagePercent)}
                        />
                        <div className="text-xs text-gray-500 flex justify-between">
                            <span>Cores: {hostUsage.cpu.cores}</span>
                            <span>Logical Processors: {hostUsage.cpu.logicalProcessors}</span>
                        </div>
                    </Card>
                    <Card title={<div className="flex items-center gap-2"><MemoryChipIcon className="h-5 w-5"/> Physical Memory Usage</div>}>
                        <UsageBar 
                            label="RAM Occupancy" 
                            percent={hostUsage.memory.usagePercent} 
                            value={`${hostUsage.memory.usedMB} MB`} 
                            color={getCpuColor(hostUsage.memory.usagePercent)}
                        />
                        <div className="text-xs text-gray-500 flex justify-between">
                            <span>Available: {hostUsage.memory.availableMB} MB</span>
                            <span>Total Physical: {hostUsage.memory.totalPhysicalMB} MB</span>
                        </div>
                    </Card>
                </div>

                <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                    <MetricTable 
                        title="Physical Disks (System & Data)" 
                        icon={<HardDiskIconSimple className="h-5 w-5 text-gray-600"/>}
                        headers={['Disk ID', 'Read IOPS', 'Write IOPS', 'Latency', 'Queue']}
                        rows={hostUsage.physicalDisks.map(d => [
                            d.diskId,
                            d.readIops,
                            d.writeIops,
                            `${d.latencyMs}ms`,
                            d.queueLength
                        ])}
                    />
                    <MetricTable 
                        title="Physical Network Interfaces" 
                        icon={<NetworkIcon className="h-5 w-5 text-gray-600"/>}
                        headers={['Interface', 'Speed', 'Sent', 'Received', 'Status']}
                        rows={hostUsage.networkAdapters.map(n => [
                            n.name,
                            formatBits(n.speedBitsPerSec),
                            `${formatBytes(n.bytesSentPerSec)}/s`,
                            `${formatBytes(n.bytesReceivedPerSec)}/s`,
                            <span className={n.status === 'Up' ? 'text-green-600 font-bold' : 'text-red-600'}>{n.status}</span>
                        ])}
                    />
                </div>

                <MetricTable 
                    title="Host Storage Adapters (HBAs / Controllers)" 
                    icon={<ScsiControllerIcon className="h-5 w-5 text-gray-600"/>}
                    headers={['Adapter', 'Manufacturer', 'Throughput', 'Status']}
                    rows={hostUsage.storageAdapters.map(s => [
                        s.name,
                        s.manufacturer,
                        `${formatBytes(s.throughputBytesPerSec)}/s`,
                        <span className={s.status === 'OK' ? 'text-green-600' : 'text-yellow-600'}>{s.status}</span>
                    ])}
                />
            </div>
        );
    };

    return (
        <div className="flex flex-col h-full bg-gray-50">
            <header className="p-4 bg-white border-b border-panel-border flex flex-col sm:flex-row items-center justify-between flex-shrink-0 shadow-sm gap-4">
                <div className="flex items-center gap-4">
                    <h1 className="text-lg font-semibold text-gray-800">Performance Monitor</h1>
                    
                    <div className="flex bg-gray-100 rounded-md p-1 shadow-inner">
                        <button 
                            onClick={() => { setScope('vm'); setVmUsage(null); }}
                            className={`px-3 py-1 text-xs font-bold rounded-sm transition-all ${scope === 'vm' ? 'bg-white text-primary shadow-sm' : 'text-gray-500 hover:text-gray-700'}`}
                        >
                            VM WORKLOADS
                        </button>
                        <button 
                            onClick={() => { setScope('host'); setHostUsage(null); }}
                            className={`px-3 py-1 text-xs font-bold rounded-sm transition-all ${scope === 'host' ? 'bg-white text-primary shadow-sm' : 'text-gray-500 hover:text-gray-700'}`}
                        >
                            PHYSICAL HOST
                        </button>
                    </div>

                    {scope === 'vm' && (
                        <select 
                            className="bg-white border border-gray-300 rounded-md shadow-sm py-1 px-3 text-sm focus:ring-primary-500 focus:border-primary-500"
                            value={selectedVm}
                            onChange={e => {
                                setSelectedVm(e.target.value);
                                setVmUsage(null);
                            }}
                        >
                            {vms.map(vm => <option key={vm.id} value={vm.name}>{vm.name} ({vm.status})</option>)}
                        </select>
                    )}
                </div>
                <div className="flex items-center gap-2">
                    <Button 
                        variant={isPolling ? "secondary" : "primary"} 
                        size="sm" 
                        onClick={togglePolling}
                        leftIcon={<RefreshIcon className={`h-4 w-4 ${isPolling ? 'animate-spin' : ''}`} />}
                    >
                        {isPolling ? 'Stop Live View' : 'Start Live View'}
                    </Button>
                </div>
            </header>

            <main className="flex-1 overflow-y-auto p-4 space-y-4">
                {isLoading && !vms.length ? (
                    <div className="flex justify-center items-center h-64"><Spinner /></div>
                ) : (
                    scope === 'vm' ? renderVmMetrics() : renderHostMetrics()
                )}
            </main>
        </div>
    );
};
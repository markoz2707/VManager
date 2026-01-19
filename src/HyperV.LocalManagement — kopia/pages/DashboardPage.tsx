import React, { useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Spinner } from '../components/Spinner';
import { getHostDetails } from '../services/hypervService';
import { OutletContextType } from '../App';
import { Gauge } from '../components/Widgets/Gauge';
import { ServerIcon, ChipIcon, VmIcon } from '../components/Icons';

export const DashboardPage = () => {
  const [hostData, setHostData] = useState<any | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const { addNotification } = useOutletContext<OutletContextType>();

  useEffect(() => {
    const fetchStats = async () => {
      try {
        const data = await getHostDetails();
        setHostData(data);
      } catch (err: any) {
        addNotification('error', `Failed to load host details: ${err.message}`);
      } finally {
        setIsLoading(false);
      }
    };

    fetchStats();
    const interval = setInterval(fetchStats, 5000); // Refresh every 5s
    return () => clearInterval(interval);
  }, [addNotification]);

  if (isLoading && !hostData) {
    return <div className="flex justify-center items-center h-full"><Spinner /></div>;
  }

  if (!hostData) {
    return <div className="p-6 text-center text-gray-400">Could not load dashboard stats.</div>
  }

  const { hardware, system, performance } = hostData;

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header Section */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-white flex items-center">
            <ServerIcon className="w-6 h-6 mr-2 text-blue-500" />
            {system.computerName}
          </h2>
          <p className="text-slate-400 text-sm mt-1">
            {system.osName} | Uptime: {system.uptime}
          </p>
        </div>
        <div className="flex space-x-4">
          <div className="glass-panel px-4 py-2 flex flex-col items-center">
            <span className="text-xs text-slate-400">Processor</span>
            <span className="font-mono text-sm text-white">{hardware.processorModel}</span>
          </div>
          <div className="glass-panel px-4 py-2 flex flex-col items-center">
            <span className="text-xs text-slate-400">Memory</span>
            <span className="font-mono text-sm text-white">{Math.round(hardware.totalMemoryGB)} GB</span>
          </div>
        </div>
      </div>

      {/* Performance Gauges */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="glass-panel p-6 flex flex-col items-center">
          <Gauge
            value={performance.cpuLoad}
            label="CPU Usage"
            color={performance.cpuLoad > 80 ? '#ef4444' : '#3b82f6'}
          />
        </div>
        <div className="glass-panel p-6 flex flex-col items-center">
          <Gauge
            value={performance.memoryLoad}
            label="Memory Usage"
            subLabel={`${(performance.availableMemoryGB).toFixed(1)} GB Free`}
            color={performance.memoryLoad > 90 ? '#ef4444' : '#10b981'}
          />
        </div>
        <div className="glass-panel p-6 flex flex-col items-center justify-center space-y-4">
          <div className="text-center">
            <div className="text-slate-400 text-sm mb-1">Disk Activity</div>
            <div className="text-2xl font-bold text-white">
              {/* Mock data if not available in performance summary yet */}
              {performance.diskActivity || 0}%
            </div>
          </div>
          <div className="w-full bg-slate-700 h-2 rounded-full overflow-hidden">
            <div
              className="bg-purple-500 h-full transition-all duration-500"
              style={{ width: `${performance.diskActivity || 0}%` }}
            />
          </div>
        </div>
      </div>

      {/* Quick Stats Row */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="glass-panel p-4 flex items-center space-x-4 hover:bg-slate-800/50 transition-colors cursor-pointer">
          <div className="p-3 bg-green-500/20 rounded-lg">
            <VmIcon className="w-6 h-6 text-green-400" />
          </div>
          <div>
            <div className="text-2xl font-bold text-white">--</div>
            <div className="text-xs text-slate-400">Running VMs</div>
          </div>
        </div>

        <div className="glass-panel p-4 flex items-center space-x-4 hover:bg-slate-800/50 transition-colors cursor-pointer">
          <div className="p-3 bg-blue-500/20 rounded-lg">
            <ChipIcon className="w-6 h-6 text-blue-400" />
          </div>
          <div>
            <div className="text-2xl font-bold text-white">{hardware.logicalProcessors}</div>
            <div className="text-xs text-slate-400">Logical Cores</div>
          </div>
        </div>
      </div>
    </div>
  );
};

import React from 'react';
import type { Host } from '../../../types';
import { MoreVerticalIcon, ServerIcon } from '../../../components/icons/Icons';

const Card: React.FC<{ title: string; children: React.ReactNode; className?: string }> = ({ title, children, className }) => (
  <div className={`bg-white border border-gray-200 rounded ${className}`}>
    <div className="flex justify-between items-center p-3 border-b border-gray-200">
      <h4 className="text-sm font-semibold">{title}</h4>
      <button className="text-gray-400 hover:text-gray-600">
        <MoreVerticalIcon className="w-4 h-4" />
      </button>
    </div>
    <div className="p-3 text-sm">{children}</div>
  </div>
);

const InfoBar: React.FC<{ value: number; total: number; unit: string, label: string }> = ({ value, total, unit, label }) => {
  const percentage = total > 0 ? (value / total) * 100 : 0;
  return (
    <div>
      <div className="text-xs font-bold mb-1">{label}</div>
      <div className="w-full bg-gray-200 rounded-sm h-3 relative">
        <div className="bg-blue-500 h-3 rounded-sm" style={{ width: `${percentage}%` }}></div>
      </div>
      <div className="flex justify-between text-xs text-gray-500 mt-1">
        <span>{value.toFixed(2)} {unit} used</span>
        <span>{(total - value).toFixed(2)} {unit} free</span>
      </div>
       <div className="text-right text-xs font-bold text-gray-600 mt-1">
         {total.toFixed(2)} {unit} capacity
      </div>
    </div>
  );
};

const DetailItem: React.FC<{ label: string, value: string | number | React.ReactNode}> = ({ label, value }) => (
    <div className="flex justify-between py-1.5">
        <span className="text-gray-500">{label}</span>
        <span className="font-medium text-right">{value}</span>
    </div>
)

export const HostSummaryTab: React.FC<{host: Host}> = ({ host }) => (
    <div className="space-y-4">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <Card title="Hyper-V Host Details" className="lg:col-span-1">
                 <div className="flex items-start">
                    <ServerIcon className="w-8 h-8 text-blue-500 mr-3 mt-1"/>
                    <div>
                        <DetailItem label="Hypervisor" value={host.hypervisor} />
                        <DetailItem label="Model" value={host.model} />
                        <DetailItem label="Processor Type" value={host.processorType} />
                        <DetailItem label="Logical Processors" value={host.logicalProcessors} />
                        <DetailItem label="NICs" value={host.nics} />
                        <DetailItem label="Virtual Machines" value={host.vmCount} />
                        <DetailItem label="State" value={host.state} />
                        <DetailItem label="Uptime" value={host.uptime} />
                    </div>
                </div>
            </Card>
             <Card title="Capacity and Usage" className="lg:col-span-3">
                <p className="text-xs text-gray-400 mb-4">Last updated at {new Date().toLocaleTimeString()}</p>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                    <InfoBar label="CPU" value={host.cpuUsage} total={host.totalCpu} unit="GHz" />
                    <InfoBar label="Memory" value={host.memoryUsage} total={host.totalMemory} unit="GB" />
                    <InfoBar label="Storage" value={host.storageUsage / 1024} total={host.totalStorage / 1024} unit="TB" />
                </div>
            </Card>
        </div>
    </div>
);

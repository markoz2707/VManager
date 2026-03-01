import React from 'react';
import type { VmSummaryData, VirtualMachine } from '../../../types';
import {
    MoreVerticalIcon, InfoCircleIcon, RemoteConsoleIcon, WebConsoleIcon, CpuChipIcon, MemoryStickIcon,
    HardDiskIcon, ClusterIcon, HostIcon, ResourcePoolIcon, NetworkIcon, CheckCircleIcon,
    MinusCircleIcon, TagIcon, PencilIcon
} from '../../../components/icons/Icons';

const Card: React.FC<{ title: string; children?: React.ReactNode; className?: string, footer?: React.ReactNode, actions?: React.ReactNode }> = ({ title, children, className, footer, actions }) => (
  <div className={`bg-white border border-gray-200 rounded-md flex flex-col ${className}`}>
    <div className="flex justify-between items-center p-3 border-b border-gray-200">
      <h4 className="text-sm font-semibold text-gray-700">{title}</h4>
      <div className="flex items-center space-x-2">
        {actions}
        <button className="text-gray-400 hover:text-gray-600">
          <MoreVerticalIcon className="w-4 h-4" />
        </button>
      </div>
    </div>
    <div className="p-4 text-sm flex-grow">
        {children}
    </div>
    {footer && <div className="p-3 border-t border-gray-200 text-xs">{footer}</div>}
  </div>
);

const DetailItem: React.FC<{ label: string, value: string | number | React.ReactNode, icon?: React.ReactNode, valueClassName?: string }> = ({ label, value, icon, valueClassName }) => (
    <div className="flex items-start justify-between py-1.5">
        <div className="flex items-center">
            {icon && <span className="mr-2 text-gray-400">{icon}</span>}
            <span className="text-gray-500">{label}</span>
        </div>
        <div className={`font-medium text-right ${valueClassName || 'text-gray-800'}`}>{value}</div>
    </div>
);

const LinkButton: React.FC<{children: React.ReactNode}> = ({ children }) => (
    <button className="font-semibold text-blue-600 hover:underline">{children}</button>
);

const HaStatus: React.FC<{status: string}> = ({status}) => {
    const isEnabled = status === 'Protected' || status.includes('Restart') || status === 'Enabled';
    return (
        <div className="flex items-center">
            {isEnabled ? <CheckCircleIcon className="w-4 h-4 text-green-500 mr-2"/> : <MinusCircleIcon className="w-4 h-4 text-gray-400 mr-2"/>}
            {status}
        </div>
    )
}

const GuestOsCard: React.FC<{ summary: VmSummaryData }> = ({ summary }) => (
  <Card title="Guest OS">
    <div className="flex flex-col items-center justify-center h-full space-y-3">
        <div className="rounded-md border-2 border-gray-300 w-full h-48 bg-gray-800 flex items-center justify-center text-gray-400 text-sm">
          Console Preview
        </div>
        <div className="w-full space-y-2 pt-2">
             <button className="w-full flex items-center justify-center px-3 py-1.5 border border-gray-300 text-gray-700 rounded text-sm hover:bg-gray-100 transition-colors font-semibold shadow-sm">
                <RemoteConsoleIcon className="w-4 h-4 mr-2" />
                LAUNCH REMOTE CONSOLE
             </button>
             <button className="w-full flex items-center justify-center px-3 py-1.5 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 transition-colors font-semibold shadow-sm">
                 <WebConsoleIcon className="w-4 h-4 mr-2" />
                LAUNCH WEB CONSOLE
             </button>
        </div>
    </div>
  </Card>
);

const VmDetailsCard: React.FC<{ summary: VmSummaryData }> = ({ summary }) => (
    <Card title="Virtual Machine Details" actions={<LinkButton>ACTIONS</LinkButton>}>
        <DetailItem label="Power Status" value={summary.details.powerStatus} />
        <DetailItem label="Guest OS" value={summary.details.guestOs} />
        <DetailItem label="Integration Services" value={<>{summary.details.integrationServices} <InfoCircleIcon className="w-4 h-4 inline-block text-blue-500" /></>} />
        <DetailItem label="DNS Name" value={summary.details.dnsName} />
        <DetailItem label="IP Addresses" value={<div className="flex flex-col items-end">{summary.details.ipAddresses.slice(0,2).map(ip => <div key={ip}>{ip}</div>)}{summary.details.ipAddresses.length > 2 && <LinkButton>AND {summary.details.ipAddresses.length - 2} MORE</LinkButton>}</div>} />
        <DetailItem label="Encryption" value={summary.details.encryption} />
    </Card>
);

const UsageCard: React.FC<{ summary: VmSummaryData }> = ({ summary }) => (
    <Card title="Usage" footer={<div className="text-center"><LinkButton>VIEW STATS</LinkButton></div>}>
        <p className="text-xs text-gray-400 mb-4">Last updated: {summary.usage.lastUpdated}</p>
        <div className="space-y-4">
            <div className="flex items-center">
                <CpuChipIcon className="w-8 h-8 text-gray-500 mr-4" />
                <div>
                    <p className="text-gray-500 text-xs">CPU</p>
                    <p className="text-lg font-light"><span className="font-semibold">{summary.usage.cpu.usedMhz}</span> MHz <span className="text-gray-500">used</span></p>
                </div>
            </div>
             <div className="flex items-center">
                <MemoryStickIcon className="w-8 h-8 text-gray-500 mr-4" />
                <div>
                    <p className="text-gray-500 text-xs">Memory</p>
                    <p className="text-lg font-light"><span className="font-semibold">{summary.usage.memory.usedMb}</span> MB <span className="text-gray-500">used</span></p>
                </div>
            </div>
             <div className="flex items-center">
                <HardDiskIcon className="w-8 h-8 text-gray-500 mr-4" />
                <div>
                    <p className="text-gray-500 text-xs">Storage</p>
                    <p className="text-lg font-light"><span className="font-semibold">{summary.usage.storage.usedGb}</span> GB <span className="text-gray-500">used</span></p>
                </div>
            </div>
        </div>
    </Card>
);

const VmHardwareCard: React.FC<{ summary: VmSummaryData }> = ({ summary }) => (
    <Card title="VM Hardware" footer={<div className="text-center"><LinkButton>EDIT</LinkButton></div>}>
        <DetailItem label="CPU" value={summary.hardwareSummary.cpu}/>
        <DetailItem label="Memory" value={summary.hardwareSummary.memory}/>
        <DetailItem label="Hard disk 1" value={<>{summary.hardwareSummary.hardDisk} <InfoCircleIcon className="w-4 h-4 inline-block text-blue-500" /></>}/>
        <DetailItem label="Network adapter 1" value={<LinkButton>{summary.hardwareSummary.networkAdapter}</LinkButton>}/>
        <DetailItem label="CD/DVD drive 1" value={summary.hardwareSummary.cdDvdDrive}/>
        <DetailItem label="VM Generation" value={summary.hardwareSummary.generation}/>
    </Card>
);

const PciDevicesCard: React.FC = () => (
    <Card title="PCI Devices" footer={<div className="text-center"><LinkButton>EDIT</LinkButton></div>}>
        <div className="text-center text-gray-500 flex flex-col items-center justify-center h-full">
            <InfoCircleIcon className="w-10 h-10 text-gray-400 mb-2" />
            <p>No PCI devices</p>
        </div>
    </Card>
);

const RelatedObjectsCard: React.FC<{ summary: VmSummaryData }> = ({ summary }) => (
    <Card title="Related Objects">
        <DetailItem label="Cluster" icon={<ClusterIcon className="w-4 h-4" />} value={<LinkButton>{summary.relatedObjects.cluster.name}</LinkButton>} />
        <DetailItem label="Hyper-V Host" icon={<HostIcon className="w-4 h-4" />} value={<LinkButton>{summary.relatedObjects.host.name}</LinkButton>} />
        <DetailItem label="Resource pool" icon={<ResourcePoolIcon className="w-4 h-4" />} value={<LinkButton>{summary.relatedObjects.resourcePool.name}</LinkButton>} />
        <DetailItem label="Networks" icon={<NetworkIcon className="w-4 h-4" />} value={<div className="flex flex-col items-end">{summary.relatedObjects.networks.map(n => <LinkButton key={n.name}>{n.name}</LinkButton>)}</div>} />
        <DetailItem label="Storage" icon={<HardDiskIcon className="w-4 h-4" />} value={<LinkButton>{summary.relatedObjects.storage.name}</LinkButton>} />
    </Card>
);

const StoragePoliciesCard: React.FC<{ summary: VmSummaryData }> = ({ summary }) => (
  <Card title="Storage Policies" footer={<div className="text-center"><LinkButton>CHECK COMPLIANCE</LinkButton></div>}>
    <DetailItem label="VM Storage Policies" value={summary.storagePolicies.vmStoragePolicies} />
    <DetailItem label="VM Storage Policy Compliance" value={summary.storagePolicies.vmStoragePolicyCompliance} />
    <DetailItem label="Last Checked Date" value={summary.storagePolicies.lastCheckedDate || '--'} />
  </Card>
);

const CustomAttributesCard: React.FC<{ summary: VmSummaryData }> = ({ summary }) => (
  <Card title="Custom Attributes" footer={<div className="text-center"><LinkButton>EDIT</LinkButton></div>}>
    {Object.entries(summary.customAttributes).map(([key, value]) => (
      <DetailItem key={key} label={key} value={value || '--'} />
    ))}
  </Card>
);

const SnapshotsCard: React.FC<{ summary: VmSummaryData }> = ({ summary }) => (
  <Card title="Snapshots" footer={<div className="flex justify-center space-x-4"><LinkButton>MANAGE</LinkButton><LinkButton>VIEW ALL</LinkButton></div>}>
    <div className="flex items-center justify-around">
      <div>
        <p className="text-3xl font-light">{summary.snapshots.count}</p>
        <p className="text-xs text-gray-500">Count</p>
      </div>
      <div>
        <p className="text-3xl font-light">{summary.snapshots.diskUsedGb}</p>
        <p className="text-xs text-gray-500">Disk used</p>
      </div>
    </div>
    <hr className="my-3 border-gray-200" />
    <p className="font-semibold text-xs mb-1">Latest snapshot</p>
    <p>{summary.snapshots.latest.name}</p>
    <p className="text-gray-500">{summary.snapshots.latest.sizeGb} GB</p>
    <p className="text-gray-500">{summary.snapshots.latest.date}</p>
  </Card>
);

const ClusterHaCard: React.FC<{ summary: VmSummaryData }> = ({ summary }) => (
  <Card title="Cluster HA">
    <DetailItem label="Cluster HA Protection" value={<><HaStatus status={summary.clusterHa.protection} /> <InfoCircleIcon className="w-4 h-4 inline-block text-blue-500" /></>} />
    <DetailItem label="Proactive HA" value={<HaStatus status={summary.clusterHa.proactiveHa} />} />
    <DetailItem label="Host failure" value={<HaStatus status={summary.clusterHa.hostFailure} />} />
    <DetailItem label="Host Isolation" value={<HaStatus status={summary.clusterHa.hostIsolation} />} />
    <DetailItem label="Storage - Permanent Device Loss" value={<HaStatus status={summary.clusterHa.storagePermanentDeviceLoss} />} />
    <DetailItem label="Storage - All Paths Down" value={<HaStatus status={summary.clusterHa.storageAllPathsDown} />} />
  </Card>
);

const CpuTopologyCard: React.FC<{ summary: VmSummaryData }> = ({ summary }) => (
  <Card title="CPU Topology">
    <DetailItem label="CPU topology" value={<>Assigned at power on <InfoCircleIcon className="w-4 h-4 inline-block text-blue-500" /></>} />
    <DetailItem label="vCPUs" value={summary.cpuTopology.vCpus} />
    <DetailItem label="Cores per socket" value={<>{summary.cpuTopology.coresPerSocket} (Sockets: 1) <InfoCircleIcon className="w-4 h-4 inline-block text-blue-500" /></>} />
    <DetailItem label="Threads per core" value={summary.cpuTopology.threadsPerCore} />
    <DetailItem label="NUMA nodes" value={<>{summary.cpuTopology.numaNodes} <InfoCircleIcon className="w-4 h-4 inline-block text-blue-500" /></>} />
  </Card>
);

const TagsCard: React.FC = () => (
  <Card title="Tags">
      <div className="text-center text-gray-500 flex flex-col items-center justify-center h-full">
          <TagIcon className="w-10 h-10 text-gray-400 mb-2" />
          <p>No tags assigned</p>
      </div>
  </Card>
);

const NotesCard: React.FC<{ summary: VmSummaryData }> = ({ summary }) => (
  <Card title="Notes">
      <div className="text-center text-gray-500 flex flex-col items-center justify-center h-full">
          <PencilIcon className="w-10 h-10 text-gray-400 mb-2" />
          <p>{summary.notes}</p>
      </div>
  </Card>
);

export const VmSummaryTab: React.FC<{vm: VirtualMachine}> = ({ vm }) => {
    const summary = vm.summary;

    if (!summary) {
        return <Card title="Summary"><p className="text-center text-gray-400 py-8">Summary data is not available for this VM.</p></Card>;
    }

    return (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 auto-rows-min gap-4">
          <GuestOsCard summary={summary} />
          <VmDetailsCard summary={summary} />
          <UsageCard summary={summary} />
          <VmHardwareCard summary={summary} />
          <PciDevicesCard />
          <RelatedObjectsCard summary={summary} />
          <StoragePoliciesCard summary={summary} />
          <CustomAttributesCard summary={summary} />
          <SnapshotsCard summary={summary} />
          <ClusterHaCard summary={summary} />
          <CpuTopologyCard summary={summary} />
          <TagsCard />
          <NotesCard summary={summary} />
        </div>
    );
};

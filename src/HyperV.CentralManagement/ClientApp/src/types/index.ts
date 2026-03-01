export enum VmStatus {
  Running = 'Running',
  PoweredOn = 'PoweredOn',
  Stopped = 'Stopped',
  Saved = 'Saved',
  Creating = 'Creating',
}

export interface VmHardware {
  cpu: { cores: number };
  memory: { size: number; unit: 'GB' | 'MB' };
  hardDisks: Array<{
    id: number;
    size: number;
    unit: 'GB' | 'MB' | 'TB';
  }>;
  scsiControllers: Array<{ id: number; type: string }>;
  networkAdapters: Array<{
    id: number;
    networkName: string;
    connected: boolean;
  }>;
  cdDvdDrives: Array<{
    id: number;
    deviceType: string;
    connectAtPowerOn: boolean;
  }>;
  usbControllers: Array<{ id: number; type: string }>;
  videoCard: { settings: string };
  sataControllers: Array<{ id: number; type: string }>;
  serialPorts: Array<{
    id: number;
    portType: string;
    connected: boolean;
  }>;
  securityDevices: { status: string };
  other: { description: string };
}

export interface VmSummaryData {
  guestOs: {
    thumbnailUrl: string;
  };
  details: {
    powerStatus: 'Powered On' | 'Powered Off';
    guestOs: string;
    integrationServices: string;
    dnsName: string;
    ipAddresses: string[];
    encryption: string;
  };
  usage: {
    lastUpdated: string;
    cpu: { usedMhz: number };
    memory: { usedMb: number };
    storage: { usedGb: number };
  };
  hardwareSummary: {
    cpu: string;
    memory: string;
    hardDisk: string;
    networkAdapter: string;
    cdDvdDrive: string;
    generation: string;
  };
  relatedObjects: {
    cluster: { name: string; link: string };
    host: { name: string; link: string };
    resourcePool: { name: string; link: string };
    networks: { name: string; link: string }[];
    storage: { name: string; link: string };
  };
  storagePolicies: {
    vmStoragePolicies: string;
    vmStoragePolicyCompliance: string;
    lastCheckedDate: string;
    vmReplicationGroups: string;
  };
  customAttributes: {
    [key: string]: string;
  };
  snapshots: {
    count: number;
    diskUsedGb: number;
    latest: {
      name: string;
      sizeGb: number;
      date: string;
    };
  };
  clusterHa: {
    protection: 'Protected' | 'Unprotected';
    proactiveHa: 'Disabled' | 'Enabled';
    hostFailure: 'Restart VMs' | 'Disabled';
    hostIsolation: 'Disabled' | 'Power off and restart VMs';
    storagePermanentDeviceLoss: 'Power off and restart VMs' | 'Disabled';
    storageAllPathsDown: 'Power off and restart VMs' | 'Disabled';
  };
  cpuTopology: {
    assignedOnPowerOn: boolean;
    vCpus: number;
    coresPerSocket: number;
    threadsPerCore: number;
    numaNodes: number;
  };
  tags: string[];
  notes: string;
}

export interface VirtualMachine {
  id: string;
  name: string;
  status: VmStatus;
  hostId: string;
  hostName: string;
  os: 'windows' | 'linux' | 'linux-other' | 'windows-other';
  cpuUsage: number;
  memoryUsage: number;
  totalMemory: number;
  provisionedSpaceGB: number;
  usedSpaceGB: number;
  hardware?: VmHardware;
  summary?: VmSummaryData;
}

export interface Host {
  id: string;
  name: string;
  hypervisor: string;
  model: string;
  processorType: string;
  logicalProcessors: number;
  nics: number;
  vmCount: number;
  state: string;
  uptime: string;
  cpuUsage: number;
  totalCpu: number;
  memoryUsage: number;
  totalMemory: number;
  storageUsage: number;
  totalStorage: number;
}

export interface InfrastructureNode {
  id: string;
  name: string;
  type: 'datacenter' | 'cluster' | 'host' | 'vm' | 'folder';
  children?: InfrastructureNode[];
}

export interface VmPerformanceDataPoint {
  time: string;
  cpuUsage: number;
  cpuReady: number;
  cpuUsageMhz: number;
  memActive: number;
  memConsumed: number;
  memGranted: number;
  memBalloon: number;
  memSwapInRate: number;
  memSwapOutRate: number;
  diskHighestLatency: number;
  diskUsageRate: number;
}

export interface VmSnapshot {
  id: string;
  name: string;
  description: string;
  createdAt: string;
  children: VmSnapshot[];
}

export interface StorageInterface {
  id: string;
  adapter: string;
  model: string;
  type: 'Fibre Channel' | 'SAS' | 'Block SCSI';
  status: 'Online' | 'Unknown';
  identifier: string | null;
  targets: number;
  devices: number;
  paths: number;
}

export interface StorageDeviceDetails {
  general: {
    name: string;
    applicationProtocol: string;
    identifier: string;
    type: string;
    location: string;
    capacity: string;
    driveType: string;
    hardwareAcceleration: string;
    owner: string;
    sectorFormat: string;
  };
  multipathingPolicies: {
    pathSelectionPolicy: string;
    storageArrayTypePolicy: string;
  };
}

export interface StorageDevice {
  id: string;
  name: string;
  lun: number;
  type: 'disk';
  capacity: string;
  datastore: string | null;
  datastoreConsumed: 'Consumed' | 'Not Consumed';
  operationalState: 'Attached';
  driveType: 'HDD' | 'Flash';
  transport: 'Fibre Channel' | 'SAS';
  details: StorageDeviceDetails;
}

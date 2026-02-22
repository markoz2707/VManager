export type VmEnvironment = 'WMI' | 'HCS';

export enum VmStatus {
  RUNNING = 'Running',
  STOPPED = 'Stopped',
  PAUSED = 'Paused',
  SAVED = 'Saved',
  SAVING = 'Saving',
  STARTING = 'Starting',
  STOPPING = 'Stopping',
  PAUSING = 'Pausing',
  RESUMING = 'Resuming',
  SUSPENDED = 'Suspended',
  UNKNOWN = 'Unknown',
}

export interface VirtualMachine {
  id: string;
  name: string;
  status: VmStatus;
  environment: VmEnvironment;
  cpuCount?: number;
  memoryMB?: number;
  enableDynamicMemory?: boolean;
  minMemoryMB?: number;
  maxMemoryMB?: number;
}

export interface VmSnapshot {
  id: string;
  name: string;
  creationTime: string;
  type?: string;
}

export interface Container {
    id: string;
    name: string;
    state: string;
    image: string;
    memoryMB: number;
    cpuCount: number;
    networkMode?: string;
    createdAt?: string;
    startedAt?: string;
}

export enum NetworkType {
    INTERNAL = 'Internal',
    EXTERNAL = 'External',
    PRIVATE = 'Private',
    NAT = 'NAT',
}

export interface VirtualNetwork {
    id: string;
    name: string;
    type: NetworkType;
    subnet: string;
    environment: VmEnvironment;
    notes?: string;
}

export interface PhysicalAdapterInfo {
    name: string;
    guid: string;
    pnpDeviceId: string;
}

export enum VlanOperationMode {
    ACCESS = 1,
    TRUNK = 2,
    PRIVATE = 3,
}

export interface VlanConfiguration {
    vlanId: number;
    operationMode: VlanOperationMode;
    operationModeName: string;
    nativeVlanId?: number;
    trunkVlanIds?: number[];
}

export interface ServiceInfo {
  name: string;
  version: string;
  description: string;
  endpoints: Record<string, string>;
  capabilities: string[];
}

export type NotificationType = 'success' | 'error' | 'info';

export interface Notification {
  id: number;
  type: NotificationType;
  message: string;
}

export interface StorageJob {
    id: string;
    jobId: string;
    operation?: string;
    operationType?: string;
    status: string;
    state?: string;
    percentComplete: number;
    completionTime?: string;
    errors?: string[];
}

export interface VhdMetadata {
    [key: string]: any;
}

export interface VirtualDiskChanges {
    [key:string]: any;
}

export interface VhdSetInformation {
    [key: string]: any;
}

export interface StorageDevice {
    id: string;
    deviceType: string;
    path: string;
    readOnly?: boolean;
    controllerId: string;
}

export interface StorageController {
    id: string;
    name: string;
    type: string;
}

export interface ReplicationRelationship {
    relationshipId: string;
    sourceVm: string;
    targetHost: string;
    state: string;
    health: string;
    mode: string;
}

export type FailoverMode = 'Planned' | 'Test' | 'Live';

export interface Metric {
    name: string;
    type: string;
    value?: any; 
}

export interface VmUsageSummary {
  timestamp: string;
  cpu: {
    usagePercent: number;
    guestAverageUsage: number;
  };
  memory: {
    assignedMB: number;
    demandMB: number;
    usagePercent: number;
    status: string;
  };
  disks: Array<{
    name: string;
    readIops: number;
    writeIops: number;
    latencyMs: number;
    throughputBytesPerSec: number;
  }>;
  networks: Array<{
    adapterName: string;
    bytesReceivedPerSec: number;
    bytesSentPerSec: number;
    packetsDropped: number;
  }>;
  storageAdapters: Array<{
    name: string;
    queueDepth: number;
    throughputBytesPerSec: number;
    errorsCount: number;
  }>;
}

export interface HostUsageSummary {
  timestamp: string;
  cpu: {
    usagePercent: number;
    cores: number;
    logicalProcessors: number;
  };
  memory: {
    totalPhysicalMB: number;
    availableMB: number;
    usedMB: number;
    usagePercent: number;
  };
  physicalDisks: Array<{
    diskId: string;
    model: string;
    readIops: number;
    writeIops: number;
    throughputBytesPerSec: number;
    latencyMs: number;
    queueLength: number;
  }>;
  networkAdapters: Array<{
    name: string;
    interfaceDescription: string;
    status: string;
    speedBitsPerSec: number;
    bytesReceivedPerSec: number;
    bytesSentPerSec: number;
  }>;
  storageAdapters: Array<{
    name: string;
    manufacturer: string;
    status: string;
    throughputBytesPerSec: number;
  }>;
}

export interface FibreChannelSan {
    poolId: string;
    name: string;
    notes?: string;
    wwpnArray: string[];
    wwnnArray: string[];
}

export interface StorageQoSPolicy {
    policyId: string;
    maxIops: number;
    maxBandwidth: number;
    description?: string;
}

export interface AppHealthStatus {
    status: string;
    appStatus: string;
}

export interface StorageDeviceInfo {
    name: string;
    filesystem: string;
    size: number;
    usedSpace: number;
    freeSpace: number;
}

export interface StorageLocation {
    drive: string;
    freeSpaceBytes: number;
    freeSpaceGb: number;
    suggestedPaths: string[];
    isSuitable: boolean;
}

export interface HostHardware {
  manufacturer: string;
  model: string;
  biosVersion: string;
  biosSerialNumber: string;
  systemUuid: string;
  motherboardManufacturer: string;
  motherboardModel: string;
  totalPhysicalMemory: number;
}

export interface HostSystem {
  osName: string;
  osVersion:string;
  osBuildNumber: string;
  lastBootUpTime: string;
  totalVisibleMemorySize: number;
  freePhysicalMemory: number;
}

export interface HostPerformance {
  cpuUsagePercent: number;
  memoryUsagePercent: number;
  storageUsagePercent: Record<string, number>;
}

export interface HostTask {
  target: string;
  initiator: string;
  status: string;
  started: string;
  result: string;
}

export interface HostDetails {
  hostname: string;
  manufacturer: string;
  model: string;
  cpuInfo: string;
  totalMemoryGB: number;
  serialNumber: string;
  biosVersion: string;
  uptime: string;
  version: string;
}

export interface Stats {
  runningVms: number;
  totalVms: number;
  totalNetworks: number;
  totalMemoryAssignedGB: number;
  totalMemoryCapacityGB: number;
  cpuUsagePercent: number;
  storageUsedTB: number;
  storageCapacityTB: number;
}
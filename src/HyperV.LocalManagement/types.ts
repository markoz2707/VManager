export enum VmStatus {
  RUNNING = 'Running',
  STOPPED = 'Stopped',
  PAUSED = 'Paused',
  SAVED = 'Saved',
  SAVING = 'Saving',
  STOPPING = 'Stopping',
  STARTING = 'Starting',
  PAUSING = 'Pausing',
  RESUMING = 'Resuming',
  SUSPENDED = 'Suspended',
  UNKNOWN = 'Unknown',
}

export type VmEnvironment = 'WMI' | 'HCS';

export interface VirtualMachine {
  id: string;
  name: string;
  status: VmStatus;
  environment: VmEnvironment;
  cpuCount?: number;
  memoryMB?: number;
}

export enum NetworkType {
  INTERNAL = 'Internal',
  EXTERNAL = 'External',
  EXTERNAL_ONLY = 'External_Only',
  PRIVATE = 'Private',
  NAT = 'NAT',
}

export interface PhysicalAdapterInfo {
  name: string;
  guid: string;
  pnpDeviceId: string;
}

export interface VirtualNetwork {
    id: string;
    name: string;
    type: NetworkType;
    subnet: string;
    environment: VmEnvironment;
    notes?: string; // optional WMI notes
    extensions?: Array<{
        name: string;
        type: string;
        enabled: boolean;
        vendor: string;
    }>;
    supportsTrunk?: boolean;
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

export interface VmSnapshot {
  id: string;
  name: string;
  creationTime: string;
  type: 'Standard' | 'Production';
  parentSnapshotId: string | null;
}

export interface StorageDevice {
  deviceId: string;
  name: string;
  deviceType: string;
  path: string;
  controllerId: string;
  controllerType: string;
  isReadOnly: boolean;
  operationalStatus: string;
  location: number;
}

export interface StorageController {
  controllerId: string;
  name: string;
  controllerType: string;
  maxDevices: number;
  attachedDevices: number;
  supportsHotPlug: boolean;
  operationalStatus: string;
  protocol: string;
  availableLocations: number[];
}

export interface Container {
  id: string;
  name: string;
  state: 'Running' | 'Stopped' | 'Paused';
  image: string;
  memoryMB: number;
  cpuCount: number;
  networkMode: string;
  createdAt: string;
  startedAt: string | null;
}

export interface StorageJob {
  jobId: string;
  operationType: string;
  state: 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';
  startTime: string;
  endTime: string | null;
  percentComplete: number;
  description: string;
  targetPath?: string;
  estimatedTimeRemaining?: string;
  bytesProcessed?: number;
  totalBytes?: number;
}

export interface VirtualDiskChanges {
  changeTrackingId: string;
  changes: {
    byteOffset: number;
    byteLength: number;
    changeType: 'Modified' | 'Added' | 'Removed';
  }[];
  totalChangedBytes: number;
}

export interface VhdMetadata {
  path: string;
  format: 'VHD' | 'VHDX';
  type: 'Fixed' | 'Dynamic' | 'Differencing';
  uniqueId: string;
  virtualSize: number;
  physicalSize: number;
  isAttached: boolean;
  attachedTo?: string;
  blockSize: number;
  logicalSectorSize: number;
  physicalSectorSize: number;
}

export interface VhdSetInformation {
  path: string;
  virtualSize: number;
  physicalSize: number;
  blockSize: number;
  snapshotCount: number;
  activeSnapshotId: string;
}

export interface CreateReplicationRequest {
  sourceVm: string;
  targetHost: string;
  authMode?: string;
}

export interface FailoverRequest {
  mode?: 'Planned' | 'Test' | 'Live';
}

export interface ReplicationStateResponse {
  enabledState: number;
  replicationHealth: string;
}

export interface MigrateRequest {
  destinationHost: string;
  live: boolean;
  storage: boolean;
}

export interface AppHealthResponse {
  status: 'OK' | 'Critical';
  appStatus: number;
}

export interface GuestFileRequest {
  sourcePath: string;
  destPath: string;
  overwrite?: boolean;
}

export interface CreateSanRequest {
  sanName: string;
  wwpnArray: string[];
  wwnnArray: string[];
  notes?: string;
}

export interface CreateFcPortRequest {
  sanPoolId: string;
  wwpn: string;
  wwnn: string;
}

export interface SanInfoResponse {
  poolId: string;
  name: string;
  notes: string;
  allocations: any[];
}

export interface CreateQoSRequest {
  policyId: string;
  maxIops?: number;
  maxBandwidth?: number;
  description?: string;
}

export interface QoSInfoResponse {
  policyId: string;
  maxIops: number;
  maxBandwidth: number;
  description: string;
}
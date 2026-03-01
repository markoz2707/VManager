// Backend DTO types matching CentralManagement API responses

export interface DatacenterDto {
  id: string;
  name: string;
  description: string | null;
  createdUtc: string;
}

export interface AgentHostDto {
  id: string;
  hostname: string;
  apiBaseUrl: string;
  ipAddress: string | null;
  hyperVVersion: string | null;
  hypervisorVersion: string | null;
  operatingSystem: string | null;
  agentVersion: string | null;
  hostType: string | null;
  tags: string | null;
  clusterName: string | null;
  status: string;
  lastSeenUtc: string;
  registeredAtUtc: string;
  datacenterId: string | null;
}

export interface VmInventoryDto {
  id: string;
  agentHostId: string;
  agentHostName: string | null;
  vmId: string;
  name: string;
  state: string;
  cpuCount: number;
  memoryMB: number;
  environment: string;
  lastSyncUtc: string;
  folderId: string | null;
  folderName: string | null;
  tags: string | null;
  notes: string | null;
}

export interface VmFolderDto {
  id: string;
  name: string;
  parentId: string | null;
  childCount: number;
}

export interface ClusterDto {
  id: string;
  name: string;
  description: string | null;
  datacenterId: string | null;
  nodes: ClusterNodeDto[];
}

export interface ClusterNodeDto {
  id: string;
  clusterId: string;
  agentHostId: string;
  agentHost: AgentHostDto | null;
}

export interface MetricTimeSeriesDto {
  agentId?: string;
  vmId?: string;
  metricName: string;
  from: string;
  to: string;
  dataPoints: MetricDataPointDto[];
}

export interface MetricDataPointDto {
  value: number;
  timestampUtc: string;
}

export interface ClusterSummaryDto {
  clusterId: string;
  hosts: ClusterHostSummaryDto[];
}

export interface ClusterHostSummaryDto {
  agentId: string;
  hostname: string;
  status: string;
  cpuUsagePercent: number;
  memoryUsagePercent: number;
  vmCount: number;
  lastUpdated: string | null;
}

export interface DashboardDto {
  totalAgents: number;
  onlineAgents: number;
  totalVms: number;
  runningVms: number;
  activeAlerts: number;
  agents: DashboardAgentDto[];
}

export interface DashboardAgentDto {
  id: string;
  hostname: string;
  status: string;
  hostType: string;
  lastSeenUtc: string;
}

export interface LoginResponse {
  token: string;
  auth: 'local' | 'ldap';
  roles: string[];
}

export interface PowerOperationDto {
  operation: 'start' | 'stop' | 'shutdown' | 'pause' | 'resume';
}

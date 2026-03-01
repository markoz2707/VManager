import { getAgents } from './agentService';
import { getAllVms } from './vmService';
import { getClusters } from './clusterService';
import { getFolders } from './vmService';
import { getDatacenters } from './datacenterService';
import type { AgentHostDto, VmInventoryDto, ClusterDto, VmFolderDto, DatacenterDto } from '../types/api';
import type { Host, VirtualMachine, InfrastructureNode } from '../types';
import { VmStatus } from '../types';

function mapAgentToHost(agent: AgentHostDto, vmCount: number): Host {
  return {
    id: agent.id,
    name: agent.hostname,
    hypervisor: agent.operatingSystem ?? 'Windows Server with Hyper-V',
    model: agent.hostType ?? 'Unknown',
    processorType: 'N/A',
    logicalProcessors: 0,
    nics: 0,
    vmCount,
    state: agent.status === 'Online' ? 'Connected' : agent.status,
    uptime: 'N/A',
    cpuUsage: 0,
    totalCpu: 100,
    memoryUsage: 0,
    totalMemory: 0,
    storageUsage: 0,
    totalStorage: 0,
  };
}

function mapVmState(state: string): VmStatus {
  const lower = state.toLowerCase();
  if (lower === 'running' || lower === 'enabled') return VmStatus.Running;
  if (lower === 'off' || lower === 'stopped' || lower === 'disabled') return VmStatus.Stopped;
  if (lower === 'saved' || lower === 'paused') return VmStatus.Saved;
  if (lower === 'creating') return VmStatus.Creating;
  return VmStatus.Stopped;
}

function guessOs(name: string): VirtualMachine['os'] {
  const lower = name.toLowerCase();
  if (lower.includes('linux') || lower.includes('ubuntu') || lower.includes('centos') || lower.includes('debian'))
    return 'linux';
  return 'windows';
}

function mapVmToVirtualMachine(
  vm: VmInventoryDto,
  agentHostname: string
): VirtualMachine {
  return {
    id: vm.id,
    name: vm.name,
    status: mapVmState(vm.state),
    hostId: vm.agentHostId,
    hostName: vm.agentHostName ?? agentHostname,
    os: guessOs(vm.name),
    cpuUsage: 0,
    memoryUsage: vm.memoryMB / 1024,
    totalMemory: vm.memoryMB / 1024,
    provisionedSpaceGB: 0,
    usedSpaceGB: 0,
  };
}

function buildHostNode(
  agentId: string,
  agentMap: Map<string, AgentHostDto>,
  vmsByAgent: Map<string, VmInventoryDto[]>
): InfrastructureNode {
  const agent = agentMap.get(agentId);
  const agentVms = vmsByAgent.get(agentId) ?? [];
  return {
    id: agentId,
    name: agent?.hostname ?? 'Unknown Host',
    type: 'host' as const,
    children: agentVms.map((vm) => ({
      id: vm.id,
      name: vm.name,
      type: 'vm' as const,
    })),
  };
}

function buildTree(
  datacenters: DatacenterDto[],
  clusters: ClusterDto[],
  agents: AgentHostDto[],
  vms: VmInventoryDto[],
  _folders: VmFolderDto[]
): InfrastructureNode[] {
  const agentMap = new Map(agents.map((a) => [a.id, a]));
  const vmsByAgent = new Map<string, VmInventoryDto[]>();
  for (const vm of vms) {
    const list = vmsByAgent.get(vm.agentHostId) ?? [];
    list.push(vm);
    vmsByAgent.set(vm.agentHostId, list);
  }

  const clusteredAgentIds = new Set<string>();

  // Build cluster nodes grouped by datacenterId
  const clustersByDc = new Map<string | null, InfrastructureNode[]>();
  for (const cluster of clusters) {
    const hostNodes: InfrastructureNode[] = cluster.nodes.map((node) => {
      clusteredAgentIds.add(node.agentHostId);
      return buildHostNode(node.agentHostId, agentMap, vmsByAgent);
    });

    const clusterNode: InfrastructureNode = {
      id: cluster.id,
      name: cluster.name,
      type: 'cluster' as const,
      children: hostNodes,
    };

    const dcId = cluster.datacenterId ?? null;
    const list = clustersByDc.get(dcId) ?? [];
    list.push(clusterNode);
    clustersByDc.set(dcId, list);
  }

  // Group standalone hosts by datacenterId
  const standaloneByDc = new Map<string | null, InfrastructureNode[]>();
  for (const agent of agents) {
    if (clusteredAgentIds.has(agent.id)) continue;
    const hostNode = buildHostNode(agent.id, agentMap, vmsByAgent);
    const dcId = agent.datacenterId ?? null;
    const list = standaloneByDc.get(dcId) ?? [];
    list.push(hostNode);
    standaloneByDc.set(dcId, list);
  }

  // If no datacenters exist, fall back to old single root
  if (datacenters.length === 0) {
    const allClusters = Array.from(clustersByDc.values()).flat();
    const allStandalone = Array.from(standaloneByDc.values()).flat();
    return [
      {
        id: 'vmanager-central',
        name: 'VManager Central',
        type: 'datacenter' as const,
        children: [...allClusters, ...allStandalone],
      },
    ];
  }

  // Build one datacenter node per real datacenter
  const dcNodes: InfrastructureNode[] = datacenters.map((dc) => {
    const dcClusters = clustersByDc.get(dc.id) ?? [];
    const dcStandalone = standaloneByDc.get(dc.id) ?? [];
    return {
      id: dc.id,
      name: dc.name,
      type: 'datacenter' as const,
      children: [...dcClusters, ...dcStandalone],
    };
  });

  // Any clusters/hosts with null datacenterId go under an "Unassigned" root
  const unassignedClusters = clustersByDc.get(null) ?? [];
  const unassignedHosts = standaloneByDc.get(null) ?? [];
  if (unassignedClusters.length > 0 || unassignedHosts.length > 0) {
    dcNodes.push({
      id: 'unassigned',
      name: 'Unassigned',
      type: 'datacenter' as const,
      children: [...unassignedClusters, ...unassignedHosts],
    });
  }

  return dcNodes;
}

export interface InfrastructureData {
  tree: InfrastructureNode[];
  hosts: Host[];
  vms: VirtualMachine[];
}

export async function getInfrastructure(): Promise<InfrastructureData> {
  const [agents, vmDtos, clusters, folders, datacenters] = await Promise.all([
    getAgents(),
    getAllVms(),
    getClusters(),
    getFolders(),
    getDatacenters(),
  ]);

  const agentMap = new Map(agents.map((a) => [a.id, a]));
  const vmsByAgent = new Map<string, VmInventoryDto[]>();
  for (const vm of vmDtos) {
    const list = vmsByAgent.get(vm.agentHostId) ?? [];
    list.push(vm);
    vmsByAgent.set(vm.agentHostId, list);
  }

  const hosts: Host[] = agents.map((a) =>
    mapAgentToHost(a, vmsByAgent.get(a.id)?.length ?? 0)
  );

  const vms: VirtualMachine[] = vmDtos.map((vm) => {
    const agent = agentMap.get(vm.agentHostId);
    return mapVmToVirtualMachine(vm, agent?.hostname ?? 'Unknown');
  });

  const tree = buildTree(datacenters, clusters, agents, vmDtos, folders);

  return { tree, hosts, vms };
}

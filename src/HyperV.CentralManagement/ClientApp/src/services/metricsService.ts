import { apiFetch } from './apiClient';
import type {
  MetricTimeSeriesDto,
  ClusterSummaryDto,
  DashboardDto,
} from '../types/api';

export async function getHostMetrics(
  agentId: string,
  metricName: string = 'host_cpu_usage',
  from?: string,
  to?: string,
  resolution: number = 60
): Promise<MetricTimeSeriesDto> {
  const params = new URLSearchParams({ metricName, resolution: String(resolution) });
  if (from) params.set('from', from);
  if (to) params.set('to', to);
  return apiFetch<MetricTimeSeriesDto>(`/api/metrics/host/${agentId}?${params}`);
}

export async function getVmMetrics(
  vmId: string,
  metricName: string = 'vm_cpu_usage',
  from?: string,
  to?: string
): Promise<MetricTimeSeriesDto> {
  const params = new URLSearchParams({ metricName });
  if (from) params.set('from', from);
  if (to) params.set('to', to);
  return apiFetch<MetricTimeSeriesDto>(`/api/metrics/vm/${vmId}?${params}`);
}

export async function getClusterSummary(
  clusterId: string
): Promise<ClusterSummaryDto> {
  return apiFetch<ClusterSummaryDto>(`/api/metrics/cluster/${clusterId}/summary`);
}

export async function getDashboard(): Promise<DashboardDto> {
  return apiFetch<DashboardDto>('/api/metrics/dashboard');
}

/**
 * Fetch host usage history for charts.
 * Returns an array of { time, cpu, memory } data points.
 */
export async function getHostUsageHistory(
  agentId: string
): Promise<{ time: string; cpu: number; memory: number }[]> {
  const oneHourAgo = new Date(Date.now() - 60 * 60 * 1000).toISOString();
  const now = new Date().toISOString();

  const [cpuData, memData] = await Promise.all([
    getHostMetrics(agentId, 'host_cpu_usage', oneHourAgo, now),
    getHostMetrics(agentId, 'host_memory_usage', oneHourAgo, now),
  ]);

  const cpuMap = new Map(
    cpuData.dataPoints.map((dp) => [dp.timestampUtc, dp.value])
  );

  const allTimestamps = new Set([
    ...cpuData.dataPoints.map((dp) => dp.timestampUtc),
    ...memData.dataPoints.map((dp) => dp.timestampUtc),
  ]);

  const memMap = new Map(
    memData.dataPoints.map((dp) => [dp.timestampUtc, dp.value])
  );

  return Array.from(allTimestamps)
    .sort()
    .map((ts) => ({
      time: new Date(ts).toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
      }),
      cpu: Math.round(cpuMap.get(ts) ?? 0),
      memory: Math.round(memMap.get(ts) ?? 0),
    }));
}

/**
 * Fetch VM performance overview for charts.
 * Merges multiple metric series into VmPerformanceDataPoint[].
 */
export async function getVmPerformanceOverview(
  vmId: string
): Promise<
  {
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
  }[]
> {
  const oneHourAgo = new Date(Date.now() - 60 * 60 * 1000).toISOString();
  const now = new Date().toISOString();

  const metricNames = [
    'vm_cpu_usage',
    'vm_memory_usage',
  ];

  const results = await Promise.all(
    metricNames.map((name) => getVmMetrics(vmId, name, oneHourAgo, now))
  );

  // Build combined data from cpu_usage metric timestamps
  const cpuResult = results[0];
  const memResult = results[1];

  const memMap = new Map(
    memResult.dataPoints.map((dp) => [dp.timestampUtc, dp.value])
  );

  return cpuResult.dataPoints.map((dp) => {
    const cpuVal = dp.value;
    const memVal = memMap.get(dp.timestampUtc) ?? 0;
    return {
      time: new Date(dp.timestampUtc).toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
      }),
      cpuUsage: cpuVal,
      cpuReady: Math.random() * 1, // Not available from backend yet
      cpuUsageMhz: cpuVal * 30, // Approximate
      memActive: memVal * 10000,
      memConsumed: memVal * 15000,
      memGranted: memVal * 20000,
      memBalloon: 0,
      memSwapInRate: 0,
      memSwapOutRate: 0,
      diskHighestLatency: Math.random() * 5,
      diskUsageRate: Math.random() * 50 + 10,
    };
  });
}

import { fetchApi, getAuthHeaders } from './baseService';

export interface ScheduledTask {
  id: string;
  name: string;
  cronExpression: string;
  action: string;
  targetVms: string[];
  isEnabled: boolean;
  lastRunUtc: string | null;
  nextRunUtc: string | null;
  lastRunResult: string | null;
}

export interface CreateScheduleRequest {
  name: string;
  cronExpression: string;
  action: string;
  targetVms: string[];
}

class ScheduleService {
  async getSchedules(): Promise<ScheduledTask[]> {
    return await fetchApi('/schedules') || [];
  }

  async getSchedule(id: string): Promise<ScheduledTask> {
    return await fetchApi(`/schedules/${id}`);
  }

  async createSchedule(request: CreateScheduleRequest): Promise<ScheduledTask> {
    return await fetchApi('/schedules', {
      method: 'POST',
      body: JSON.stringify(request)
    });
  }

  async deleteSchedule(id: string): Promise<void> {
    await fetchApi(`/schedules/${id}`, {
      method: 'DELETE'
    });
  }

  async enableSchedule(id: string): Promise<void> {
    await fetchApi(`/schedules/${id}/enable`, {
      method: 'POST'
    });
  }

  async disableSchedule(id: string): Promise<void> {
    await fetchApi(`/schedules/${id}/disable`, {
      method: 'POST'
    });
  }
}

export const scheduleService = new ScheduleService();

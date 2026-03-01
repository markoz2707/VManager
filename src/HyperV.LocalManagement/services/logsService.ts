import { fetchApi } from './baseService';

export interface LogEntry {
    id: string;
    timestamp: string;
    level: 'Information' | 'Warning' | 'Error' | 'Critical';
    source: string;
    message: string;
    eventId?: number;
    category?: string;
}

export interface LogQueryParams {
    source?: string;
    level?: string;
    startTime?: string;
    endTime?: string;
    limit?: number;
    search?: string;
}

export interface LogsResponse {
    entries: LogEntry[];
    totalCount: number;
    sources: string[];
}

class LogsService {
    private readonly basePath = '/host/logs';

    async getLogs(params?: LogQueryParams): Promise<LogsResponse> {
        const queryParams = new URLSearchParams();

        if (params?.source) queryParams.append('source', params.source);
        if (params?.level) queryParams.append('level', params.level);
        if (params?.startTime) queryParams.append('startTime', params.startTime);
        if (params?.endTime) queryParams.append('endTime', params.endTime);
        if (params?.limit) queryParams.append('limit', params.limit.toString());
        if (params?.search) queryParams.append('search', params.search);

        const queryString = queryParams.toString();
        const url = queryString ? `${this.basePath}?${queryString}` : this.basePath;

        return fetchApi(url);
    }

    async getLogSources(): Promise<string[]> {
        return fetchApi(`${this.basePath}/sources`);
    }

    async exportLogs(params?: LogQueryParams, format: 'json' | 'csv' = 'json'): Promise<Blob> {
        const queryParams = new URLSearchParams();
        queryParams.append('format', format);

        if (params?.source) queryParams.append('source', params.source);
        if (params?.level) queryParams.append('level', params.level);
        if (params?.startTime) queryParams.append('startTime', params.startTime);
        if (params?.endTime) queryParams.append('endTime', params.endTime);

        const response = await fetch(`https://localhost:8743/api/v1${this.basePath}/export?${queryParams.toString()}`);
        return response.blob();
    }
}

export const logsService = new LogsService();

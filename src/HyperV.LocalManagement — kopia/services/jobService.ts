import { fetchApi, ApiError } from './baseService';
import { StorageJob } from '../types';

export const getStorageJobs = async (): Promise<StorageJob[]> => {
    return fetchApi('/jobs/storage');
};

export const getStorageJobDetails = async (jobId: string): Promise<StorageJob> => {
    return fetchApi(`/jobs/storage/${encodeURIComponent(jobId)}`);
};

export const getJobAffectedElements = async (jobId: string): Promise<any[]> => {
    return fetchApi(`/jobs/storage/${encodeURIComponent(jobId)}/affected-elements`);
};

export const cancelStorageJob = async (jobId: string): Promise<void> => {
    await fetchApi(`/jobs/storage/${encodeURIComponent(jobId)}/cancel`, {
        method: 'POST'
    });
};

export const deleteStorageJob = async (jobId: string): Promise<void> => {
    await fetchApi(`/jobs/storage/${encodeURIComponent(jobId)}`, {
        method: 'DELETE'
    });
};
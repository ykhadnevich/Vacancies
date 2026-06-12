import apiClient from './client'
import type { JobsResponse, JobSearchParams, JobVacancy } from '../types/job'
import type { JobsV6Response } from '../types/jobV6'

export interface SavedUrl {
    id: string
    url: string
    alias?: string
    createdAt: string
    lastParsedAt?: string
    lastParsedCount: number
}

export const jobsApi = {
    getJobs: async (params: JobSearchParams): Promise<JobsResponse> => {
        const response = await apiClient.get('/Jobs', { params })
        return response.data
    },

    getJobsV6: async (params: JobSearchParams & { limit?: number }): Promise<JobsV6Response> => {
        const response = await apiClient.get('/jobs/v6', { params })
        return response.data
    },

    /**
     * Cheap GET that returns the previously-cached v6 result for these query
     * params, if any. Returns null on 204 No Content. The UI calls this first
     * on mount so the user sees yesterday's analysis instantly; a fresh v6
     * run only happens on the explicit Refresh button.
     */
    getJobsV6Snapshot: async (
        params: JobSearchParams & { limit?: number },
    ): Promise<{ response: JobsV6Response; executedAt: string; queryHash: string } | null> => {
        const response = await apiClient.get('/jobs/v6/snapshot', {
            params,
            validateStatus: (s) => s === 200 || s === 204,
        })
        if (response.status === 204) return null
        return response.data
    },

    getRawJobs: async (params: { keywords: string; location?: string; limit?: number }): Promise<{
        jobs:              JobVacancy[]
        totalCount:        number
        duplicatesRemoved: number
    }> => {
        const response = await apiClient.get('/jobs/raw', { params })
        return response.data
    },

    addManualUrl: async (url: string): Promise<{ savedUrlId: string; jobsFound: number }> => {
        const response = await apiClient.post('/Jobs/manual', { url })
        return response.data
    },

    getSavedUrls: async (): Promise<SavedUrl[]> => {
        const response = await apiClient.get('/Jobs/manual')
        return response.data
    },

    refreshSavedUrl: async (id: string): Promise<{ parsedCount: number; addedCount: number }> => {
        const response = await apiClient.post(`/Jobs/manual/${id}/refresh`)
        return response.data
    },

    getManualVacancies: async (): Promise<JobVacancy[]> => {
        const response = await apiClient.get('/Jobs/manual/vacancies')
        return response.data
    },
}

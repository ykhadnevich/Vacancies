import apiClient from './client'
import type { JobsResponse, JobSearchParams, JobVacancy } from '../types/job'

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
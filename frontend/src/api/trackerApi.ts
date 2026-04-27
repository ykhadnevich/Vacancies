import apiClient from './client'
import type { TrackerEntry, CreateTrackerEntry } from '../types/tracker'

export const trackerApi = {
    getAll: async (): Promise<TrackerEntry[]> => {
        const response = await apiClient.get('/Tracker')
        return response.data
    },

    add: async (entry: CreateTrackerEntry): Promise<TrackerEntry> => {
        const response = await apiClient.post('/Tracker', entry)
        return response.data
    },

    updateStatus: async (id: string, status: number): Promise<void> => {
        await apiClient.patch(`/Tracker/${id}`, { status })
    },

    updatePipelineStep: async (id: string, step: string, value: boolean): Promise<void> => {
        await apiClient.patch(`/Tracker/${id}`, {
            pipelineStep: step,
            pipelineStepValue: value,
        })
    },

    delete: async (id: string): Promise<void> => {
        await apiClient.delete(`/Tracker/${id}`)
    },
}
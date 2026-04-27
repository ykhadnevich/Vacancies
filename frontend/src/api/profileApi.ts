import apiClient from './client'
import type { UserProfile, UpdateProfileRequest } from '../types/profile'

export const profileApi = {
    get: async (): Promise<UserProfile> => {
        const response = await apiClient.get('/User/profile')
        return response.data
    },

    update: async (data: UpdateProfileRequest): Promise<void> => {
        await apiClient.put('/User/profile', data)
    },
}
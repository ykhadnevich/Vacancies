import apiClient from './client'
import type { AuthResponse } from '../types/auth'

export const authApi = {
    login: async (email: string, password: string): Promise<AuthResponse> => {
        const response = await apiClient.post('/User/login', { email, password })
        return { token: response.data.token, userId: response.data.id, email }
    },

    register: async (email: string, password: string, displayName?: string): Promise<AuthResponse> => {
        const response = await apiClient.post('/User/register', { email, password, displayName })
        return { token: response.data.token, userId: response.data.id, email }
    },
}

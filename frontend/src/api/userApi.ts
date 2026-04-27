import apiClient from './client'

export interface UserProfile {
    id: string
    email: string
    displayName?: string
    category?: string
    skills: string[]
    seniorityLevel: number
    hasCv: boolean
    cvFileName?: string
}

export const userApi = {
    getProfile: async (): Promise<UserProfile> => {
        const response = await apiClient.get('/User/profile')
        return response.data
    },

    updatePreferences: async (data: {
        category?: string
        skills: string[]
        seniorityLevel: number
        displayName?: string
    }): Promise<void> => {
        await apiClient.put('/User/preferences', {
            displayName: data.displayName,
            category: data.category,
            skills: data.skills,
            seniorityLevel: data.seniorityLevel,
            expectedSalary: null,
            workFormat: 1,
            preferredLocation: null,
        })
    },

    uploadCv: async (file: File): Promise<{ extractedLength: number }> => {
        const form = new FormData()
        form.append('file', file)
        const response = await apiClient.post('/User/cv', form, {
            headers: { 'Content-Type': 'multipart/form-data' },
        })
        return response.data
    },
}

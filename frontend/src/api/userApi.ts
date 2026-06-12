import apiClient from './client'
import type { UserRole } from '../types/recruiter'

export type CvStatus = 'NoCv' | 'PendingNormalization' | 'Ready' | 'Failed'

export interface UserProfile {
    id: string
    email: string
    displayName?: string
    role: UserRole
    category?: string
    skills: string[]
    seniorityLevel: number
    hasCv: boolean
    cvFileName?: string
}

export interface CvStatusResponse {
    status:           CvStatus
    hasCv:            boolean
    cvFileName?:      string
    cvRawTextLength:  number
    cvSummaryLength:  number
    cvVersionId:      string
}

export interface NormalizeCvResponse {
    message:          string
    status:           CvStatus
    modelVersion?:    string
    cvSummaryLength?: number
}

export const userApi = {
    getProfile: async (): Promise<UserProfile> => {
        const response = await apiClient.get('/User/profile')
        return response.data
    },

    updatePreferences: async (data: {
        category?:      string
        skills:         string[]
        seniorityLevel: number
        displayName?:   string
    }): Promise<void> => {
        await apiClient.put('/User/preferences', {
            displayName:        data.displayName,
            category:           data.category,
            skills:             data.skills,
            seniorityLevel:     data.seniorityLevel,
            expectedSalary:     null,
            workFormat:         1,
            preferredLocation:  null,
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


    normalizeCv: async (): Promise<NormalizeCvResponse> => {
        const response = await apiClient.post('/User/cv/normalize')
        return response.data
    },


    getCvStatus: async (): Promise<CvStatusResponse> => {
        const response = await apiClient.get('/User/cv/status')
        return response.data
    },

    /**
     * Switches the caller's role and receives a fresh JWT in response. Used by
     * the "Activate recruiter cabinet" toggle on the profile page.
     */
    setRole: async (role: UserRole): Promise<{ role: UserRole; token: string }> => {
        // Backend enum is serialised as a number — 0=Candidate, 1=Recruiter, 2=Both.
        const roleInt = role === 'Recruiter' ? 1 : role === 'Both' ? 2 : 0
        const response = await apiClient.post('/User/role', { role: roleInt })
        return response.data
    },
}

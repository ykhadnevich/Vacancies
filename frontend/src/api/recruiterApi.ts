import apiClient from './client'
import type {
    AddCandidatesResponse,
    AnalyzeResult,
    CandidateAnalysisResultDto,
    CandidateInListDto,
    CandidateListDto,
    CreateVacancyRequest,
    CreateVacancyResponse,
    PastedCandidateInput,
    RecruiterVacancyDto,
} from '../types/recruiter'

export const recruiterApi = {
    createVacancy: async (body: CreateVacancyRequest): Promise<CreateVacancyResponse> => {
        const response = await apiClient.post('/recruiter/vacancy', body)
        return response.data
    },

    createVacancyFromUrl: async (url: string): Promise<CreateVacancyResponse> => {
        const response = await apiClient.post('/recruiter/vacancy/from-url', { url })
        return response.data
    },

    listVacancies: async (): Promise<RecruiterVacancyDto[]> => {
        const response = await apiClient.get('/recruiter/vacancies')
        return response.data
    },

    createList: async (body: { name: string; description?: string }): Promise<{ listId: string }> => {
        const response = await apiClient.post('/recruiter/candidate-list', body)
        return response.data
    },

    listLists: async (): Promise<CandidateListDto[]> => {
        const response = await apiClient.get('/recruiter/candidate-lists')
        return response.data
    },

    getListDetails: async (listId: string): Promise<CandidateInListDto[]> => {
        const response = await apiClient.get(`/recruiter/candidate-list/${listId}`)
        return response.data
    },

    deleteList: async (listId: string): Promise<void> => {
        await apiClient.delete(`/recruiter/candidate-list/${listId}`)
    },

    deleteCandidate: async (candidateId: string): Promise<void> => {
        await apiClient.delete(`/recruiter/candidate/${candidateId}`)
    },

    addCandidatesText: async (
        listId: string,
        candidates: PastedCandidateInput[],
    ): Promise<AddCandidatesResponse> => {
        const response = await apiClient.post(
            `/recruiter/candidate-list/${listId}/candidates`,
            { candidates },
        )
        return response.data
    },

    addCandidatesFiles: async (
        listId: string,
        files: File[],
        candidateNames: (string | undefined)[],
    ): Promise<AddCandidatesResponse> => {
        const form = new FormData()
        for (const file of files) form.append('files', file)
        form.append(
            'candidateNames',
            candidateNames.map((n) => n ?? '').join(','),
        )
        const response = await apiClient.post(
            `/recruiter/candidate-list/${listId}/candidates`,
            form,
            { headers: { 'Content-Type': 'multipart/form-data' } },
        )
        return response.data
    },

    analyze: async (vacancyId: string, listId: string): Promise<AnalyzeResult> => {
        const response = await apiClient.post(
            `/recruiter/vacancy/${vacancyId}/analyze`,
            null,
            { params: { listId } },
        )
        return response.data
    },

    getResults: async (
        vacancyId: string,
        listId: string,
    ): Promise<CandidateAnalysisResultDto[]> => {
        const response = await apiClient.get(
            `/recruiter/vacancy/${vacancyId}/results`,
            { params: { listId } },
        )
        return response.data
    },
}

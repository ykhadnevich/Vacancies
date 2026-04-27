import { create } from 'zustand'
import type { JobVacancy, JobSearchParams } from '../types/job'

interface JobStore {
    jobs: JobVacancy[]
    totalCount: number
    duplicatesRemoved: number
    searchParams: JobSearchParams
    isLoading: boolean
    error: string | null

    setJobs: (jobs: JobVacancy[], totalCount: number, duplicatesRemoved: number) => void
    setSearchParams: (params: Partial<JobSearchParams>) => void
    setLoading: (loading: boolean) => void
    setError: (error: string | null) => void
}

export const useJobStore = create<JobStore>((set) => ({
    jobs: [],
    totalCount: 0,
    duplicatesRemoved: 0,
    searchParams: {
        keywords: '',
        location: 'Ukraine',
        runRelevancePipeline: false,
    },
    isLoading: false,
    error: null,

    setJobs: (jobs, totalCount, duplicatesRemoved) =>
        set({ jobs, totalCount, duplicatesRemoved }),
    setSearchParams: (params) =>
        set((state) => ({ searchParams: { ...state.searchParams, ...params } })),
    setLoading: (isLoading) => set({ isLoading }),
    setError: (error) => set({ error }),
}))
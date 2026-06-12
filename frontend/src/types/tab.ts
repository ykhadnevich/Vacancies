import type { JobSearchParams, ReasoningProvider } from './job'

export interface SearchTab {
    id: string
    label: string
    searchParams: JobSearchParams
    analysisMode: ReasoningProvider
    createdAt: number
}

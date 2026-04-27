import type { JobSearchParams } from './job'

export interface SearchTab {
    id: string
    label: string        // "java · Ukraine"
    searchParams: JobSearchParams
    runMl: boolean
    createdAt: number    // Date.now() — для сортування
}

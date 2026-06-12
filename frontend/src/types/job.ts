


export enum JobSource {
    RobotaUa = 'RobotaUa',
    Jooble   = 'Jooble',
    DOU      = 'DOU',
    LinkedIn = 'LinkedIn',
    WorkUa   = 'WorkUa',
    Djinni   = 'Djinni',
    Manual   = 'Manual',
}

export enum WorkFormat {
    Remote       = 'Remote',
    Office       = 'Office',
    Hybrid       = 'Hybrid',
    NotSpecified = 'NotSpecified',
}

export enum SeniorityLevel {
    Internship   = 'Internship',
    Junior       = 'Junior',
    Middle       = 'Middle',
    Senior       = 'Senior',
    Lead         = 'Lead',
    NotSpecified = 'NotSpecified',
}

export interface JobVacancy {
    id: string
    title: string
    company: string
    location: string
    description?: string
    salary?: string
    primaryUrl: string
    allUrls: string[]
    source: JobSource
    workFormat?: WorkFormat
    seniorityLevel?: SeniorityLevel
    relevanceScore?: number
    relevanceStage?: string
    relevanceReason?: string
    isDuplicate: boolean
    isManuallyAdded: boolean
    publishedAt?: string
}

export interface JobsResponse {
    jobs: JobVacancy[]
    duplicates: JobVacancy[]
    totalCount: number
    duplicatesRemoved: number
    relevancePipelineRan: boolean
}


export type ReasoningProvider = 'None' | 'Groq' | 'Gemini'

export interface JobSearchParams {
    keywords: string
    location?: string
    reasoningProvider?: ReasoningProvider
}
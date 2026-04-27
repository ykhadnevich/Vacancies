export enum JobSource {
    RobotaUa = 0,
    Jooble = 1,
    Dou = 2,
    LinkedIn = 3,
    WorkUa = 4,
    Djinni = 5,
    Manual = 6,
}

export enum WorkFormat {
    Office = 0,
    Remote = 1,
    Hybrid = 2,
}

export enum SeniorityLevel {
    Intern = 0,
    Junior = 1,
    Middle = 2,
    Senior = 3,
    Lead = 4,
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

export interface JobSearchParams {
    keywords: string
    location?: string
    runRelevancePipeline?: boolean
}
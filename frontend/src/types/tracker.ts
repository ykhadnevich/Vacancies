export enum ApplicationStatus {
    InReview = 0,
    Rejected = 1,
    Offer = 2,
    Archived = 3,
}

export enum SeniorityLevel {
    Intern = 0,
    Junior = 1,
    Middle = 2,
    Senior = 3,
    Lead = 4,
}

export interface PipelineSteps {
    cvSent: boolean
    responded: boolean
    followUpSent: boolean
    shortInterview: boolean
    testTask: boolean
    technicalInterview: boolean
    finalInterview: boolean
    jobOffer: boolean
}

export interface TrackerEntry {
    id: string
    jobVacancyId?: string
    title: string
    company: string
    salary?: string
    url?: string
    seniorityLevel?: SeniorityLevel
    status: ApplicationStatus
    pipelineSteps: PipelineSteps
    notes?: string
    addedAt: string
    updatedAt: string
    isManuallyAdded: boolean
}

export interface CreateTrackerEntry {
    title: string
    company: string
    salary?: string
    url?: string
    seniorityLevel?: SeniorityLevel
    jobVacancyId?: string
}
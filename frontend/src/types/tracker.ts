export enum ApplicationStatus {
    InReview = 0,
    Rejected = 1,
    Offer    = 2,
    Archived = 3,
}


export enum SeniorityLevel {
    Internship   = 'Internship',
    Junior       = 'Junior',
    Middle       = 'Middle',
    Senior       = 'Senior',
    Lead         = 'Lead',
    NotSpecified = 'NotSpecified',
}

export interface PipelineSteps {
    cvSent:             boolean
    responded:          boolean
    followUpSent:       boolean
    shortInterview:     boolean
    testTask:           boolean
    technicalInterview: boolean
    finalInterview:     boolean
    jobOffer:           boolean
}

export interface TrackerEntry {
    id:               string
    jobVacancyId?:    string
    title:            string
    company:          string
    location?:        string
    salary?:          string
    url?:             string
    seniorityLevel?:  SeniorityLevel
    status:           ApplicationStatus
    pipelineSteps:    PipelineSteps
    notes?:           string
    addedAt:          string
    updatedAt:        string
    isManuallyAdded:  boolean


    score?:              number | null
    verdict?:            string | null
    matchedSkills?:      string[] | null
    missingMustHaves?:   string[] | null
    triggeredAntiFlags?: string[] | null
    reasonShort?:        string | null
    strengthsEn?:        string | null
    strengthsUk?:        string | null
    gapsEn?:             string | null
    gapsUk?:             string | null
    recommendationEn?:   string | null
    recommendationUk?:   string | null
    subScores?:          Record<string, number> | null
    cvFileName?:         string | null
    pipelineVersion?:    string | null
    analyzedAt?:         string | null
}

export interface CreateTrackerEntry {
    title:           string
    company:         string
    location?:       string
    salary?:         string
    url?:            string
    seniorityLevel?: SeniorityLevel
    jobVacancyId?:   string


    score?:              number
    verdict?:            string
    matchedSkills?:      string[]
    missingMustHaves?:   string[]
    triggeredAntiFlags?: string[]
    reasonShort?:        string
    strengthsEn?:        string
    strengthsUk?:        string
    gapsEn?:             string
    gapsUk?:             string
    recommendationEn?:   string
    recommendationUk?:   string
    subScores?:          Record<string, number>
    pipelineVersion?:    string
}

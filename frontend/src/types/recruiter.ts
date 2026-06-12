export type UserRole = 'Candidate' | 'Recruiter' | 'Both'

export type NormalizationStatus = 'Pending' | 'Normalized' | 'Failed'

export type AnalyzeStatusValue =
    | 'Completed'
    | 'AlreadyRunning'
    | 'VacancyNotNormalized'
    | 'NothingToScore'

export interface RecruiterVacancyDto {
    id:                    string
    title:                 string
    company:               string
    location?:             string | null
    description?:          string | null
    createdAt:             string
    isNormalized:          boolean
    scoredCandidatesCount: number
}

export interface CandidateListDto {
    id:                   string
    name:                 string
    description?:         string | null
    createdAt:            string
    totalCandidates:      number
    normalizedCandidates: number
    failedCandidates:     number
}

export interface CandidateInListDto {
    id:                  string
    candidateName?:      string | null
    normalizationStatus: NormalizationStatus
    lastError?:          string | null
    addedAt:             string
}

export interface CandidateAnalysisResultDto {
    candidateId:        string
    candidateName?:     string | null
    score:              number
    verdict:            string
    reasonUk?:          string | null
    reasonEn?:          string | null
    matchedSkills:      string[]
    missingMustHaves:   string[]
    triggeredAntiFlags: string[]
    subScores:          Record<string, number>
    antiFlagPenalty:    number
    confidence?:        number | null
    inputTokens:        number
    outputTokens:       number
    estimatedCostUsd:   number
    modelVersion:       string
    scoredAt:           string
}

export interface AnalyzeResult {
    status:         AnalyzeStatusValue
    newlyScored:    number
    alreadyScored:  number
    skipped:        number
    failed:         number
    scoringVersion: string
}

export interface CreateVacancyRequest {
    title:          string
    company:        string
    rawDescription: string
    location?:      string | null
}

export interface CreateVacancyResponse {
    vacancyId:              string
    normalizationSucceeded: boolean
    normalizationError?:    string | null
}

export interface AddedCandidateSummary {
    candidateId:    string
    candidateName?: string | null
    normalized:     boolean
    error?:         string | null
}

export interface AddCandidatesResponse {
    normalized: number
    failed:     number
    items:      AddedCandidateSummary[]
}

export interface PastedCandidateInput {
    cvRawText:      string
    candidateName?: string
}

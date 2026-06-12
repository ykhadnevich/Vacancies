import { JobSource, WorkFormat, SeniorityLevel } from './job'


export type Verdict = 'Strong' | 'Partial' | 'Weak' | 'Mismatch'


export type SubScoreKey =
    | 'skill_match'
    | 'seniority_match'
    | 'experience_match'
    | 'language_match'
    | 'education_match'
    | 'role_intent_match'
    | 'domain_alignment'

export type SubScores = Partial<Record<SubScoreKey, number>>


export const SUB_SCORE_WEIGHTS: Record<SubScoreKey, number> = {
    skill_match:       0.40,
    seniority_match:   0.15,
    experience_match:  0.15,
    language_match:    0.05,
    education_match:   0.02,
    role_intent_match: 0.15,
    domain_alignment:  0.08,
}

export const SUB_SCORE_LABELS: Record<SubScoreKey, { en: string; uk: string }> = {
    skill_match:       { en: 'Skills',      uk: 'Навички' },
    seniority_match:   { en: 'Seniority',   uk: 'Рівень'  },
    experience_match:  { en: 'Experience',  uk: 'Досвід'  },
    language_match:    { en: 'Language',    uk: 'Мова'    },
    education_match:   { en: 'Education',   uk: 'Освіта'  },
    role_intent_match: { en: 'Role intent', uk: 'Роль'    },
    domain_alignment:  { en: 'Domain',      uk: 'Домен'   },
}


export const SUB_SCORE_ORDER: SubScoreKey[] = [
    'skill_match',
    'role_intent_match',
    'seniority_match',
    'experience_match',
    'domain_alignment',
    'language_match',
    'education_match',
]


export interface JobVacancyV6 {
    id:              string
    title:           string
    company:         string
    location?:       string
    description?:    string | null
    source:          JobSource
    workFormat:      WorkFormat
    seniorityLevel:  SeniorityLevel
    category?:       string | null
    urls:            string[]
    publishedAt:     string


    score:           number
    verdict:         Verdict


    reasonEn:        string
    reasonUk:        string

    subScores:       SubScores
    antiFlagPenalty: number

    matchedSkills:      string[]
    missingMustHaves:   string[]
    triggeredAntiFlags: string[]

    pipelineVersion:  string


    strengthsEn?:      string | null
    strengthsUk?:      string | null
    gapsEn?:           string | null
    gapsUk?:           string | null
    recommendationEn?: string | null
    recommendationUk?: string | null
}


export function primaryUrlOf(job: JobVacancyV6): string {
    return job.urls?.[0] ?? ''
}


export interface JobsV6Response {
    jobs:              JobVacancyV6[]
    totalReturned:     number
    totalAvailable:    number
    skippedNoAnalysis: number
    pipelineVersion:   string
}

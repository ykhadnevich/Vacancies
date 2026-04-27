export interface UserProfile {
    id: string
    email: string
    fullName?: string
    skills: string[]
    preferredLocation?: string
    expectedSalary?: number
    preferredWorkFormat?: number
}

export interface UpdateProfileRequest {
    fullName?: string
    skills: string[]
    preferredLocation?: string
    expectedSalary?: number
    preferredWorkFormat?: number
}
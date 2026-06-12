import { create } from 'zustand'
import { del } from 'idb-keyval'
import { QUERY_STORAGE_KEY } from '../persistence/queryPersister'
import { queryClient } from '../main'
import type { UserRole } from '../types/recruiter'

interface AuthStore {
    token: string | null
    userId: string | null
    email: string | null
    role: UserRole
    isAuthenticated: boolean

    login: (token: string, userId: string, email: string) => void
    logout: () => Promise<void>
    setRole: (role: UserRole, token?: string) => void
}

function readRole(): UserRole {
    const raw = localStorage.getItem('role')
    if (raw === 'Recruiter' || raw === 'Both' || raw === 'Candidate') return raw
    return 'Candidate'
}

export const useAuthStore = create<AuthStore>((set) => ({
    token: localStorage.getItem('token'),
    userId: localStorage.getItem('userId'),
    email: localStorage.getItem('email'),
    role: readRole(),
    isAuthenticated: !!localStorage.getItem('token'),

    login: (token, userId, email) => {
        queryClient.clear()
        localStorage.setItem('token', token)
        localStorage.setItem('userId', userId)
        localStorage.setItem('email', email)
        // Role defaults to Candidate on login; the next /User/profile fetch
        // will hydrate the real value via setRole.
        localStorage.setItem('role', 'Candidate')
        set({ token, userId, email, role: 'Candidate', isAuthenticated: true })
    },

    logout: async () => {
        localStorage.removeItem('token')
        localStorage.removeItem('userId')
        localStorage.removeItem('email')
        localStorage.removeItem('role')
        Object.keys(localStorage)
            .filter((k) => k.startsWith('vacancies_tabs:'))
            .forEach((k) => localStorage.removeItem(k))
        try {
            await del(QUERY_STORAGE_KEY)
        } catch {

        }
        queryClient.clear()
        set({ token: null, userId: null, email: null, role: 'Candidate', isAuthenticated: false })
    },

    setRole: (role, token) => {
        localStorage.setItem('role', role)
        if (token) localStorage.setItem('token', token)
        set((s) => ({ role, token: token ?? s.token }))
    },
}))

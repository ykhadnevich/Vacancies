import { create } from 'zustand'

interface AuthStore {
    token: string | null
    userId: string | null
    email: string | null
    isAuthenticated: boolean

    login: (token: string, userId: string, email: string) => void
    logout: () => void
}

export const useAuthStore = create<AuthStore>((set) => ({
    token: localStorage.getItem('token'),
    userId: localStorage.getItem('userId'),
    email: localStorage.getItem('email'),
    isAuthenticated: !!localStorage.getItem('token'),

    login: (token, userId, email) => {
        localStorage.setItem('token', token)
        localStorage.setItem('userId', userId)
        localStorage.setItem('email', email)
        set({ token, userId, email, isAuthenticated: true })
    },

    logout: () => {
        localStorage.removeItem('token')
        localStorage.removeItem('userId')
        localStorage.removeItem('email')
        set({ token: null, userId: null, email: null, isAuthenticated: false })
    },
}))
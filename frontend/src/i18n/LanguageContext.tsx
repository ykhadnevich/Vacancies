import {
    createContext,
    useContext,
    useState,
    useEffect,
    useCallback,
    type ReactNode,
} from 'react'
import { translations, type Language } from './translations'

const STORAGE_KEY = 'vacancies_language'


export const SUPPORTED_LANGUAGES = Object.keys(translations) as Language[]

interface LanguageContextValue {
    language:    Language
    setLanguage: (lang: Language) => void
    toggle:      () => void
}

const LanguageContext = createContext<LanguageContextValue | null>(null)


export function LanguageProvider({ children }: { children: ReactNode }) {
    const [language, setLanguageState] = useState<Language>(() => {
        const saved = localStorage.getItem(STORAGE_KEY) as Language | null
        return saved && SUPPORTED_LANGUAGES.includes(saved) ? saved : 'uk'
    })

    useEffect(() => {
        localStorage.setItem(STORAGE_KEY, language)


        document.documentElement.lang = language
    }, [language])

    const setLanguage = useCallback((lang: Language) => {
        if (SUPPORTED_LANGUAGES.includes(lang)) setLanguageState(lang)
    }, [])

    const toggle = useCallback(() => {
        setLanguageState((prev) => {
            const idx = SUPPORTED_LANGUAGES.indexOf(prev)
            return SUPPORTED_LANGUAGES[(idx + 1) % SUPPORTED_LANGUAGES.length]
        })
    }, [])

    return (
        <LanguageContext.Provider value={{ language, setLanguage, toggle }}>
            {children}
        </LanguageContext.Provider>
    )
}

export function useLanguage(): LanguageContextValue {
    const ctx = useContext(LanguageContext)
    if (!ctx) throw new Error('useLanguage must be used within LanguageProvider')
    return ctx
}

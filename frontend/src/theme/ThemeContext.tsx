import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'

/**
 * Theme system — light / dark mode toggle.
 *
 * Persists the user's choice to <code>localStorage</code> so it survives reloads;
 * on first visit honours the OS-level <code>prefers-color-scheme</code> media query.
 * Applies the choice by setting <code>document.documentElement.dataset.theme</code>,
 * which the dark-mode token override in <code>styles/tokens.css</code> reads via the
 * <code>:root[data-theme='dark']</code> selector. No per-component dark-mode code —
 * every existing var token flips automatically.
 */
type Theme = 'light' | 'dark'

interface ThemeApi {
    theme:  Theme
    toggle: () => void
    setTheme: (t: Theme) => void
}

const STORAGE_KEY = 'vakansio.theme'
const ThemeContext = createContext<ThemeApi | null>(null)

function initialTheme(): Theme {
    if (typeof window === 'undefined') return 'light'
    const stored = window.localStorage.getItem(STORAGE_KEY)
    if (stored === 'light' || stored === 'dark') return stored
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

export function ThemeProvider({ children }: { children: ReactNode }) {
    const [theme, setTheme] = useState<Theme>(initialTheme)

    // Reflect into the document root + persist.
    useEffect(() => {
        const root = document.documentElement
        root.dataset.theme = theme
        window.localStorage.setItem(STORAGE_KEY, theme)
    }, [theme])

    const toggle = useCallback(() => {
        setTheme((prev) => (prev === 'light' ? 'dark' : 'light'))
    }, [])

    const api = useMemo<ThemeApi>(() => ({ theme, toggle, setTheme }), [theme, toggle])
    return <ThemeContext.Provider value={api}>{children}</ThemeContext.Provider>
}

export function useTheme(): ThemeApi {
    const api = useContext(ThemeContext)
    if (!api) throw new Error('useTheme must be used inside <ThemeProvider>')
    return api
}

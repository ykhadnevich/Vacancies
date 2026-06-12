import { useEffect, useState } from 'react'

/**
 * Lightweight viewport hook: tells you whether the window currently matches
 * the mobile breakpoint (640px). Listens via `matchMedia` so it reacts to
 * orientation changes without polling. Defaults to `false` on the server.
 */
export function useIsMobile(breakpoint = 640): boolean {
    const query = `(max-width: ${breakpoint}px)`
    const [isMobile, setIsMobile] = useState<boolean>(() => {
        if (typeof window === 'undefined') return false
        return window.matchMedia(query).matches
    })

    useEffect(() => {
        if (typeof window === 'undefined') return
        const mql = window.matchMedia(query)
        const onChange = (e: MediaQueryListEvent) => setIsMobile(e.matches)
        mql.addEventListener('change', onChange)
        return () => mql.removeEventListener('change', onChange)
    }, [query])

    return isMobile
}

import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import Icon from './Icon'
import { useT } from '../../i18n/useT'

/**
 * Lightweight toast system — replaces ad-hoc <code>alert()</code> calls.
 *
 * Design constraints (matches the rest of this codebase):
 * <ul>
 *   <li>No external dependency. We pull the toast UI from the same
 *       <code>var(--color-*)</code> token surface every other component uses.</li>
 *   <li>Singleton provider mounted once in <code>main.tsx</code>. Components
 *       call <code>useToast()</code> to push notifications.</li>
 *   <li>Each toast auto-dismisses after 4 s (configurable). The user can
 *       click the × to dismiss immediately.</li>
 *   <li>Variants: <code>success</code> / <code>error</code> / <code>info</code>.</li>
 * </ul>
 */
type ToastVariant = 'success' | 'error' | 'info'

interface ToastMessage {
    id:       number
    text:     string
    variant:  ToastVariant
}

interface ToastApi {
    push:    (text: string, variant?: ToastVariant) => void
    success: (text: string) => void
    error:   (text: string) => void
    info:    (text: string) => void
}

const ToastContext = createContext<ToastApi | null>(null)

const DEFAULT_TTL_MS = 4_000
let toastIdSeq = 0

const VARIANT_COLORS: Record<ToastVariant, { bg: string; fg: string; border: string }> = {
    success: { bg: 'var(--color-success-50)',  fg: 'var(--color-success-700)',  border: 'var(--color-success-300)'  },
    error:   { bg: 'var(--color-danger-50)',   fg: 'var(--color-danger-700)',   border: 'var(--color-danger-300)'   },
    info:    { bg: 'var(--color-info-50)',     fg: 'var(--color-info-700)',     border: 'var(--color-info-300)'     },
}

const VARIANT_ICON: Record<ToastVariant, 'check-circle' | 'alert-circle' | 'info'> = {
    success: 'check-circle',
    error:   'alert-circle',
    info:    'info',
}

/**
 * Mount once near the root (above the BrowserRouter / app tree).
 * Renders a single stacked column of toast cards in the corner.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
    const [toasts, setToasts] = useState<ToastMessage[]>([])
    // Track active auto-dismiss timers so unmounting the provider doesn't leave
    // orphan `setState` callbacks firing on a dead tree (React 19 warns and
    // it's a memory leak in long-lived tabs that lazy-load modals).
    const timersRef = useRef<Map<number, number>>(new Map())

    const dismiss = useCallback((id: number) => {
        const handle = timersRef.current.get(id)
        if (handle !== undefined) {
            window.clearTimeout(handle)
            timersRef.current.delete(id)
        }
        setToasts((prev) => prev.filter((t) => t.id !== id))
    }, [])

    const push = useCallback((text: string, variant: ToastVariant = 'info') => {
        const id = ++toastIdSeq
        setToasts((prev) => [...prev, { id, text, variant }])
        const handle = window.setTimeout(() => dismiss(id), DEFAULT_TTL_MS)
        timersRef.current.set(id, handle)
    }, [dismiss])

    // Clear every active timer on unmount.
    useEffect(() => {
        const timers = timersRef.current
        return () => {
            timers.forEach((handle) => window.clearTimeout(handle))
            timers.clear()
        }
    }, [])

    const api: ToastApi = {
        push,
        success: (t) => push(t, 'success'),
        error:   (t) => push(t, 'error'),
        info:    (t) => push(t, 'info'),
    }

    return (
        <ToastContext.Provider value={api}>
            {children}
            <div
                aria-live="polite"
                aria-atomic="true"
                style={{
                    position:       'fixed',
                    bottom:         24,
                    right:          24,
                    display:        'flex',
                    flexDirection:  'column',
                    gap:            8,
                    zIndex:         9999,
                    pointerEvents:  'none',
                }}
            >
                {toasts.map((t) => (
                    <ToastCard key={t.id} toast={t} onDismiss={() => dismiss(t.id)} />
                ))}
            </div>
        </ToastContext.Provider>
    )
}

function ToastCard({ toast, onDismiss }: { toast: ToastMessage; onDismiss: () => void }) {
    const colors = VARIANT_COLORS[toast.variant]
    const t = useT()
    const [mounted, setMounted] = useState(false)
    // Animation entry: render with mounted=false, then trigger CSS transition.
    useEffect(() => {
        // eslint-disable-next-line react-hooks/set-state-in-effect
        setMounted(true)
    }, [])
    return (
        <div
            role="status"
            style={{
                pointerEvents:  'auto',
                display:        'flex',
                alignItems:     'flex-start',
                gap:            10,
                minWidth:       260,
                maxWidth:       400,
                padding:        '10px 12px',
                background:     colors.bg,
                color:          colors.fg,
                border:         `1px solid ${colors.border}`,
                borderRadius:   'var(--radius-md)',
                boxShadow:      'var(--shadow-lg)',
                fontSize:       'var(--text-sm)',
                lineHeight:     1.4,
                opacity:        mounted ? 1 : 0,
                transform:      mounted ? 'translateY(0)' : 'translateY(8px)',
                transition:     'opacity 180ms ease-out, transform 180ms ease-out',
            }}
        >
            <Icon name={VARIANT_ICON[toast.variant]} size={16} />
            <span style={{ flex: 1 }}>{toast.text}</span>
            <button
                onClick={onDismiss}
                aria-label={t('common.dismiss')}
                style={{
                    background: 'transparent',
                    border:     'none',
                    cursor:     'pointer',
                    padding:    0,
                    color:      'inherit',
                    opacity:    0.7,
                    display:    'flex',
                }}
            >
                <Icon name="close" size={14} />
            </button>
        </div>
    )
}

/** Returns the toast API. Throws if called outside <code>ToastProvider</code>. */
// eslint-disable-next-line react-refresh/only-export-components
export function useToast(): ToastApi {
    const api = useContext(ToastContext)
    if (!api) throw new Error('useToast must be used inside <ToastProvider>')
    return api
}

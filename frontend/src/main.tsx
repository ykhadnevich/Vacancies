import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClient } from '@tanstack/react-query'
import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client'
import * as Sentry from '@sentry/react'

import './index.css'
import App from './App.tsx'
import { queryPersister } from './persistence/queryPersister'
import { migrateLegacyCache } from './persistence/migration'
import { attachMultiTabSync } from './persistence/multiTabSync'
import { evictIfOversize } from './persistence/cacheEviction'
import { LanguageProvider } from './i18n/LanguageContext'
import { useT } from './i18n/useT'
import { ToastProvider } from './components/ui/Toast'
import { ThemeProvider } from './theme/ThemeContext'

// VITE_SENTRY_DSN injected at build time (deploy-frontend.yml); empty → SDK skipped.
const sentryDsn = import.meta.env.VITE_SENTRY_DSN
if (sentryDsn) {
    Sentry.init({
        dsn:                sentryDsn,
        environment:        import.meta.env.MODE,
        release:            import.meta.env.VITE_RELEASE,
        tracesSampleRate:   0,
        replaysSessionSampleRate: 0,
        replaysOnErrorSampleRate: 0,
        sendDefaultPii:     false,
    })
}

export const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            staleTime:            1000 * 60 * 10,
            gcTime:               1000 * 60 * 60 * 24,
            refetchOnWindowFocus: false,
            refetchOnMount:       false,
        },
    },
})

void migrateLegacyCache()
void evictIfOversize()
setInterval(() => { void evictIfOversize() }, 1000 * 60 * 5)
attachMultiTabSync(queryClient)

function ErrorBoundaryFallback() {
    const t = useT()
    return (
        <div style={{
            padding:        24,
            maxWidth:       480,
            margin:         '64px auto',
            textAlign:      'center',
            color:          'var(--color-text-primary)',
        }}>
            <h2 style={{ margin: '0 0 8px 0', fontSize: 'var(--text-lg)' }}>
                {t('errors.boundary.title')}
            </h2>
            <p style={{ margin: '0 0 16px 0', color: 'var(--color-text-secondary)' }}>
                {t('errors.boundary.description')}
            </p>
            <button
                onClick={() => window.location.reload()}
                style={{
                    padding:        '8px 16px',
                    fontSize:       'var(--text-md)',
                    border:         '1px solid var(--color-border-default)',
                    borderRadius:   'var(--radius-md)',
                    background:     'var(--color-bg-surface)',
                    color:          'var(--color-text-primary)',
                    cursor:         'pointer',
                }}
            >
                {t('errors.boundary.reload')}
            </button>
        </div>
    )
}

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <BrowserRouter>
            <PersistQueryClientProvider
                client={queryClient}
                persistOptions={{
                    persister: queryPersister,
                    maxAge:    1000 * 60 * 60 * 72,
                    buster:    'v6.7.2',
                }}
            >
                <ThemeProvider>
                    <LanguageProvider>
                        <Sentry.ErrorBoundary fallback={<ErrorBoundaryFallback />}>
                            <ToastProvider>
                                <App />
                            </ToastProvider>
                        </Sentry.ErrorBoundary>
                    </LanguageProvider>
                </ThemeProvider>
            </PersistQueryClientProvider>
        </BrowserRouter>
    </StrictMode>,
)

import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClient } from '@tanstack/react-query'
import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client'
import { createSyncStoragePersister } from '@tanstack/query-sync-storage-persister'
import './index.css'
import App from './App.tsx'

const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            staleTime: 1000 * 60 * 10,
            gcTime: 1000 * 60 * 60 * 24,
            refetchOnWindowFocus: false,
            refetchOnMount: false,
        },
    },
})

const persister = createSyncStoragePersister({
    storage: window.localStorage,
    key: 'vacancies_query_cache',
    throttleTime: 1000,
})

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <BrowserRouter>
            <PersistQueryClientProvider
                client={queryClient}
                persistOptions={{
                    persister,
                    maxAge: 1000 * 60 * 60 * 24,
                }}
            >
                <App />
            </PersistQueryClientProvider>
        </BrowserRouter>
    </StrictMode>,
)

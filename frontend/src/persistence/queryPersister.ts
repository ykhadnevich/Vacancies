import { createAsyncStoragePersister } from '@tanstack/query-async-storage-persister'
import { get, set, del } from 'idb-keyval'


const STORAGE_KEY = 'vacancies_query_cache_v2'

const idbStorage = {
    getItem:    async (key: string): Promise<string | null> => {
        const value = await get<string>(key)
        return value ?? null
    },
    setItem:    async (key: string, value: string): Promise<void> => {
        await set(key, value)
    },
    removeItem: async (key: string): Promise<void> => {
        await del(key)
    },
}

export const queryPersister = createAsyncStoragePersister({
    storage:       idbStorage,
    key:           STORAGE_KEY,
    throttleTime:  1000,
})


export const QUERY_STORAGE_KEY = STORAGE_KEY

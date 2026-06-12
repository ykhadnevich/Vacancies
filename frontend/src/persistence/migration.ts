import { set } from 'idb-keyval'
import { QUERY_STORAGE_KEY } from './queryPersister'

const LEGACY_LOCALSTORAGE_KEY = 'vacancies_query_cache'


export async function migrateLegacyCache(): Promise<void> {
    const legacy = localStorage.getItem(LEGACY_LOCALSTORAGE_KEY)
    if (!legacy) return
    try {
        await set(QUERY_STORAGE_KEY, legacy)
    } catch {

    } finally {
        localStorage.removeItem(LEGACY_LOCALSTORAGE_KEY)
    }
}


export async function clearAllQueryCache(): Promise<void> {
    const { del } = await import('idb-keyval')
    await del(QUERY_STORAGE_KEY).catch(() => {  })
}

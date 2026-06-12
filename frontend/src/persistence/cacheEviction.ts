import { get, del } from 'idb-keyval'
import { QUERY_STORAGE_KEY } from './queryPersister'

const TARGET_SIZE_BYTES = 8 * 1024 * 1024
const HARD_LIMIT_BYTES  = 15 * 1024 * 1024

interface EvictionResult {
    keptBytes: number
    cleared:   boolean
}


export async function evictIfOversize(): Promise<EvictionResult> {
    try {
        const blob = await get<string>(QUERY_STORAGE_KEY)
        if (!blob) return { keptBytes: 0, cleared: false }


        const sizeBytes = blob.length * 2
        if (sizeBytes > HARD_LIMIT_BYTES) {
            await del(QUERY_STORAGE_KEY)
            return { keptBytes: 0, cleared: true }
        }
        return { keptBytes: sizeBytes, cleared: false }
    } catch {
        return { keptBytes: 0, cleared: false }
    }
}

export { HARD_LIMIT_BYTES, TARGET_SIZE_BYTES }



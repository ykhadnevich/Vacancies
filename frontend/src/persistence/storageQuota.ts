export async function isStorageAvailable(): Promise<boolean> {
    if (typeof indexedDB === 'undefined') return false
    try {
        const { set, get, del } = await import('idb-keyval')
        await set('_probe', 1)
        const v = await get('_probe')
        await del('_probe')
        return v === 1
    } catch {
        return false
    }
}

export async function getStorageEstimate(): Promise<{
    usageBytes: number
    quotaBytes: number
    percentUsed: number
}> {
    if (!navigator.storage?.estimate) {
        return { usageBytes: 0, quotaBytes: 0, percentUsed: 0 }
    }
    const est = await navigator.storage.estimate()
    const usage = est.usage ?? 0
    const quota = est.quota ?? 0
    return {
        usageBytes:  usage,
        quotaBytes:  quota,
        percentUsed: quota > 0 ? (usage / quota) * 100 : 0,
    }
}

export function isQuotaExceededError(e: unknown): boolean {
    if (!(e instanceof Error)) return false
    if (!(e instanceof DOMException)) return e.message.includes('quota')
    return e.name === 'QuotaExceededError'
        || e.name === 'NS_ERROR_DOM_QUOTA_REACHED'
        || (e as DOMException).code === 22
        || (e as DOMException).code === 1014
}

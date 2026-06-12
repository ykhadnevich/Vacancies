import type { QueryClient } from '@tanstack/react-query'

const CHANNEL_NAME = 'vacancies_cache_sync'

interface InvalidateMessage {
    type: 'INVALIDATE_QUERY'
    queryKey: readonly unknown[]
}

interface LogoutMessage {
    type: 'LOGOUT'
}

type SyncMessage = InvalidateMessage | LogoutMessage


export function attachMultiTabSync(queryClient: QueryClient): () => void {
    if (typeof BroadcastChannel === 'undefined') {

        return () => {}
    }
    const bc = new BroadcastChannel(CHANNEL_NAME)

    bc.onmessage = (e: MessageEvent<SyncMessage>) => {
        const msg = e.data
        if (!msg) return
        switch (msg.type) {
            case 'INVALIDATE_QUERY':
                queryClient.invalidateQueries({ queryKey: msg.queryKey as never[] })
                break
            case 'LOGOUT':
                queryClient.clear()
                break
        }
    }

    return () => bc.close()
}

function getChannel(): BroadcastChannel | null {
    if (typeof BroadcastChannel === 'undefined') return null
    return new BroadcastChannel(CHANNEL_NAME)
}

export function broadcastInvalidate(queryKey: readonly unknown[]): void {
    const ch = getChannel()
    if (!ch) return
    ch.postMessage({ type: 'INVALIDATE_QUERY', queryKey } satisfies InvalidateMessage)
    ch.close()
}

export function broadcastLogout(): void {
    const ch = getChannel()
    if (!ch) return
    ch.postMessage({ type: 'LOGOUT' } satisfies LogoutMessage)
    ch.close()
}

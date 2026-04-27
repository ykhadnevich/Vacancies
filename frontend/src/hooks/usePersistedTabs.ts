import { useState } from 'react'
import type { SearchTab } from '../types/tab'

// Ключі в localStorage прив'язані до userId → різні юзери мають різні таби
function tabsKey(userId: string | null)  { return userId ? `vacancies_tabs_${userId}`   : null }
function activeKey(userId: string | null){ return userId ? `vacancies_active_${userId}` : null }

function readTabs(userId: string | null): SearchTab[] {
    const key = tabsKey(userId)
    if (!key) return []
    try {
        const raw = localStorage.getItem(key)
        return raw ? (JSON.parse(raw) as SearchTab[]) : []
    } catch {
        return []
    }
}

function readActiveId(userId: string | null): string | null {
    const key = activeKey(userId)
    if (!key) return null
    return localStorage.getItem(key)
}

// ─────────────────────────────────────────────────────────────────────────────

export function usePersistedTabs(userId: string | null) {

    // Ініціалізуємо одразу з localStorage — без useEffect, без "мерехтіння"
    const [tabs, setTabsState] = useState<SearchTab[]>(() => readTabs(userId))
    const [activeTabId, setActiveTabIdState] = useState<string | null>(() => {
        const saved = readActiveId(userId)
        const tabs  = readTabs(userId)
        // Якщо збережений activeId вже не існує серед табів — беремо перший
        if (saved && tabs.some(t => t.id === saved)) return saved
        return tabs[0]?.id ?? null
    })

    // Обгортки що синхронно зберігають в localStorage при кожній зміні
    const setTabs = (updater: SearchTab[] | ((prev: SearchTab[]) => SearchTab[])) => {
        setTabsState(prev => {
            const next = typeof updater === 'function' ? updater(prev) : updater
            const key = tabsKey(userId)
            if (key) localStorage.setItem(key, JSON.stringify(next))
            return next
        })
    }

    const setActiveTabId = (id: string | null) => {
        setActiveTabIdState(id)
        const key = activeKey(userId)
        if (!key) return
        if (id) localStorage.setItem(key, id)
        else    localStorage.removeItem(key)
    }

    // Додати новий таб
    const addTab = (tab: SearchTab) => {
        setTabs(prev => [...prev, tab])
        setActiveTabId(tab.id)
    }

    // Закрити таб (видаляємо зі списку, переходимо на сусідній)
    const closeTab = (tabId: string) => {
        setTabs(prev => {
            const next = prev.filter(t => t.id !== tabId)
            const key = tabsKey(userId)
            if (key) localStorage.setItem(key, JSON.stringify(next))

            // Якщо закриваємо активний — визначаємо новий активний
            if (activeTabId === tabId) {
                const idx  = prev.findIndex(t => t.id === tabId)
                const next2 = next[idx] ?? next[idx - 1] ?? null
                setActiveTabId(next2?.id ?? null)
            }

            return next
        })
    }

    return { tabs, activeTabId, addTab, closeTab, setActiveTabId }
}

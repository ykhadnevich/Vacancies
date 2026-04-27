import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { jobsApi } from '../../api/jobsApi'
import { useAuthStore } from '../../store/authStore'
import { usePersistedTabs } from '../../hooks/usePersistedTabs'
import type { SearchTab } from '../../types/tab'
import type { JobSearchParams } from '../../types/job'
import JobCard from '../../components/JobCard'
import SearchBar from '../../components/SearchBar'
import ManualUrlInput from '../../components/ManualUrlInput'
import ManualVacanciesSection from '../../components/ManualVacanciesSection'

const sourceOptions = [
    { value: '', label: 'Всі джерела' },
    { value: '0', label: 'robota.ua' },
    { value: '2', label: 'dou' },
    { value: '3', label: 'linkedin' },
    { value: '4', label: 'work.ua' },
    { value: '5', label: 'djinni' },
    { value: '6', label: 'вручну' },
]

const formatOptions = [
    { value: '', label: 'Будь-який формат' },
    { value: '0', label: 'Офіс' },
    { value: '1', label: 'Remote' },
    { value: '2', label: 'Гібрид' },
]

const seniorityOptions = [
    { value: '', label: 'Будь-який рівень' },
    { value: '0', label: 'Intern' },
    { value: '1', label: 'Junior' },
    { value: '2', label: 'Middle' },
    { value: '3', label: 'Senior' },
    { value: '4', label: 'Lead' },
]

function MlToggle({ enabled, onChange, disabled }: {
    enabled: boolean; onChange: () => void; disabled: boolean
}) {
    return (
        <button
            onClick={onChange}
            disabled={disabled}
            title={enabled ? 'Вимкнути ML-аналіз' : 'Увімкнути ML-аналіз релевантності (Gemini)'}
            style={{
                display: 'flex', alignItems: 'center', gap: 8,
                padding: '7px 14px', borderRadius: 8,
                border: `1.5px solid ${enabled ? '#7c3aed' : '#e5e7eb'}`,
                background: enabled ? '#f5f3ff' : '#fff',
                color: enabled ? '#7c3aed' : '#6b7280',
                cursor: disabled ? 'wait' : 'pointer',
                fontSize: 14, fontWeight: enabled ? 600 : 400,
                transition: 'all 0.15s', whiteSpace: 'nowrap',
            }}
        >
            <span style={{
                position: 'relative', display: 'inline-block',
                width: 32, height: 18, borderRadius: 9,
                background: enabled ? '#7c3aed' : '#d1d5db',
                transition: 'background 0.15s', flexShrink: 0,
            }}>
                <span style={{
                    position: 'absolute', top: 2,
                    left: enabled ? 16 : 2,
                    width: 14, height: 14, borderRadius: '50%',
                    background: '#fff', transition: 'left 0.15s',
                    boxShadow: '0 1px 2px rgba(0,0,0,0.2)',
                }} />
            </span>
            🤖 ML-аналіз
        </button>
    )
}

function TabBar({ tabs, activeId, onSelect, onClose }: {
    tabs: SearchTab[]
    activeId: string | null
    onSelect: (id: string) => void
    onClose: (id: string) => void
}) {
    if (tabs.length === 0) return null

    return (
        <div style={{
            display: 'flex', gap: 4, overflowX: 'auto',
            borderBottom: '2px solid #e5e7eb',
            scrollbarWidth: 'none',
        }}>
            {tabs.map(tab => {
                const isActive = tab.id === activeId
                return (
                    <div
                        key={tab.id}
                        style={{
                            display: 'flex', alignItems: 'center', gap: 6,
                            padding: '8px 14px',
                            borderRadius: '8px 8px 0 0',
                            border: '1px solid',
                            borderColor: isActive ? '#e5e7eb' : 'transparent',
                            borderBottom: isActive ? '2px solid #fff' : '1px solid transparent',
                            marginBottom: isActive ? -2 : 0,
                            background: isActive ? '#fff' : 'transparent',
                            cursor: 'pointer',
                            whiteSpace: 'nowrap',
                            flexShrink: 0,
                            transition: 'background 0.1s',
                            userSelect: 'none',
                        }}
                        onClick={() => onSelect(tab.id)}
                    >
                        {tab.runMl && (
                            <span title="ML увімкнено" style={{ fontSize: 11 }}>🤖</span>
                        )}
                        <span style={{
                            fontSize: 13,
                            fontWeight: isActive ? 600 : 400,
                            color: isActive ? '#111827' : '#6b7280',
                            maxWidth: 160,
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                        }}>
                            {tab.label}
                        </span>
                        <button
                            onClick={e => { e.stopPropagation(); onClose(tab.id) }}
                            title="Закрити таб"
                            style={{
                                background: 'none', border: 'none',
                                cursor: 'pointer', padding: '0 2px',
                                color: '#9ca3af', fontSize: 16, lineHeight: 1,
                                borderRadius: 4, display: 'flex', alignItems: 'center',
                            }}
                        >
                            ×
                        </button>
                    </div>
                )
            })}
        </div>
    )
}

function JobFeedPage() {
    const { userId } = useAuthStore()

    const { tabs, activeTabId, addTab, closeTab, setActiveTabId } = usePersistedTabs(userId)

    const [runMl, setRunMl]               = useState(false)
    const [sourceFilter, setSourceFilter]  = useState('')
    const [formatFilter, setFormatFilter]  = useState('')
    const [seniorityFilter, setSeniorityFilter] = useState('')
    const [showDuplicates, setShowDuplicates]   = useState(false)

    const activeTab = tabs.find(t => t.id === activeTabId) ?? null

    const { data, isFetching, error } = useQuery({
        queryKey: ['jobs', activeTab?.searchParams ?? null],
        queryFn: () => jobsApi.getJobs(activeTab!.searchParams),
        enabled: !!activeTab,
    })

    const handleSearch = (keywords: string, location: string | null) => {
        const searchParams: JobSearchParams = {
            keywords,
            runRelevancePipeline: runMl,
        }

        const existing = tabs.find(t =>
            t.searchParams.keywords === keywords &&
            t.runMl === runMl
        )

        if (existing) {
            setActiveTabId(existing.id)
            setSourceFilter('')
            setFormatFilter('')
            setSeniorityFilter('')
            setShowDuplicates(false)
            return
        }

        const newTab: SearchTab = {
            id: crypto.randomUUID(),
            label: [keywords, location].filter(Boolean).join(' · ') || 'Пошук',
            searchParams,
            runMl,
            createdAt: Date.now(),
        }

        addTab(newTab)

        setSourceFilter('')
        setFormatFilter('')
        setSeniorityFilter('')
        setShowDuplicates(false)
    }

    const handleSelectTab = (tabId: string) => {
        if (tabId === activeTabId) return
        setActiveTabId(tabId)
        setSourceFilter('')
        setFormatFilter('')
        setSeniorityFilter('')
        setShowDuplicates(false)
    }

    const jobs = (data?.jobs ?? []).filter(job => {
        if (sourceFilter !== '' && job.source !== Number(sourceFilter)) return false
        if (formatFilter !== '' && job.workFormat !== Number(formatFilter)) return false
        if (seniorityFilter !== '' && job.seniorityLevel !== Number(seniorityFilter)) return false
        return true
    })

    const selectStyle = {
        padding: '8px 12px', borderRadius: 8,
        border: '1px solid #e5e7eb', fontSize: 14,
        color: '#374151', background: '#fff', cursor: 'pointer',
    }

    return (
        <div style={{ maxWidth: 900, margin: '0 auto', padding: '24px 16px' }}>

            {/* Search bar */}
            <SearchBar onSearch={handleSearch} isLoading={isFetching} />

            {/* ML toggle */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, margin: '10px 0 12px', flexWrap: 'wrap' }}>
                <MlToggle enabled={runMl} onChange={() => setRunMl(v => !v)} disabled={isFetching} />
                {runMl && !isFetching && (
                    <span style={{ fontSize: 13, color: '#7c3aed' }}>
                        Gemini аналізує кожну вакансію і виставляє % відповідності до профілю
                    </span>
                )}
                {runMl && isFetching && (
                    <span style={{ fontSize: 13, color: '#9ca3af' }}>
                        ⏳ ML-аналіз може тривати 15–30 сек...
                    </span>
                )}
            </div>

            <ManualUrlInput />

            {/* Tabs row */}
            {tabs.length > 0 && (
                <div style={{ marginTop: 20 }}>
                    <TabBar
                        tabs={tabs}
                        activeId={activeTabId}
                        onSelect={handleSelectTab}
                        onClose={closeTab}
                    />
                </div>
            )}

            {/* Empty state */}
            {tabs.length === 0 && (
                <div style={{ textAlign: 'center', marginTop: 60, color: '#9ca3af' }}>
                    <div style={{ fontSize: 40, marginBottom: 12 }}>🔍</div>
                    <p style={{ fontSize: 15 }}>Введи ключові слова та натисни «Пошук»</p>
                    <p style={{ fontSize: 13 }}>Кожен пошук відкривається в новому табі</p>
                </div>
            )}

            {/* Filters */}
            {activeTab && (
                <div style={{ display: 'flex', gap: 8, margin: '16px 0 8px', flexWrap: 'wrap' }}>
                    <select style={selectStyle} value={sourceFilter}
                            onChange={e => setSourceFilter(e.target.value)}>
                        {sourceOptions.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </select>
                    <select style={selectStyle} value={formatFilter}
                            onChange={e => setFormatFilter(e.target.value)}>
                        {formatOptions.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </select>
                    <select style={selectStyle} value={seniorityFilter}
                            onChange={e => setSeniorityFilter(e.target.value)}>
                        {seniorityOptions.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </select>
                    {(sourceFilter || formatFilter || seniorityFilter) && (
                        <button
                            onClick={() => { setSourceFilter(''); setFormatFilter(''); setSeniorityFilter('') }}
                            style={{ ...selectStyle, color: '#dc2626', border: '1px solid #dc2626' }}>
                            Скинути фільтри
                        </button>
                    )}
                </div>
            )}

            {/* Stats bar */}
            {data && !isFetching && activeTab && (
                <div style={{ display: 'flex', alignItems: 'center', gap: 10, margin: '4px 0 16px', flexWrap: 'wrap' }}>
                    <span style={{ color: '#6b7280', fontSize: 14 }}>
                        Показано: {jobs.length} з {data.totalCount}
                        {data.duplicatesRemoved > 0 && ` · дублікатів прибрано: ${data.duplicatesRemoved}`}
                    </span>
                    {data.relevancePipelineRan && (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: 5,
                            background: '#f5f3ff', color: '#7c3aed',
                            border: '1px solid #ddd6fe',
                            borderRadius: 20, padding: '2px 10px', fontSize: 12, fontWeight: 500,
                        }}>
                            🤖 Відсортовано за ML-релевантністю
                        </span>
                    )}
                    {activeTab.runMl && !data.relevancePipelineRan && (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: 5,
                            background: '#fefce8', color: '#92400e',
                            border: '1px solid #fde68a',
                            borderRadius: 20, padding: '2px 10px', fontSize: 12,
                        }}>
                            ⚠️ ML не виконано — потрібен профіль з навичками або CV
                        </span>
                    )}
                </div>
            )}

            {/* Loading */}
            {isFetching && (
                <p style={{ color: '#6b7280', fontSize: 14, margin: '16px 0' }}>
                    {activeTab?.runMl ? '🤖 Завантаження з ML-аналізом...' : 'Завантаження...'}
                </p>
            )}

            {error && (
                <p style={{ color: 'red' }}>Помилка завантаження. Перевір чи запущений бекенд.</p>
            )}

            {/* Results */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                {jobs.map(job => <JobCard key={job.id} job={job} />)}
            </div>

            {/* Duplicates */}
            {data && (data.duplicates?.length ?? 0) > 0 && (
                <div style={{ marginTop: 32 }}>
                    <button
                        onClick={() => setShowDuplicates(prev => !prev)}
                        style={{
                            background: 'none', border: '1px solid #e5e7eb', borderRadius: 8,
                            padding: '8px 16px', cursor: 'pointer', fontSize: 14, color: '#6b7280',
                            display: 'flex', alignItems: 'center', gap: 8,
                        }}>
                        {showDuplicates ? '▲' : '▼'}
                        Можливі дублікати ({data.duplicates.filter(job => {
                            if (sourceFilter !== '' && job.source !== Number(sourceFilter)) return false
                            if (formatFilter !== '' && job.workFormat !== Number(formatFilter)) return false
                            if (seniorityFilter !== '' && job.seniorityLevel !== Number(seniorityFilter)) return false
                            return true
                        }).length})
                    </button>
                    {showDuplicates && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 12, marginTop: 12, opacity: 0.75 }}>
                            {data.duplicates
                                .filter(job => {
                                    if (sourceFilter !== '' && job.source !== Number(sourceFilter)) return false
                                    if (formatFilter !== '' && job.workFormat !== Number(formatFilter)) return false
                                    if (seniorityFilter !== '' && job.seniorityLevel !== Number(seniorityFilter)) return false
                                    return true
                                })
                                .map(job => <JobCard key={job.id} job={job} />)}
                        </div>
                    )}
                </div>
            )}

            <ManualVacanciesSection />
        </div>
    )
}

export default JobFeedPage

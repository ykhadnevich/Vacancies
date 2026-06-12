import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { jobsApi } from '../../api/jobsApi'
import { userApi } from '../../api/userApi'
import { useAuthStore } from '../../store/authStore'
import { usePersistedTabs } from '../../hooks/usePersistedTabs'
import type { SearchTab } from '../../types/tab'
import type { JobSearchParams, JobVacancy } from '../../types/job'
import type { JobVacancyV6 } from '../../types/jobV6'

import { useT } from '../../i18n/useT'
import SearchBar from '../../components/SearchBar'
import ManualUrlInput from '../../components/ManualUrlInput'
import JobCardV6 from '../../components/jobs/JobCardV6'
import JobCardRaw from '../../components/jobs/JobCardRaw'
import VacancyDetailDrawer from '../../components/jobs/VacancyDetailDrawer'
import SearchModeToggle, { type SearchMode } from '../../components/jobs/SearchModeToggle'
import Icon from '../../components/ui/Icon'
import Badge from '../../components/ui/Badge'
import Card from '../../components/ui/Card'

import { Link } from 'react-router-dom'

const selectStyle: React.CSSProperties = {
    padding:        '7px 12px',
    borderRadius:   'var(--radius-md)',
    border:         '1px solid var(--color-border-default)',
    fontSize:       'var(--text-sm)',
    color:          'var(--color-text-secondary)',
    background:     'var(--color-bg-surface)',
    cursor:         'pointer',
    fontFamily:     'inherit',
}

function TabBar({ tabs, activeId, onSelect, onClose }: {
    tabs:     SearchTab[]
    activeId: string | null
    onSelect: (id: string) => void
    onClose:  (id: string) => void
}) {
    if (tabs.length === 0) return null
    return (
        <div style={{
            display:        'flex',
            gap:            2,
            borderBottom:   '0.5px solid var(--color-border-default)',
            marginBottom:   16,
            overflowX:      'auto',
            scrollbarWidth: 'none',
        }}>
            {tabs.map((tab) => {
                const isActive = tab.id === activeId
                return (
                    <div
                        key={tab.id}
                        onClick={() => onSelect(tab.id)}
                        style={{
                            display:      'flex',
                            alignItems:   'center',
                            gap:          6,
                            padding:      '8px 14px',
                            borderRadius: 'var(--radius-md) var(--radius-md) 0 0',
                            background:   isActive ? 'var(--color-bg-surface)' : 'transparent',
                            borderBottom: isActive ? '2px solid var(--color-primary-600)' : '2px solid transparent',
                            marginBottom: -1,
                            cursor:       'pointer',
                            whiteSpace:   'nowrap',
                            transition:   'all var(--transition-fast)',
                        }}
                    >
                        <span style={{
                            fontSize:   'var(--text-sm)',
                            fontWeight: (isActive ? 'var(--font-weight-medium)' : 'var(--font-weight-regular)') as unknown as number,
                            color:      isActive ? 'var(--color-text-primary)' : 'var(--color-text-secondary)',
                            maxWidth:   180,
                            overflow:   'hidden',
                            textOverflow: 'ellipsis',
                        }}>
                            {tab.label}
                        </span>
                        <button
                            onClick={(e) => { e.stopPropagation(); onClose(tab.id) }}
                            aria-label="Закрити вкладку"
                            style={{
                                background: 'none',
                                border:     'none',
                                cursor:     'pointer',
                                color:      'var(--color-text-tertiary)',
                                padding:    2,
                                display:    'flex',
                                fontFamily: 'inherit',
                            }}
                        >
                            <Icon name="close" size={14} />
                        </button>
                    </div>
                )
            })}
        </div>
    )
}

function CvWarning() {
    return (
        <Card padding="md" style={{
            background: 'var(--color-warning-50)',
            border:     '1px solid var(--color-warning-100)',
        }}>
            <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
                <Icon name="alert-circle" size={18} color="var(--color-warning-600)" style={{ marginTop: 2 }} />
                <div style={{ flex: 1 }}>
                    <p style={{
                        margin:     0,
                        fontSize:   'var(--text-md)',
                        color:      'var(--color-warning-700)',
                        fontWeight: 'var(--font-weight-medium)' as unknown as number,
                    }}>
                        Резюме ще не оброблене
                    </p>
                    <p style={{
                        margin:    '4px 0 0',
                        fontSize:  'var(--text-sm)',
                        color:     'var(--color-warning-700)',
                    }}>
                        Завантажте та обробіть PDF у{' '}
                        <Link to="/profile" style={{ color: 'var(--color-warning-700)', textDecoration: 'underline' }}>
                            Профілі
                        </Link>{' '}
                        перед запуском пошуку з аналізом.
                    </p>
                </div>
            </div>
        </Card>
    )
}

function JobFeedPage() {
    const tr = useT()
    const { isAuthenticated, userId } = useAuthStore()
    const {
        tabs, activeTabId, addTab, closeTab, setActiveTabId,
    } = usePersistedTabs(userId)

    const SOURCES_LOC = [
        { value: '',         label: tr('filter.allSources')      },
        { value: 'RobotaUa', label: 'robota.ua'                  },
        { value: 'Jooble',   label: 'jooble'                     },
        { value: 'DOU',      label: 'dou'                        },
        { value: 'LinkedIn', label: 'linkedin'                   },
        { value: 'WorkUa',   label: 'work.ua'                    },
        { value: 'Djinni',   label: 'djinni'                     },
        { value: 'Manual',   label: 'manual'                     },
    ]
    const FORMATS_LOC = [
        { value: '',       label: tr('filter.allFormats')          },
        { value: 'Remote', label: tr('filter.workFormat.remote')   },
        { value: 'Office', label: tr('filter.workFormat.office')   },
        { value: 'Hybrid', label: tr('filter.workFormat.hybrid')   },
    ]
    const SENIORITIES_LOC = [
        { value: '',           label: tr('filter.allLevels')        },
        { value: 'Internship', label: 'Intern'                      },
        { value: 'Junior',     label: tr('filter.level.junior')     },
        { value: 'Middle',     label: tr('filter.level.middle')     },
        { value: 'Senior',     label: tr('filter.level.senior')     },
        { value: 'Lead',       label: 'Lead'                        },
    ]

    const [searchMode,      setSearchMode]      = useState<SearchMode>('analyzed')
    const [sourceFilter,    setSourceFilter]    = useState('')
    const [formatFilter,    setFormatFilter]    = useState('')
    const [seniorityFilter, setSeniorityFilter] = useState('')

    const [v6Limit] = useState<number>(500)
    const [drawerJob, setDrawerJob] = useState<JobVacancyV6 | null>(null)
    const [refreshedTabs, setRefreshedTabs] = useState<Set<string>>(new Set())

    const activeTab = tabs.find((t) => t.id === activeTabId) ?? null
    const activeMode: SearchMode = activeTab?.analysisMode === 'None' ? 'raw' : 'analyzed'

    const { data: cvStatus } = useQuery({
        queryKey: ['cvStatus'],
        queryFn:  userApi.getCvStatus,
        enabled:  isAuthenticated,
    })

    // Cheap server-side snapshot — shows yesterday's analysis instantly with zero
    // Gemini cost. Fresh v6 only runs on the user's explicit Refresh action.
    const snapshotQuery = useQuery({
        queryKey: ['jobs', 'v6Snapshot', activeTab?.searchParams ?? null, v6Limit],
        queryFn:  () => jobsApi.getJobsV6Snapshot({ ...activeTab!.searchParams, limit: v6Limit }),
        enabled:  !!activeTab && activeMode === 'analyzed',
        staleTime:            Infinity,
        gcTime:               24 * 60 * 60 * 1000,
        refetchOnMount:       false,
        refetchOnWindowFocus: false,
        refetchOnReconnect:   false,
    })
    const hasSnapshot = !!snapshotQuery.data
    const isRefreshingThisTab = activeTab ? refreshedTabs.has(activeTab.id) : false

    const v6Query = useQuery({
        queryKey: ['jobs', 'v6', activeTab?.searchParams ?? null, v6Limit, isRefreshingThisTab],
        queryFn:  () => jobsApi.getJobsV6({ ...activeTab!.searchParams, limit: v6Limit }),
        enabled:  !!activeTab
                  && activeMode === 'analyzed'
                  && snapshotQuery.isFetched
                  && (!hasSnapshot || isRefreshingThisTab),
        staleTime:            Infinity,
        gcTime:               24 * 60 * 60 * 1000,
        refetchOnMount:       false,
        refetchOnWindowFocus: false,
        refetchOnReconnect:   false,
    })

    const rawQuery = useQuery({
        queryKey: ['jobs', 'raw', activeTab?.searchParams.keywords ?? null],
        queryFn:  () => jobsApi.getRawJobs({
            keywords: activeTab!.searchParams.keywords,
            limit:    500,
        }),
        enabled:  !!activeTab && activeMode === 'raw',
        staleTime:            Infinity,
        gcTime:               24 * 60 * 60 * 1000,
        refetchOnMount:       false,
        refetchOnWindowFocus: false,
        refetchOnReconnect:   false,
    })

    const isFetching = v6Query.isFetching || rawQuery.isFetching

    const resetFilters = () => {
        setSourceFilter('')
        setFormatFilter('')
        setSeniorityFilter('')
    }

    const handleSearch = (keywords: string, location: string | null) => {
        const searchParams: JobSearchParams = { keywords }

        const existing = tabs.find((t) =>
            t.searchParams.keywords === keywords &&
            (searchMode === 'raw' ? t.analysisMode === 'None' : t.analysisMode !== 'None'),
        )
        if (existing) {
            setActiveTabId(existing.id)
            resetFilters()
            return
        }

        const newTab: SearchTab = {
            id:           crypto.randomUUID(),
            label:        [keywords, location].filter(Boolean).join(' · ') || 'Пошук',
            searchParams,
            analysisMode: searchMode === 'raw' ? 'None' : 'Gemini',
            createdAt:    Date.now(),
        }
        addTab(newTab)
        resetFilters()
    }

    const filterV6 = (jobs: JobVacancyV6[]) => jobs.filter((job) => {
        if (sourceFilter    !== '' && job.source         !== sourceFilter)    return false
        if (formatFilter    !== '' && job.workFormat     !== formatFilter)    return false
        if (seniorityFilter !== '' && job.seniorityLevel !== seniorityFilter) return false
        return true
    })

    const filterRaw = (jobs: JobVacancy[]) => jobs.filter((job) => {
        if (sourceFilter    !== '' && job.source         !== sourceFilter)    return false
        if (formatFilter    !== '' && job.workFormat     !== formatFilter)    return false
        if (seniorityFilter !== '' && job.seniorityLevel !== seniorityFilter) return false
        return true
    })

    const v6Data = v6Query.data ?? snapshotQuery.data?.response ?? null

    const v6Jobs  = v6Data ? filterV6(v6Data.jobs) : []
    const rawJobs = rawQuery.data ? filterRaw(rawQuery.data.jobs) : []

    const visibleJobs = activeMode === 'analyzed' ? v6Jobs.length : rawJobs.length
    const totalCount  = activeMode === 'analyzed' ? v6Data?.totalAvailable : rawQuery.data?.totalCount
    const dupsRemoved = activeMode === 'analyzed' ? undefined : rawQuery.data?.duplicatesRemoved

    const showCvWarning =
        isAuthenticated && searchMode === 'analyzed' && cvStatus && cvStatus.status !== 'Ready'

    const handleRefresh = () => {
        if (!activeTab) return
        setRefreshedTabs((prev) => {
            const next = new Set(prev)
            next.add(activeTab.id)
            return next
        })
    }

    const snapshotAgeText: string | null = (() => {
        if (v6Query.data) return null
        const iso = snapshotQuery.data?.executedAt
        if (!iso) return null
        // eslint-disable-next-line react-hooks/purity
        const diffMs = Date.now() - new Date(iso).getTime()
        const min = Math.floor(diffMs / 60_000)
        if (min < 1)    return tr('search.snapshotFresh')
        if (min < 60)   return tr('search.snapshotAge', { age: `${min} min` })
        const hours = Math.floor(min / 60)
        if (hours < 24) return tr('search.snapshotAge', { age: `${hours} h` })
        const days = Math.floor(hours / 24)
        return tr('search.snapshotAge', { age: `${days} d` })
    })()

    return (
        <div style={{ width: '100%', maxWidth: 'var(--max-width-content)', margin: '0 auto', padding: '24px 16px', display: 'flex', flexDirection: 'column', gap: 16 }}>

            <SearchBar onSearch={handleSearch} isLoading={isFetching} />

            <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                <SearchModeToggle value={searchMode} onChange={setSearchMode} disabled={isFetching} />
                <ManualUrlInput />
            </div>

            {showCvWarning && <CvWarning />}

            {tabs.length > 0 && (
                <div>
                    <TabBar tabs={tabs} activeId={activeTabId} onSelect={(id) => { setActiveTabId(id); resetFilters() }} onClose={closeTab} />
                </div>
            )}

            {tabs.length === 0 && (
                <div style={{ textAlign: 'center', padding: '64px 16px', color: 'var(--color-text-tertiary)' }}>
                    <Icon name="search" size={28} color="var(--color-text-tertiary)" />
                    <p style={{ marginTop: 12, fontSize: 'var(--text-lg)', color: 'var(--color-text-secondary)' }}>
                        Введіть ключові слова та натисніть «Пошук»
                    </p>
                    <p style={{ marginTop: 4, fontSize: 'var(--text-sm)' }}>
                        Кожен запит відкриється у новій вкладці
                    </p>
                </div>
            )}

            {activeTab && (
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                    <select style={selectStyle} value={sourceFilter}    onChange={(e) => setSourceFilter(e.target.value)}>
                        {SOURCES_LOC.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </select>
                    <select style={selectStyle} value={formatFilter}    onChange={(e) => setFormatFilter(e.target.value)}>
                        {FORMATS_LOC.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </select>
                    <select style={selectStyle} value={seniorityFilter} onChange={(e) => setSeniorityFilter(e.target.value)}>
                        {SENIORITIES_LOC.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </select>
                    {(sourceFilter || formatFilter || seniorityFilter) && (
                        <button
                            onClick={resetFilters}
                            style={{ ...selectStyle, color: 'var(--color-danger-600)', borderColor: 'var(--color-danger-100)' }}
                        >
                            Скинути фільтри
                        </button>
                    )}
                </div>
            )}

            {activeTab && !isFetching && (totalCount != null) && (
                <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
                    <span style={{ color: 'var(--color-text-secondary)', fontSize: 'var(--text-sm)' }}>
                        {tr('list.showing')}: <span style={{ fontVariantNumeric: 'tabular-nums', color: 'var(--color-text-primary)' }}>{visibleJobs}</span> {tr('list.of')} <span style={{ fontVariantNumeric: 'tabular-nums' }}>{totalCount}</span>
                        {dupsRemoved != null && dupsRemoved > 0 && (
                            <> · {tr('list.dedupRemoved')} <span style={{ fontVariantNumeric: 'tabular-nums' }}>{dupsRemoved}</span></>
                        )}
                    </span>
                    {activeMode === 'analyzed' && visibleJobs > 0 && (
                        <Badge color="primary" size="sm">{tr('list.sortedRelevance')}</Badge>
                    )}
                    {activeMode === 'raw' && (
                        <Badge color="neutral" size="sm">{tr('list.noAnalysis')}</Badge>
                    )}
                    {activeMode === 'analyzed' && snapshotAgeText && (
                        <>
                            <Badge color="neutral" size="sm">
                                <Icon name="refresh" size={11} /> {snapshotAgeText}
                            </Badge>
                            <button
                                onClick={handleRefresh}
                                disabled={isFetching}
                                style={{
                                    background:   'transparent',
                                    border:       '1px solid var(--color-border-default)',
                                    borderRadius: 'var(--radius-md)',
                                    padding:      '4px 10px',
                                    fontSize:     'var(--text-xs)',
                                    cursor:       isFetching ? 'not-allowed' : 'pointer',
                                    color:        'var(--color-text-secondary)',
                                    fontFamily:   'inherit',
                                    display:      'inline-flex',
                                    alignItems:   'center',
                                    gap:          4,
                                }}
                            >
                                <Icon name="sparkle" size={11} /> {tr('search.refresh')}
                            </button>
                        </>
                    )}
                </div>
            )}

            {isFetching && (
                <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-text-secondary)' }}>
                    {tr('search.searching')}
                </div>
            )}

            {!isFetching && activeMode === 'analyzed' && v6Jobs.length === 0 && (
                <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-text-secondary)' }}>
                    {tr('list.empty')}
                </div>
            )}

            {!isFetching && activeMode === 'raw' && rawJobs.length === 0 && (
                <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-text-secondary)' }}>
                    {tr('list.empty')}
                </div>
            )}

            {!isFetching && activeMode === 'analyzed' && v6Jobs.length > 0 && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                    {v6Jobs.map((job) => (
                        <JobCardV6 key={job.id} job={job} onOpenDetails={() => setDrawerJob(job)} />
                    ))}
                </div>
            )}

            {!isFetching && activeMode === 'raw' && rawJobs.length > 0 && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                    {rawJobs.map((job) => (
                        <JobCardRaw key={job.id} job={job} />
                    ))}
                </div>
            )}

            <VacancyDetailDrawer job={drawerJob} onClose={() => setDrawerJob(null)} />
        </div>
    )
}

export default JobFeedPage

import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { jobsApi } from '../../api/jobsApi'
import { userApi } from '../../api/userApi'
import { useAuthStore } from '../../store/authStore'
import { usePersistedTabs } from '../../hooks/usePersistedTabs'
import { useIsMobile } from '../../hooks/useViewport'
import type { SearchTab } from '../../types/tab'
import type { Country, JobSearchParams, JobVacancy } from '../../types/job'
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
import { WideShell, SidebarCard, SectionHead } from '../../components/layout/Shell'
import { eyebrow } from '../../components/layout/_layout'

const selectStyle: React.CSSProperties = {
    width:          '100%',
    padding:        '8px 12px',
    borderRadius:   'var(--radius-md)',
    border:         '1px solid var(--color-border-default)',
    fontSize:       'var(--text-sm)',
    color:          'var(--color-text-secondary)',
    background:     'var(--color-bg-elevated)',
    cursor:         'pointer',
    fontFamily:     'inherit',
    boxShadow:      'var(--shadow-inset)',
}

function SavedSearches({ tabs, activeId, onSelect, onClose }: {
    tabs:     SearchTab[]
    activeId: string | null
    onSelect: (id: string) => void
    onClose:  (id: string) => void
}) {
    const t = useT()
    if (tabs.length === 0) return null
    return (
        <SidebarCard>
            <SectionHead>{t('jobFeed.savedSearches')}</SectionHead>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                {tabs.map((tab) => {
                    const isActive = tab.id === activeId
                    return (
                        <div key={tab.id} onClick={() => onSelect(tab.id)}
                            style={{
                                display: 'flex', alignItems: 'center', gap: 6, padding: '8px 10px',
                                borderRadius: 'var(--radius-md)', cursor: 'pointer',
                                background: isActive ? 'var(--color-bg-muted)' : 'transparent',
                                border: `1px solid ${isActive ? 'var(--color-border-default)' : 'transparent'}`,
                                transition: 'background var(--transition-fast)',
                            }}>
                            <span style={{
                                flex: 1, fontSize: 'var(--text-sm)',
                                fontWeight: (isActive ? 'var(--font-weight-medium)' : 'var(--font-weight-regular)') as unknown as number,
                                color: isActive ? 'var(--color-text-primary)' : 'var(--color-text-secondary)',
                                overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                            }}>
                                {tab.label}
                            </span>
                            <button onClick={(e) => { e.stopPropagation(); onClose(tab.id) }} aria-label={t('jobFeed.closeTab')}
                                style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--color-text-tertiary)', padding: 2, display: 'flex', fontFamily: 'inherit' }}>
                                <Icon name="close" size={13} />
                            </button>
                        </div>
                    )
                })}
            </div>
        </SidebarCard>
    )
}

function CvWarning() {
    const t = useT()
    return (
        <div style={{
            display: 'flex', gap: 12, alignItems: 'flex-start',
            background: 'var(--color-warning-50)', border: '1px solid var(--color-warning-100)',
            borderRadius: 'var(--radius-lg)', padding: '14px 16px',
        }}>
            <Icon name="alert-circle" size={18} color="var(--color-warning-600)" style={{ marginTop: 2, flexShrink: 0 }} />
            <div style={{ flex: 1 }}>
                <p style={{ margin: 0, fontSize: 'var(--text-md)', color: 'var(--color-warning-700)', fontWeight: 'var(--font-weight-medium)' as unknown as number }}>
                    {t('jobFeed.cvWarn.title')}
                </p>
                <p style={{ margin: '4px 0 0', fontSize: 'var(--text-sm)', color: 'var(--color-warning-700)' }}>
                    {t('jobFeed.cvWarn.body')}
                </p>
                <Link
                    to="/profile"
                    style={{
                        display:        'inline-flex',
                        alignItems:     'center',
                        gap:            4,
                        marginTop:      8,
                        fontSize:       'var(--text-sm)',
                        color:          'var(--color-warning-700)',
                        textDecoration: 'underline',
                        fontWeight:     'var(--font-weight-medium)' as unknown as number,
                    }}
                >
                    {t('nav.profile')} <Icon name="arrow-right" size={12} />
                </Link>
            </div>
        </div>
    )
}

function JobFeedPage() {
    const tr = useT()
    const narrow = useIsMobile(1024)
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
        { value: 'Internship', label: tr('filter.level.internship') },
        { value: 'Junior',     label: tr('filter.level.junior')     },
        { value: 'Middle',     label: tr('filter.level.middle')     },
        { value: 'Senior',     label: tr('filter.level.senior')     },
        { value: 'Lead',       label: tr('filter.level.lead')       },
    ]

    const [searchMode,      setSearchMode]      = useState<SearchMode>('analyzed')
    const [country,         setCountry]         = useState<Country>('Ukraine')
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
        queryKey: ['jobs', 'raw', activeTab?.searchParams.keywords ?? null, activeTab?.searchParams.country ?? 'Ukraine'],
        queryFn:  () => jobsApi.getRawJobs({
            keywords: activeTab!.searchParams.keywords,
            country:  activeTab!.searchParams.country,
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

    const handleSearch = (keywords: string, location: string | null, selectedCountry: Country) => {
        const effectiveCountry: Country = selectedCountry ?? 'Ukraine'
        const searchParams: JobSearchParams = { keywords, country: effectiveCountry }

        const existing = tabs.find((t) =>
            t.searchParams.keywords === keywords &&
            (t.searchParams.country ?? 'Ukraine') === effectiveCountry &&
            (searchMode === 'raw' ? t.analysisMode === 'None' : t.analysisMode !== 'None'),
        )
        if (existing) {
            setActiveTabId(existing.id)
            resetFilters()
            return
        }

        const label = [keywords, location].filter(Boolean).join(' · ') || tr('jobFeed.search')

        const newTab: SearchTab = {
            id:           crypto.randomUUID(),
            label,
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
        if (min < 60)   return tr('search.snapshotAge', { age: `${min} ${tr('common.unit.minutes')}` })
        const hours = Math.floor(min / 60)
        if (hours < 24) return tr('search.snapshotAge', { age: `${hours} ${tr('common.unit.hours')}` })
        const days = Math.floor(hours / 24)
        return tr('search.snapshotAge', { age: `${days} ${tr('common.unit.days')}` })
    })()

    const hasFilters = !!(sourceFilter || formatFilter || seniorityFilter)
    const gridStyle: React.CSSProperties = {
        display: 'grid',
        gridTemplateColumns: narrow ? '1fr' : 'repeat(2, minmax(0, 1fr))',
        gap: 16,
    }

    const sidebar = (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            {activeTab && (
                <SidebarCard>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <SectionHead>{tr('jobFeed.filters')}</SectionHead>
                        {hasFilters && (
                            <button onClick={resetFilters}
                                style={{ background: 'transparent', border: 'none', cursor: 'pointer',
                                    color: 'var(--color-danger-600)', fontSize: 'var(--text-xs)', fontFamily: 'inherit' }}>
                                {tr('common.reset')}
                            </button>
                        )}
                    </div>
                    <select style={selectStyle} value={sourceFilter} onChange={(e) => setSourceFilter(e.target.value)}>
                        {SOURCES_LOC.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </select>
                    <select style={selectStyle} value={formatFilter} onChange={(e) => setFormatFilter(e.target.value)}>
                        {FORMATS_LOC.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </select>
                    <select style={selectStyle} value={seniorityFilter} onChange={(e) => setSeniorityFilter(e.target.value)}>
                        {SENIORITIES_LOC.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </select>
                </SidebarCard>
            )}
            <SavedSearches tabs={tabs} activeId={activeTabId}
                onSelect={(id) => { setActiveTabId(id); resetFilters() }} onClose={closeTab} />
        </div>
    )

    return (
        <WideShell sidebar={sidebar} sidebarWidth={280}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                    <SearchBar onSearch={handleSearch} isLoading={isFetching}
                        country={activeTab?.searchParams.country ?? country} onCountryChange={setCountry} />
                    <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                        <SearchModeToggle value={activeTab ? activeMode : searchMode} onChange={setSearchMode} disabled={isFetching} />
                        <ManualUrlInput />
                    </div>
                </div>

                {showCvWarning && <CvWarning />}

                {tabs.length === 0 && (
                    <div style={{ textAlign: 'center', padding: '72px 16px', color: 'var(--color-text-tertiary)' }}>
                        <Icon name="search" size={28} color="var(--color-text-tertiary)" />
                        <p style={{ marginTop: 12, fontFamily: 'var(--font-serif)', fontSize: 'var(--text-2xl)', color: 'var(--color-text-secondary)' }}>
                            {tr('jobFeed.emptyState.title')}
                        </p>
                        <p style={{ marginTop: 4, fontSize: 'var(--text-sm)' }}>
                            {tr('jobFeed.emptyState.body')}
                        </p>
                    </div>
                )}

                {activeTab && !isFetching && (totalCount != null) && (
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
                        <span style={{ ...eyebrow, color: 'var(--color-text-secondary)' }}>
                            {tr('list.showing')}: <span style={{ fontVariantNumeric: 'tabular-nums', color: 'var(--color-text-primary)', fontFamily: 'var(--font-mono)' }}>{visibleJobs}</span> {tr('list.of')} <span style={{ fontVariantNumeric: 'tabular-nums', fontFamily: 'var(--font-mono)' }}>{totalCount}</span>
                            {dupsRemoved != null && dupsRemoved > 0 && (
                                <> · {tr('list.dedupRemoved')} <span style={{ fontVariantNumeric: 'tabular-nums', fontFamily: 'var(--font-mono)' }}>{dupsRemoved}</span></>
                            )}
                        </span>
                        {activeMode === 'analyzed' && visibleJobs > 0 && (<Badge color="primary" size="sm">{tr('list.sortedRelevance')}</Badge>)}
                        {activeMode === 'raw' && (<Badge color="neutral" size="sm">{tr('list.noAnalysis')}</Badge>)}
                        {activeMode === 'analyzed' && snapshotAgeText && (
                            <>
                                <Badge color="neutral" size="sm"><Icon name="refresh" size={11} /> {snapshotAgeText}</Badge>
                                <button onClick={handleRefresh} disabled={isFetching}
                                    style={{ background: 'transparent', border: '1px solid var(--color-border-default)', borderRadius: 'var(--radius-md)',
                                        padding: '4px 10px', fontSize: 'var(--text-xs)', cursor: isFetching ? 'not-allowed' : 'pointer',
                                        color: 'var(--color-text-secondary)', fontFamily: 'inherit', display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                                    <Icon name="sparkle" size={11} /> {tr('search.refresh')}
                                </button>
                            </>
                        )}
                    </div>
                )}

                {isFetching && (
                    <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-text-secondary)' }}>{tr('search.searching')}</div>
                )}

                {!isFetching && activeMode === 'analyzed' && activeTab && v6Jobs.length === 0 && (
                    <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-text-secondary)' }}>{tr('list.empty')}</div>
                )}
                {!isFetching && activeMode === 'raw' && activeTab && rawJobs.length === 0 && (
                    <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-text-secondary)' }}>{tr('list.empty')}</div>
                )}

                {!isFetching && activeMode === 'analyzed' && v6Jobs.length > 0 && (
                    <div style={gridStyle}>
                        {v6Jobs.map((job) => (<JobCardV6 key={job.id} job={job} onOpenDetails={() => setDrawerJob(job)} />))}
                    </div>
                )}
                {!isFetching && activeMode === 'raw' && rawJobs.length > 0 && (
                    <div style={gridStyle}>
                        {rawJobs.map((job) => (<JobCardRaw key={job.id} job={job} />))}
                    </div>
                )}
            </div>

            <VacancyDetailDrawer job={drawerJob} onClose={() => setDrawerJob(null)} />
        </WideShell>
    )
}

export default JobFeedPage

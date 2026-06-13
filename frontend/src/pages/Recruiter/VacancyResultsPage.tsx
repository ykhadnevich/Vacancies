import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { recruiterApi } from '../../api/recruiterApi'
import type { AnalyzeResult, CandidateAnalysisResultDto } from '../../types/recruiter'
import { useT } from '../../i18n/useT'
import { useIsMobile } from '../../hooks/useViewport'
import Button from '../../components/ui/Button'
import Icon from '../../components/ui/Icon'
import Badge from '../../components/ui/Badge'
import EmptyState from '../../components/ui/EmptyState'
import { useLanguage } from '../../i18n/LanguageContext'
import { verdictColor } from '../../utils/verdict'
import CandidateDetailDrawer from '../../components/recruiter/CandidateDetailDrawer'
import CandidateCard from '../../components/recruiter/CandidateCard'
import ScoreWidget from '../../components/recruiter/ScoreWidget'
import { wideWrap, eyebrow, th, td, tdMono } from '../../components/layout/_layout'

function SubScoreMini({ subScores }: { subScores: Record<string, number> }) {
    const entries = Object.entries(subScores).slice(0, 7)
    if (entries.length === 0) return <span style={{ color: 'var(--color-text-tertiary)' }}>—</span>
    const barColor = (v: number) =>
        v >= 0.75 ? 'var(--color-success-500)'
        : v >= 0.5 ? 'var(--color-info-500)'
        : v >= 0.25 ? 'var(--color-warning-500)'
        : 'var(--color-danger-500)'
    return (
        <div style={{ display: 'flex', alignItems: 'flex-end', gap: 3, height: 26 }}>
            {entries.map(([k, v]) => (
                <div key={k} title={`${k}: ${Math.round(v * 100)}%`}
                    style={{ width: 6, height: `${Math.max(8, v * 100)}%`, minHeight: 3,
                        background: barColor(v), borderRadius: 1 }} />
            ))}
        </div>
    )
}

function VacancyResultsPage() {
    const { id } = useParams<{ id: string }>()
    const t = useT()
    const narrow = useIsMobile(1024)
    const { language } = useLanguage()
    const queryClient = useQueryClient()

    const [selectedListId, setSelectedListId] = useState<string>('')
    const [lastAnalyze, setLastAnalyze] = useState<AnalyzeResult | null>(null)
    const [errorMsg, setErrorMsg] = useState<string | null>(null)
    const [openCandidate, setOpenCandidate] = useState<CandidateAnalysisResultDto | null>(null)
    const [staggerReady, setStaggerReady] = useState(false)

    const { data: lists } = useQuery({
        queryKey: ['recruiter', 'lists'],
        queryFn:  recruiterApi.listLists,
    })

    const { data: vacancy } = useQuery({
        queryKey: ['recruiter', 'vacancies'],
        queryFn:  recruiterApi.listVacancies,
        select:   (vs) => vs.find((v) => v.id === id),
    })

    const { data: results, isLoading: resultsLoading } = useQuery({
        queryKey: ['recruiter', 'results', id, selectedListId],
        queryFn:  () => recruiterApi.getResults(id!, selectedListId),
        enabled:  !!id && !!selectedListId,
    })

    useEffect(() => {
        // eslint-disable-next-line react-hooks/set-state-in-effect
        setStaggerReady(false)
        const rafId = requestAnimationFrame(() => setStaggerReady(true))
        return () => cancelAnimationFrame(rafId)
    }, [results])

    const analyzeMut = useMutation({
        mutationFn: () => recruiterApi.analyze(id!, selectedListId),
        onSuccess: (res) => {
            setLastAnalyze(res); setErrorMsg(null)
            queryClient.invalidateQueries({ queryKey: ['recruiter', 'results', id, selectedListId] })
            queryClient.invalidateQueries({ queryKey: ['recruiter', 'vacancies'] })
        },
        onError: (e: unknown) => {
            const respData = (e as { response?: { data?: AnalyzeResult; status?: number } })?.response?.data
            if (respData && 'status' in respData) {
                setLastAnalyze(respData); setErrorMsg(null)
            } else {
                setErrorMsg(e instanceof Error ? e.message : 'Error')
            }
        },
    })

    const summary = useMemo(() => {
        if (!lastAnalyze) return null
        if (lastAnalyze.status === 'AlreadyRunning')       return t('recruiter.analyze.alreadyRunning')
        if (lastAnalyze.status === 'VacancyNotNormalized') return t('recruiter.analyze.notNormalized')
        if (lastAnalyze.status === 'NothingToScore')       return t('recruiter.analyze.nothingToScore')
        return t('recruiter.analyze.summary', {
            newly:   lastAnalyze.newlyScored,
            already: lastAnalyze.alreadyScored,
            skipped: lastAnalyze.skipped,
            failed:  lastAnalyze.failed,
        })
    }, [lastAnalyze, t])

    if (!id) return null

    const verdictLabel = (verdict: string): string => {
        const v = verdict.toLowerCase()
        if (v.startsWith('strong'))  return t('scoring.verdict.strong')
        if (v.startsWith('partial')) return t('scoring.verdict.partial')
        if (v.startsWith('weak'))    return t('scoring.verdict.weak')
        return t('scoring.verdict.notRelevant')
    }

    return (
        <div style={wideWrap}>
            <div style={{
                position: 'sticky', top: 64, zIndex: 18,
                background: 'color-mix(in srgb, var(--color-bg-page) 88%, transparent)',
                backdropFilter: 'blur(8px)',
                borderBottom: '1px solid var(--color-border-default)',
                margin: '-28px -24px 24px', padding: '20px 24px',
            }}>
                <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 20, flexWrap: 'wrap' }}>
                    <div style={{ minWidth: 0 }}>
                        <div style={{ ...eyebrow, marginBottom: 6 }}>
                            {vacancy?.company}{vacancy?.location ? ` · ${vacancy.location}` : ''}
                        </div>
                        <h1 style={{ fontFamily: 'var(--font-serif)', fontSize: 'var(--display-sm)', fontWeight: 600, letterSpacing: '-0.02em', margin: 0 }}>
                            {vacancy?.title ?? '—'}
                        </h1>
                    </div>
                    <div style={{ display: 'flex', gap: 10, alignItems: 'flex-end', flexWrap: 'wrap' }}>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, minWidth: 240 }}>
                            <label style={{ fontSize: 'var(--text-xs)', fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--color-text-tertiary)' }}>
                                {t('recruiter.analyze.selectList')}
                            </label>
                            <select
                                value={selectedListId}
                                onChange={(e) => { setSelectedListId(e.target.value); setLastAnalyze(null); setErrorMsg(null) }}
                                style={{ width: '100%', padding: '9px 14px', fontSize: 'var(--text-md)', fontFamily: 'inherit',
                                    color: 'var(--color-text-primary)', background: 'var(--color-bg-elevated)',
                                    border: '1px solid var(--color-border-default)', borderRadius: 'var(--radius-md)',
                                    outline: 'none', boxShadow: 'var(--shadow-inset)' }}
                            >
                                <option value="">—</option>
                                {lists?.map((l) => (
                                    <option key={l.id} value={l.id}>{l.name} ({l.normalizedCandidates}/{l.totalCandidates})</option>
                                ))}
                            </select>
                        </div>
                        <Button
                            onClick={() => analyzeMut.mutate()}
                            disabled={!selectedListId || analyzeMut.isPending}
                            isLoading={analyzeMut.isPending}
                            leftIcon={<Icon name="sparkle" size={14} />}
                        >
                            {analyzeMut.isPending ? t('recruiter.analyze.running') : t('recruiter.analyze.run')}
                        </Button>
                    </div>
                </div>
                {summary && (<p style={{ margin: '12px 0 0', fontSize: 'var(--text-sm)', color: 'var(--color-text-secondary)', fontFamily: 'var(--font-mono)' }}>{summary}</p>)}
                {errorMsg && (<p style={{ margin: '8px 0 0', fontSize: 'var(--text-sm)', color: 'var(--color-danger-600)' }}>{errorMsg}</p>)}
            </div>

            {!selectedListId ? (
                <div style={{ background: 'var(--color-bg-surface)', border: '1px solid var(--color-border-default)', borderRadius: 'var(--radius-lg)', textAlign: 'center' }}>
                    <EmptyState icon="sparkle" title={t('recruiter.results.empty')} />
                </div>
            ) : resultsLoading ? (
                <p style={{ color: 'var(--color-text-tertiary)' }}>{t('common.loading')}</p>
            ) : !results || results.length === 0 ? (
                <div style={{ background: 'var(--color-bg-surface)', border: '1px solid var(--color-border-default)', borderRadius: 'var(--radius-lg)', textAlign: 'center' }}>
                    <EmptyState icon="sparkle" title={t('recruiter.results.empty')} />
                </div>
            ) : narrow ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                    {results.map((r, i) => (
                        <CandidateCard key={r.candidateId} result={r} rank={i + 1} language={language}
                            onClick={() => setOpenCandidate(r)}
                            style={{
                                opacity: staggerReady ? 1 : 0,
                                transform: staggerReady ? 'translateY(0)' : 'translateY(6px)',
                                transition: 'opacity var(--transition-base), transform var(--transition-base)',
                                transitionDelay: staggerReady ? `${Math.min(i, 12) * 35}ms` : '0ms',
                            }} />
                    ))}
                </div>
            ) : (
                <div style={{ overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'separate', borderSpacing: 0,
                        background: 'var(--color-bg-surface)', border: '1px solid var(--color-border-default)',
                        borderRadius: 'var(--radius-lg)', overflow: 'hidden', minWidth: 860 }}>
                        <thead>
                            <tr>
                                <th style={{ ...th, width: 56, textAlign: 'right' }}>#</th>
                                <th style={th}>{t('recruiter.list.form.name')}</th>
                                <th style={{ ...th, width: 240 }}>{t('reason.scoreBreakdown')}</th>
                                <th style={{ ...th, width: 150 }}>{t('recruiter.results.verdict')}</th>
                                <th style={{ ...th, width: 120 }}>{t('recruiter.results.axes')}</th>
                                <th style={{ ...th, width: 48 }}></th>
                            </tr>
                        </thead>
                        <tbody>
                            {results.map((r, i) => {
                                const conf = r.confidence ?? undefined
                                const vc = verdictColor(r.verdict)
                                return (
                                    <tr key={r.candidateId}
                                        onClick={() => setOpenCandidate(r)}
                                        style={{
                                            cursor: 'pointer', transition: 'background var(--transition-fast)',
                                            opacity: staggerReady ? 1 : 0,
                                            transform: staggerReady ? 'translateY(0)' : 'translateY(6px)',
                                            transitionDelay: staggerReady ? `${Math.min(i, 12) * 35}ms` : '0ms',
                                        }}
                                        onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--color-bg-muted)')}
                                        onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}>
                                        <td style={{ ...tdMono, textAlign: 'right', fontSize: 'var(--text-xl)', fontWeight: 700,
                                            color: i === 0 ? 'var(--color-primary-600)' : 'var(--color-border-strong)' }}>
                                            {String(i + 1).padStart(2, '0')}
                                        </td>
                                        <td style={td}>
                                            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                                                <span style={{ fontSize: 'var(--text-lg)', fontWeight: 600, color: 'var(--color-text-primary)' }}>
                                                    {r.candidateName || r.candidateId.slice(0, 8)}
                                                </span>
                                                {typeof conf === 'number' && conf < 0.7 && (
                                                    <Badge color={conf < 0.5 ? 'danger' : 'warning'} size="sm" title={t('recruiter.confidence.tooltip')}>
                                                        <Icon name="alert-circle" size={10} /> {(conf * 100).toFixed(0)}%
                                                    </Badge>
                                                )}
                                            </div>
                                        </td>
                                        <td style={{ ...td, paddingTop: 10, paddingBottom: 10 }}>
                                            <div style={{ pointerEvents: 'none' }}>
                                                <ScoreWidget score={r.score} verdict={r.verdict} confidence={conf} />
                                            </div>
                                        </td>
                                        <td style={td}>
                                            <span style={{ fontSize: 'var(--text-xs)', fontWeight: 600, letterSpacing: '0.06em',
                                                textTransform: 'uppercase', color: `var(--color-${vc}-700)` }}>
                                                {verdictLabel(r.verdict)}
                                            </span>
                                        </td>
                                        <td style={td}><SubScoreMini subScores={r.subScores} /></td>
                                        <td style={{ ...td, textAlign: 'center', color: 'var(--color-text-tertiary)' }}>
                                            <Icon name="arrow-right" size={15} />
                                        </td>
                                    </tr>
                                )
                            })}
                        </tbody>
                    </table>
                </div>
            )}

            <CandidateDetailDrawer result={openCandidate} onClose={() => setOpenCandidate(null)} />
        </div>
    )
}

export default VacancyResultsPage

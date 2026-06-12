import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { recruiterApi } from '../../api/recruiterApi'
import type { AnalyzeResult, CandidateAnalysisResultDto } from '../../types/recruiter'
import { useT } from '../../i18n/useT'
import { useIsMobile } from '../../hooks/useViewport'
import Button from '../../components/ui/Button'
import Card from '../../components/ui/Card'
import Icon from '../../components/ui/Icon'
import { useLanguage } from '../../i18n/LanguageContext'
import EmptyState from '../../components/ui/EmptyState'
import { mutedText, pageHeader, pageTitle, pageWrap } from './_styles'
import CandidateDetailDrawer from '../../components/recruiter/CandidateDetailDrawer'
import CandidateCard from '../../components/recruiter/CandidateCard'

function VacancyResultsPage() {
    const { id } = useParams<{ id: string }>()
    const t = useT()
    const isMobile = useIsMobile()
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
        // Stagger animation: reset on results change, then rAF-trigger entry.
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

    return (
        <div style={pageWrap}>
            <div style={pageHeader}>
                <div>
                    <h1 style={pageTitle}>{vacancy?.title ?? '—'}</h1>
                    <p style={{ margin: '4px 0 0', color: 'var(--color-text-secondary)', fontSize: 'var(--text-md)' }}>
                        {vacancy?.company}{vacancy?.location ? ` · ${vacancy.location}` : ''}
                    </p>
                </div>
            </div>

            <Card padding="lg">
                <div style={{ display: 'flex', gap: 12, alignItems: 'flex-end', flexWrap: 'wrap' }}>
                    <div style={{ flex: 1, minWidth: 260 }}>
                        <label style={{ display: 'block', fontSize: 'var(--text-sm)', fontWeight: 500, color: 'var(--color-text-secondary)', marginBottom: 6 }}>
                            {t('recruiter.analyze.selectList')}
                        </label>
                        <select
                            value={selectedListId}
                            onChange={(e) => { setSelectedListId(e.target.value); setLastAnalyze(null); setErrorMsg(null) }}
                            style={{ width: '100%', padding: '10px 14px', fontSize: 'var(--text-md)', fontFamily: 'inherit', color: 'var(--color-text-primary)', background: 'var(--color-bg-surface)', border: '1px solid var(--color-border-default)', borderRadius: 'var(--radius-md)', outline: 'none' }}
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
                        fullWidth={isMobile}
                    >
                        {analyzeMut.isPending ? t('recruiter.analyze.running') : t('recruiter.analyze.run')}
                    </Button>
                </div>
                {summary && (<p style={{ margin: '12px 0 0', fontSize: 'var(--text-sm)', color: 'var(--color-text-secondary)' }}>{summary}</p>)}
                {errorMsg && (<p style={{ margin: '8px 0 0', fontSize: 'var(--text-sm)', color: 'var(--color-danger-600)' }}>{errorMsg}</p>)}
            </Card>

            {!selectedListId ? (
                <Card padding="lg" style={{ textAlign: 'center' }}>
                    <EmptyState icon="sparkle" title={t('recruiter.results.empty')} />
                </Card>
            ) : resultsLoading ? (
                <p style={mutedText}>{t('common.loading')}</p>
            ) : !results || results.length === 0 ? (
                <Card padding="lg" style={{ textAlign: 'center' }}>
                    <EmptyState icon="sparkle" title={t('recruiter.results.empty')} />
                </Card>
            ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                    {results.map((r, i) => (
                        <CandidateCard
                            key={r.candidateId}
                            result={r}
                            rank={i + 1}
                            language={language}
                            onClick={() => setOpenCandidate(r)}
                            style={{
                                opacity:         staggerReady ? 1 : 0,
                                transform:       staggerReady ? 'translateY(0)' : 'translateY(6px)',
                                transition:      'opacity var(--transition-base), transform var(--transition-base)',
                                transitionDelay: staggerReady ? `${Math.min(i, 12) * 35}ms` : '0ms',
                            }}
                        />
                    ))}
                </div>
            )}

            <CandidateDetailDrawer result={openCandidate} onClose={() => setOpenCandidate(null)} />
        </div>
    )
}

export default VacancyResultsPage

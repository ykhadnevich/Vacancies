import { useState } from 'react'
import Card from '../ui/Card'
import Icon from '../ui/Icon'
import { JobSource } from '../../types/job'
import { type JobVacancyV6, primaryUrlOf } from '../../types/jobV6'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { trackerApi } from '../../api/trackerApi'
import { useT } from '../../i18n/useT'
import { useLanguage } from '../../i18n/LanguageContext'
import { verdictColor } from '../../utils/verdict'

const RAIL: Record<string, string> = {
    success: 'linear-gradient(to right, var(--score-bar-fill-start), var(--score-bar-fill-end))',
    warning: 'linear-gradient(to right, var(--color-warning-500), var(--color-warning-600))',
    danger:  'linear-gradient(to right, var(--color-danger-500), var(--color-danger-600))',
    neutral: 'var(--color-text-tertiary)',
}

interface Props {
    job:           JobVacancyV6
    onOpenDetails: (job: JobVacancyV6) => void
}

function JobCardV6({ job, onOpenDetails }: Props) {
    const queryClient = useQueryClient()
    const [trackerAdded, setTrackerAdded] = useState(false)
    const t = useT()
    const { language } = useLanguage()

    const SOURCE_LABEL: Record<JobSource, string> = {
        [JobSource.RobotaUa]: 'robota.ua',
        [JobSource.Jooble]:   'jooble',
        [JobSource.DOU]:      'dou',
        [JobSource.LinkedIn]: 'linkedin',
        [JobSource.WorkUa]:   'work.ua',
        [JobSource.Djinni]:   'djinni',
        [JobSource.Manual]:   t('source.manual'),
    }

    const url = primaryUrlOf(job)

    const addToTracker = useMutation({
        mutationFn: () => trackerApi.add({
            title:    job.title,
            company:  job.company,
            location: job.location ?? undefined,
            url,
            score:              job.score,
            verdict:            job.verdict,
            matchedSkills:      job.matchedSkills,
            missingMustHaves:   job.missingMustHaves,
            triggeredAntiFlags: job.triggeredAntiFlags,
            reasonShort:        job.strengthsUk ?? job.reasonUk ?? job.reasonEn ?? undefined,
            strengthsEn:        job.strengthsEn      ?? undefined,
            strengthsUk:        job.strengthsUk      ?? undefined,
            gapsEn:             job.gapsEn           ?? undefined,
            gapsUk:             job.gapsUk           ?? undefined,
            recommendationEn:   job.recommendationEn ?? undefined,
            recommendationUk:   job.recommendationUk ?? undefined,
            subScores:          job.subScores,
            pipelineVersion:    job.pipelineVersion,
        }),
        onSuccess: () => {
            setTrackerAdded(true)
            queryClient.invalidateQueries({ queryKey: ['tracker'] })
        },
    })

    const publishedDate = job.publishedAt
        ? new Date(job.publishedAt).toLocaleDateString(language === 'uk' ? 'uk-UA' : 'en-GB', { day: 'numeric', month: 'long' })
        : null

    const vc  = verdictColor(job.verdict)
    const pct = Math.max(0, Math.min(100, job.score * 100))
    const verdictLabel = (() => {
        const v = (job.verdict ?? '').toLowerCase()
        if (v.startsWith('strong'))  return t('scoring.verdict.strong')
        if (v.startsWith('partial')) return t('scoring.verdict.partial')
        if (v.startsWith('weak'))    return t('scoring.verdict.weak')
        return t('scoring.verdict.notRelevant')
    })()
    const labelColor = vc === 'neutral' ? 'var(--color-text-tertiary)' : `var(--color-${vc}-700)`
    const numColor   = vc === 'success' ? 'var(--color-primary-600)'
                     : vc === 'danger'  ? 'var(--color-text-secondary)'
                     : 'var(--color-text-primary)'

    const matched = job.matchedSkills    ?? []
    const missing = job.missingMustHaves ?? []
    const hasSkills = matched.length > 0 || missing.length > 0
    const MAX_MATCHED = 5
    const MAX_MISSING = 3
    const shownMatched = matched.slice(0, MAX_MATCHED)
    const shownMissing = missing.slice(0, MAX_MISSING)
    const matchedExtra = Math.max(0, matched.length - MAX_MATCHED)
    const missingExtra = Math.max(0, missing.length - MAX_MISSING)

    const teaser = language === 'uk'
        ? (job.strengthsUk || job.reasonUk || job.strengthsEn || job.reasonEn || '')
        : (job.strengthsEn || job.reasonEn || job.strengthsUk || job.reasonUk || '')

    return (
        <Card interactive padding="md" style={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 11, flex: 1 }}>

                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 14 }}>
                    <div style={{ minWidth: 0, flex: 1 }}>
                        <a
                            href={url}
                            target="_blank"
                            rel="noreferrer"
                            onClick={(e) => e.stopPropagation()}
                            style={{
                                fontFamily:     'var(--font-serif)',
                                fontSize:       'var(--text-xl)',
                                fontWeight:     600,
                                letterSpacing:  '-0.01em',
                                lineHeight:     1.2,
                                color:          'var(--color-text-primary)',
                                textDecoration: 'none',
                                display:        'block',
                                overflow:       'hidden',
                                textOverflow:   'ellipsis',
                                whiteSpace:     'nowrap',
                            }}
                        >
                            {job.title}
                        </a>
                        <p style={{ fontSize: 'var(--text-md)', color: 'var(--color-text-secondary)', margin: '3px 0 0', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                            {job.company}
                        </p>
                    </div>
                    <div style={{ textAlign: 'right', flexShrink: 0 }}>
                        <div style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, fontVariantNumeric: 'tabular-nums', fontSize: 'var(--score-md)', lineHeight: 1, letterSpacing: '-0.02em', color: numColor }}>
                            {pct.toFixed(1)}<span style={{ fontSize: 'var(--text-md)', color: 'var(--color-text-tertiary)' }}>%</span>
                        </div>
                        <div style={{ fontSize: 'var(--text-xs)', fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase', color: labelColor, marginTop: 4 }}>
                            {verdictLabel}
                        </div>
                    </div>
                </div>

                <div style={{ height: 6, borderRadius: 'var(--radius-pill)', background: 'var(--score-bar-track)', overflow: 'hidden', boxShadow: 'var(--shadow-inset)' }}>
                    <div style={{ height: '100%', width: `${pct}%`, borderRadius: 'var(--radius-pill)', background: RAIL[vc] }} />
                </div>

                <div style={{ display: 'flex', gap: 8, fontFamily: 'var(--font-mono)', fontSize: 'var(--text-xs)', textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--color-text-tertiary)', flexWrap: 'wrap', alignItems: 'center' }}>
                    <span>{SOURCE_LABEL[job.source]}</span>
                    {job.location && (<><span>·</span><span style={{ textTransform: 'none', letterSpacing: 0, fontFamily: 'var(--font-sans)' }}>{job.location}</span></>)}
                    {publishedDate && (<><span>·</span><span>{publishedDate}</span></>)}
                </div>

                {teaser && (
                    <p style={teaserStyle}>
                        {teaser}
                    </p>
                )}

                {hasSkills ? (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                        {matched.length > 0 ? (
                            <div style={chipRow}>
                                {shownMatched.map((s) => (
                                    <span key={`m-${s}`} style={chipMatched}>{s}</span>
                                ))}
                                {matchedExtra > 0 && <span style={chipMore}>+{matchedExtra}</span>}
                            </div>
                        ) : (
                            <div style={chipRow}>
                                <span style={noMatchesChip}>{t('card.noMatches')}</span>
                            </div>
                        )}
                        {missing.length > 0 ? (
                            <div style={chipRow}>
                                {shownMissing.map((s) => (<span key={`x-${s}`} style={chipMissing}>— {s}</span>))}
                                {missingExtra > 0 && <span style={chipMore}>+{missingExtra}</span>}
                            </div>
                        ) : matched.length > 0 && (
                            <div style={chipRow}>
                                <span style={noGapsChip}>{t('card.noGaps')}</span>
                            </div>
                        )}
                    </div>
                ) : (
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                        <span style={chipPlaceholder}>{t('card.skillsAfterCv')}</span>
                    </div>
                )}

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, paddingTop: 4, marginTop: 'auto', borderTop: '1px solid var(--color-border-subtle)' }}>
                    <button
                        onClick={(e) => { e.stopPropagation(); onOpenDetails(job) }}
                        style={actionLink}
                    >
                        {t('card.details')} <Icon name="arrow-right" size={13} />
                    </button>
                    <button
                        onClick={(e) => { e.stopPropagation(); if (!trackerAdded) addToTracker.mutate() }}
                        disabled={trackerAdded || addToTracker.isPending}
                        style={{
                            ...actionBtn,
                            background:  trackerAdded ? 'var(--color-success-50)' : 'transparent',
                            borderColor: trackerAdded ? 'var(--color-success-100)' : 'var(--color-border-default)',
                            color:       trackerAdded ? 'var(--color-success-700)' : 'var(--color-text-secondary)',
                            cursor:      trackerAdded ? 'default' : 'pointer',
                        }}
                    >
                        {trackerAdded ? t('card.tracked') : addToTracker.isPending ? '…' : '+'}
                    </button>
                </div>
            </div>
        </Card>
    )
}

const chipBase: React.CSSProperties = {
    padding: '3px 9px', borderRadius: 'var(--radius-sm)', fontSize: 'var(--text-xs)',
    fontWeight: 'var(--font-weight-medium)' as unknown as number, whiteSpace: 'nowrap',
    overflow: 'hidden', textOverflow: 'ellipsis', maxWidth: 180, minWidth: 0, flexShrink: 1,
}
const chipMatched: React.CSSProperties = { ...chipBase, background: 'var(--color-success-50)', color: 'var(--color-success-700)', border: '1px solid var(--color-success-100)' }
const chipMissing: React.CSSProperties = { ...chipBase, background: 'var(--color-danger-50)', color: 'var(--color-danger-700)', border: '1px solid var(--color-danger-300)' }
const chipPlaceholder: React.CSSProperties = { ...chipBase, fontStyle: 'italic', fontWeight: 400, color: 'var(--color-text-tertiary)', border: '1px dashed var(--color-border-default)', background: 'transparent' }
const chipMore: React.CSSProperties = { ...chipBase, color: 'var(--color-text-tertiary)', border: '1px dashed var(--color-border-default)', background: 'transparent', fontVariantNumeric: 'tabular-nums', flexShrink: 0, maxWidth: 'none' }

const chipRow: React.CSSProperties = {
    display:    'flex',
    flexWrap:   'nowrap',
    gap:        6,
    alignItems: 'center',
    overflow:   'hidden',
    minWidth:   0,
}

const noGapsChip: React.CSSProperties = {
    ...chipMissing,
    fontFamily: 'var(--font-serif)',
    fontStyle:  'italic',
    fontWeight: 400,
    maxWidth:   'none',
}

const noMatchesChip: React.CSSProperties = {
    ...chipBase,
    fontFamily: 'var(--font-serif)',
    fontStyle:  'italic',
    fontWeight: 400,
    color:      'var(--color-text-tertiary)',
    border:     '1px dashed var(--color-border-default)',
    background: 'transparent',
    maxWidth:   'none',
}

const teaserStyle: React.CSSProperties = {
    margin: 0,
    fontFamily:    'var(--font-sans)',
    fontSize:      'var(--text-sm)',
    fontWeight:    400,
    lineHeight:    1.55,
    color:         'var(--color-text-primary)',
    display:       '-webkit-box',
    WebkitLineClamp: 2,
    WebkitBoxOrient: 'vertical',
    overflow:      'hidden',
}

const actionLink: React.CSSProperties = {
    display: 'inline-flex', alignItems: 'center', gap: 4, background: 'transparent', border: 'none',
    color: 'var(--color-primary-600)', fontSize: 'var(--text-sm)', cursor: 'pointer', padding: '4px 10px',
    borderRadius: 'var(--radius-md)', fontWeight: 'var(--font-weight-medium)' as unknown as number, fontFamily: 'inherit',
}
const actionBtn: React.CSSProperties = {
    display: 'inline-flex', alignItems: 'center', gap: 4, border: '1px solid var(--color-border-default)',
    fontSize: 'var(--text-sm)', padding: '4px 10px', borderRadius: 'var(--radius-md)',
    fontWeight: 'var(--font-weight-medium)' as unknown as number, fontFamily: 'inherit',
}

export default JobCardV6

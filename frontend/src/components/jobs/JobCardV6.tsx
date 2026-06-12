import { useState } from 'react'
import Card from '../ui/Card'
import Icon from '../ui/Icon'
import VerdictBadge from './VerdictBadge'
import EvidenceChips from './EvidenceChips'
import { JobSource } from '../../types/job'
import { type JobVacancyV6, primaryUrlOf } from '../../types/jobV6'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { trackerApi } from '../../api/trackerApi'
import { useT } from '../../i18n/useT'
import { useLanguage } from '../../i18n/LanguageContext'

const SOURCE_LABEL: Record<JobSource, string> = {
    [JobSource.RobotaUa]: 'robota.ua',
    [JobSource.Jooble]:   'jooble',
    [JobSource.DOU]:      'dou',
    [JobSource.LinkedIn]: 'linkedin',
    [JobSource.WorkUa]:   'work.ua',
    [JobSource.Djinni]:   'djinni',
    [JobSource.Manual]:   'вручну',
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
        ? new Date(job.publishedAt).toLocaleDateString(language === 'uk' ? 'uk-UA' : 'en-GB', { day: 'numeric', month: 'short' })
        : null

    return (
        <Card interactive padding="md">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>

                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
                    <div style={{ minWidth: 0, flex: 1 }}>
                        <a
                            href={url}
                            target="_blank"
                            rel="noreferrer"
                            style={{
                                fontSize:       'var(--text-lg)',
                                fontWeight:     'var(--font-weight-semibold)' as unknown as number,
                                color:          'var(--color-text-primary)',
                                textDecoration: 'none',
                                display:        'block',
                                marginBottom:   2,
                            }}
                            onClick={(e) => e.stopPropagation()}
                        >
                            {job.title}
                        </a>
                        <p style={{
                            fontSize: 'var(--text-md)',
                            color:    'var(--color-text-secondary)',
                            margin:   0,
                        }}>
                            {job.company}
                        </p>
                    </div>
                    <VerdictBadge verdict={job.verdict} score={job.score} />
                </div>

                <div style={{
                    display:    'flex',
                    gap:        12,
                    fontSize:   'var(--text-sm)',
                    color:      'var(--color-text-tertiary)',
                    flexWrap:   'wrap',
                    alignItems: 'center',
                }}>
                    {job.location && <span>{job.location}</span>}
                    <span>·</span>
                    <span>{SOURCE_LABEL[job.source]}</span>
                    {publishedDate && (
                        <>
                            <span>·</span>
                            <span>{publishedDate}</span>
                        </>
                    )}
                </div>

                <EvidenceChips
                    matched={job.matchedSkills ?? []}
                    missing={job.missingMustHaves ?? []}
                    antiFlags={job.triggeredAntiFlags ?? []}
                    limit={3}
                />

                <div style={{
                    display:        'flex',
                    justifyContent: 'flex-end',
                    gap:            8,
                    paddingTop:     4,
                    marginTop:      4,
                    borderTop:      '0.5px solid var(--color-border-subtle)',
                }}>
                    <button
                        onClick={(e) => { e.stopPropagation(); onOpenDetails(job) }}
                        style={{
                            display:      'inline-flex',
                            alignItems:   'center',
                            gap:          4,
                            background:   'transparent',
                            border:       'none',
                            color:        'var(--color-primary-600)',
                            fontSize:     'var(--text-sm)',
                            cursor:       'pointer',
                            padding:      '4px 10px',
                            borderRadius: 'var(--radius-md)',
                            fontWeight:   'var(--font-weight-medium)' as unknown as number,
                            fontFamily:   'inherit',
                        }}
                    >
                        {t('card.details')} <Icon name="arrow-right" size={13} />
                    </button>
                    <button
                        onClick={(e) => { e.stopPropagation(); if (!trackerAdded) addToTracker.mutate() }}
                        disabled={trackerAdded || addToTracker.isPending}
                        style={{
                            display:      'inline-flex',
                            alignItems:   'center',
                            gap:          4,
                            background:   trackerAdded ? 'var(--color-success-50)' : 'transparent',
                            border:       `1px solid ${trackerAdded ? 'var(--color-success-100)' : 'var(--color-border-default)'}`,
                            color:        trackerAdded ? 'var(--color-success-700)' : 'var(--color-text-secondary)',
                            fontSize:     'var(--text-sm)',
                            cursor:       trackerAdded ? 'default' : 'pointer',
                            padding:      '4px 10px',
                            borderRadius: 'var(--radius-md)',
                            fontWeight:   'var(--font-weight-medium)' as unknown as number,
                            fontFamily:   'inherit',
                        }}
                    >
                        {trackerAdded
                            ? t('card.tracked')
                            : addToTracker.isPending
                                ? '…'
                                : '+'}
                    </button>
                </div>
            </div>
        </Card>
    )
}

export default JobCardV6

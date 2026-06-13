import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import Card from '../ui/Card'
import Icon from '../ui/Icon'
import { JobSource, type JobVacancy } from '../../types/job'
import { trackerApi } from '../../api/trackerApi'
import { useT } from '../../i18n/useT'
import { useLanguage } from '../../i18n/LanguageContext'

interface Props {
    job: JobVacancy
}

function JobCardRaw({ job }: Props) {
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

    const addToTracker = useMutation({
        mutationFn: () => trackerApi.add({
            title:   job.title,
            company: job.company,
            url:     job.primaryUrl,
        }),
        onSuccess: () => {
            setTrackerAdded(true)
            queryClient.invalidateQueries({ queryKey: ['tracker'] })
        },
    })

    const publishedDate = job.publishedAt
        ? new Date(job.publishedAt).toLocaleDateString(language === 'uk' ? 'uk-UA' : 'en-GB', { day: 'numeric', month: 'long' })
        : null

    return (
        <Card padding="md" style={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10, flex: 1 }}>

                <div style={{ minWidth: 0 }}>
                    <a
                        href={job.primaryUrl}
                        target="_blank"
                        rel="noreferrer"
                        style={{
                            fontFamily:     'var(--font-serif)',
                            fontSize:       'var(--text-xl)',
                            fontWeight:     600,
                            letterSpacing:  '-0.01em',
                            lineHeight:     1.25,
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
                    <p style={{ fontSize: 'var(--text-md)', color: 'var(--color-text-secondary)', margin: '4px 0 0', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {job.company}
                    </p>
                </div>

                <div style={{ display: 'flex', gap: 8, fontFamily: 'var(--font-mono)', fontSize: 'var(--text-xs)', textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--color-text-tertiary)', flexWrap: 'wrap', alignItems: 'center' }}>
                    <span>{SOURCE_LABEL[job.source]}</span>
                    {job.location && (<><span>·</span><span style={{ textTransform: 'none', letterSpacing: 0, fontFamily: 'var(--font-sans)' }}>{job.location}</span></>)}
                    {publishedDate && (<><span>·</span><span>{publishedDate}</span></>)}
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, paddingTop: 8, marginTop: 'auto', borderTop: '1px solid var(--color-border-subtle)' }}>
                    <a
                        href={job.primaryUrl}
                        target="_blank"
                        rel="noreferrer"
                        style={{ display: 'inline-flex', alignItems: 'center', gap: 4, background: 'transparent', color: 'var(--color-primary-600)', fontSize: 'var(--text-sm)', padding: '4px 10px', borderRadius: 'var(--radius-md)', fontWeight: 'var(--font-weight-medium)' as unknown as number, textDecoration: 'none' }}
                    >
                        {t('common.open')} <Icon name="arrow-up-right" size={13} />
                    </a>
                    <button
                        onClick={() => { if (!trackerAdded) addToTracker.mutate() }}
                        disabled={trackerAdded || addToTracker.isPending}
                        style={{
                            display: 'inline-flex', alignItems: 'center', gap: 4,
                            background:  trackerAdded ? 'var(--color-success-50)' : 'transparent',
                            border:      `1px solid ${trackerAdded ? 'var(--color-success-100)' : 'var(--color-border-default)'}`,
                            color:       trackerAdded ? 'var(--color-success-700)' : 'var(--color-text-secondary)',
                            fontSize:    'var(--text-sm)', cursor: trackerAdded ? 'default' : 'pointer',
                            padding:     '4px 10px', borderRadius: 'var(--radius-md)',
                            fontWeight:  'var(--font-weight-medium)' as unknown as number, fontFamily: 'inherit',
                        }}
                    >
                        {trackerAdded ? t('card.tracked') : addToTracker.isPending ? '…' : <><Icon name="plus" size={14} /> {t('card.track')}</>}
                    </button>
                </div>
            </div>
        </Card>
    )
}

export default JobCardRaw

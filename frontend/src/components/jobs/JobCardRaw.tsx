import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import Card from '../ui/Card'
import Icon from '../ui/Icon'
import { JobSource, type JobVacancy } from '../../types/job'
import { trackerApi } from '../../api/trackerApi'

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
    job: JobVacancy
}


function JobCardRaw({ job }: Props) {
    const queryClient = useQueryClient()
    const [trackerAdded, setTrackerAdded] = useState(false)

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
        ? new Date(job.publishedAt).toLocaleDateString('uk-UA', { day: 'numeric', month: 'short' })
        : null

    return (
        <Card padding="md">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>

                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
                    <div style={{ minWidth: 0, flex: 1 }}>
                        <a
                            href={job.primaryUrl}
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

                <div style={{
                    display:        'flex',
                    justifyContent: 'flex-end',
                    gap:            8,
                    paddingTop:     4,
                    marginTop:      4,
                    borderTop:      '0.5px solid var(--color-border-subtle)',
                }}>
                    <a
                        href={job.primaryUrl}
                        target="_blank"
                        rel="noreferrer"
                        style={{
                            display:        'inline-flex',
                            alignItems:     'center',
                            gap:            4,
                            background:     'transparent',
                            color:          'var(--color-primary-600)',
                            fontSize:       'var(--text-sm)',
                            padding:        '4px 10px',
                            borderRadius:   'var(--radius-md)',
                            fontWeight:     'var(--font-weight-medium)' as unknown as number,
                            textDecoration: 'none',
                        }}
                    >
                        Відкрити <Icon name="arrow-up-right" size={13} />
                    </a>
                    <button
                        onClick={() => { if (!trackerAdded) addToTracker.mutate() }}
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
                            ? 'Додано'
                            : addToTracker.isPending
                                ? '…'
                                : <><Icon name="plus" size={14} /> Трекер</>}
                    </button>
                </div>
            </div>
        </Card>
    )
}

export default JobCardRaw

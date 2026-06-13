import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { jobsApi } from '../api/jobsApi'
import JobCard from './JobCard'
import Icon from './ui/Icon'
import { useT } from '../i18n/useT'

function ManualVacanciesSection() {
    const t = useT()
    const [expanded, setExpanded] = useState(false)

    const { data: jobs = [], isLoading } = useQuery({
        queryKey: ['manualVacancies'],
        queryFn:  jobsApi.getManualVacancies,
        enabled:  expanded,
    })

    return (
        <div style={{ marginTop: 8, borderTop: '0.5px solid var(--color-border-default)', paddingTop: 20 }}>
            <button
                onClick={() => setExpanded((p) => !p)}
                style={{
                    display:      'inline-flex',
                    alignItems:   'center',
                    gap:          8,
                    background:   'transparent',
                    border:       '1px solid var(--color-border-default)',
                    borderRadius: 'var(--radius-md)',
                    padding:      '8px 14px',
                    cursor:       'pointer',
                    fontSize:     'var(--text-sm)',
                    color:        'var(--color-text-secondary)',
                    fontFamily:   'inherit',
                }}>
                <Icon name={expanded ? 'chevron-up' : 'chevron-down'} size={14} />
                {t('list.savedUrls')}
            </button>

            {expanded && (
                <div style={{ marginTop: 16 }}>
                    {isLoading && <p style={{ color: 'var(--color-text-tertiary)', fontSize: 'var(--text-sm)' }}>{t('common.loading')}</p>}
                    {!isLoading && jobs.length === 0 && (
                        <p style={{ color: 'var(--color-text-tertiary)', fontSize: 'var(--text-sm)' }}>
                            {t('manualVacancies.empty')}
                        </p>
                    )}
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                        {jobs.map((job) => <JobCard key={job.id} job={job} />)}
                    </div>
                </div>
            )}
        </div>
    )
}

export default ManualVacanciesSection

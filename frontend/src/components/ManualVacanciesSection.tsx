import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { jobsApi } from '../api/jobsApi'
import JobCard from './JobCard'

function ManualVacanciesSection() {
    const [expanded, setExpanded] = useState(false)

    const { data: jobs = [], isLoading } = useQuery({
        queryKey: ['manualVacancies'],
        queryFn: jobsApi.getManualVacancies,
        enabled: expanded,
    })

    return (
        <div style={{ marginTop: 32 }}>
            <div style={{ borderTop: '2px solid #e5e7eb', paddingTop: 20 }}>
                <button
                    onClick={() => setExpanded(prev => !prev)}
                    style={{
                        background: 'none', border: '1px solid #e5e7eb', borderRadius: 8,
                        padding: '8px 16px', cursor: 'pointer', fontSize: 14, color: '#374151',
                        display: 'flex', alignItems: 'center', gap: 8, fontWeight: 600,
                    }}>
                    {expanded ? '▲' : '▼'}
                    🔗 Вакансії з моїх сайтів
                </button>

                {expanded && (
                    <div style={{ marginTop: 16 }}>
                        {isLoading && <p style={{ color: '#6b7280' }}>Завантаження...</p>}
                        {!isLoading && jobs.length === 0 && (
                            <p style={{ color: '#9ca3af', fontSize: 14 }}>
                                Немає вакансій. Додай посилання на сайт з вакансіями через кнопку вище.
                            </p>
                        )}
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                            {jobs.map(job => (
                                <JobCard key={job.id} job={job} />
                            ))}
                        </div>
                    </div>
                )}
            </div>
        </div>
    )
}

export default ManualVacanciesSection
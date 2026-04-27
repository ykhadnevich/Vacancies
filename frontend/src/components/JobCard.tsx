import type { JobVacancy } from '../types/job'
import { JobSource } from '../types/job'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { trackerApi } from '../api/trackerApi'

const sourceLabels: Record<JobSource, string> = {
    [JobSource.RobotaUa]: 'robota.ua',
    [JobSource.Jooble]: 'jooble',
    [JobSource.Dou]: 'dou',
    [JobSource.LinkedIn]: 'linkedin',
    [JobSource.WorkUa]: 'work.ua',
    [JobSource.Djinni]: 'djinni',
    [JobSource.Manual]: 'вручну',
}

const sourceColors: Record<JobSource, string> = {
    [JobSource.RobotaUa]: '#e74c3c',
    [JobSource.Jooble]: '#3498db',
    [JobSource.Dou]: '#2ecc71',
    [JobSource.LinkedIn]: '#0077b5',
    [JobSource.WorkUa]: '#e67e22',
    [JobSource.Djinni]: '#9b59b6',
    [JobSource.Manual]: '#95a5a6',
}

interface Props {
    job: JobVacancy
}

function JobCard({ job }: Props) {
    const queryClient = useQueryClient()
    const addMutation = useMutation({
        mutationFn: () => trackerApi.add({
            title: job.title,
            company: job.company,
            url: job.primaryUrl,
        }),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })

    return (
        <div style={{
            border: '1px solid #e5e7eb',
            borderRadius: 12,
            padding: '20px 24px',
            background: '#fff',
            boxShadow: '0 1px 3px rgba(0,0,0,0.06)',
        }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <div>
                    <a href={job.primaryUrl} target="_blank" rel="noreferrer"
                       style={{ fontSize: 18, fontWeight: 600, color: '#1d4ed8', textDecoration: 'none' }}>
                        {job.title}
                    </a>
                    <p style={{ margin: '4px 0 0', color: '#374151', fontSize: 15 }}>{job.company}</p>
                </div>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <span style={{
              background: sourceColors[job.source],
              color: '#fff',
              borderRadius: 6,
              padding: '3px 10px',
              fontSize: 12,
              fontWeight: 500,
              whiteSpace: 'nowrap',
          }}>
            {sourceLabels[job.source]}
          </span>
                    <button
                        onClick={() => addMutation.mutate()}
                        disabled={addMutation.isPending || addMutation.isSuccess}
                        style={{
                            padding: '4px 12px',
                            fontSize: 12,
                            borderRadius: 6,
                            border: '1px solid #2563eb',
                            background: addMutation.isSuccess ? '#dcfce7' : '#fff',
                            color: addMutation.isSuccess ? '#16a34a' : '#2563eb',
                            cursor: addMutation.isPending ? 'wait' : 'pointer',
                            whiteSpace: 'nowrap',
                        }}>
                        {addMutation.isSuccess ? '✓ Додано' : addMutation.isPending ? '...' : '+ Трекер'}
                    </button>
                </div>
            </div>

            <div style={{ display: 'flex', gap: 16, marginTop: 12, flexWrap: 'wrap', fontSize: 14, color: '#6b7280' }}>
                {job.location && <span>📍 {job.location}</span>}
                {job.salary && <span>💰 {job.salary}</span>}
                {job.publishedAt && <span>📅 {new Date(job.publishedAt).toLocaleDateString('uk-UA')}</span>}
                {job.relevanceScore != null && (
                    <span style={{ color: '#059669', fontWeight: 600 }}>✓ {job.relevanceScore}% релевантність</span>
                )}
            </div>
        </div>
    )
}

export default JobCard
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { trackerApi } from '../../api/trackerApi'
import type { TrackerEntry, CreateTrackerEntry } from '../../types/tracker'
import { ApplicationStatus } from '../../types/tracker'

const statusLabels: Record<ApplicationStatus, string> = {
    [ApplicationStatus.InReview]: 'В розгляді',
    [ApplicationStatus.Rejected]: 'Відмова',
    [ApplicationStatus.Offer]: 'Оффер',
    [ApplicationStatus.Archived]: 'Архів',
}

const statusColors: Record<ApplicationStatus, string> = {
    [ApplicationStatus.InReview]: '#2563eb',
    [ApplicationStatus.Rejected]: '#dc2626',
    [ApplicationStatus.Offer]: '#16a34a',
    [ApplicationStatus.Archived]: '#6b7280',
}

const pipelineStepLabels: Record<string, string> = {
    cvSent: 'CV надіслано',
    responded: 'Відгукнулись',
    followUpSent: 'Follow-up',
    shortInterview: 'Коротке інтерв\'ю',
    testTask: 'Тестове',
    technicalInterview: 'Технічне',
    finalInterview: 'Фінальне',
    jobOffer: 'Оффер',
}

function AddEntryForm({ onAdd }: { onAdd: (entry: CreateTrackerEntry) => void }) {
    const [title, setTitle] = useState('')
    const [company, setCompany] = useState('')
    const [salary, setSalary] = useState('')
    const [url, setUrl] = useState('')

    const handleSubmit = () => {
        if (!title.trim() || !company.trim()) return
        onAdd({ title: title.trim(), company: company.trim(), salary: salary || undefined, url: url || undefined })
        setTitle(''); setCompany(''); setSalary(''); setUrl('')
    }

    return (
        <div style={{ background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: 12, padding: 20, marginBottom: 24 }}>
            <h3 style={{ margin: '0 0 16px', fontSize: 16 }}>Додати вакансію вручну</h3>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                <input placeholder="Посада *" value={title} onChange={e => setTitle(e.target.value)}
                       style={{ flex: 2, minWidth: 160, padding: '8px 12px', borderRadius: 8, border: '1px solid #cbd5e1', fontSize: 14 }} />
                <input placeholder="Компанія *" value={company} onChange={e => setCompany(e.target.value)}
                       style={{ flex: 2, minWidth: 160, padding: '8px 12px', borderRadius: 8, border: '1px solid #cbd5e1', fontSize: 14 }} />
                <input placeholder="Зарплата" value={salary} onChange={e => setSalary(e.target.value)}
                       style={{ flex: 1, minWidth: 100, padding: '8px 12px', borderRadius: 8, border: '1px solid #cbd5e1', fontSize: 14 }} />
                <input placeholder="URL вакансії" value={url} onChange={e => setUrl(e.target.value)}
                       style={{ flex: 3, minWidth: 200, padding: '8px 12px', borderRadius: 8, border: '1px solid #cbd5e1', fontSize: 14 }} />
                <button onClick={handleSubmit}
                        style={{ padding: '8px 20px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: 8, cursor: 'pointer', fontSize: 14 }}>
                    Додати
                </button>
            </div>
        </div>
    )
}

function TrackerRow({ entry }: { entry: TrackerEntry }) {
    const queryClient = useQueryClient()

    const stepMutation = useMutation({
        mutationFn: ({ step, value }: { step: string; value: boolean }) =>
            trackerApi.updatePipelineStep(entry.id, step, value),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })

    const statusMutation = useMutation({
        mutationFn: (status: number) => trackerApi.updateStatus(entry.id, status),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })

    const deleteMutation = useMutation({
        mutationFn: () => trackerApi.delete(entry.id),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })

    return (
        <div style={{ border: '1px solid #e5e7eb', borderRadius: 12, padding: '16px 20px', background: '#fff', marginBottom: 12 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 12 }}>
                <div>
                    <span style={{ fontWeight: 600, fontSize: 16 }}>{entry.title}</span>
                    <span style={{ color: '#6b7280', marginLeft: 12, fontSize: 14 }}>{entry.company}</span>
                    {entry.salary && <span style={{ color: '#059669', marginLeft: 12, fontSize: 14 }}>💰 {entry.salary}</span>}
                    {entry.url && (
                        <a href={entry.url} target="_blank" rel="noreferrer"
                           style={{ marginLeft: 12, fontSize: 13, color: '#2563eb' }}>↗ посилання</a>
                    )}
                </div>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                    <select
                        value={entry.status}
                        onChange={e => statusMutation.mutate(Number(e.target.value))}
                        style={{
                            padding: '4px 10px', borderRadius: 6, border: '1px solid #e5e7eb',
                            fontSize: 13, color: statusColors[entry.status as ApplicationStatus], fontWeight: 600, cursor: 'pointer'
                        }}>
                        {Object.entries(statusLabels).map(([val, label]) => (
                            <option key={val} value={val}>{label}</option>
                        ))}
                    </select>
                    <button onClick={() => deleteMutation.mutate()}
                            style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#dc2626', fontSize: 18 }}>
                        ✕
                    </button>
                </div>
            </div>

            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                {Object.entries(pipelineStepLabels).map(([step, label]) => {
                    const checked = entry.pipelineSteps[step as keyof typeof entry.pipelineSteps]
                    return (
                        <label key={step} style={{
                            display: 'flex', alignItems: 'center', gap: 4, cursor: 'pointer',
                            fontSize: 13, color: checked ? '#16a34a' : '#6b7280',
                            background: checked ? '#f0fdf4' : '#f9fafb',
                            border: `1px solid ${checked ? '#bbf7d0' : '#e5e7eb'}`,
                            borderRadius: 6, padding: '4px 10px',
                        }}>
                            <input type="checkbox" checked={!!checked}
                                   onChange={e => stepMutation.mutate({ step, value: e.target.checked })}
                                   style={{ accentColor: '#16a34a' }} />
                            {label}
                        </label>
                    )
                })}
            </div>
        </div>
    )
}

function TrackerPage() {
    const queryClient = useQueryClient()

    const { data: entries = [], isLoading } = useQuery({
        queryKey: ['tracker'],
        queryFn: trackerApi.getAll,
        refetchOnMount: true,
        staleTime: 0,
    })

    const addMutation = useMutation({
        mutationFn: trackerApi.add,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })

    if (isLoading) return <div style={{ padding: 32 }}>Завантаження...</div>

    return (
        <div style={{ maxWidth: 900, margin: '0 auto', padding: '24px 16px' }}>
            <h2 style={{ marginBottom: 24 }}>Трекер заявок</h2>
            <AddEntryForm onAdd={entry => addMutation.mutate(entry)} />
            {entries.length === 0 && (
                <p style={{ color: '#6b7280', textAlign: 'center', marginTop: 48 }}>
                    Ще немає заявок. Додай першу вручну або зі стрічки вакансій.
                </p>
            )}
            {entries.map(entry => (
                <TrackerRow key={entry.id} entry={entry} />
            ))}
        </div>
    )
}

export default TrackerPage
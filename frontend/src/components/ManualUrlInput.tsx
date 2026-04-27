import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { jobsApi } from '../api/jobsApi'

function ManualUrlInput() {
    const [url, setUrl] = useState('')
    const [expanded, setExpanded] = useState(false)
    const [showSaved, setShowSaved] = useState(false)
    const queryClient = useQueryClient()

    const { data: savedUrls = [] } = useQuery({
        queryKey: ['savedUrls'],
        queryFn: jobsApi.getSavedUrls,
        enabled: showSaved,
    })

    const addMutation = useMutation({
        mutationFn: () => jobsApi.addManualUrl(url),
        onSuccess: () => {
            setUrl('')
            queryClient.invalidateQueries({ queryKey: ['savedUrls'] })
        },
    })

    const refreshMutation = useMutation({
        mutationFn: (id: string) => jobsApi.refreshSavedUrl(id),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['savedUrls'] }),
    })

    if (!expanded) {
        return (
            <button
                onClick={() => setExpanded(true)}
                style={{
                    padding: '8px 16px', borderRadius: 8, fontSize: 14, cursor: 'pointer',
                    background: '#fff', color: '#6b7280', border: '1px solid #e5e7eb',
                    marginBottom: 8,
                }}>
                + Додати вакансії з сайту
            </button>
        )
    }

    return (
        <div style={{
            background: '#f8fafc', border: '1px solid #e2e8f0',
            borderRadius: 12, padding: 16, marginBottom: 8,
        }}>
            <p style={{ margin: '0 0 10px', fontSize: 14, color: '#374151', fontWeight: 600 }}>
                Вставте посилання на сторінку з вакансіями
            </p>
            <p style={{ margin: '0 0 10px', fontSize: 13, color: '#6b7280' }}>
                Наприклад: https://www.nixsolutions.com/ua/cooperation/vacancies/
            </p>
            <div style={{ display: 'flex', gap: 8 }}>
                <input
                    type="url"
                    value={url}
                    onChange={e => setUrl(e.target.value)}
                    onKeyDown={e => e.key === 'Enter' && url && addMutation.mutate()}
                    placeholder="https://..."
                    style={{
                        flex: 1, padding: '10px 14px', borderRadius: 8,
                        border: '1px solid #e5e7eb', fontSize: 14,
                    }} />
                <button
                    onClick={() => addMutation.mutate()}
                    disabled={addMutation.isPending || !url}
                    style={{
                        padding: '10px 20px', borderRadius: 8, fontSize: 14, cursor: 'pointer',
                        background: '#2563eb', color: '#fff', border: 'none', fontWeight: 600,
                        opacity: !url ? 0.5 : 1,
                    }}>
                    {addMutation.isPending ? 'Парсинг...' : 'Додати'}
                </button>
                <button
                    onClick={() => { setExpanded(false); setUrl('') }}
                    style={{
                        padding: '10px 14px', borderRadius: 8, fontSize: 14, cursor: 'pointer',
                        background: '#fff', color: '#6b7280', border: '1px solid #e5e7eb',
                    }}>
                    Скасувати
                </button>
            </div>

            {addMutation.isSuccess && (
                <p style={{ color: '#16a34a', fontSize: 14, margin: '10px 0 0' }}>
                    ✓ Знайдено {addMutation.data.jobsFound} вакансій
                </p>
            )}
            {addMutation.isError && (
                <p style={{ color: '#dc2626', fontSize: 14, margin: '10px 0 0' }}>
                    Не вдалось спарсити сторінку. Перевір посилання.
                </p>
            )}

            {/* Збережені URL */}
            <div style={{ marginTop: 16, borderTop: '1px solid #e2e8f0', paddingTop: 12 }}>
                <button
                    onClick={() => setShowSaved(prev => !prev)}
                    style={{
                        background: 'none', border: 'none', cursor: 'pointer',
                        fontSize: 13, color: '#6b7280', padding: 0,
                    }}>
                    {showSaved ? '▲' : '▼'} Збережені посилання
                </button>

                {showSaved && (
                    <div style={{ marginTop: 10, display: 'flex', flexDirection: 'column', gap: 8 }}>
                        {savedUrls.length === 0 && (
                            <p style={{ color: '#9ca3af', fontSize: 13 }}>Немає збережених посилань</p>
                        )}
                        {savedUrls.map(saved => (
                            <div key={saved.id} style={{
                                display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                                background: '#fff', borderRadius: 8, padding: '8px 12px',
                                border: '1px solid #e5e7eb', gap: 8,
                            }}>
                                <div style={{ flex: 1, overflow: 'hidden' }}>
                                    <a href={saved.url} target="_blank" rel="noreferrer"
                                       style={{ fontSize: 13, color: '#2563eb', textDecoration: 'none' }}>
                                        {saved.alias || saved.url}
                                    </a>
                                    <p style={{ margin: 0, fontSize: 12, color: '#9ca3af' }}>
                                        {saved.lastParsedAt
                                            ? `Оновлено: ${new Date(saved.lastParsedAt).toLocaleDateString('uk-UA')} · ${saved.lastParsedCount} вакансій`
                                            : 'Ще не парсилось'}
                                    </p>
                                </div>
                                <button
                                    onClick={() => refreshMutation.mutate(saved.id)}
                                    disabled={refreshMutation.isPending}
                                    style={{
                                        padding: '6px 12px', borderRadius: 6, fontSize: 12, cursor: 'pointer',
                                        background: '#eff6ff', color: '#2563eb',
                                        border: '1px solid #bfdbfe', whiteSpace: 'nowrap',
                                    }}>
                                    {refreshMutation.isPending ? '...' : '↻ Оновити'}
                                </button>
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </div>
    )
}

export default ManualUrlInput
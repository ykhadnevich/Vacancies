import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { jobsApi } from '../api/jobsApi'
import Modal from './ui/Modal'
import Icon from './ui/Icon'

const FIXED_HEIGHT = 38

const inputStyle: React.CSSProperties = {
    width:        '100%',
    padding:      '10px 14px',
    borderRadius: 'var(--radius-md)',
    border:       '1px solid var(--color-border-default)',
    fontSize:     'var(--text-md)',
    color:        'var(--color-text-primary)',
    background:   'var(--color-bg-surface)',
    outline:      'none',
    fontFamily:   'inherit',
}

const focusOn = (e: React.FocusEvent<HTMLInputElement>) => {
    e.currentTarget.style.borderColor = 'var(--color-primary-500)'
    e.currentTarget.style.boxShadow   = '0 0 0 3px var(--color-primary-100)'
}
const focusOff = (e: React.FocusEvent<HTMLInputElement>) => {
    e.currentTarget.style.borderColor = 'var(--color-border-default)'
    e.currentTarget.style.boxShadow   = 'none'
}

function ManualUrlInput() {
    const [open, setOpen] = useState(false)
    const [url, setUrl]   = useState('')
    const [showSaved, setShowSaved] = useState(false)
    const queryClient = useQueryClient()

    const { data: savedUrls = [] } = useQuery({
        queryKey: ['savedUrls'],
        queryFn:  jobsApi.getSavedUrls,
        enabled:  open && showSaved,
    })

    const addMutation = useMutation({
        mutationFn: () => jobsApi.addManualUrl(url),
        onSuccess:  () => {
            setUrl('')
            queryClient.invalidateQueries({ queryKey: ['savedUrls']      })
            queryClient.invalidateQueries({ queryKey: ['manualVacancies'] })
        },
    })

    const refreshMutation = useMutation({
        mutationFn: (id: string) => jobsApi.refreshSavedUrl(id),
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['savedUrls'] }),
    })

    return (
        <>
            <button
                onClick={() => setOpen(true)}
                style={{
                    display:      'inline-flex',
                    alignItems:   'center',
                    justifyContent: 'center',
                    gap:          6,
                    height:       FIXED_HEIGHT,
                    padding:      '0 14px',
                    borderRadius: 'var(--radius-md)',
                    fontSize:     'var(--text-sm)',
                    cursor:       'pointer',
                    background:   'var(--color-bg-surface)',
                    color:        'var(--color-text-secondary)',
                    border:       '1px solid var(--color-border-default)',
                    fontFamily:   'inherit',
                    whiteSpace:   'nowrap',
                }}
            >
                <Icon name="plus" size={14} /> Додати вакансії з сайту
            </button>

            <Modal
                open={open}
                onClose={() => { setOpen(false); setShowSaved(false) }}
                title="Додати вакансії з сайту"
                width="md"
            >
                <p style={{
                    margin: '0 0 12px',
                    fontSize: 'var(--text-sm)',
                    color: 'var(--color-text-secondary)',
                }}>
                    Вставте посилання на сторінку з вакансіями. Наприклад,
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-xs)' }}>
                        {' '}https:
                    </span>
                </p>

                <div style={{ display: 'flex', gap: 8 }}>
                    <input
                        type="url"
                        value={url}
                        onChange={(e) => setUrl(e.target.value)}
                        onKeyDown={(e) => e.key === 'Enter' && url && addMutation.mutate()}
                        placeholder="https://…"
                        style={inputStyle}
                        onFocus={focusOn}
                        onBlur={focusOff}
                    />
                    <button
                        onClick={() => addMutation.mutate()}
                        disabled={addMutation.isPending || !url}
                        style={{
                            padding:      '10px 18px',
                            borderRadius: 'var(--radius-md)',
                            fontSize:     'var(--text-md)',
                            cursor:       (!url || addMutation.isPending) ? 'not-allowed' : 'pointer',
                            background:   'var(--color-primary-600)',
                            color:        '#fff',
                            border:       'none',
                            fontWeight:   'var(--font-weight-medium)' as unknown as number,
                            opacity:      !url ? 0.6 : 1,
                            fontFamily:   'inherit',
                            whiteSpace:   'nowrap',
                        }}
                    >
                        {addMutation.isPending ? 'Парсимо…' : 'Додати'}
                    </button>
                </div>

                {addMutation.isSuccess && (
                    <p style={{ color: 'var(--color-success-700)', fontSize: 'var(--text-sm)', margin: '12px 0 0' }}>
                        Знайдено {addMutation.data.jobsFound} вакансій.
                    </p>
                )}
                {addMutation.isError && (
                    <p style={{ color: 'var(--color-danger-600)', fontSize: 'var(--text-sm)', margin: '12px 0 0' }}>
                        Не вдалось спарсити сторінку. Перевірте посилання.
                    </p>
                )}

                <div style={{ marginTop: 18, borderTop: '0.5px solid var(--color-border-default)', paddingTop: 12 }}>
                    <button
                        onClick={() => setShowSaved((p) => !p)}
                        style={{
                            display:    'inline-flex',
                            alignItems: 'center',
                            gap:        6,
                            background: 'none',
                            border:     'none',
                            cursor:     'pointer',
                            fontSize:   'var(--text-sm)',
                            color:      'var(--color-text-secondary)',
                            padding:    0,
                            fontFamily: 'inherit',
                        }}
                    >
                        <Icon name={showSaved ? 'chevron-up' : 'chevron-down'} size={12} />
                        Збережені посилання
                    </button>

                    {showSaved && (
                        <div style={{ marginTop: 10, display: 'flex', flexDirection: 'column', gap: 8 }}>
                            {savedUrls.length === 0 && (
                                <p style={{ color: 'var(--color-text-tertiary)', fontSize: 'var(--text-sm)' }}>
                                    Немає збережених посилань
                                </p>
                            )}
                            {savedUrls.map((saved) => (
                                <div
                                    key={saved.id}
                                    style={{
                                        display:        'flex',
                                        alignItems:     'center',
                                        justifyContent: 'space-between',
                                        background:     'var(--color-bg-muted)',
                                        borderRadius:   'var(--radius-md)',
                                        padding:        '8px 12px',
                                        gap:            8,
                                    }}
                                >
                                    <div style={{ flex: 1, overflow: 'hidden' }}>
                                        <a
                                            href={saved.url}
                                            target="_blank"
                                            rel="noreferrer"
                                            style={{
                                                fontSize:       'var(--text-sm)',
                                                color:          'var(--color-primary-600)',
                                                textDecoration: 'none',
                                            }}
                                        >
                                            {saved.alias || saved.url}
                                        </a>
                                        <p style={{ margin: 0, fontSize: 'var(--text-xs)', color: 'var(--color-text-tertiary)' }}>
                                            {saved.lastParsedAt
                                                ? `Оновлено ${new Date(saved.lastParsedAt).toLocaleDateString('uk-UA')} · ${saved.lastParsedCount} вакансій`
                                                : 'Ще не парсилось'}
                                        </p>
                                    </div>
                                    <button
                                        onClick={() => refreshMutation.mutate(saved.id)}
                                        disabled={refreshMutation.isPending}
                                        style={{
                                            display:        'inline-flex',
                                            alignItems:     'center',
                                            gap:            4,
                                            padding:        '4px 10px',
                                            borderRadius:   'var(--radius-md)',
                                            fontSize:       'var(--text-xs)',
                                            cursor:         'pointer',
                                            background:     'var(--color-primary-50)',
                                            color:          'var(--color-primary-700)',
                                            border:         '1px solid var(--color-primary-100)',
                                            whiteSpace:     'nowrap',
                                            fontFamily:     'inherit',
                                        }}
                                    >
                                        {refreshMutation.isPending
                                            ? '…'
                                            : <><Icon name="refresh" size={12} /> Оновити</>}
                                    </button>
                                </div>
                            ))}
                        </div>
                    )}
                </div>
            </Modal>
        </>
    )
}

export default ManualUrlInput

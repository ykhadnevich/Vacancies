import { useEffect, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { trackerApi } from '../../api/trackerApi'
import type { TrackerEntry, CreateTrackerEntry, PipelineSteps } from '../../types/tracker'
import { ApplicationStatus } from '../../types/tracker'
import Card from '../../components/ui/Card'
import Icon from '../../components/ui/Icon'
import Badge from '../../components/ui/Badge'
import TrackerVerdictPill from '../../components/jobs/TrackerVerdictPill'
import EvidenceChips from '../../components/jobs/EvidenceChips'
import SubScoresBar from '../../components/jobs/SubScoresBar'
import BilingualReason from '../../components/jobs/BilingualReason'
import type { SubScores } from '../../types/jobV6'

const STATUS_LABEL: Record<ApplicationStatus, string> = {
    [ApplicationStatus.InReview]: 'В розгляді',
    [ApplicationStatus.Rejected]: 'Відмова',
    [ApplicationStatus.Offer]:    'Оффер',
    [ApplicationStatus.Archived]: 'Архів',
}

const STATUS_COLOR: Record<ApplicationStatus, { bg: string; text: string }> = {
    [ApplicationStatus.InReview]: { bg: 'var(--color-info-50)',    text: 'var(--color-info-700)'    },
    [ApplicationStatus.Rejected]: { bg: 'var(--color-danger-50)',  text: 'var(--color-danger-700)'  },
    [ApplicationStatus.Offer]:    { bg: 'var(--color-success-50)', text: 'var(--color-success-700)' },
    [ApplicationStatus.Archived]: { bg: 'var(--color-bg-muted)',   text: 'var(--color-text-secondary)' },
}

const PIPELINE: { key: keyof PipelineSteps; label: string; short: string }[] = [
    { key: 'cvSent',             label: 'CV надіслано',     short: 'CV'      },
    { key: 'responded',          label: 'Відгукнулись',     short: 'Відг.'   },
    { key: 'followUpSent',       label: 'Follow-up',         short: 'F/U'     },
    { key: 'shortInterview',     label: 'Коротке інтерв’ю', short: 'Коротке' },
    { key: 'testTask',           label: 'Тестове',           short: 'Тест'    },
    { key: 'technicalInterview', label: 'Технічне',          short: 'Тех.'    },
    { key: 'finalInterview',     label: 'Фінальне',          short: 'Фінал'   },
    { key: 'jobOffer',           label: 'Оффер',              short: 'Оффер'   },
]

type ViewMode = 'cards' | 'table'
const VIEW_STORAGE_KEY = 'vacancies_tracker_view'


const inputStyle: React.CSSProperties = {
    padding:      '8px 12px',
    borderRadius: 'var(--radius-md)',
    border:       '1px solid var(--color-border-default)',
    fontSize:     'var(--text-sm)',
    color:        'var(--color-text-primary)',
    background:   'var(--color-bg-surface)',
    fontFamily:   'inherit',
    outline:      'none',
}

const focusOn = (e: React.FocusEvent<HTMLInputElement | HTMLSelectElement>) => {
    e.currentTarget.style.borderColor = 'var(--color-primary-500)'
    e.currentTarget.style.boxShadow   = '0 0 0 3px var(--color-primary-100)'
}
const focusOff = (e: React.FocusEvent<HTMLInputElement | HTMLSelectElement>) => {
    e.currentTarget.style.borderColor = 'var(--color-border-default)'
    e.currentTarget.style.boxShadow   = 'none'
}


function AddEntryForm({ onAdd }: { onAdd: (entry: CreateTrackerEntry) => void }) {
    const [title,    setTitle]    = useState('')
    const [company,  setCompany]  = useState('')
    const [salary,   setSalary]   = useState('')
    const [url,      setUrl]      = useState('')

    const submit = () => {
        if (!title.trim() || !company.trim()) return
        onAdd({
            title:   title.trim(),
            company: company.trim(),
            salary:  salary || undefined,
            url:     url    || undefined,
        })
        setTitle('')
        setCompany('')
        setSalary('')
        setUrl('')
    }

    return (
        <Card padding="md">
            <h3 style={{
                margin:     '0 0 12px',
                fontSize:   'var(--text-md)',
                fontWeight: 'var(--font-weight-medium)' as unknown as number,
                color:      'var(--color-text-primary)',
            }}>
                Додати вакансію вручну
            </h3>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                <input placeholder="Посада *"      value={title}   onChange={(e) => setTitle(e.target.value)}
                    style={{ ...inputStyle, flex: '2 1 160px' }} onFocus={focusOn} onBlur={focusOff} />
                <input placeholder="Компанія *"    value={company} onChange={(e) => setCompany(e.target.value)}
                    style={{ ...inputStyle, flex: '2 1 160px' }} onFocus={focusOn} onBlur={focusOff} />
                <input placeholder="Зарплата"      value={salary}  onChange={(e) => setSalary(e.target.value)}
                    style={{ ...inputStyle, flex: '1 1 100px' }} onFocus={focusOn} onBlur={focusOff} />
                <input placeholder="URL вакансії"  value={url}     onChange={(e) => setUrl(e.target.value)}
                    style={{ ...inputStyle, flex: '3 1 200px' }} onFocus={focusOn} onBlur={focusOff} />
                <button
                    onClick={submit}
                    style={{
                        padding:      '8px 18px',
                        background:   'var(--color-primary-600)',
                        color:        '#fff',
                        border:       'none',
                        borderRadius: 'var(--radius-md)',
                        cursor:       'pointer',
                        fontSize:     'var(--text-sm)',
                        fontWeight:   'var(--font-weight-medium)' as unknown as number,
                        fontFamily:   'inherit',
                    }}
                >
                    Додати
                </button>
            </div>
        </Card>
    )
}


function ViewToggle({ value, onChange }: { value: ViewMode; onChange: (v: ViewMode) => void }) {
    const options: { value: ViewMode; label: string; icon: 'file-text' | 'briefcase' }[] = [
        { value: 'cards', label: 'Картки',  icon: 'file-text' },
        { value: 'table', label: 'Таблиця', icon: 'briefcase' },
    ]
    return (
        <div style={{
            display:      'inline-flex',
            background:   'var(--color-bg-muted)',
            padding:      2,
            borderRadius: 'var(--radius-md)',
            border:       '1px solid var(--color-border-default)',
        }}>
            {options.map((opt) => {
                const active = opt.value === value
                return (
                    <button
                        key={opt.value}
                        onClick={() => onChange(opt.value)}
                        style={{
                            display:      'inline-flex',
                            alignItems:   'center',
                            gap:          6,
                            padding:      '5px 12px',
                            background:   active ? 'var(--color-bg-surface)' : 'transparent',
                            color:        active ? 'var(--color-text-primary)' : 'var(--color-text-secondary)',
                            border:       'none',
                            borderRadius: 'var(--radius-sm)',
                            fontSize:     'var(--text-sm)',
                            fontWeight:   (active ? 'var(--font-weight-medium)' : 'var(--font-weight-regular)') as unknown as number,
                            cursor:       'pointer',
                            fontFamily:   'inherit',
                            boxShadow:    active ? 'var(--shadow-xs)' : 'none',
                            transition:   'all var(--transition-fast)',
                        }}
                    >
                        <Icon name={opt.icon} size={13} />
                        {opt.label}
                    </button>
                )
            })}
        </div>
    )
}


function CardRow({ entry }: { entry: TrackerEntry }) {
    const queryClient = useQueryClient()
    const [analysisOpen, setAnalysisOpen] = useState(false)

    const stepMutation = useMutation({
        mutationFn: ({ step, value }: { step: string; value: boolean }) =>
            trackerApi.updatePipelineStep(entry.id, step, value),
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })
    const statusMutation = useMutation({
        mutationFn: (status: number) => trackerApi.updateStatus(entry.id, status),
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })
    const deleteMutation = useMutation({
        mutationFn: () => trackerApi.delete(entry.id),
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })

    const status      = (entry.status ?? ApplicationStatus.InReview) as ApplicationStatus
    const statusClr   = STATUS_COLOR[status] ?? STATUS_COLOR[ApplicationStatus.InReview]
    const steps       = entry.pipelineSteps ?? {} as PipelineSteps
    const hasAnalysis = entry.score != null && !!entry.verdict

    return (
        <Card padding="md">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 12, gap: 12 }}>
                <div style={{ minWidth: 0, flex: 1 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                        <p style={{ margin: 0, fontWeight: 'var(--font-weight-semibold)' as unknown as number, fontSize: 'var(--text-md)' }}>
                            {entry.title}
                        </p>
                        <TrackerVerdictPill verdict={entry.verdict} score={entry.score} />
                    </div>
                    <p style={{ margin: '2px 0 0', color: 'var(--color-text-secondary)', fontSize: 'var(--text-sm)' }}>
                        {entry.company}
                        {' · '}
                        <span style={{ color: entry.location ? 'inherit' : 'var(--color-text-tertiary)' }}>
                            {entry.location || 'Локацію не вказано'}
                        </span>
                        {entry.salary && <span> · {entry.salary}</span>}
                        {entry.url && (
                            <>
                                {' · '}
                                <a href={entry.url} target="_blank" rel="noreferrer" style={{ color: 'var(--color-primary-600)', textDecoration: 'none' }}>
                                    посилання
                                </a>
                            </>
                        )}
                    </p>
                </div>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                    <select
                        value={status}
                        onChange={(e) => statusMutation.mutate(Number(e.target.value))}
                        style={{
                            ...inputStyle,
                            padding:    '4px 10px',
                            background: statusClr.bg,
                            color:      statusClr.text,
                            fontWeight: 'var(--font-weight-medium)' as unknown as number,
                            cursor:     'pointer',
                            border:     '1px solid transparent',
                        }}
                    >
                        {Object.entries(STATUS_LABEL).map(([val, lbl]) => (
                            <option key={val} value={val}>{lbl}</option>
                        ))}
                    </select>
                    <button
                        onClick={() => deleteMutation.mutate()}
                        aria-label="Видалити"
                        style={{
                            background: 'transparent',
                            border:     'none',
                            cursor:     'pointer',
                            color:      'var(--color-text-tertiary)',
                            padding:    4,
                            borderRadius: 'var(--radius-md)',
                            display:    'flex',
                            fontFamily: 'inherit',
                        }}
                    >
                        <Icon name="trash" size={16} />
                    </button>
                </div>
            </div>

            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                {PIPELINE.map(({ key, label }) => {
                    const checked = !!steps[key]
                    return (
                        <label key={key} style={{
                            display:      'flex',
                            alignItems:   'center',
                            gap:          5,
                            padding:      '3px 10px',
                            borderRadius: 'var(--radius-pill)',
                            fontSize:     'var(--text-xs)',
                            cursor:       'pointer',
                            background:   checked ? 'var(--color-success-50)' : 'var(--color-bg-muted)',
                            color:        checked ? 'var(--color-success-700)' : 'var(--color-text-secondary)',
                            border:       `1px solid ${checked ? 'var(--color-success-100)' : 'transparent'}`,
                        }}>
                            <input
                                type="checkbox"
                                checked={checked}
                                onChange={(e) => stepMutation.mutate({ step: key, value: e.target.checked })}
                                style={{ accentColor: 'var(--color-success-600)' }}
                            />
                            {label}
                        </label>
                    )
                })}
            </div>

            {hasAnalysis && (
                <>
                    <button
                        onClick={() => setAnalysisOpen((p) => !p)}
                        style={{
                            display:    'inline-flex',
                            alignItems: 'center',
                            gap:        4,
                            marginTop:  12,
                            background: 'transparent',
                            border:     'none',
                            cursor:     'pointer',
                            color:      'var(--color-text-secondary)',
                            fontSize:   'var(--text-sm)',
                            padding:    0,
                            fontFamily: 'inherit',
                        }}
                    >
                        <Icon name={analysisOpen ? 'chevron-up' : 'chevron-down'} size={13} />
                        Деталі аналізу
                    </button>
                    {analysisOpen && (
                        <div style={{ marginTop: 12, paddingTop: 12, borderTop: '0.5px solid var(--color-border-subtle)', display: 'flex', flexDirection: 'column', gap: 20 }}>
                            <BilingualReason
                                strengths={{      en: entry.strengthsEn      ?? null, uk: entry.strengthsUk      ?? null }}
                                gaps={{           en: entry.gapsEn           ?? null, uk: entry.gapsUk           ?? null }}
                                recommendation={{ en: entry.recommendationEn ?? null, uk: entry.recommendationUk ?? null }}
                                flat={{           en: null,                          uk: entry.reasonShort      ?? null }}
                            />

                            {entry.subScores && (
                                <div>
                                    <h4 style={{
                                        fontSize:      'var(--text-xs)',
                                        textTransform: 'uppercase',
                                        letterSpacing: '0.06em',
                                        color:         'var(--color-text-tertiary)',
                                        margin:        '0 0 10px',
                                        fontWeight:    'var(--font-weight-medium)' as unknown as number,
                                    }}>
                                        Деталізація оцінки
                                    </h4>
                                    <SubScoresBar subScores={entry.subScores as SubScores} />
                                </div>
                            )}

                            {(entry.matchedSkills?.length || entry.missingMustHaves?.length || entry.triggeredAntiFlags?.length) ? (
                                <div>
                                    <h4 style={{
                                        fontSize:      'var(--text-xs)',
                                        textTransform: 'uppercase',
                                        letterSpacing: '0.06em',
                                        color:         'var(--color-text-tertiary)',
                                        margin:        '0 0 10px',
                                        fontWeight:    'var(--font-weight-medium)' as unknown as number,
                                    }}>
                                        Збіги і прогалини
                                    </h4>
                                    <EvidenceChips
                                        matched={entry.matchedSkills      ?? []}
                                        missing={entry.missingMustHaves   ?? []}
                                        antiFlags={entry.triggeredAntiFlags ?? []}
                                    />
                                </div>
                            ) : null}

                            {(entry.analyzedAt || entry.cvFileName) && (
                                <p style={{
                                    margin:     0,
                                    fontSize:   'var(--text-xs)',
                                    color:      'var(--color-text-tertiary)',
                                    paddingTop: 8,
                                    borderTop:  '0.5px solid var(--color-border-subtle)',
                                }}>
                                    {entry.analyzedAt && (
                                        <>
                                            Проаналізовано {new Date(entry.analyzedAt).toLocaleDateString('uk-UA', { day: 'numeric', month: 'long', year: 'numeric' })}
                                        </>
                                    )}
                                    {entry.cvFileName && (
                                        <> · резюме <span style={{ color: 'var(--color-text-secondary)', fontFamily: 'var(--font-mono)' }}>{entry.cvFileName}</span></>
                                    )}
                                    {entry.pipelineVersion && (
                                        <> · {entry.pipelineVersion}</>
                                    )}
                                </p>
                            )}
                        </div>
                    )}
                </>
            )}
        </Card>
    )
}


function TableView({ entries }: { entries: TrackerEntry[] }) {
    const queryClient = useQueryClient()

    const stepMutation   = useMutation({
        mutationFn: ({ id, step, value }: { id: string; step: string; value: boolean }) =>
            trackerApi.updatePipelineStep(id, step, value),
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })
    const statusMutation = useMutation({
        mutationFn: ({ id, status }: { id: string; status: number }) =>
            trackerApi.updateStatus(id, status),
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })
    const deleteMutation = useMutation({
        mutationFn: (id: string) => trackerApi.delete(id),
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })

    const cellBase: React.CSSProperties = {
        padding:      '8px 10px',
        borderRight:  '1px solid var(--color-border-subtle)',
        borderBottom: '1px solid var(--color-border-subtle)',
        verticalAlign: 'middle',
        fontSize:     'var(--text-sm)',
        color:        'var(--color-text-primary)',
        background:   'var(--color-bg-surface)',
    }
    const headerCell: React.CSSProperties = {
        ...cellBase,
        background:   'var(--color-bg-muted)',
        fontWeight:   'var(--font-weight-medium)' as unknown as number,
        color:        'var(--color-text-secondary)',
        fontSize:     'var(--text-xs)',
        textTransform: 'uppercase',
        letterSpacing: '0.04em',
        position:     'sticky',
        top:          0,
        zIndex:       1,
        textAlign:    'left',
    }

    return (
        <Card padding="none" style={{ overflow: 'hidden' }}>
            <div style={{ overflowX: 'auto' }}>
                <table style={{
                    width:           '100%',
                    borderCollapse:  'separate',
                    borderSpacing:    0,
                    minWidth:        920,
                }}>
                    <thead>
                        <tr>
                            <th style={{ ...headerCell, minWidth: 180 }}>Посада</th>
                            <th style={{ ...headerCell, minWidth: 140 }}>Компанія</th>
                            <th style={{ ...headerCell, minWidth: 110 }}>Локація</th>
                            <th style={{ ...headerCell, minWidth: 110 }}>Оцінка</th>
                            <th style={{ ...headerCell, minWidth: 120 }}>Статус</th>
                            {PIPELINE.map((p) => (
                                <th key={p.key} style={{ ...headerCell, width: 64, textAlign: 'center' }} title={p.label}>
                                    {p.short}
                                </th>
                            ))}
                            <th style={{ ...headerCell, width: 40, borderRight: 'none' }}></th>
                        </tr>
                    </thead>
                    <tbody>
                        {entries.map((entry) => {
                            const status    = (entry.status ?? ApplicationStatus.InReview) as ApplicationStatus
                            const statusClr = STATUS_COLOR[status] ?? STATUS_COLOR[ApplicationStatus.InReview]
                            const steps     = entry.pipelineSteps ?? {} as PipelineSteps
                            return (
                                <tr key={entry.id}>
                                    <td style={cellBase}>
                                        {entry.url
                                            ? (
                                                <a
                                                    href={entry.url}
                                                    target="_blank"
                                                    rel="noreferrer"
                                                    style={{ color: 'var(--color-primary-600)', textDecoration: 'none', fontWeight: 'var(--font-weight-medium)' as unknown as number }}
                                                >
                                                    {entry.title}
                                                </a>
                                            )
                                            : <span style={{ fontWeight: 'var(--font-weight-medium)' as unknown as number }}>{entry.title}</span>}
                                    </td>
                                    <td style={{ ...cellBase, color: 'var(--color-text-secondary)' }}>
                                        {entry.company}
                                    </td>
                                    <td style={{ ...cellBase, color: entry.location ? 'var(--color-text-secondary)' : 'var(--color-text-tertiary)' }}>
                                        {entry.location || '—'}
                                    </td>
                                    <td style={cellBase}>
                                        {entry.score != null && entry.verdict
                                            ? <TrackerVerdictPill verdict={entry.verdict} score={entry.score} size="sm" />
                                            : <Badge color="neutral" size="sm">—</Badge>}
                                    </td>
                                    <td style={cellBase}>
                                        <select
                                            value={status}
                                            onChange={(e) => statusMutation.mutate({ id: entry.id, status: Number(e.target.value) })}
                                            style={{
                                                padding:      '3px 8px',
                                                background:   statusClr.bg,
                                                color:        statusClr.text,
                                                border:       'none',
                                                borderRadius: 'var(--radius-sm)',
                                                fontSize:     'var(--text-xs)',
                                                fontWeight:   'var(--font-weight-medium)' as unknown as number,
                                                cursor:       'pointer',
                                                fontFamily:   'inherit',
                                            }}
                                        >
                                            {Object.entries(STATUS_LABEL).map(([val, lbl]) => (
                                                <option key={val} value={val}>{lbl}</option>
                                            ))}
                                        </select>
                                    </td>
                                    {PIPELINE.map((p) => {
                                        const checked = !!steps[p.key]
                                        return (
                                            <td key={p.key} style={{ ...cellBase, textAlign: 'center', cursor: 'pointer' }}
                                                onClick={() => stepMutation.mutate({ id: entry.id, step: p.key, value: !checked })}
                                            >
                                                <input
                                                    type="checkbox"
                                                    checked={checked}
                                                    onChange={() => {  }}
                                                    style={{
                                                        accentColor: 'var(--color-success-600)',
                                                        cursor: 'pointer',
                                                    }}
                                                />
                                            </td>
                                        )
                                    })}
                                    <td style={{ ...cellBase, borderRight: 'none', textAlign: 'center' }}>
                                        <button
                                            onClick={() => deleteMutation.mutate(entry.id)}
                                            aria-label="Видалити"
                                            style={{
                                                background: 'transparent',
                                                border:     'none',
                                                cursor:     'pointer',
                                                color:      'var(--color-text-tertiary)',
                                                padding:    4,
                                                borderRadius: 'var(--radius-md)',
                                                display:    'inline-flex',
                                                fontFamily: 'inherit',
                                            }}
                                        >
                                            <Icon name="trash" size={14} />
                                        </button>
                                    </td>
                                </tr>
                            )
                        })}
                    </tbody>
                </table>
            </div>
        </Card>
    )
}


function TrackerPage() {
    const queryClient = useQueryClient()

    const [viewMode, setViewMode] = useState<ViewMode>(() => {
        const saved = localStorage.getItem(VIEW_STORAGE_KEY)
        return saved === 'table' ? 'table' : 'cards'
    })
    useEffect(() => {
        localStorage.setItem(VIEW_STORAGE_KEY, viewMode)
    }, [viewMode])

    const { data: entries = [], isLoading } = useQuery({
        queryKey:        ['tracker'],
        queryFn:         trackerApi.getAll,
        refetchOnMount:  true,
        staleTime:       0,
    })

    const addMutation = useMutation({
        mutationFn: trackerApi.add,
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })

    if (isLoading) {
        return (
            <div style={{ padding: 32, color: 'var(--color-text-tertiary)' }}>
                Завантаження…
            </div>
        )
    }

    return (
        <div style={{ width: '100%', maxWidth: 'var(--max-width-content)', margin: '0 auto', padding: '24px 16px', display: 'flex', flexDirection: 'column', gap: 16 }}>

            {}
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
                <div>
                    <h1 style={{ fontSize: 'var(--text-2xl)', margin: 0 }}>Трекер заявок</h1>
                    {entries.length > 0 && (
                        <p style={{ margin: '4px 0 0', color: 'var(--color-text-secondary)', fontSize: 'var(--text-sm)' }}>
                            {entries.length} {entries.length === 1 ? 'заявка' : 'заявок'}
                        </p>
                    )}
                </div>
                <ViewToggle value={viewMode} onChange={setViewMode} />
            </div>

            <AddEntryForm onAdd={(entry) => addMutation.mutate(entry)} />

            {entries.length === 0 && (
                <div style={{ textAlign: 'center', padding: '48px 16px', color: 'var(--color-text-tertiary)' }}>
                    <p style={{ fontSize: 'var(--text-md)' }}>
                        Ще немає заявок. Додайте першу вручну або зі стрічки вакансій.
                    </p>
                </div>
            )}

            {entries.length > 0 && viewMode === 'cards' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                    {entries.map((entry) => <CardRow key={entry.id} entry={entry} />)}
                </div>
            )}

            {entries.length > 0 && viewMode === 'table' && (
                <TableView entries={entries} />
            )}

        </div>
    )
}

export default TrackerPage

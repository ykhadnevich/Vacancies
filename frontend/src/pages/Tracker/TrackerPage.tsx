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
import { useIsMobile } from '../../hooks/useViewport'
import { wideWrap, eyebrow, mono } from '../../components/layout/_layout'
import { useT } from '../../i18n/useT'
import { useLanguage } from '../../i18n/LanguageContext'
import { usePlural } from '../../i18n/usePlural'

function normalizeStatus(s: ApplicationStatus | string | null | undefined): ApplicationStatus {
    if (typeof s === 'number') return s
    if (typeof s === 'string') {
        const v = ApplicationStatus[s as keyof typeof ApplicationStatus]
        if (typeof v === 'number') return v
    }
    return ApplicationStatus.InReview
}

type TFn = ReturnType<typeof useT>

function buildStatusLabel(t: TFn): Record<ApplicationStatus, string> {
    return {
        [ApplicationStatus.InReview]: t('tracker.status.inReview'),
        [ApplicationStatus.Rejected]: t('tracker.status.rejected'),
        [ApplicationStatus.Offer]:    t('tracker.status.offer'),
        [ApplicationStatus.Archived]: t('tracker.status.archived'),
    }
}

const STATUS_COLOR: Record<ApplicationStatus, { bg: string; text: string }> = {
    [ApplicationStatus.InReview]: { bg: 'var(--color-info-50)',    text: 'var(--color-info-700)'    },
    [ApplicationStatus.Rejected]: { bg: 'var(--color-danger-50)',  text: 'var(--color-danger-700)'  },
    [ApplicationStatus.Offer]:    { bg: 'var(--color-success-50)', text: 'var(--color-success-700)' },
    [ApplicationStatus.Archived]: { bg: 'var(--color-bg-muted)',   text: 'var(--color-text-secondary)' },
}

const STATUS_ACCENT: Record<ApplicationStatus, string> = {
    [ApplicationStatus.InReview]: 'var(--color-info-500)',
    [ApplicationStatus.Rejected]: 'var(--color-danger-500)',
    [ApplicationStatus.Offer]:    'var(--color-success-500)',
    [ApplicationStatus.Archived]: 'var(--color-border-strong)',
}

const BOARD_ORDER: ApplicationStatus[] = [
    ApplicationStatus.InReview,
    ApplicationStatus.Offer,
    ApplicationStatus.Rejected,
    ApplicationStatus.Archived,
]

type PipelineEntry = { key: keyof PipelineSteps; label: string; short: string }

function buildPipeline(t: TFn): PipelineEntry[] {
    return [
        { key: 'cvSent',             label: t('tracker.pipeline.cvSent.label'),             short: t('tracker.pipeline.cvSent.short')             },
        { key: 'responded',          label: t('tracker.pipeline.replied.label'),            short: t('tracker.pipeline.replied.short')            },
        { key: 'followUpSent',       label: t('tracker.pipeline.followUp.label'),           short: t('tracker.pipeline.followUp.short')           },
        { key: 'shortInterview',     label: t('tracker.pipeline.shortInterview.label'),     short: t('tracker.pipeline.shortInterview.short')     },
        { key: 'testTask',           label: t('tracker.pipeline.testTask.label'),           short: t('tracker.pipeline.testTask.short')           },
        { key: 'technicalInterview', label: t('tracker.pipeline.technicalInterview.label'), short: t('tracker.pipeline.technicalInterview.short') },
        { key: 'finalInterview',     label: t('tracker.pipeline.finalInterview.label'),     short: t('tracker.pipeline.finalInterview.short')     },
        { key: 'jobOffer',           label: t('tracker.pipeline.offer.label'),              short: t('tracker.pipeline.offer.short')              },
    ]
}

type ViewMode = 'board' | 'cards' | 'table'
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
    const t = useT()
    const [title,    setTitle]    = useState('')
    const [company,  setCompany]  = useState('')
    const [salary,   setSalary]   = useState('')
    const [url,      setUrl]      = useState('')

    const canSubmit = title.trim().length > 0 && company.trim().length > 0

    const submit = () => {
        if (!canSubmit) return
        onAdd({
            title:   title.trim(),
            company: company.trim(),
            salary:  salary || undefined,
            url:     url    || undefined,
        })
        setTitle(''); setCompany(''); setSalary(''); setUrl('')
    }

    const onEnter = (e: React.KeyboardEvent<HTMLInputElement>) => { if (e.key === 'Enter') submit() }

    return (
        <Card padding="md">
            <h3 style={{ margin: '0 0 12px', fontSize: 'var(--text-md)', fontWeight: 'var(--font-weight-medium)' as unknown as number, color: 'var(--color-text-primary)' }}>
                {t('tracker.addManual.title')}
            </h3>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                <input placeholder={t('tracker.form.title')}   value={title}   onChange={(e) => setTitle(e.target.value)}   onKeyDown={onEnter} style={{ ...inputStyle, flex: '2 1 160px' }} onFocus={focusOn} onBlur={focusOff} />
                <input placeholder={t('tracker.form.company')} value={company} onChange={(e) => setCompany(e.target.value)} onKeyDown={onEnter} style={{ ...inputStyle, flex: '2 1 160px' }} onFocus={focusOn} onBlur={focusOff} />
                <input placeholder={t('tracker.form.salary')}  value={salary}  onChange={(e) => setSalary(e.target.value)}  onKeyDown={onEnter} style={{ ...inputStyle, flex: '1 1 100px' }} onFocus={focusOn} onBlur={focusOff} />
                <input placeholder={t('tracker.form.url')}     value={url}     onChange={(e) => setUrl(e.target.value)}     onKeyDown={onEnter} style={{ ...inputStyle, flex: '3 1 200px' }} onFocus={focusOn} onBlur={focusOff} />
                <button onClick={submit} disabled={!canSubmit} style={{ padding: '8px 18px', background: 'var(--color-primary-600)', color: '#fff', border: 'none', borderRadius: 'var(--radius-md)', cursor: canSubmit ? 'pointer' : 'not-allowed', fontSize: 'var(--text-sm)', fontWeight: 'var(--font-weight-medium)' as unknown as number, fontFamily: 'inherit', opacity: canSubmit ? 1 : 0.5 }}>
                    {t('common.add')}
                </button>
            </div>
        </Card>
    )
}

function ViewToggle({ value, onChange }: { value: ViewMode; onChange: (v: ViewMode) => void }) {
    const t = useT()
    const options: { value: ViewMode; label: string; icon: 'briefcase' | 'file-text' }[] = [
        { value: 'board', label: t('tracker.viewBoard'), icon: 'briefcase' },
        { value: 'cards', label: t('tracker.viewCards'), icon: 'file-text' },
        { value: 'table', label: t('tracker.viewTable'), icon: 'file-text' },
    ]
    return (
        <div style={{ display: 'inline-flex', background: 'var(--color-bg-muted)', padding: 2, borderRadius: 'var(--radius-md)', border: '1px solid var(--color-border-default)' }}>
            {options.map((opt) => {
                const active = opt.value === value
                return (
                    <button key={opt.value} onClick={() => onChange(opt.value)}
                        style={{ display: 'inline-flex', alignItems: 'center', gap: 6, padding: '5px 12px',
                            background: active ? 'var(--color-bg-surface)' : 'transparent',
                            color: active ? 'var(--color-text-primary)' : 'var(--color-text-secondary)',
                            border: 'none', borderRadius: 'var(--radius-sm)', fontSize: 'var(--text-sm)',
                            fontWeight: (active ? 'var(--font-weight-medium)' : 'var(--font-weight-regular)') as unknown as number,
                            cursor: 'pointer', fontFamily: 'inherit', boxShadow: active ? 'var(--shadow-xs)' : 'none',
                            transition: 'all var(--transition-fast)' }}>
                        <Icon name={opt.icon} size={13} />
                        {opt.label}
                    </button>
                )
            })}
        </div>
    )
}

function BoardCard({ entry }: { entry: TrackerEntry }) {
    const t = useT()
    const STATUS_LABEL = buildStatusLabel(t)
    const PIPELINE = buildPipeline(t)
    const queryClient = useQueryClient()
    const statusMutation = useMutation({
        mutationFn: (status: number) => trackerApi.updateStatus(entry.id, status),
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })
    const deleteMutation = useMutation({
        mutationFn: () => trackerApi.delete(entry.id),
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })

    const status = normalizeStatus(entry.status)
    const steps  = entry.pipelineSteps ?? {} as PipelineSteps
    const done   = PIPELINE.filter((p) => steps[p.key]).length

    return (
        <Card padding="md" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
                <div style={{ minWidth: 0, flex: 1 }}>
                    {entry.url
                        ? <a href={entry.url} target="_blank" rel="noreferrer" title={entry.title}
                              style={{ fontWeight: 600, fontSize: 'var(--text-lg)', lineHeight: 1.25, color: 'var(--color-text-primary)', textDecoration: 'none', display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>{entry.title}</a>
                        : <span title={entry.title}
                              style={{ fontWeight: 600, fontSize: 'var(--text-lg)', lineHeight: 1.25, color: 'var(--color-text-primary)', display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>{entry.title}</span>}
                    <p title={entry.company} style={{ margin: '4px 0 0', color: 'var(--color-text-secondary)', fontSize: 'var(--text-sm)', lineHeight: 1.4, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {entry.company}{entry.salary ? <span style={{ color: 'var(--color-text-tertiary)' }}> · {entry.salary}</span> : null}
                    </p>
                </div>
                <button onClick={() => deleteMutation.mutate()} aria-label={t('common.delete')}
                    style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--color-text-tertiary)', padding: 4, display: 'flex', fontFamily: 'inherit', flexShrink: 0 }}>
                    <Icon name="trash" size={15} />
                </button>
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
                {entry.score != null && entry.verdict
                    ? <TrackerVerdictPill verdict={entry.verdict} score={entry.score} size="sm" />
                    : <Badge color="neutral" size="sm">—</Badge>}
                <div
                    style={{ display: 'flex', gap: 4, alignItems: 'center' }}
                    title={`${t('tracker.analysisDetails')}: ${done}/${PIPELINE.length}`}
                    aria-label={`Pipeline ${done} of ${PIPELINE.length}`}
                >
                    {PIPELINE.map((p) => (
                        <span key={p.key} style={{
                            width: 9, height: 9, borderRadius: '50%',
                            background: steps[p.key] ? 'var(--color-success-500)' : 'var(--color-border-default)',
                            transition: 'background var(--transition-fast)',
                        }} />
                    ))}
                </div>
            </div>

            <select value={status} onChange={(e) => statusMutation.mutate(Number(e.target.value))}
                style={{ ...inputStyle, padding: '5px 8px', fontSize: 'var(--text-xs)', cursor: 'pointer',
                    background: STATUS_COLOR[status].bg, color: STATUS_COLOR[status].text, border: '1px solid transparent', fontWeight: 600 }}>
                {Object.entries(STATUS_LABEL).map(([val, lbl]) => (<option key={val} value={val}>{lbl}</option>))}
            </select>
        </Card>
    )
}

function BoardView({ entries }: { entries: TrackerEntry[] }) {
    const t = useT()
    const STATUS_LABEL = buildStatusLabel(t)
    const narrow = useIsMobile(900)
    return (
        <div style={{
            display: 'grid',
            gridTemplateColumns: narrow ? '1fr' : `repeat(${BOARD_ORDER.length}, minmax(300px, 1fr))`,
            gap: 14, alignItems: 'start', overflowX: narrow ? undefined : 'auto',
        }}>
            {BOARD_ORDER.map((status) => {
                const column = entries.filter((e) => normalizeStatus(e.status) === status)
                return (
                    <div key={status} style={{ display: 'flex', flexDirection: 'column', gap: 10, minWidth: 0 }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '0 2px 8px', borderBottom: `2px solid ${STATUS_ACCENT[status]}` }}>
                            <span style={{ fontFamily: 'var(--font-sans)', fontSize: 'var(--text-sm)', fontWeight: 600, color: 'var(--color-text-primary)' }}>
                                {STATUS_LABEL[status]}
                            </span>
                            <span style={{ ...mono, fontSize: 'var(--text-xs)', color: 'var(--color-text-tertiary)' }}>{column.length}</span>
                        </div>
                        {column.length === 0 ? (
                            <div style={{ padding: '20px 12px', textAlign: 'center', color: 'var(--color-text-tertiary)', fontSize: 'var(--text-sm)', border: '1px dashed var(--color-border-default)', borderRadius: 'var(--radius-lg)' }}>
                                —
                            </div>
                        ) : column.map((entry) => <BoardCard key={entry.id} entry={entry} />)}
                    </div>
                )
            })}
        </div>
    )
}

function CardRow({ entry }: { entry: TrackerEntry }) {
    const t = useT()
    const { language } = useLanguage()
    const STATUS_LABEL = buildStatusLabel(t)
    const PIPELINE = buildPipeline(t)
    const queryClient = useQueryClient()
    const [analysisOpen, setAnalysisOpen] = useState(false)

    const stepMutation = useMutation({
        mutationFn: ({ step, value }: { step: string; value: boolean }) => trackerApi.updatePipelineStep(entry.id, step, value),
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

    const status      = normalizeStatus(entry.status)
    const statusClr   = STATUS_COLOR[status] ?? STATUS_COLOR[ApplicationStatus.InReview]
    const steps       = entry.pipelineSteps ?? {} as PipelineSteps
    const hasAnalysis = entry.score != null && !!entry.verdict

    return (
        <Card padding="md">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 12, gap: 12 }}>
                <div style={{ minWidth: 0, flex: 1 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
                        <p title={entry.title}
                            style={{ margin: 0, fontWeight: 'var(--font-weight-semibold)' as unknown as number, fontSize: 'var(--text-lg)', lineHeight: 1.25, display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden', minWidth: 0 }}>{entry.title}</p>
                        <TrackerVerdictPill verdict={entry.verdict} score={entry.score} />
                    </div>
                    <p style={{ margin: '2px 0 0', color: 'var(--color-text-secondary)', fontSize: 'var(--text-sm)' }}>
                        {entry.company}{' · '}
                        <span style={{ color: entry.location ? 'inherit' : 'var(--color-text-tertiary)' }}>{entry.location || t('tracker.locationUnknown')}</span>
                        {entry.salary && <span> · {entry.salary}</span>}
                        {entry.url && (<>{' · '}<a href={entry.url} target="_blank" rel="noreferrer" style={{ color: 'var(--color-primary-600)', textDecoration: 'none' }}>{t('common.link')}</a></>)}
                    </p>
                </div>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                    <select value={status} onChange={(e) => statusMutation.mutate(Number(e.target.value))}
                        style={{ ...inputStyle, padding: '4px 10px', background: statusClr.bg, color: statusClr.text, fontWeight: 'var(--font-weight-medium)' as unknown as number, cursor: 'pointer', border: '1px solid transparent' }}>
                        {Object.entries(STATUS_LABEL).map(([val, lbl]) => (<option key={val} value={val}>{lbl}</option>))}
                    </select>
                    <button onClick={() => deleteMutation.mutate()} aria-label={t('common.delete')}
                        style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--color-text-tertiary)', padding: 4, borderRadius: 'var(--radius-md)', display: 'flex', fontFamily: 'inherit' }}>
                        <Icon name="trash" size={16} />
                    </button>
                </div>
            </div>

            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                {PIPELINE.map(({ key, label }) => {
                    const checked = !!steps[key]
                    return (
                        <button
                            key={key}
                            type="button"
                            onClick={() => stepMutation.mutate({ step: key, value: !checked })}
                            style={{
                                display: 'inline-flex', alignItems: 'center', gap: 6,
                                padding: '4px 10px 4px 8px', borderRadius: 'var(--radius-pill)',
                                fontSize: 'var(--text-xs)', cursor: 'pointer', fontFamily: 'inherit',
                                background: checked ? 'var(--color-success-50)' : 'var(--color-bg-muted)',
                                color: checked ? 'var(--color-success-700)' : 'var(--color-text-secondary)',
                                border: `1px solid ${checked ? 'var(--color-success-100)' : 'transparent'}`,
                                fontWeight: (checked ? 'var(--font-weight-medium)' : 'var(--font-weight-regular)') as unknown as number,
                                transition: 'background var(--transition-fast)',
                            }}
                        >
                            <Icon name={checked ? 'check-circle' : 'circle'} size={12} />
                            {label}
                        </button>
                    )
                })}
            </div>

            {hasAnalysis && (
                <>
                    <button onClick={() => setAnalysisOpen((p) => !p)}
                        style={{ display: 'inline-flex', alignItems: 'center', gap: 4, marginTop: 12, background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--color-text-secondary)', fontSize: 'var(--text-sm)', padding: 0, fontFamily: 'inherit' }}>
                        <Icon name={analysisOpen ? 'chevron-up' : 'chevron-down'} size={13} />
                        {t('tracker.analysisDetails')}
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
                                    <h4 style={analysisHead}>{t('reason.scoreBreakdown')}</h4>
                                    <SubScoresBar subScores={entry.subScores as SubScores} />
                                </div>
                            )}
                            {(entry.matchedSkills?.length || entry.missingMustHaves?.length || entry.triggeredAntiFlags?.length) ? (
                                <div>
                                    <h4 style={analysisHead}>{t('reason.evidence')}</h4>
                                    <EvidenceChips matched={entry.matchedSkills ?? []} missing={entry.missingMustHaves ?? []} antiFlags={entry.triggeredAntiFlags ?? []} />
                                </div>
                            ) : null}
                            {(entry.analyzedAt || entry.cvFileName) && (
                                <p style={{ margin: 0, fontSize: 'var(--text-xs)', color: 'var(--color-text-tertiary)', paddingTop: 8, borderTop: '0.5px solid var(--color-border-subtle)' }}>
                                    {entry.analyzedAt && (
                                        <>
                                            {t('tracker.analyzedOn').replace(
                                                '{date}',
                                                new Date(entry.analyzedAt).toLocaleDateString(
                                                    language === 'uk' ? 'uk-UA' : 'en-GB',
                                                    { day: 'numeric', month: 'long', year: 'numeric' },
                                                ),
                                            )}
                                        </>
                                    )}
                                    {entry.cvFileName && (<> · {t('tracker.cvLabel')} <span style={{ color: 'var(--color-text-secondary)', fontFamily: 'var(--font-mono)' }}>{entry.cvFileName}</span></>)}
                                    {entry.pipelineVersion && (<> · {entry.pipelineVersion}</>)}
                                </p>
                            )}
                        </div>
                    )}
                </>
            )}
        </Card>
    )
}

const analysisHead: React.CSSProperties = {
    fontSize: 'var(--text-xs)', textTransform: 'uppercase', letterSpacing: '0.06em',
    color: 'var(--color-text-tertiary)', margin: '0 0 10px', fontWeight: 'var(--font-weight-medium)' as unknown as number,
}

function TableView({ entries }: { entries: TrackerEntry[] }) {
    const t = useT()
    const STATUS_LABEL = buildStatusLabel(t)
    const PIPELINE = buildPipeline(t)
    const queryClient = useQueryClient()

    const stepMutation   = useMutation({
        mutationFn: ({ id, step, value }: { id: string; step: string; value: boolean }) => trackerApi.updatePipelineStep(id, step, value),
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })
    const statusMutation = useMutation({
        mutationFn: ({ id, status }: { id: string; status: number }) => trackerApi.updateStatus(id, status),
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })
    const deleteMutation = useMutation({
        mutationFn: (id: string) => trackerApi.delete(id),
        onSuccess:  () => queryClient.invalidateQueries({ queryKey: ['tracker'] }),
    })

    const cellBase: React.CSSProperties = {
        padding: '12px 14px', borderRight: '1px solid var(--color-border-subtle)', borderBottom: '1px solid var(--color-border-subtle)',
        verticalAlign: 'middle', fontSize: 'var(--text-md)', color: 'var(--color-text-primary)', background: 'var(--color-bg-surface)',
    }
    const headerCell: React.CSSProperties = {
        ...cellBase, background: 'var(--color-bg-muted)', fontWeight: 'var(--font-weight-medium)' as unknown as number,
        color: 'var(--color-text-secondary)', fontSize: 'var(--text-sm)', textTransform: 'uppercase', letterSpacing: '0.04em',
        position: 'sticky', top: 0, zIndex: 1, textAlign: 'left',
    }

    return (
        <Card padding="none" style={{ overflow: 'hidden' }}>
            <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'separate', borderSpacing: 0, minWidth: 920 }}>
                    <thead>
                        <tr>
                            <th style={{ ...headerCell, minWidth: 180 }}>{t('tracker.table.position')}</th>
                            <th style={{ ...headerCell, minWidth: 140 }}>{t('tracker.table.company')}</th>
                            <th style={{ ...headerCell, minWidth: 110 }}>{t('tracker.table.location')}</th>
                            <th style={{ ...headerCell, minWidth: 110 }}>{t('tracker.table.score')}</th>
                            <th style={{ ...headerCell, minWidth: 120 }}>{t('common.status')}</th>
                            {PIPELINE.map((p) => (<th key={p.key} style={{ ...headerCell, width: 64, textAlign: 'center' }} title={p.label}>{p.short}</th>))}
                            <th style={{ ...headerCell, width: 40, borderRight: 'none' }}></th>
                        </tr>
                    </thead>
                    <tbody>
                        {entries.map((entry) => {
                            const status    = normalizeStatus(entry.status)
                            const statusClr = STATUS_COLOR[status] ?? STATUS_COLOR[ApplicationStatus.InReview]
                            const steps     = entry.pipelineSteps ?? {} as PipelineSteps
                            return (
                                <tr key={entry.id}>
                                    <td style={cellBase}>
                                        {entry.url
                                            ? <a href={entry.url} target="_blank" rel="noreferrer" style={{ color: 'var(--color-primary-600)', textDecoration: 'none', fontWeight: 'var(--font-weight-medium)' as unknown as number }}>{entry.title}</a>
                                            : <span style={{ fontWeight: 'var(--font-weight-medium)' as unknown as number }}>{entry.title}</span>}
                                    </td>
                                    <td style={{ ...cellBase, color: 'var(--color-text-secondary)' }}>{entry.company}</td>
                                    <td style={{ ...cellBase, color: entry.location ? 'var(--color-text-secondary)' : 'var(--color-text-tertiary)' }}>{entry.location || '—'}</td>
                                    <td style={cellBase}>
                                        {entry.score != null && entry.verdict ? (() => {
                                            const vc = (() => {
                                                const v = (entry.verdict ?? '').toLowerCase()
                                                if (v.startsWith('strong'))  return 'success'
                                                if (v.startsWith('partial')) return 'info'
                                                if (v.startsWith('weak'))    return 'warning'
                                                return 'danger'
                                            })()
                                            return (
                                                <span style={{ fontFamily: 'var(--font-mono)', fontVariantNumeric: 'tabular-nums',
                                                    fontWeight: 'var(--font-weight-medium)' as unknown as number,
                                                    color: `var(--color-${vc}-700)` }}>
                                                    {(entry.score * 100).toFixed(1)}%
                                                </span>
                                            )
                                        })() : <span style={{ color: 'var(--color-text-tertiary)' }}>—</span>}
                                    </td>
                                    <td style={cellBase}>
                                        <select value={status} onChange={(e) => statusMutation.mutate({ id: entry.id, status: Number(e.target.value) })}
                                            style={{ padding: '4px 10px', background: statusClr.bg, color: statusClr.text, border: 'none', borderRadius: 'var(--radius-sm)', fontSize: 'var(--text-sm)', fontWeight: 'var(--font-weight-medium)' as unknown as number, cursor: 'pointer', fontFamily: 'inherit' }}>
                                            {Object.entries(STATUS_LABEL).map(([val, lbl]) => (<option key={val} value={val}>{lbl}</option>))}
                                        </select>
                                    </td>
                                    {PIPELINE.map((p) => {
                                        const checked = !!steps[p.key]
                                        return (
                                            <td key={p.key} style={{ ...cellBase, textAlign: 'center', cursor: 'pointer', padding: 0 }} onClick={() => stepMutation.mutate({ id: entry.id, step: p.key, value: !checked })}>
                                                <span aria-label={checked ? `${p.label}: done` : `${p.label}: pending`}
                                                    style={{
                                                        display: 'inline-block',
                                                        width: 20, height: 20,
                                                        borderRadius: 'var(--radius-xs)',
                                                        background: checked ? 'var(--color-success-500)' : 'transparent',
                                                        border: checked
                                                            ? '1px solid var(--color-success-600)'
                                                            : '1px dashed var(--color-border-default)',
                                                        transition: 'background var(--transition-fast)',
                                                    }} />
                                            </td>
                                        )
                                    })}
                                    <td style={{ ...cellBase, borderRight: 'none', textAlign: 'center' }}>
                                        <button onClick={() => deleteMutation.mutate(entry.id)} aria-label={t('common.delete')}
                                            style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--color-text-tertiary)', padding: 4, borderRadius: 'var(--radius-md)', display: 'inline-flex', fontFamily: 'inherit' }}>
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
    const t = useT()
    const tp = usePlural()
    const queryClient = useQueryClient()
    const isMobile = useIsMobile(640)

    const [viewMode, setViewMode] = useState<ViewMode>(() => {
        const saved = localStorage.getItem(VIEW_STORAGE_KEY)
        if (saved === 'table' || saved === 'cards' || saved === 'board') return saved
        return 'board'
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
        return <div style={{ padding: 32, color: 'var(--color-text-tertiary)' }}>{t('common.loading')}</div>
    }

    const effectiveView: ViewMode = isMobile && viewMode === 'board' ? 'cards' : viewMode

    return (
        <div style={wideWrap}>
            <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap', marginBottom: 20 }}>
                <div>
                    <div style={{ ...eyebrow, marginBottom: 6 }}>
                        {tp('tracker.entries', entries.length)}
                    </div>
                    <h1 style={{ fontFamily: 'var(--font-serif)', fontSize: 'var(--display-sm)', fontWeight: 600, letterSpacing: '-0.02em', margin: 0 }}>
                        {t('tracker.title')}
                    </h1>
                </div>
                <ViewToggle value={viewMode} onChange={setViewMode} />
            </div>

            <div style={{ marginBottom: 20 }}>
                <AddEntryForm onAdd={(entry) => addMutation.mutate(entry)} />
            </div>

            {entries.length === 0 ? (
                <div style={{ textAlign: 'center', padding: '48px 16px', color: 'var(--color-text-tertiary)' }}>
                    <p style={{ fontSize: 'var(--text-md)' }}>{t('tracker.empty')}</p>
                </div>
            ) : effectiveView === 'board' ? (
                <BoardView entries={entries} />
            ) : effectiveView === 'cards' ? (
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(420px, 1fr))', gap: 12, alignItems: 'start' }}>
                    {entries.map((entry) => <CardRow key={entry.id} entry={entry} />)}
                </div>
            ) : (
                <TableView entries={entries} />
            )}
        </div>
    )
}

export default TrackerPage

import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { recruiterApi } from '../../api/recruiterApi'
import { useT } from '../../i18n/useT'
import { useLanguage } from '../../i18n/LanguageContext'
import { useIsMobile } from '../../hooks/useViewport'
import Button from '../../components/ui/Button'
import Card from '../../components/ui/Card'
import Icon from '../../components/ui/Icon'
import Badge from '../../components/ui/Badge'
import EmptyState from '../../components/ui/EmptyState'
import { WideShell, SidebarCard, SectionHead, TableScroll } from '../../components/layout/Shell'
import { tableShell, th, td, tdMono, mono, eyebrow } from '../../components/layout/_layout'
import { textareaStyle } from './_styles'
import { FieldRow, BareInput } from './_fields'

type FormMode = 'manual' | 'url'

function VacancyManualForm({ onCreated }: { onCreated: (id: string) => void }) {
    const t = useT()
    const queryClient = useQueryClient()
    const [title, setTitle] = useState('')
    const [company, setCompany] = useState('')
    const [location, setLocation] = useState('')
    const [description, setDescription] = useState('')
    const [error, setError] = useState<string | null>(null)

    const mut = useMutation({
        mutationFn: () => recruiterApi.createVacancy({
            title, company,
            location: location || null,
            rawDescription: description,
        }),
        onSuccess: (res) => {
            queryClient.invalidateQueries({ queryKey: ['recruiter', 'vacancies'] })
            setTitle(''); setCompany(''); setLocation(''); setDescription('')
            onCreated(res.vacancyId)
        },
        onError: (e: unknown) => { setError(e instanceof Error ? e.message : 'Error') },
    })

    const canSubmit = title.trim().length > 0 && company.trim().length > 0 && description.trim().length >= 20

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <FieldRow label={t('recruiter.vacancy.form.title')}>
                <BareInput value={title} onChange={setTitle} placeholder="Backend Engineer" />
            </FieldRow>
            <FieldRow label={t('recruiter.vacancy.form.company')}>
                <BareInput value={company} onChange={setCompany} placeholder="Acme" />
            </FieldRow>
            <FieldRow label={t('recruiter.vacancy.form.location')}>
                <BareInput value={location} onChange={setLocation} placeholder="Kyiv / Remote" />
            </FieldRow>
            <FieldRow label={t('recruiter.vacancy.form.description')} hint={t('recruiter.vacancy.form.descHint')}>
                <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={8} style={textareaStyle} />
            </FieldRow>
            {error && (<div style={{ color: 'var(--color-danger-600)', fontSize: 'var(--text-sm)' }}>{error}</div>)}
            <Button onClick={() => { setError(null); mut.mutate() }} disabled={!canSubmit} isLoading={mut.isPending}
                fullWidth leftIcon={<Icon name="plus" size={14} />}>
                {mut.isPending ? t('recruiter.vacancy.form.creating') : t('recruiter.vacancy.form.submit')}
            </Button>
        </div>
    )
}

function VacancyUrlForm({ onCreated }: { onCreated: (id: string) => void }) {
    const t = useT()
    const queryClient = useQueryClient()
    const [url, setUrl] = useState('')
    const [error, setError] = useState<string | null>(null)

    const mut = useMutation({
        mutationFn: () => recruiterApi.createVacancyFromUrl(url.trim()),
        onSuccess: (res) => {
            queryClient.invalidateQueries({ queryKey: ['recruiter', 'vacancies'] })
            setUrl('')
            if (res.vacancyId) onCreated(res.vacancyId)
        },
        onError: (e: unknown) => {
            const respData = (e as { response?: { data?: { message?: string } } })?.response?.data
            setError(respData?.message ?? (e instanceof Error ? e.message : 'Error'))
        },
    })

    const isValidUrl = /^https?:\/\/.+/i.test(url.trim())

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <FieldRow label={t('recruiter.vacancy.form.urlLabel')} hint={t('recruiter.vacancy.form.urlHint')}>
                <BareInput value={url} onChange={setUrl} placeholder="https://djinni.co/jobs/..." />
            </FieldRow>
            {error && (<div style={{ color: 'var(--color-danger-600)', fontSize: 'var(--text-sm)' }}>{error}</div>)}
            <Button onClick={() => { setError(null); mut.mutate() }} disabled={!isValidUrl} isLoading={mut.isPending}
                fullWidth leftIcon={<Icon name="arrow-up-right" size={14} />}>
                {mut.isPending ? t('recruiter.vacancy.form.creating') : t('recruiter.vacancy.form.urlSubmit')}
            </Button>
        </div>
    )
}

function CreatePanel({ onCreated }: { onCreated: (id: string) => void }) {
    const t = useT()
    const [mode, setMode] = useState<FormMode>('url')
    return (
        <SidebarCard>
            <SectionHead>{t('recruiter.vacancies.new')}</SectionHead>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                <Button size="sm" variant={mode === 'url' ? 'primary' : 'secondary'} onClick={() => setMode('url')}
                    fullWidth leftIcon={<Icon name="arrow-up-right" size={13} />}>
                    {t('recruiter.vacancy.form.modeUrl')}
                </Button>
                <Button size="sm" variant={mode === 'manual' ? 'primary' : 'secondary'} onClick={() => setMode('manual')}
                    fullWidth leftIcon={<Icon name="file-text" size={13} />}>
                    {t('recruiter.vacancy.form.modeManual')}
                </Button>
            </div>
            {mode === 'url' ? <VacancyUrlForm onCreated={onCreated} /> : <VacancyManualForm onCreated={onCreated} />}
        </SidebarCard>
    )
}

function fmtDate(iso: string, lang: string): string {
    try {
        return new Date(iso).toLocaleDateString(lang === 'uk' ? 'uk-UA' : 'en-GB',
            { day: 'numeric', month: 'short', year: 'numeric' })
    } catch { return '—' }
}

function VacanciesPage() {
    const t = useT()
    const { language } = useLanguage()
    const narrow = useIsMobile(1024)
    const navigate = useNavigate()
    const { data, isLoading } = useQuery({
        queryKey: ['recruiter', 'vacancies'],
        queryFn:  recruiterApi.listVacancies,
    })

    const list = data ?? []
    const recent = [...list]
        .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
        .slice(0, 6)
    const totalScored = list.reduce((s, v) => s + v.scoredCandidatesCount, 0)

    const sidebar = (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <CreatePanel onCreated={(id) => navigate(`/recruiter/vacancy/${id}`)} />
            {recent.length > 0 && (
                <SidebarCard>
                    <SectionHead>{t('nav.recruiterVacancies')}</SectionHead>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                        {recent.map((v) => (
                            <Link key={v.id} to={`/recruiter/vacancy/${v.id}`}
                                style={{ textDecoration: 'none', display: 'flex', justifyContent: 'space-between',
                                    alignItems: 'baseline', gap: 10, padding: '7px 8px', borderRadius: 'var(--radius-md)',
                                    color: 'var(--color-text-primary)' }}
                                onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--color-bg-muted)')}
                                onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}>
                                <span style={{ fontSize: 'var(--text-sm)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{v.title}</span>
                                <span style={{ ...mono, fontSize: 'var(--text-xs)', color: 'var(--color-text-tertiary)', flexShrink: 0 }}>{v.scoredCandidatesCount}</span>
                            </Link>
                        ))}
                    </div>
                </SidebarCard>
            )}
        </div>
    )

    return (
        <WideShell sidebar={sidebar} sidebarWidth={320}>
            <div style={{ marginBottom: 20 }}>
                <div style={{ ...eyebrow, marginBottom: 8 }}>
                    {list.length} {t('recruiter.vacancies.title')} · {totalScored} {t('recruiter.vacancies.scored')}
                </div>
                <h1 style={{ fontFamily: 'var(--font-serif)', fontSize: 'var(--display-sm)', fontWeight: 600, letterSpacing: '-0.02em', margin: 0 }}>
                    {t('recruiter.vacancies.title')}
                </h1>
            </div>

            {isLoading ? (
                <p style={{ color: 'var(--color-text-tertiary)' }}>{t('common.loading')}</p>
            ) : list.length === 0 ? (
                <Card padding="lg" style={{ textAlign: 'center' }}>
                    <EmptyState icon="briefcase" title={t('recruiter.vacancies.empty')} />
                </Card>
            ) : (
                <TableScroll>
                    <table style={{ ...tableShell, minWidth: narrow ? 720 : undefined }}>
                        <thead>
                            <tr>
                                <th style={th}>{t('recruiter.vacancy.form.title')}</th>
                                <th style={th}>{t('recruiter.vacancy.form.company')}</th>
                                <th style={th}>{t('common.status')}</th>
                                <th style={{ ...th, textAlign: 'right' }}>{t('recruiter.vacancies.scored')}</th>
                                <th style={th}>{t('recruiter.vacancy.createdAt')}</th>
                                <th style={{ ...th, width: 56 }}></th>
                            </tr>
                        </thead>
                        <tbody>
                            {list.map((v) => (
                                <tr key={v.id}
                                    onClick={() => navigate(`/recruiter/vacancy/${v.id}`)}
                                    style={{ cursor: 'pointer', transition: 'background var(--transition-fast)' }}
                                    onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--color-bg-muted)')}
                                    onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}>
                                    <td style={{ ...td, fontWeight: 600, fontFamily: 'var(--font-serif)', fontSize: 'var(--text-lg)' }}>{v.title}</td>
                                    <td style={{ ...td, color: 'var(--color-text-secondary)' }}>
                                        {v.company}{v.location ? <span style={{ color: 'var(--color-text-tertiary)' }}> · {v.location}</span> : null}
                                    </td>
                                    <td style={td}>
                                        {v.isNormalized
                                            ? <Badge color="success" size="sm"><Icon name="check-circle" size={11} /> {t('recruiter.vacancy.ready')}</Badge>
                                            : <Badge color="warning" size="sm"><Icon name="alert-circle" size={11} /> {t('recruiter.vacancies.notNormalized')}</Badge>}
                                    </td>
                                    <td style={{ ...tdMono, textAlign: 'right', fontWeight: 600 }}>{v.scoredCandidatesCount}</td>
                                    <td style={{ ...tdMono, color: 'var(--color-text-tertiary)', fontSize: 'var(--text-sm)' }}>{fmtDate(v.createdAt, language)}</td>
                                    <td style={{ ...td, textAlign: 'center', color: 'var(--color-text-tertiary)' }}>
                                        <Icon name="arrow-right" size={16} />
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </TableScroll>
            )}
        </WideShell>
    )
}

export default VacanciesPage

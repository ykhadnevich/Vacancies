import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { recruiterApi } from '../../api/recruiterApi'
import { useT } from '../../i18n/useT'
import { useIsMobile } from '../../hooks/useViewport'
import Button from '../../components/ui/Button'
import Card from '../../components/ui/Card'
import Icon from '../../components/ui/Icon'
import Badge from '../../components/ui/Badge'
import EmptyState from '../../components/ui/EmptyState'
import { pageWrap, pageHeader, pageTitle, mutedText, textareaStyle } from './_styles'
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
                <textarea
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    rows={8}
                    style={textareaStyle}
                />
            </FieldRow>
            {error && (<div style={{ color: 'var(--color-danger-600)', fontSize: 'var(--text-sm)' }}>{error}</div>)}
            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                <Button
                    onClick={() => { setError(null); mut.mutate() }}
                    disabled={!canSubmit}
                    isLoading={mut.isPending}
                    leftIcon={<Icon name="plus" size={14} />}
                >
                    {mut.isPending ? t('recruiter.vacancy.form.creating') : t('recruiter.vacancy.form.submit')}
                </Button>
            </div>
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
                <BareInput
                    value={url}
                    onChange={setUrl}
                    placeholder="https://djinni.co/jobs/..."
                />
            </FieldRow>
            {error && (<div style={{ color: 'var(--color-danger-600)', fontSize: 'var(--text-sm)' }}>{error}</div>)}
            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                <Button
                    onClick={() => { setError(null); mut.mutate() }}
                    disabled={!isValidUrl}
                    isLoading={mut.isPending}
                    leftIcon={<Icon name="arrow-up-right" size={14} />}
                >
                    {mut.isPending ? t('recruiter.vacancy.form.creating') : t('recruiter.vacancy.form.urlSubmit')}
                </Button>
            </div>
        </div>
    )
}

function VacancyForm({ onCreated }: { onCreated: (id: string) => void }) {
    const t = useT()
    const isMobile = useIsMobile()
    const [mode, setMode] = useState<FormMode>('url')

    return (
        <Card padding="lg">
            <div style={{ display: 'flex', gap: 8, marginBottom: 16, flexWrap: 'wrap' }}>
                <Button
                    size="sm"
                    variant={mode === 'url' ? 'primary' : 'secondary'}
                    onClick={() => setMode('url')}
                    fullWidth={isMobile}
                    leftIcon={<Icon name="arrow-up-right" size={13} />}
                >
                    {t('recruiter.vacancy.form.modeUrl')}
                </Button>
                <Button
                    size="sm"
                    variant={mode === 'manual' ? 'primary' : 'secondary'}
                    onClick={() => setMode('manual')}
                    fullWidth={isMobile}
                    leftIcon={<Icon name="file-text" size={13} />}
                >
                    {t('recruiter.vacancy.form.modeManual')}
                </Button>
            </div>
            {mode === 'url'
                ? <VacancyUrlForm    onCreated={onCreated} />
                : <VacancyManualForm onCreated={onCreated} />}
        </Card>
    )
}

function VacanciesPage() {
    const t = useT()
    const isMobile = useIsMobile()
    const [showForm, setShowForm] = useState(false)
    const { data, isLoading } = useQuery({
        queryKey: ['recruiter', 'vacancies'],
        queryFn:  recruiterApi.listVacancies,
    })

    return (
        <div style={pageWrap}>
            <div style={{ ...pageHeader, flexWrap: 'wrap', gap: 12 }}>
                <h1 style={pageTitle}>{t('recruiter.vacancies.title')}</h1>
                <Button
                    variant={showForm ? 'secondary' : 'primary'}
                    onClick={() => setShowForm((s) => !s)}
                    leftIcon={<Icon name={showForm ? 'close' : 'plus'} size={14} />}
                    fullWidth={isMobile}
                >
                    {showForm ? t('common.close') : t('recruiter.vacancies.new')}
                </Button>
            </div>

            {showForm && <VacancyForm onCreated={() => setShowForm(false)} />}

            {isLoading ? (
                <p style={mutedText}>{t('common.loading')}</p>
            ) : !data || data.length === 0 ? (
                <Card padding="lg" style={{ textAlign: 'center' }}>
                    <EmptyState icon="briefcase" title={t('recruiter.vacancies.empty')} />
                </Card>
            ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                    {data.map((v) => (
                        <Link key={v.id} to={`/recruiter/vacancy/${v.id}`} style={{ textDecoration: 'none' }}>
                            <Card interactive>
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap' }}>
                                    <div style={{ minWidth: 0, flex: 1 }}>
                                        <div style={{ fontSize: 'var(--text-lg)', fontWeight: 600, color: 'var(--color-text-primary)' }}>
                                            {v.title}
                                        </div>
                                        <div style={{ fontSize: 'var(--text-sm)', color: 'var(--color-text-secondary)', marginTop: 2 }}>
                                            {v.company}{v.location ? ` · ${v.location}` : ''}
                                        </div>
                                    </div>
                                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 6 }}>
                                        {!v.isNormalized && (
                                            <Badge color="warning" size="sm">
                                                <Icon name="alert-circle" size={11} /> {t('recruiter.vacancies.notNormalized')}
                                            </Badge>
                                        )}
                                        <span style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-tertiary)' }}>
                                            {v.scoredCandidatesCount} {t('recruiter.vacancies.scored')}
                                        </span>
                                    </div>
                                </div>
                            </Card>
                        </Link>
                    ))}
                </div>
            )}
        </div>
    )
}

export default VacanciesPage

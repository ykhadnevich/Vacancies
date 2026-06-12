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
import {
    BareInput, FieldRow, mutedText, pageHeader, pageTitle, pageWrap, textareaStyle,
} from './VacanciesPage'

function ListForm({ onCreated }: { onCreated: () => void }) {
    const t = useT()
    const queryClient = useQueryClient()
    const [name, setName] = useState('')
    const [description, setDescription] = useState('')

    const mut = useMutation({
        mutationFn: () => recruiterApi.createList({ name, description: description || undefined }),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['recruiter', 'lists'] })
            setName(''); setDescription('')
            onCreated()
        },
    })

    return (
        <Card padding="lg">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
                <FieldRow label={t('recruiter.list.form.name')}>
                    <BareInput value={name} onChange={setName} placeholder="DevOps mid-level pool" />
                </FieldRow>
                <FieldRow label={t('recruiter.list.form.description')}>
                    <textarea
                        value={description}
                        onChange={(e) => setDescription(e.target.value)}
                        rows={3}
                        style={textareaStyle}
                    />
                </FieldRow>
                <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                    <Button
                        onClick={() => mut.mutate()}
                        disabled={name.trim().length === 0}
                        isLoading={mut.isPending}
                        leftIcon={<Icon name="plus" size={14} />}
                    >
                        {t('recruiter.list.form.submit')}
                    </Button>
                </div>
            </div>
        </Card>
    )
}

function CandidateListsPage() {
    const t = useT()
    const isMobile = useIsMobile()
    const queryClient = useQueryClient()
    const [showForm, setShowForm] = useState(false)
    const { data, isLoading } = useQuery({
        queryKey: ['recruiter', 'lists'],
        queryFn:  recruiterApi.listLists,
    })

    const deleteMut = useMutation({
        mutationFn: (id: string) => recruiterApi.deleteList(id),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['recruiter', 'lists'] })
        },
    })

    const onDelete = (e: React.MouseEvent, id: string) => {
        e.preventDefault(); e.stopPropagation()
        if (window.confirm(t('recruiter.confirmDeleteList'))) {
            deleteMut.mutate(id)
        }
    }

    return (
        <div style={pageWrap}>
            <div style={{ ...pageHeader, flexWrap: 'wrap', gap: 12 }}>
                <h1 style={pageTitle}>{t('recruiter.lists.title')}</h1>
                <Button
                    variant={showForm ? 'secondary' : 'primary'}
                    onClick={() => setShowForm((s) => !s)}
                    leftIcon={<Icon name={showForm ? 'close' : 'plus'} size={14} />}
                    fullWidth={isMobile}
                >
                    {showForm ? t('common.close') : t('recruiter.lists.new')}
                </Button>
            </div>

            {showForm && <ListForm onCreated={() => setShowForm(false)} />}

            {isLoading ? (
                <p style={mutedText}>{t('common.loading')}</p>
            ) : !data || data.length === 0 ? (
                <Card padding="lg" style={{ textAlign: 'center' }}>
                    <EmptyState icon="user" title={t('recruiter.lists.empty')} />
                </Card>
            ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                    {data.map((l) => (
                        <Link key={l.id} to={`/recruiter/list/${l.id}`} style={{ textDecoration: 'none' }}>
                            <Card interactive>
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap' }}>
                                    <div style={{ minWidth: 0, flex: 1 }}>
                                        <div style={{ fontSize: 'var(--text-lg)', fontWeight: 600, color: 'var(--color-text-primary)' }}>
                                            {l.name}
                                        </div>
                                        {l.description && (
                                            <div style={{ fontSize: 'var(--text-sm)', color: 'var(--color-text-secondary)', marginTop: 2 }}>
                                                {l.description}
                                            </div>
                                        )}
                                    </div>
                                    <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', justifyContent: 'flex-end', alignItems: 'center' }}>
                                        <Badge color="neutral" size="sm">{l.totalCandidates} {t('recruiter.lists.candidates')}</Badge>
                                        {l.normalizedCandidates > 0 && (
                                            <Badge color="success" size="sm">{l.normalizedCandidates} {t('recruiter.lists.normalized')}</Badge>
                                        )}
                                        {l.failedCandidates > 0 && (
                                            <Badge color="danger" size="sm">{l.failedCandidates} {t('recruiter.lists.failed')}</Badge>
                                        )}
                                        <button
                                            onClick={(e) => onDelete(e, l.id)}
                                            aria-label={t('recruiter.delete')}
                                            title={t('recruiter.delete')}
                                            style={iconButton}
                                        >
                                            <Icon name="trash" size={14} />
                                        </button>
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

const iconButton: React.CSSProperties = {
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
    background: 'transparent', border: '1px solid var(--color-border-default)',
    borderRadius: 'var(--radius-md)', padding: 6, cursor: 'pointer',
    color: 'var(--color-text-tertiary)', fontFamily: 'inherit',
}

export default CandidateListsPage

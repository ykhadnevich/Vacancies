import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { recruiterApi } from '../../api/recruiterApi'
import { useT } from '../../i18n/useT'
import { useLanguage } from '../../i18n/LanguageContext'
import Button from '../../components/ui/Button'
import Card from '../../components/ui/Card'
import Icon from '../../components/ui/Icon'
import Badge from '../../components/ui/Badge'
import EmptyState from '../../components/ui/EmptyState'
import { WideShell, SidebarCard, SectionHead } from '../../components/layout/Shell'
import { eyebrow, mono, rowActions } from '../../components/layout/_layout'
import { textareaStyle } from './_styles'
import { BareInput, FieldRow } from './_fields'

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
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <FieldRow label={t('recruiter.list.form.name')}>
                <BareInput value={name} onChange={setName} placeholder="DevOps mid-level pool" />
            </FieldRow>
            <FieldRow label={t('recruiter.list.form.description')}>
                <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3} style={textareaStyle} />
            </FieldRow>
            <Button onClick={() => mut.mutate()} disabled={name.trim().length === 0} isLoading={mut.isPending}
                fullWidth leftIcon={<Icon name="plus" size={14} />}>
                {t('recruiter.list.form.submit')}
            </Button>
        </div>
    )
}

function CandidateListsPage() {
    const t = useT()
    const { language } = useLanguage()
    const queryClient = useQueryClient()
    const [showForm, setShowForm] = useState(false)
    const [selectedId, setSelectedId] = useState<string | null>(null)

    const { data, isLoading } = useQuery({
        queryKey: ['recruiter', 'lists'],
        queryFn:  recruiterApi.listLists,
    })

    const deleteMut = useMutation({
        mutationFn: (id: string) => recruiterApi.deleteList(id),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['recruiter', 'lists'] }),
    })

    const onDelete = (e: React.MouseEvent, id: string) => {
        e.preventDefault(); e.stopPropagation()
        if (window.confirm(t('recruiter.confirmDeleteList'))) deleteMut.mutate(id)
    }

    const lists = data ?? []
    const selected = lists.find((l) => l.id === selectedId) ?? lists[0] ?? null

    const { data: members, isLoading: membersLoading } = useQuery({
        queryKey: ['recruiter', 'listMembers', selected?.id],
        queryFn:  () => recruiterApi.getListDetails(selected!.id),
        enabled:  !!selected,
    })

    const sidebar = (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <SidebarCard>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <SectionHead>{t('recruiter.lists.title')}</SectionHead>
                    <button onClick={() => setShowForm((s) => !s)} aria-label={t('recruiter.lists.new')}
                        title={t('recruiter.lists.new')} style={rowActions}>
                        <Icon name={showForm ? 'close' : 'plus'} size={15} />
                    </button>
                </div>
                {showForm && <ListForm onCreated={() => setShowForm(false)} />}
                {!showForm && (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                        {lists.map((l) => {
                            const active = selected?.id === l.id
                            return (
                                <button key={l.id} onClick={() => setSelectedId(l.id)}
                                    style={{
                                        display: 'flex', flexDirection: 'column', gap: 3, alignItems: 'stretch',
                                        textAlign: 'left', padding: '10px 10px', borderRadius: 'var(--radius-md)',
                                        border: '1px solid transparent', cursor: 'pointer', fontFamily: 'inherit',
                                        background: active ? 'var(--color-bg-muted)' : 'transparent',
                                        borderColor: active ? 'var(--color-border-default)' : 'transparent',
                                    }}>
                                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 8 }}>
                                        <span style={{ fontSize: 'var(--text-md)', fontWeight: active ? 600 : 500, color: 'var(--color-text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{l.name}</span>
                                        <span style={{ ...mono, fontSize: 'var(--text-xs)', color: 'var(--color-text-tertiary)', flexShrink: 0 }}>{l.totalCandidates}</span>
                                    </div>
                                    <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                                        {l.normalizedCandidates > 0 && <span style={{ ...mono, fontSize: 'var(--text-xs)', color: 'var(--color-success-700)' }}>{l.normalizedCandidates} ✓</span>}
                                        {l.failedCandidates > 0 && <span style={{ ...mono, fontSize: 'var(--text-xs)', color: 'var(--color-danger-700)' }}>{l.failedCandidates} ✕</span>}
                                    </div>
                                </button>
                            )
                        })}
                    </div>
                )}
            </SidebarCard>
        </div>
    )

    return (
        <WideShell sidebar={sidebar} sidebarWidth={300}>
            {isLoading ? (
                <p style={{ color: 'var(--color-text-tertiary)' }}>{t('common.loading')}</p>
            ) : lists.length === 0 ? (
                <Card padding="lg" style={{ textAlign: 'center' }}>
                    <EmptyState icon="user" title={t('recruiter.lists.empty')}
                        action={<Button onClick={() => setShowForm(true)} leftIcon={<Icon name="plus" size={14} />}>{t('recruiter.lists.new')}</Button>} />
                </Card>
            ) : selected ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
                    <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap' }}>
                        <div style={{ minWidth: 0 }}>
                            <div style={{ ...eyebrow, marginBottom: 8 }}>
                                {selected.totalCandidates} {t('recruiter.lists.candidates')}
                                {selected.normalizedCandidates > 0 && <> · {selected.normalizedCandidates} {t('recruiter.lists.normalized')}</>}
                                {selected.failedCandidates > 0 && <> · {selected.failedCandidates} {t('recruiter.lists.failed')}</>}
                            </div>
                            <h1 style={{ fontFamily: 'var(--font-serif)', fontSize: 'var(--display-sm)', fontWeight: 600, letterSpacing: '-0.02em', margin: 0 }}>{selected.name}</h1>
                            {selected.description && <p style={{ margin: '6px 0 0', color: 'var(--color-text-secondary)', fontSize: 'var(--text-md)', maxWidth: 560 }}>{selected.description}</p>}
                        </div>
                        <div style={{ display: 'flex', gap: 8 }}>
                            <Link to={`/recruiter/list/${selected.id}`} style={{ textDecoration: 'none' }}>
                                <Button variant="secondary" leftIcon={<Icon name="arrow-up-right" size={14} />}>{t('common.open')}</Button>
                            </Link>
                            <button onClick={(e) => onDelete(e, selected.id)} aria-label={t('recruiter.delete')} title={t('recruiter.delete')} style={rowActions}>
                                <Icon name="trash" size={15} />
                            </button>
                        </div>
                    </div>

                    <Card padding="none" style={{ overflow: 'hidden' }}>
                        {membersLoading ? (
                            <p style={{ color: 'var(--color-text-tertiary)', padding: 20, margin: 0 }}>{t('common.loading')}</p>
                        ) : !members || members.length === 0 ? (
                            <EmptyState icon="user" title={t('recruiter.lists.empty')} />
                        ) : (
                            <div style={{ overflowX: 'auto' }}>
                                <table style={{ width: '100%', borderCollapse: 'separate', borderSpacing: 0, minWidth: 560 }}>
                                    <thead>
                                        <tr>
                                            <th style={memTh}>{t('recruiter.list.form.name')}</th>
                                            <th style={memTh}>{t('common.status')}</th>
                                            <th style={memTh}>{t('recruiter.list.addedAt')}</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {members.map((c) => (
                                            <tr key={c.id}>
                                                <td style={{ ...memTd, fontWeight: 500 }}>
                                                    {c.candidateName || <span style={{ ...mono, color: 'var(--color-text-tertiary)' }}>{c.id.slice(0, 8)}</span>}
                                                </td>
                                                <td style={memTd}>
                                                    {c.normalizationStatus === 'Normalized'
                                                        ? <Badge color="success" size="sm"><Icon name="check-circle" size={11} /> {t('recruiter.lists.normalized')}</Badge>
                                                        : c.normalizationStatus === 'Failed'
                                                            ? <Badge color="danger" size="sm" title={c.lastError ?? undefined}><Icon name="alert-circle" size={11} /> {t('recruiter.lists.failed')}</Badge>
                                                            : <Badge color="warning" size="sm">{c.normalizationStatus}</Badge>}
                                                </td>
                                                <td style={{ ...memTd, ...mono, color: 'var(--color-text-tertiary)', fontSize: 'var(--text-sm)' }}>
                                                    {new Date(c.addedAt).toLocaleDateString(language === 'uk' ? 'uk-UA' : 'en-GB', { day: 'numeric', month: 'short', year: 'numeric' })}
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        )}
                    </Card>
                </div>
            ) : null}
        </WideShell>
    )
}

const memTh: React.CSSProperties = {
    textAlign: 'left', padding: '11px 16px', fontSize: 'var(--text-xs)', fontWeight: 600,
    letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--color-text-tertiary)',
    background: 'var(--color-bg-muted)', borderBottom: '1px solid var(--color-border-default)', whiteSpace: 'nowrap',
}
const memTd: React.CSSProperties = {
    padding: '12px 16px', fontSize: 'var(--text-md)', color: 'var(--color-text-primary)',
    borderBottom: '1px solid var(--color-border-subtle)', verticalAlign: 'middle',
}

export default CandidateListsPage

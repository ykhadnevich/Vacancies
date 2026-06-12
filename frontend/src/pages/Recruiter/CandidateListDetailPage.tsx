import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { recruiterApi } from '../../api/recruiterApi'
import type {
    AddCandidatesResponse,
    NormalizationStatus,
    PastedCandidateInput,
} from '../../types/recruiter'
import { useT } from '../../i18n/useT'
import { useIsMobile } from '../../hooks/useViewport'
import Button from '../../components/ui/Button'
import Card from '../../components/ui/Card'
import Icon from '../../components/ui/Icon'
import Badge from '../../components/ui/Badge'
import EmptyState from '../../components/ui/EmptyState'
import { mutedText, pageHeader, pageTitle, pageWrap, textareaStyle } from './_styles'
import { BareInput, FieldRow } from './_fields'

type Mode = 'text' | 'pdf'

function PdfUploader({ listId, onDone }: { listId: string; onDone: (r: AddCandidatesResponse) => void }) {
    const t = useT()
    const queryClient = useQueryClient()
    const fileRef = useRef<HTMLInputElement>(null)
    const [files, setFiles] = useState<File[]>([])
    const [names, setNames] = useState<string[]>([])
    const [isDragging, setIsDragging] = useState(false)

    const mut = useMutation({
        mutationFn: () => recruiterApi.addCandidatesFiles(listId, files, names),
        onSuccess: (res) => {
            queryClient.invalidateQueries({ queryKey: ['recruiter', 'list', listId] })
            queryClient.invalidateQueries({ queryKey: ['recruiter', 'lists'] })
            setFiles([]); setNames([])
            onDone(res)
        },
    })

    const onFiles = (list: FileList | null) => {
        if (!list) return
        const arr = Array.from(list).filter((f) => f.type === 'application/pdf')
        if (arr.length === 0) return
        setFiles((prev) => [...prev, ...arr])
        setNames((prev) => [...prev, ...arr.map((f) => f.name.replace(/\.pdf$/i, ''))])
    }

    const removeAt = (i: number) => {
        setFiles((prev) => prev.filter((_, idx) => idx !== i))
        setNames((prev) => prev.filter((_, idx) => idx !== i))
    }

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <input
                ref={fileRef}
                type="file"
                accept="application/pdf,.pdf"
                multiple
                onChange={(e) => { onFiles(e.target.files); e.target.value = '' }}
                style={{ display: 'none' }}
            />
            <div
                role="button"
                tabIndex={0}
                onClick={() => fileRef.current?.click()}
                onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') fileRef.current?.click() }}
                onDragOver={(e) => { e.preventDefault(); setIsDragging(true) }}
                onDragEnter={(e) => { e.preventDefault(); setIsDragging(true) }}
                onDragLeave={() => setIsDragging(false)}
                onDrop={(e) => { e.preventDefault(); setIsDragging(false); onFiles(e.dataTransfer.files) }}
                style={{
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    gap: 10, padding: 20, borderRadius: 'var(--radius-md)', cursor: 'pointer',
                    border: `1px dashed ${isDragging ? 'var(--color-primary-500)' : 'var(--color-border-strong)'}`,
                    background: isDragging ? 'var(--color-primary-50)' : 'var(--color-bg-muted)',
                    color: 'var(--color-text-secondary)', fontSize: 'var(--text-md)',
                    transition: 'all var(--transition-fast)',
                }}
            >
                <Icon name="upload" size={18} />
                <span>{t('recruiter.candidates.dropPdf')}</span>
            </div>

            {files.length > 0 && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                    {files.map((f, i) => (
                        <div key={i} style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                            <Icon name="file-text" size={14} color="var(--color-text-tertiary)" />
                            <span style={{ fontSize: 'var(--text-sm)', color: 'var(--color-text-secondary)', flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                {f.name}
                            </span>
                            <input
                                value={names[i] ?? ''}
                                onChange={(e) => {
                                    const next = [...names]
                                    next[i] = e.target.value
                                    setNames(next)
                                }}
                                placeholder={t('recruiter.candidates.candidateName')}
                                style={{
                                    flex: '0 0 220px', padding: '6px 10px', fontSize: 'var(--text-sm)',
                                    border: '1px solid var(--color-border-default)', borderRadius: 'var(--radius-md)',
                                    fontFamily: 'inherit',
                                }}
                            />
                            <button
                                onClick={() => removeAt(i)}
                                aria-label={t('common.close')}
                                title={t('common.close')}
                                style={{
                                    background: 'transparent', border: 'none', cursor: 'pointer',
                                    padding: 4, color: 'var(--color-text-tertiary)', fontFamily: 'inherit',
                                    display: 'inline-flex',
                                }}
                            >
                                <Icon name="close" size={14} />
                            </button>
                        </div>
                    ))}
                </div>
            )}

            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                <Button
                    onClick={() => mut.mutate()}
                    disabled={files.length === 0}
                    isLoading={mut.isPending}
                    leftIcon={<Icon name="check" size={14} />}
                >
                    {mut.isPending ? t('recruiter.candidates.uploading') : t('recruiter.candidates.submit')}
                </Button>
            </div>
        </div>
    )
}

function TextUploader({ listId, onDone }: { listId: string; onDone: (r: AddCandidatesResponse) => void }) {
    const t = useT()
    const queryClient = useQueryClient()
    const [items, setItems] = useState<PastedCandidateInput[]>([{ cvRawText: '', candidateName: '' }])

    const mut = useMutation({
        mutationFn: () => recruiterApi.addCandidatesText(
            listId,
            items.filter((x) => x.cvRawText.trim().length > 0),
        ),
        onSuccess: (res) => {
            queryClient.invalidateQueries({ queryKey: ['recruiter', 'list', listId] })
            queryClient.invalidateQueries({ queryKey: ['recruiter', 'lists'] })
            setItems([{ cvRawText: '', candidateName: '' }])
            onDone(res)
        },
    })

    const canSubmit = items.some((x) => x.cvRawText.trim().length > 0)

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            {items.map((item, i) => (
                <Card key={i} padding="md">
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                        <FieldRow label={t('recruiter.candidates.candidateName')}>
                            <BareInput
                                value={item.candidateName ?? ''}
                                onChange={(v) => {
                                    const next = [...items]
                                    next[i] = { ...next[i], candidateName: v }
                                    setItems(next)
                                }}
                            />
                        </FieldRow>
                        <FieldRow label={t('recruiter.candidates.cvText')}>
                            <textarea
                                value={item.cvRawText}
                                onChange={(e) => {
                                    const next = [...items]
                                    next[i] = { ...next[i], cvRawText: e.target.value }
                                    setItems(next)
                                }}
                                rows={6}
                                style={textareaStyle}
                            />
                        </FieldRow>
                    </div>
                </Card>
            ))}
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <Button
                    variant="ghost"
                    onClick={() => setItems([...items, { cvRawText: '', candidateName: '' }])}
                    leftIcon={<Icon name="plus" size={14} />}
                >
                    {t('recruiter.candidates.addOne')}
                </Button>
                <Button
                    onClick={() => mut.mutate()}
                    disabled={!canSubmit}
                    isLoading={mut.isPending}
                    leftIcon={<Icon name="check" size={14} />}
                >
                    {mut.isPending ? t('recruiter.candidates.uploading') : t('recruiter.candidates.submit')}
                </Button>
            </div>
        </div>
    )
}

const STATUS_COLOR: Record<NormalizationStatus, 'success' | 'warning' | 'danger'> = {
    Normalized: 'success',
    Pending:    'warning',
    Failed:     'danger',
}

function CandidateListDetailPage() {
    const { id } = useParams<{ id: string }>()
    const t = useT()
    const isMobile = useIsMobile()
    const queryClient = useQueryClient()
    const [mode, setMode] = useState<Mode>('text')
    const [lastResult, setLastResult] = useState<AddCandidatesResponse | null>(null)

    const { data, isLoading } = useQuery({
        queryKey: ['recruiter', 'list', id],
        queryFn:  () => recruiterApi.getListDetails(id!),
        enabled:  !!id,
        refetchInterval: (q) =>
            (q.state.data ?? []).some((c) => c.normalizationStatus === 'Pending') ? 3000 : false,
    })

    const deleteMut = useMutation({
        mutationFn: (candidateId: string) => recruiterApi.deleteCandidate(candidateId),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['recruiter', 'list', id] })
            queryClient.invalidateQueries({ queryKey: ['recruiter', 'lists'] })
        },
    })

    const onDelete = (candidateId: string) => {
        if (window.confirm(t('recruiter.confirmDeleteCandidate'))) {
            deleteMut.mutate(candidateId)
        }
    }

    if (!id) return null

    return (
        <div style={pageWrap}>
            <div style={pageHeader}>
                <h1 style={pageTitle}>{t('recruiter.candidates.title')}</h1>
            </div>

            <Card padding="lg">
                <div style={{ display: 'flex', gap: 8, marginBottom: 14, flexWrap: 'wrap' }}>
                    <Button
                        size="sm"
                        variant={mode === 'text' ? 'primary' : 'secondary'}
                        onClick={() => setMode('text')}
                        fullWidth={isMobile}
                    >
                        {t('recruiter.candidates.addText')}
                    </Button>
                    <Button
                        size="sm"
                        variant={mode === 'pdf' ? 'primary' : 'secondary'}
                        onClick={() => setMode('pdf')}
                        fullWidth={isMobile}
                    >
                        {t('recruiter.candidates.addPdf')}
                    </Button>
                </div>
                {mode === 'text'
                    ? <TextUploader listId={id} onDone={setLastResult} />
                    : <PdfUploader  listId={id} onDone={setLastResult} />}
                {lastResult && (
                    <div style={{ marginTop: 12, display: 'flex', gap: 8 }}>
                        <Badge color="success">+{lastResult.normalized}</Badge>
                        {lastResult.failed > 0 && <Badge color="danger">{lastResult.failed}</Badge>}
                    </div>
                )}
            </Card>

            {isLoading ? (
                <p style={mutedText}>{t('common.loading')}</p>
            ) : !data || data.length === 0 ? (
                <Card padding="lg" style={{ textAlign: 'center' }}>
                    <EmptyState icon="file-text" title={t('recruiter.candidates.empty')} />
                </Card>
            ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                    {data.map((c) => (
                        <Card key={c.id}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0, flex: 1 }}>
                                    <Icon name="user" size={14} color="var(--color-text-tertiary)" />
                                    <span style={{ fontSize: 'var(--text-md)', color: 'var(--color-text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                        {c.candidateName || c.id.slice(0, 8)}
                                    </span>
                                </div>
                                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                                    <Badge color={STATUS_COLOR[c.normalizationStatus]} size="sm">
                                        {t(`recruiter.candidates.status.${c.normalizationStatus}` as const)}
                                    </Badge>
                                    {c.lastError && (
                                        <span style={{ fontSize: 'var(--text-xs)', color: 'var(--color-danger-600)', maxWidth: 220, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={c.lastError}>
                                            {c.lastError}
                                        </span>
                                    )}
                                    <button
                                        onClick={() => onDelete(c.id)}
                                        aria-label={t('recruiter.delete')}
                                        title={t('recruiter.delete')}
                                        style={iconButton}
                                    >
                                        <Icon name="trash" size={13} />
                                    </button>
                                </div>
                            </div>
                        </Card>
                    ))}
                </div>
            )}
        </div>
    )
}

const iconButton: React.CSSProperties = {
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
    background: 'transparent', border: '1px solid var(--color-border-default)',
    borderRadius: 'var(--radius-md)', padding: 5, cursor: 'pointer',
    color: 'var(--color-text-tertiary)', fontFamily: 'inherit',
}

export default CandidateListDetailPage

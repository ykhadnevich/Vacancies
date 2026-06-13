import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { userApi, type CvStatus } from '../../api/userApi'
import Card from '../../components/ui/Card'
import Button from '../../components/ui/Button'
import Badge from '../../components/ui/Badge'
import Icon from '../../components/ui/Icon'
import { useT } from '../../i18n/useT'
import { useAuthStore } from '../../store/authStore'
import type { UserRole } from '../../types/recruiter'

function StatusBadge({ status }: { status: CvStatus }) {
    const t = useT()
    if (status === 'Ready') {
        return <Badge color="success" size="md"><Icon name="check-circle" size={12} /> {t('profile.cvReady')}</Badge>
    }
    if (status === 'PendingNormalization') {
        return <Badge color="warning" size="md"><Icon name="alert-circle" size={12} /> {t('profile.cvPending')}</Badge>
    }
    if (status === 'Failed') {
        return <Badge color="danger" size="md"><Icon name="alert-circle" size={12} /> {t('common.error')}</Badge>
    }
    return <Badge color="neutral" size="md">—</Badge>
}

const sectionLabel: React.CSSProperties = {
    display: 'block',
    fontSize: 'var(--text-xs)',
    textTransform: 'uppercase',
    letterSpacing: '0.1em',
    color: 'var(--color-text-tertiary)',
    fontWeight: 'var(--font-weight-semibold)' as unknown as number,
}

function ProfilePage() {
    const queryClient = useQueryClient()
    const t = useT()
    const { role: storeRole, setRole: setStoreRole } = useAuthStore()

    const { data: profile, isLoading } = useQuery({
        queryKey: ['profile'],
        queryFn:  userApi.getProfile,
    })

    useEffect(() => {
        if (profile?.role && profile.role !== storeRole) {
            setStoreRole(profile.role)
        }
    }, [profile?.role, storeRole, setStoreRole])

    const [roleErr, setRoleErr] = useState<string | null>(null)
    const setRoleMut = useMutation({
        mutationFn: (next: UserRole) => userApi.setRole(next),
        onSuccess: (res) => {
            setRoleErr(null)
            setStoreRole(res.role, res.token)
            queryClient.invalidateQueries({ queryKey: ['profile'] })
        },
        onError: (err: unknown) => {
            const msg = (err as { response?: { data?: { message?: string } }, message?: string })
                ?.response?.data?.message
                ?? (err as { message?: string })?.message
                ?? 'Unknown error'
            setRoleErr(msg)
        },
    })

    const { data: cvStatus } = useQuery({
        queryKey: ['cvStatus'],
        queryFn:  userApi.getCvStatus,
        refetchInterval: (q) => q.state.data?.status === 'PendingNormalization' ? 3000 : false,
    })

    const [displayName, setDisplayName] = useState('')
    const [saveState, setSaveState] = useState<'idle' | 'saved' | 'error'>('idle')

    useEffect(() => {
        // eslint-disable-next-line react-hooks/set-state-in-effect
        if (profile) setDisplayName(profile.displayName ?? '')
    }, [profile])

    const saveMutation = useMutation({
        mutationFn: () => userApi.updatePreferences({
            displayName,
            skills:         profile?.skills ?? [],
            seniorityLevel: profile?.seniorityLevel ?? 2,
        }),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['profile'] })
            setSaveState('saved')
            setTimeout(() => setSaveState('idle'), 2500)
        },
        onError: () => {
            setSaveState('error')
            setTimeout(() => setSaveState('idle'), 3000)
        },
    })

    const uploadMutation = useMutation({
        mutationFn: (file: File) => userApi.uploadCv(file),
        onSuccess:  () => {
            queryClient.invalidateQueries({ queryKey: ['profile'] })
            queryClient.invalidateQueries({ queryKey: ['cvStatus'] })
            normalizeMutation.mutate()
        },
    })

    const normalizeMutation = useMutation({
        mutationFn: () => userApi.normalizeCv(),
        onSuccess:  () => {
            queryClient.invalidateQueries({ queryKey: ['profile'] })
            queryClient.invalidateQueries({ queryKey: ['cvStatus'] })
            queryClient.invalidateQueries({ queryKey: ['jobs'] })
        },
    })

    const onFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0]
        if (file && file.type === 'application/pdf') {
            uploadMutation.mutate(file)
        }
    }

    if (isLoading) {
        return (
            <div style={{ maxWidth: 560, margin: '0 auto', padding: '48px 16px', textAlign: 'center', color: 'var(--color-text-tertiary)' }}>
                {t('common.loading')}
            </div>
        )
    }

    const status: CvStatus = cvStatus?.status ?? 'NoCv'
    const cvProcessing = uploadMutation.isPending || normalizeMutation.isPending

    return (
        <div style={{ width: '100%', maxWidth: 'var(--max-width-narrow)', margin: '0 auto', padding: '40px 24px 80px', display: 'flex', flexDirection: 'column', gap: 18 }}>

            <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
                <div>
                    <h1 style={{ fontSize: 'var(--display-sm)', margin: 0 }}>
                        {profile?.displayName || t('profile.title')}
                    </h1>
                    <p style={{ margin: '5px 0 0', color: 'var(--color-text-secondary)', fontFamily: 'var(--font-mono)', fontSize: 'var(--text-sm)' }}>
                        {profile?.email}
                    </p>
                </div>
                <Button
                    variant={saveState === 'saved' ? 'secondary' : 'primary'}
                    onClick={() => saveMutation.mutate()}
                    isLoading={saveMutation.isPending}
                >
                    {saveState === 'saved' ? t('profile.saved') : saveState === 'error' ? t('common.error') : t('profile.save')}
                </Button>
            </div>

            <Card padding="lg">
                <label style={{ ...sectionLabel, marginBottom: 8 }}>{t('profile.name')}</label>
                <input
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                    placeholder={t('auth.displayNamePlaceholder')}
                    style={{
                        width: '100%', padding: '10px 14px', fontSize: 'var(--text-md)',
                        fontFamily: 'var(--font-sans)', color: 'var(--color-text-primary)',
                        background: 'var(--color-bg-elevated)', border: '1px solid var(--color-border-default)',
                        borderRadius: 'var(--radius-md)', outline: 'none', boxShadow: 'var(--shadow-inset)',
                    }}
                    onFocus={(e) => {
                        e.currentTarget.style.borderColor = 'var(--color-primary-600)'
                        e.currentTarget.style.boxShadow   = 'var(--ring-focus)'
                    }}
                    onBlur={(e) => {
                        e.currentTarget.style.borderColor = 'var(--color-border-default)'
                        e.currentTarget.style.boxShadow   = 'var(--shadow-inset)'
                    }}
                />
            </Card>

            <Card padding="lg">
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
                    <label style={sectionLabel}>{t('profile.cv')}</label>
                    <StatusBadge status={cvProcessing ? 'PendingNormalization' : status} />
                </div>

                <label style={{
                    display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10,
                    padding: 22, borderRadius: 'var(--radius-md)',
                    cursor: cvProcessing ? 'wait' : 'pointer',
                    border: '1px dashed var(--color-border-strong)',
                    background: 'var(--color-bg-muted)', color: 'var(--color-text-secondary)',
                    fontSize: 'var(--text-md)', transition: 'all var(--transition-fast)',
                }}>
                    <input type="file" accept=".pdf" onChange={onFileChange} disabled={cvProcessing} style={{ display: 'none' }} />
                    <Icon name={profile?.hasCv ? 'file-text' : 'upload'} size={18} />
                    <span style={{ fontFamily: profile?.cvFileName ? 'var(--font-mono)' : 'var(--font-sans)', fontSize: 'var(--text-sm)' }}>
                        {uploadMutation.isPending
                            ? t('common.loading')
                            : profile?.cvFileName
                                ? profile.cvFileName
                                : t('profile.cvUpload')}
                    </span>
                </label>

                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 12, gap: 12, flexWrap: 'wrap' }}>
                    <p style={{ margin: 0, color: 'var(--color-text-tertiary)', fontSize: 'var(--text-sm)', flex: 1 }}>
                        {status === 'Ready'
                            ? t('profile.cvProcessed')
                            : status === 'PendingNormalization'
                                ? t('profile.cvPending')
                                : status === 'Failed'
                                    ? t('common.error')
                                    : ''}
                    </p>

                    {profile?.hasCv && status !== 'Ready' && (
                        <Button
                            size="sm"
                            variant="secondary"
                            onClick={() => normalizeMutation.mutate()}
                            isLoading={normalizeMutation.isPending}
                            leftIcon={<Icon name="sparkle" size={14} />}
                        >
                            {normalizeMutation.isPending ? t('profile.cvNormalizing') : t('profile.cvNormalize')}
                        </Button>
                    )}
                </div>

                {normalizeMutation.isError && (
                    <p style={{ marginTop: 8, fontSize: 'var(--text-sm)', color: 'var(--color-danger-700)' }}>
                        {t('profile.cvErrServer')}
                    </p>
                )}
            </Card>

            <Card padding="lg">
                <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
                        <Icon name="briefcase" size={16} color="var(--color-text-secondary)" />
                        <span style={{ fontFamily: 'var(--font-serif)', fontSize: 'var(--text-xl)', fontWeight: 600, color: 'var(--color-text-primary)', whiteSpace: 'nowrap' }}>
                            {t('recruiter.activate.title')}
                        </span>
                        {(storeRole === 'Recruiter' || storeRole === 'Both') && (
                            <Badge color="success" size="sm">
                                <Icon name="check-circle" size={11} /> {t('recruiter.activate.active')}
                            </Badge>
                        )}
                    </div>
                    <p style={{ margin: 0, fontSize: 'var(--text-sm)', color: 'var(--color-text-secondary)', lineHeight: 1.55 }}>
                        {t('recruiter.activate.description')}
                    </p>
                    {roleErr && (
                        <p style={{ margin: 0, fontSize: 'var(--text-sm)', color: 'var(--color-danger-700)', textAlign: 'center' }}>
                            {roleErr}
                        </p>
                    )}
                    <div style={{ display: 'flex', justifyContent: 'center' }}>
                        {storeRole === 'Candidate' ? (
                            <Button
                                onClick={() => setRoleMut.mutate('Both')}
                                isLoading={setRoleMut.isPending}
                                leftIcon={<Icon name="plus" size={14} />}
                            >
                                {t('recruiter.activate.cta')}
                            </Button>
                        ) : (
                            <Button
                                variant="secondary"
                                onClick={() => setRoleMut.mutate('Candidate')}
                                isLoading={setRoleMut.isPending}
                            >
                                {t('recruiter.activate.deactivate')}
                            </Button>
                        )}
                    </div>
                </div>
            </Card>

        </div>
    )
}

export default ProfilePage

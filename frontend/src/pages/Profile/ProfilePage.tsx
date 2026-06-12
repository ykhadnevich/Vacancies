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

function ProfilePage() {
    const queryClient = useQueryClient()
    const t = useT()
    const { role: storeRole, setRole: setStoreRole } = useAuthStore()

    const { data: profile, isLoading } = useQuery({
        queryKey: ['profile'],
        queryFn:  userApi.getProfile,
    })

    // Hydrate the auth store with the server-truth role whenever the profile
    // refetches — the JWT itself does not carry the role today.
    useEffect(() => {
        if (profile?.role && profile.role !== storeRole) {
            setStoreRole(profile.role)
        }
    }, [profile?.role, storeRole, setStoreRole])

    const setRoleMut = useMutation({
        mutationFn: (next: UserRole) => userApi.setRole(next),
        onSuccess: (res) => {
            setStoreRole(res.role, res.token)
            queryClient.invalidateQueries({ queryKey: ['profile'] })
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
                Завантаження профілю…
            </div>
        )
    }

    const status: CvStatus = cvStatus?.status ?? 'NoCv'
    const cvProcessing = uploadMutation.isPending || normalizeMutation.isPending

    return (
        <div style={{ width: '100%', maxWidth: 'var(--max-width-narrow)', margin: '0 auto', padding: '32px 16px', display: 'flex', flexDirection: 'column', gap: 20 }}>

            {}
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 4 }}>
                <div>
                    <h1 style={{ fontSize: 'var(--text-2xl)', margin: 0 }}>
                        {profile?.displayName || 'Профіль'}
                    </h1>
                    <p style={{ margin: '4px 0 0', color: 'var(--color-text-secondary)', fontSize: 'var(--text-md)' }}>
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

            {}
            <Card padding="lg">
                <label style={{
                    display:        'block',
                    fontSize:       'var(--text-xs)',
                    textTransform:  'uppercase',
                    letterSpacing:  '0.06em',
                    color:          'var(--color-text-tertiary)',
                    marginBottom:   8,
                    fontWeight:     'var(--font-weight-medium)' as unknown as number,
                }}>
                    {t('profile.name')}
                </label>
                <input
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                    placeholder={t('auth.displayNamePlaceholder')}
                    style={{
                        width:          '100%',
                        padding:        '10px 14px',
                        fontSize:       'var(--text-md)',
                        fontFamily:     'inherit',
                        color:          'var(--color-text-primary)',
                        background:     'var(--color-bg-surface)',
                        border:         '1px solid var(--color-border-default)',
                        borderRadius:   'var(--radius-md)',
                        outline:        'none',
                    }}
                    onFocus={(e) => {
                        e.currentTarget.style.borderColor = 'var(--color-primary-500)'
                        e.currentTarget.style.boxShadow   = '0 0 0 3px var(--color-primary-100)'
                    }}
                    onBlur={(e) => {
                        e.currentTarget.style.borderColor = 'var(--color-border-default)'
                        e.currentTarget.style.boxShadow   = 'none'
                    }}
                />
            </Card>

            {}
            <Card padding="lg">
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
                    <label style={{
                        fontSize:       'var(--text-xs)',
                        textTransform:  'uppercase',
                        letterSpacing:  '0.06em',
                        color:          'var(--color-text-tertiary)',
                        fontWeight:     'var(--font-weight-medium)' as unknown as number,
                    }}>
                        {t('profile.cv')}
                    </label>
                    <StatusBadge status={cvProcessing ? 'PendingNormalization' : status} />
                </div>

                {}
                <label style={{
                    display:        'flex',
                    alignItems:     'center',
                    justifyContent: 'center',
                    gap:            10,
                    padding:        '20px',
                    borderRadius:   'var(--radius-md)',
                    cursor:         cvProcessing ? 'wait' : 'pointer',
                    border:         '1px dashed var(--color-border-strong)',
                    background:     'var(--color-bg-muted)',
                    color:          'var(--color-text-secondary)',
                    fontSize:       'var(--text-md)',
                    transition:     'all var(--transition-fast)',
                }}>
                    <input type="file" accept=".pdf" onChange={onFileChange} disabled={cvProcessing}
                        style={{ display: 'none' }} />
                    <Icon name={profile?.hasCv ? 'file-text' : 'upload'} size={18} />
                    <span>
                        {uploadMutation.isPending
                            ? t('common.loading')
                            : profile?.cvFileName
                                ? profile.cvFileName
                                : t('profile.cvUpload')}
                    </span>
                </label>

                {}
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
                            {normalizeMutation.isPending ? 'Обробляємо…' : 'Обробити'}
                        </Button>
                    )}
                </div>

                {normalizeMutation.isError && (
                    <p style={{ marginTop: 8, fontSize: 'var(--text-sm)', color: 'var(--color-danger-600)' }}>
                        Не вдалося обробити резюме. Перевірте з’єднання з сервером.
                    </p>
                )}
            </Card>

            {/* Recruiter cabinet activation */}
            <Card padding="lg">
                <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16 }}>
                    <div style={{ flex: 1 }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
                            <Icon name="briefcase" size={16} color="var(--color-text-secondary)" />
                            <span style={{ fontSize: 'var(--text-md)', fontWeight: 600, color: 'var(--color-text-primary)' }}>
                                {t('recruiter.activate.title')}
                            </span>
                            {(storeRole === 'Recruiter' || storeRole === 'Both') && (
                                <Badge color="success" size="sm">
                                    <Icon name="check-circle" size={11} /> {t('recruiter.activate.active')}
                                </Badge>
                            )}
                        </div>
                        <p style={{ margin: 0, fontSize: 'var(--text-sm)', color: 'var(--color-text-secondary)' }}>
                            {t('recruiter.activate.description')}
                        </p>
                    </div>
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
            </Card>

        </div>
    )
}

export default ProfilePage

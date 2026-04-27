import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { userApi } from '../../api/userApi'

function ProfilePage() {
    const queryClient = useQueryClient()

    const { data: profile, isLoading } = useQuery({
        queryKey: ['profile'],
        queryFn: userApi.getProfile,
    })

    const [displayName, setDisplayName] = useState('')
    const [cvFile, setCvFile] = useState<File | null>(null)
    const [saveStatus, setSaveStatus] = useState<'idle' | 'saved' | 'error'>('idle')

    useEffect(() => {
        if (!profile) return
        setDisplayName(profile.displayName ?? '')
    }, [profile])

    const saveMutation = useMutation({
        mutationFn: () =>
            userApi.updatePreferences({
                displayName,
                skills: profile?.skills ?? [],
                seniorityLevel: profile?.seniorityLevel ?? 5,
            }),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['profile'] })
            setSaveStatus('saved')
            setTimeout(() => setSaveStatus('idle'), 2500)
        },
        onError: () => {
            setSaveStatus('error')
            setTimeout(() => setSaveStatus('idle'), 3000)
        },
    })

    const cvMutation = useMutation({
        mutationFn: (file: File) => userApi.uploadCv(file),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['profile'] })
        },
    })

    const handleCvChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0]
        if (file && file.type === 'application/pdf') {
            setCvFile(file)
            cvMutation.mutate(file)
        }
    }

    const inputStyle: React.CSSProperties = {
        width: '100%',
        padding: '10px 14px',
        borderRadius: 8,
        border: '1px solid #e5e7eb',
        fontSize: 15,
        boxSizing: 'border-box',
        outline: 'none',
        background: '#fff',
    }

    const labelStyle: React.CSSProperties = {
        fontSize: 13,
        fontWeight: 600,
        color: '#6b7280',
        marginBottom: 6,
        display: 'block',
        textTransform: 'uppercase',
        letterSpacing: '0.04em',
    }

    if (isLoading) {
        return (
            <div style={{ maxWidth: 560, margin: '0 auto', padding: '48px 16px', textAlign: 'center', color: '#9ca3af' }}>
                Завантаження профілю…
            </div>
        )
    }

    const cvUploaded = !!(cvFile || profile?.hasCv)
    const cvLabel = cvMutation.isPending
        ? '⏳ Завантаження…'
        : cvFile
        ? `✓ ${cvFile.name}`
        : profile?.hasCv && profile.cvFileName
        ? `✓ ${profile.cvFileName}`
        : '📄 Завантажити CV (PDF)'

    const cvColor = cvMutation.isPending ? '#d97706' : cvUploaded ? '#16a34a' : '#2563eb'
    const cvBg   = cvMutation.isPending ? '#fffbeb'  : cvUploaded ? '#f0fdf4' : '#eff6ff'
    const cvBorder = cvMutation.isPending ? '#fbbf24' : cvUploaded ? '#16a34a' : '#93c5fd'

    return (
        <div style={{ maxWidth: 560, margin: '0 auto', padding: '28px 16px' }}>

            {/* Header */}
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 24 }}>
                <div>
                    <h2 style={{ margin: 0, fontSize: 22, fontWeight: 700 }}>
                        {profile?.displayName || 'Мій профіль'}
                    </h2>
                    <p style={{ margin: '4px 0 0', color: '#6b7280', fontSize: 14 }}>{profile?.email}</p>
                </div>

                <button
                    onClick={() => saveMutation.mutate()}
                    disabled={saveMutation.isPending}
                    style={{
                        padding: '10px 22px', borderRadius: 8, fontSize: 14, cursor: 'pointer',
                        background: saveStatus === 'saved' ? '#16a34a' : saveStatus === 'error' ? '#dc2626' : '#2563eb',
                        color: '#fff', border: 'none', fontWeight: 600,
                        transition: 'background 0.2s',
                        opacity: saveMutation.isPending ? 0.7 : 1,
                    }}>
                    {saveMutation.isPending ? 'Збереження…' : saveStatus === 'saved' ? '✓ Збережено' : saveStatus === 'error' ? '✗ Помилка' : 'Зберегти'}
                </button>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>

                {/* Name */}
                <div style={{ background: '#fff', border: '1px solid #e5e7eb', borderRadius: 12, padding: 24 }}>
                    <label style={labelStyle}>Ім'я</label>
                    <input
                        style={inputStyle}
                        value={displayName}
                        onChange={e => setDisplayName(e.target.value)}
                        placeholder="Як вас звати?"
                    />
                </div>

                {/* CV */}
                <div style={{ background: '#fff', border: '1px solid #e5e7eb', borderRadius: 12, padding: 24 }}>
                    <label style={labelStyle}>CV (PDF)</label>
                    <label style={{
                        display: 'flex', alignItems: 'center', justifyContent: 'center',
                        gap: 12, padding: '22px', borderRadius: 8,
                        cursor: cvMutation.isPending ? 'default' : 'pointer',
                        border: `2px dashed ${cvBorder}`,
                        background: cvBg, color: cvColor,
                        fontWeight: 600, fontSize: 15,
                        transition: 'all 0.2s',
                    }}>
                        <input type="file" accept=".pdf" onChange={handleCvChange} style={{ display: 'none' }} disabled={cvMutation.isPending} />
                        {cvLabel}
                    </label>
                    <p style={{ color: '#9ca3af', fontSize: 13, margin: '8px 0 0' }}>
                        {cvUploaded ? 'Натисніть щоб замінити' : 'ML-аналіз вакансій базується на вашому CV'}
                    </p>
                </div>

            </div>
        </div>
    )
}

export default ProfilePage

import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { authApi } from '../../api/authApi'
import { useAuthStore } from '../../store/authStore'
import { useT } from '../../i18n/useT'

function RegisterPage() {
    const t = useT()
    const [displayName, setDisplayName]         = useState('')
    const [email, setEmail]                     = useState('')
    const [password, setPassword]               = useState('')
    const [confirmPassword, setConfirmPassword] = useState('')
    const [showPass, setShowPass]               = useState(false)

    const { login } = useAuthStore()
    const navigate  = useNavigate()

    const mutation = useMutation({
        mutationFn: () => authApi.register(email, password, displayName || undefined),
        onSuccess: (data) => {
            login(data.token, data.userId, data.email)
            navigate('/jobs')
        },
    })

    const passwordMismatch = confirmPassword.length > 0 && password !== confirmPassword
    const passwordTooShort = password.length > 0 && password.length < 6
    const canSubmit =
        email.trim() !== '' &&
        password.length >= 6 &&
        password === confirmPassword

    const handleSubmit = () => { if (canSubmit) mutation.mutate() }

    const inputStyle = {
        width: '100%',
        padding: '10px 14px',
        borderRadius: 8,
        border: '1px solid #e5e7eb',
        fontSize: 15,
        boxSizing: 'border-box' as const,
        outline: 'none',
    }

    const labelStyle = {
        fontSize: 14,
        fontWeight: 600,
        color: '#374151',
        marginBottom: 6,
        display: 'block' as const,
    }

    return (
        <div style={{ maxWidth: 400, margin: '60px auto', padding: '0 16px' }}>
            <div style={{
                background: '#fff',
                border: '1px solid #e5e7eb',
                borderRadius: 12,
                padding: 32,
                boxShadow: '0 2px 8px rgba(0,0,0,0.06)',
            }}>
                <h2 style={{ marginBottom: 8, textAlign: 'center', fontSize: 22 }}>{t('auth.signUp')}</h2>
                <p style={{ textAlign: 'center', color: '#6b7280', fontSize: 14, marginBottom: 24 }}>
                    {t('app.name')}
                </p>

                <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>

                    <div>
                        <label style={labelStyle}>{t('auth.displayName')}</label>
                        <input
                            style={inputStyle}
                            type="text"
                            value={displayName}
                            onChange={e => setDisplayName(e.target.value)}
                            onKeyDown={e => e.key === 'Enter' && handleSubmit()}
                            placeholder={t('auth.displayNamePlaceholder')}
                            autoComplete="name"
                        />
                    </div>

                    <div>
                        <label style={labelStyle}>{t('auth.email')}</label>
                        <input
                            style={inputStyle}
                            type="email"
                            value={email}
                            onChange={e => setEmail(e.target.value)}
                            onKeyDown={e => e.key === 'Enter' && handleSubmit()}
                            placeholder="email@example.com"
                            autoComplete="email"
                        />
                    </div>

                    <div>
                        <label style={labelStyle}>{t('auth.password')}</label>
                        <div style={{ position: 'relative' }}>
                            <input
                                style={{
                                    ...inputStyle,
                                    paddingRight: 44,
                                    borderColor: passwordTooShort ? '#f87171' : '#e5e7eb',
                                }}
                                type={showPass ? 'text' : 'password'}
                                value={password}
                                onChange={e => setPassword(e.target.value)}
                                onKeyDown={e => e.key === 'Enter' && handleSubmit()}
                                placeholder={t('auth.passwordPlaceholder')}
                                autoComplete="new-password"
                            />
                            <button
                                type="button"
                                onClick={() => setShowPass(v => !v)}
                                style={{
                                    position: 'absolute', right: 12, top: '50%',
                                    transform: 'translateY(-50%)',
                                    background: 'none', border: 'none',
                                    cursor: 'pointer', color: '#9ca3af', fontSize: 14,
                                }}
                            >
                                {showPass ? t('auth.hide') : t('auth.show')}
                            </button>
                        </div>
                        {passwordTooShort && (
                            <p style={{ color: '#dc2626', fontSize: 13, margin: '4px 0 0' }}>
                                {t('auth.errPassword')}
                            </p>
                        )}
                    </div>

                    <div>
                        <label style={labelStyle}>{t('auth.passwordConfirm')}</label>
                        <input
                            style={{
                                ...inputStyle,
                                borderColor: passwordMismatch ? '#f87171' : '#e5e7eb',
                            }}
                            type={showPass ? 'text' : 'password'}
                            value={confirmPassword}
                            onChange={e => setConfirmPassword(e.target.value)}
                            onKeyDown={e => e.key === 'Enter' && handleSubmit()}
                            placeholder={t('auth.passwordRepeat')}
                            autoComplete="new-password"
                        />
                        {passwordMismatch && (
                            <p style={{ color: '#dc2626', fontSize: 13, margin: '4px 0 0' }}>
                                {t('auth.passwordMismatch')}
                            </p>
                        )}
                    </div>

                    {mutation.isError && (
                        <p style={{
                            color: '#dc2626', fontSize: 14, margin: 0,
                            background: '#fef2f2', borderRadius: 8,
                            padding: '8px 12px',
                        }}>
                            {(mutation.error as { response?: { status?: number } })?.response?.status === 409
                                ? t('auth.errExists')
                                : t('common.error')}
                        </p>
                    )}

                    <button
                        onClick={handleSubmit}
                        disabled={mutation.isPending || !canSubmit}
                        style={{
                            padding: '12px', borderRadius: 8, fontSize: 15,
                            cursor: !canSubmit ? 'not-allowed' : 'pointer',
                            background: '#2563eb', color: '#fff',
                            border: 'none', fontWeight: 600,
                            opacity: !canSubmit ? 0.5 : 1,
                            transition: 'opacity 0.15s',
                        }}
                    >
                        {mutation.isPending ? t('common.loading') : t('auth.signUp')}
                    </button>

                    <p style={{ textAlign: 'center', fontSize: 14, color: '#6b7280', margin: 0 }}>
                        <Link to="/login" style={{ color: '#2563eb', fontWeight: 600 }}>
                            {t('auth.toLogin')}
                        </Link>
                    </p>
                </div>
            </div>
        </div>
    )
}

export default RegisterPage

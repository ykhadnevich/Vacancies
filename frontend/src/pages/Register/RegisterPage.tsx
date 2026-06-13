import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { authApi } from '../../api/authApi'
import { useAuthStore } from '../../store/authStore'
import { useT } from '../../i18n/useT'
import Button from '../../components/ui/Button'

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

    const inputStyle = (invalid: boolean): React.CSSProperties => ({
        width: '100%', padding: '10px 14px', borderRadius: 'var(--radius-md)',
        border: `1px solid ${invalid ? 'var(--color-danger-500)' : 'var(--color-border-default)'}`,
        background: 'var(--color-bg-elevated)', color: 'var(--color-text-primary)',
        fontFamily: 'var(--font-sans)', fontSize: 'var(--text-md)',
        boxSizing: 'border-box', outline: 'none', boxShadow: 'var(--shadow-inset)',
        transition: 'border-color var(--transition-fast), box-shadow var(--transition-fast)',
    })
    const onFocus = (e: React.FocusEvent<HTMLInputElement>) => {
        if (e.currentTarget.style.borderColor.includes('danger')) return
        e.currentTarget.style.borderColor = 'var(--color-primary-600)'
        e.currentTarget.style.boxShadow   = 'var(--ring-focus)'
    }
    const onBlur = (e: React.FocusEvent<HTMLInputElement>) => {
        e.currentTarget.style.boxShadow = 'var(--shadow-inset)'
    }

    const labelStyle: React.CSSProperties = {
        fontSize: 'var(--text-sm)', fontWeight: 'var(--font-weight-medium)' as unknown as number,
        color: 'var(--color-text-secondary)', marginBottom: 6, display: 'block',
    }
    const errorText: React.CSSProperties = { color: 'var(--color-danger-600)', fontSize: 'var(--text-sm)', margin: '4px 0 0' }

    return (
        <div style={{ maxWidth: 440, margin: '64px auto', padding: '0 16px' }}>
            <div style={{ textAlign: 'center', marginBottom: 24 }}>
                <img src="/logo-mark.svg" alt="" style={{ width: 40, height: 40, marginBottom: 14 }} />
                <h1 style={{ margin: '0 0 6px', fontSize: 'var(--display-sm)' }}>{t('auth.signUp')}</h1>
                <p style={{ color: 'var(--color-text-secondary)', fontSize: 'var(--text-md)', margin: 0 }}>
                    {t('app.name')}
                </p>
            </div>

            <div style={{
                background: 'var(--color-bg-surface)', border: '1px solid var(--color-border-default)',
                borderRadius: 'var(--radius-xl)', padding: 28, boxShadow: 'var(--shadow-md)',
            }}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                    <div>
                        <label style={labelStyle}>{t('auth.displayName')}</label>
                        <input style={inputStyle(false)} type="text" value={displayName}
                            onChange={e => setDisplayName(e.target.value)}
                            onKeyDown={e => e.key === 'Enter' && handleSubmit()}
                            onFocus={onFocus} onBlur={onBlur}
                            placeholder={t('auth.displayNamePlaceholder')} autoComplete="name" />
                    </div>

                    <div>
                        <label style={labelStyle}>{t('auth.email')}</label>
                        <input style={inputStyle(false)} type="email" value={email}
                            onChange={e => setEmail(e.target.value)}
                            onKeyDown={e => e.key === 'Enter' && handleSubmit()}
                            onFocus={onFocus} onBlur={onBlur}
                            placeholder="email@example.com" autoComplete="email" />
                    </div>

                    <div>
                        <label style={labelStyle}>{t('auth.password')}</label>
                        <div style={{ position: 'relative' }}>
                            <input style={{ ...inputStyle(passwordTooShort), paddingRight: 64 }}
                                type={showPass ? 'text' : 'password'} value={password}
                                onChange={e => setPassword(e.target.value)}
                                onKeyDown={e => e.key === 'Enter' && handleSubmit()}
                                onFocus={onFocus} onBlur={onBlur}
                                placeholder={t('auth.passwordPlaceholder')} autoComplete="new-password" />
                            <button type="button" onClick={() => setShowPass(v => !v)}
                                style={{ position: 'absolute', right: 10, top: '50%', transform: 'translateY(-50%)',
                                    background: 'transparent', border: 'none', cursor: 'pointer',
                                    color: 'var(--color-text-secondary)', fontFamily: 'var(--font-sans)',
                                    fontSize: 'var(--text-xs)', fontWeight: 600, letterSpacing: '0.04em',
                                    textTransform: 'uppercase', padding: 4 }}>
                                {showPass ? t('auth.hide') : t('auth.show')}
                            </button>
                        </div>
                        {passwordTooShort && <p style={errorText}>{t('auth.errPassword')}</p>}
                    </div>

                    <div>
                        <label style={labelStyle}>{t('auth.passwordConfirm')}</label>
                        <input style={inputStyle(passwordMismatch)} type={showPass ? 'text' : 'password'}
                            value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)}
                            onKeyDown={e => e.key === 'Enter' && handleSubmit()}
                            onFocus={onFocus} onBlur={onBlur}
                            placeholder={t('auth.passwordRepeat')} autoComplete="new-password" />
                        {passwordMismatch && <p style={errorText}>{t('auth.passwordMismatch')}</p>}
                    </div>

                    {mutation.isError && (
                        <p style={{ color: 'var(--color-danger-700)', fontSize: 'var(--text-sm)', margin: 0,
                            background: 'var(--color-danger-50)', borderRadius: 'var(--radius-md)', padding: '8px 12px' }}>
                            {(mutation.error as { response?: { status?: number } })?.response?.status === 409
                                ? t('auth.errExists')
                                : t('common.error')}
                        </p>
                    )}

                    <Button variant="primary" size="lg" fullWidth onClick={handleSubmit}
                        disabled={!canSubmit} isLoading={mutation.isPending}>
                        {t('auth.signUp')}
                    </Button>

                    <p style={{ textAlign: 'center', fontSize: 'var(--text-sm)', color: 'var(--color-text-secondary)', margin: 0 }}>
                        <Link to="/login" style={{ color: 'var(--color-primary-600)', fontWeight: 600 }}>
                            {t('auth.toLogin')}
                        </Link>
                    </p>
                </div>
            </div>
        </div>
    )
}

export default RegisterPage

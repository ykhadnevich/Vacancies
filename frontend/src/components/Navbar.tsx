import { NavLink, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import { useLanguage } from '../i18n/LanguageContext'
import { useT } from '../i18n/useT'
import { useTheme } from '../theme/ThemeContext'
import { useIsMobile } from '../hooks/useViewport'
import Icon from './ui/Icon'

function Navbar() {
    const { isAuthenticated, email, role, logout } = useAuthStore()
    const { toggle } = useLanguage()
    const { theme, toggle: toggleTheme } = useTheme()
    const t = useT()
    const navigate = useNavigate()
    const compact = useIsMobile(900)

    const handleLogout = async () => {
        await logout()
        navigate('/login')
    }

    const isRecruiter = role === 'Recruiter' || role === 'Both'
    const isCandidate = role === 'Candidate' || role === 'Both'

    const ROUTES: { to: string; label: string }[] = []
    if (isCandidate) {
        ROUTES.push({ to: '/jobs',    label: t('nav.jobs')    })
        ROUTES.push({ to: '/tracker', label: t('nav.tracker') })
    }
    if (isRecruiter) {
        ROUTES.push({ to: '/recruiter/vacancies', label: t('nav.recruiterVacancies') })
        ROUTES.push({ to: '/recruiter/lists',     label: t('nav.recruiterLists')     })
    }
    ROUTES.push({ to: '/profile', label: t('nav.profile') })
    ROUTES.push({ to: '/about',   label: t('nav.about')   })

    const linkStyle = ({ isActive }: { isActive: boolean }): React.CSSProperties => ({
        color:          isActive ? 'var(--color-text-primary)' : 'var(--color-text-secondary)',
        textDecoration: 'none',
        fontWeight:     (isActive ? 'var(--font-weight-medium)' : 'var(--font-weight-regular)') as unknown as number,
        fontSize:       'var(--text-md)',
        padding:        '7px 14px',
        borderRadius:   'var(--radius-md)',
        background:     isActive ? 'var(--color-bg-muted)' : 'transparent',
        transition:     'background var(--transition-fast), color var(--transition-fast)',
        whiteSpace:     'nowrap',
    })

    const navLinks = isAuthenticated && (
        <div style={{
            display: 'flex', alignItems: 'center', gap: 2,
            justifyContent: 'center', flexWrap: 'wrap',
        }}>
            {ROUTES.map(({ to, label }) => (
                <NavLink key={to} to={to} style={linkStyle}>{label}</NavLink>
            ))}
        </div>
    )

    const ctrlBtn: React.CSSProperties = {
        padding: 7, borderRadius: 'var(--radius-md)', cursor: 'pointer',
        background: 'transparent', color: 'var(--color-text-secondary)',
        border: '1px solid var(--color-border-default)',
        display: 'inline-flex', alignItems: 'center', fontFamily: 'inherit',
    }

    return (
        <nav style={{
            background: 'var(--color-bg-surface)',
            borderBottom: '1px solid var(--color-border-default)',
            width: '100%', position: 'sticky', top: 0, zIndex: 30,
        }}>
            <div style={{
                maxWidth: 'var(--max-width-wide)', margin: '0 auto', padding: '0 24px',
                display: 'grid',
                gridTemplateColumns: compact ? '1fr auto' : '1fr auto 1fr',
                alignItems: 'center', height: 64, gap: 16,
            }}>
                <NavLink
                    to={isAuthenticated ? '/jobs' : '/login'}
                    style={{ display: 'inline-flex', alignItems: 'center', gap: 10, textDecoration: 'none', justifySelf: 'start' }}
                >
                    <img src="/logo-mark.svg" alt="" style={{ width: 26, height: 26 }} />
                    <span style={{
                        fontFamily: 'var(--font-serif)', fontWeight: 600, fontSize: 'var(--text-2xl)',
                        letterSpacing: '-0.02em', color: 'var(--color-text-primary)',
                    }}>
                        {t('app.name')}
                    </span>
                </NavLink>

                {!compact && <div style={{ justifySelf: 'center' }}>{navLinks}</div>}

                <div style={{ display: 'flex', alignItems: 'center', gap: 10, justifySelf: 'end' }}>
                    <button onClick={toggleTheme} title={theme === 'dark' ? t('nav.themeLight') : t('nav.themeDark')}
                        aria-label={theme === 'dark' ? t('nav.themeLight') : t('nav.themeDark')} style={ctrlBtn}>
                        <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={15} />
                    </button>
                    <button onClick={toggle}
                        title={t('nav.langSwitchTitle')}
                        aria-label={t('nav.langSwitchTitle')}
                        style={{ ...ctrlBtn, padding: '6px 10px', fontSize: 'var(--text-xs)', fontWeight: 600,
                            fontFamily: 'var(--font-mono)', letterSpacing: '0.06em' }}>
                        {t('nav.langSwitch')}
                    </button>

                    {isAuthenticated ? (
                        <>
                            {!compact && (
                                <span style={{ color: 'var(--color-text-secondary)', fontSize: 'var(--text-sm)', fontFamily: 'var(--font-mono)' }}>
                                    {email}
                                </span>
                            )}
                            <button onClick={handleLogout} style={{ ...ctrlBtn, gap: 6, padding: '6px 12px', fontSize: 'var(--text-sm)' }}>
                                <Icon name="logout" size={14} />
                                {!compact && t('nav.logout')}
                            </button>
                        </>
                    ) : (
                        <>
                            <NavLink to="/login" style={{ color: 'var(--color-text-secondary)', fontSize: 'var(--text-sm)', textDecoration: 'none', padding: '6px 12px', borderRadius: 'var(--radius-md)' }}>
                                {t('nav.login')}
                            </NavLink>
                            <NavLink to="/register" style={{ color: '#fff', background: 'var(--color-primary-600)', fontSize: 'var(--text-sm)', textDecoration: 'none', padding: '7px 14px', borderRadius: 'var(--radius-md)', fontWeight: 'var(--font-weight-medium)' as unknown as number }}>
                                {t('nav.register')}
                            </NavLink>
                        </>
                    )}
                </div>

                {compact && isAuthenticated && (
                    <div style={{ gridColumn: '1 / -1', paddingBottom: 10, overflowX: 'auto' }}>
                        <div style={{ display: 'flex', gap: 2, minWidth: 'min-content' }}>
                            {ROUTES.map(({ to, label }) => (
                                <NavLink key={to} to={to} style={linkStyle}>{label}</NavLink>
                            ))}
                        </div>
                    </div>
                )}
            </div>
        </nav>
    )
}

export default Navbar

import { NavLink, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import { useLanguage } from '../i18n/LanguageContext'
import { useT } from '../i18n/useT'
import { useTheme } from '../theme/ThemeContext'
import Icon from './ui/Icon'

function Navbar() {
    const { isAuthenticated, email, role, logout } = useAuthStore()
    const { language, toggle } = useLanguage()
    const { theme, toggle: toggleTheme } = useTheme()
    const t = useT()
    const navigate = useNavigate()

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

    return (
        <nav style={{
            background:    'var(--color-bg-surface)',
            borderBottom:  '0.5px solid var(--color-border-default)',
            width:         '100%',
        }}>
            <div style={{
                maxWidth:    'var(--max-width-content)',
                margin:      '0 auto',
                padding:     '0 16px',
                display:     'flex',
                alignItems:  'center',
                height:      56,
                gap:         4,
            }}>
                <NavLink
                    to={isAuthenticated ? '/jobs' : '/login'}
                    style={{
                        fontWeight:    'var(--font-weight-semibold)' as unknown as number,
                        fontSize:      'var(--text-xl)',
                        color:         'var(--color-text-primary)',
                        textDecoration:'none',
                        marginRight:   24,
                        letterSpacing: '-0.01em',
                    }}
                >
                    Вакансіо
                </NavLink>

                {isAuthenticated && ROUTES.map(({ to, label }) => (
                    <NavLink
                        key={to}
                        to={to}
                        style={({ isActive }) => ({
                            color:        isActive ? 'var(--color-text-primary)' : 'var(--color-text-secondary)',
                            textDecoration:'none',
                            fontWeight:   (isActive ? 'var(--font-weight-medium)' : 'var(--font-weight-regular)') as unknown as number,
                            fontSize:     'var(--text-md)',
                            padding:      '6px 14px',
                            borderRadius: 'var(--radius-md)',
                            background:   isActive ? 'var(--color-bg-muted)' : 'transparent',
                            transition:   'background var(--transition-fast)',
                        })}
                    >
                        {label}
                    </NavLink>
                ))}

                <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 12 }}>
                    {/* Theme toggle — flips between light and dark CSS-variable themes. */}
                    <button
                        onClick={toggleTheme}
                        title={theme === 'dark' ? t('nav.themeLight') : t('nav.themeDark')}
                        aria-label={theme === 'dark' ? t('nav.themeLight') : t('nav.themeDark')}
                        style={{
                            padding:      6,
                            borderRadius: 'var(--radius-md)',
                            cursor:       'pointer',
                            background:   'transparent',
                            color:        'var(--color-text-secondary)',
                            border:       '1px solid var(--color-border-default)',
                            display:      'inline-flex',
                            alignItems:   'center',
                            fontFamily:   'inherit',
                        }}
                    >
                        <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={14} />
                    </button>

                    <button
                        onClick={toggle}
                        title={language === 'uk' ? 'Switch to English' : 'Перемкнути на українську'}
                        style={{
                            padding:      '6px 10px',
                            borderRadius: 'var(--radius-md)',
                            fontSize:     'var(--text-xs)',
                            fontWeight:   'var(--font-weight-medium)' as unknown as number,
                            cursor:       'pointer',
                            background:   'transparent',
                            color:        'var(--color-text-secondary)',
                            border:       '1px solid var(--color-border-default)',
                            fontFamily:   'inherit',
                            letterSpacing: '0.04em',
                        }}
                    >
                        {language === 'uk' ? 'EN' : 'УКР'}
                    </button>

                    {isAuthenticated ? (
                        <>
                            <span style={{
                                color:    'var(--color-text-secondary)',
                                fontSize: 'var(--text-sm)',
                            }}>
                                {email}
                            </span>
                            <button
                                onClick={handleLogout}
                                style={{
                                    display:        'inline-flex',
                                    alignItems:     'center',
                                    gap:            6,
                                    padding:        '6px 12px',
                                    borderRadius:   'var(--radius-md)',
                                    fontSize:       'var(--text-sm)',
                                    cursor:         'pointer',
                                    background:     'transparent',
                                    color:          'var(--color-text-secondary)',
                                    border:         '1px solid var(--color-border-default)',
                                    fontFamily:     'inherit',
                                }}
                            >
                                <Icon name="logout" size={14} />
                                {t('nav.logout')}
                            </button>
                        </>
                    ) : (
                        <>
                            <NavLink to="/login" style={{
                                color: 'var(--color-text-secondary)',
                                fontSize: 'var(--text-sm)',
                                textDecoration: 'none',
                                padding: '6px 12px',
                                borderRadius: 'var(--radius-md)',
                            }}>
                                {t('nav.login')}
                            </NavLink>
                            <NavLink to="/register" style={{
                                color: '#fff',
                                background: 'var(--color-primary-600)',
                                fontSize: 'var(--text-sm)',
                                textDecoration: 'none',
                                padding: '6px 12px',
                                borderRadius: 'var(--radius-md)',
                                fontWeight: 'var(--font-weight-medium)' as unknown as number,
                            }}>
                                {t('nav.register')}
                            </NavLink>
                        </>
                    )}
                </div>
            </div>
        </nav>
    )
}

export default Navbar

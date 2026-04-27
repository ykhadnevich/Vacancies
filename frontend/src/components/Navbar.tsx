import { NavLink, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'

function Navbar() {
    const { isAuthenticated, email, logout } = useAuthStore()
    const navigate = useNavigate()

    const handleLogout = () => {
        logout()
        navigate('/login')
    }

    return (
        <nav style={{
            background: '#1e40af',
            padding: '0 32px',
            display: 'flex',
            alignItems: 'center',
            height: 56,
            gap: 8,
        }}>
      <span style={{ color: '#fff', fontWeight: 700, fontSize: 20, marginRight: 32 }}>
        Вакансіо
      </span>

            {[
                { to: '/jobs', label: 'Вакансії' },
                { to: '/tracker', label: 'Трекер' },
                { to: '/profile', label: 'Профіль' },
            ].map(({ to, label }) => (
                <NavLink
                    key={to}
                    to={to}
                    style={({ isActive }) => ({
                        color: isActive ? '#fff' : '#93c5fd',
                        textDecoration: 'none',
                        fontWeight: isActive ? 600 : 400,
                        fontSize: 15,
                        padding: '6px 16px',
                        borderRadius: 8,
                        background: isActive ? 'rgba(255,255,255,0.15)' : 'transparent',
                    })}
                >
                    {label}
                </NavLink>
            ))}

            <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 12 }}>
                {isAuthenticated ? (
                    <>
                        <span style={{ color: '#93c5fd', fontSize: 14 }}>{email}</span>
                        <button onClick={handleLogout}
                                style={{
                                    padding: '6px 16px', borderRadius: 8, fontSize: 14, cursor: 'pointer',
                                    background: 'rgba(255,255,255,0.1)', color: '#fff', border: '1px solid rgba(255,255,255,0.3)',
                                }}>
                            Вийти
                        </button>
                    </>
                ) : (
                    <>
                        <NavLink to="/login"
                                 style={{ color: '#93c5fd', textDecoration: 'none', fontSize: 14 }}>
                            Увійти
                        </NavLink>
                        <NavLink to="/register"
                                 style={{
                                     padding: '6px 16px', borderRadius: 8, fontSize: 14,
                                     background: '#fff', color: '#1e40af', textDecoration: 'none', fontWeight: 600,
                                 }}>
                            Реєстрація
                        </NavLink>
                    </>
                )}
            </div>
        </nav>
    )
}

export default Navbar
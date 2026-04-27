import { Routes, Route, Navigate } from 'react-router-dom'
import Navbar from './components/Navbar'
import JobFeedPage from './pages/JobFeed/JobFeedPage'
import TrackerPage from './pages/Tracker/TrackerPage'
import ProfilePage from './pages/Profile/ProfilePage'
import LoginPage from './pages/Login/LoginPage'
import RegisterPage from './pages/Register/RegisterPage'

function App() {
    return (
        <>
            <Navbar />
            <Routes>
                <Route path="/" element={<Navigate to="/jobs" replace />} />
                <Route path="/jobs" element={<JobFeedPage />} />
                <Route path="/tracker" element={<TrackerPage />} />
                <Route path="/profile" element={<ProfilePage />} />
                <Route path="/login" element={<LoginPage />} />
                <Route path="/register" element={<RegisterPage />} />
            </Routes>
        </>
    )
}

export default App
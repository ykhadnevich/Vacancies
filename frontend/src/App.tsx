import { Routes, Route, Navigate } from 'react-router-dom'
import Navbar from './components/Navbar'
import JobFeedPage from './pages/JobFeed/JobFeedPage'
import TrackerPage from './pages/Tracker/TrackerPage'
import ProfilePage from './pages/Profile/ProfilePage'
import LoginPage from './pages/Login/LoginPage'
import RegisterPage from './pages/Register/RegisterPage'
import VacanciesPage from './pages/Recruiter/VacanciesPage'
import VacancyResultsPage from './pages/Recruiter/VacancyResultsPage'
import CandidateListsPage from './pages/Recruiter/CandidateListsPage'
import CandidateListDetailPage from './pages/Recruiter/CandidateListDetailPage'
import AboutPage from './pages/About/AboutPage'

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
                <Route path="/about" element={<AboutPage />} />
                {/* Recruiter cabinet — role enforcement is server-side via MediatR
                    behaviors. The nav only surfaces these routes when Role ∈ {Recruiter, Both},
                    but typing the URL directly still works for testing. */}
                <Route path="/recruiter/vacancies"       element={<VacanciesPage />} />
                <Route path="/recruiter/vacancy/:id"     element={<VacancyResultsPage />} />
                <Route path="/recruiter/lists"           element={<CandidateListsPage />} />
                <Route path="/recruiter/list/:id"        element={<CandidateListDetailPage />} />
            </Routes>
        </>
    )
}

export default App
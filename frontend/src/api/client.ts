import axios from 'axios'


const apiClient = axios.create({
    baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5180/api',
    timeout: 300_000,
    headers: {
        'Content-Type': 'application/json',
    },
})

apiClient.interceptors.request.use((config) => {
    const token = localStorage.getItem('token')
    if (token) {
        config.headers.Authorization = `Bearer ${token}`
    }
    return config
})

apiClient.interceptors.response.use(
    (response) => response,
    (error) => {
        if (error.response?.status === 401 && !window.location.pathname.startsWith('/login')) {
            localStorage.removeItem('token')
            localStorage.removeItem('userId')
            localStorage.removeItem('email')
            window.location.href = '/login'
        }
        return Promise.reject(error)
    },
)

export default apiClient

import { useState } from 'react'

interface Props {
    onSearch: (keywords: string, location: string | null) => void
    isLoading: boolean
}

function SearchBar({ onSearch, isLoading }: Props) {
    const [keywords, setKeywords] = useState('')

    const handleSubmit = () => {
        if (keywords.trim()) onSearch(keywords.trim(), null)
    }

    return (
        <div style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
            <input
                type="text"
                placeholder="Наприклад: .NET Developer"
                value={keywords}
                onChange={(e) => setKeywords(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSubmit()}
                style={{ flex: 1, padding: '10px 14px', fontSize: 16, borderRadius: 8, border: '1px solid #ccc' }}
            />
            <button
                onClick={handleSubmit}
                disabled={isLoading}
                style={{ padding: '10px 24px', fontSize: 16, borderRadius: 8, background: '#2563eb', color: '#fff', border: 'none', cursor: 'pointer' }}
            >
                {isLoading ? 'Шукаю...' : 'Пошук'}
            </button>
        </div>
    )
}

export default SearchBar

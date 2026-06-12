import { useState } from 'react'
import { useT } from '../i18n/useT'
import Icon from './ui/Icon'

interface Props {
    onSearch:   (keywords: string, location: string | null) => void
    isLoading:  boolean
}

function SearchBar({ onSearch, isLoading }: Props) {
    const [keywords, setKeywords] = useState('')
    const t = useT()

    const handleSubmit = () => {
        if (keywords.trim()) onSearch(keywords.trim(), null)
    }

    return (
        <div style={{ display: 'flex', gap: 8, width: '100%' }}>
            <div style={{ position: 'relative', flex: 1, minWidth: 0, display: 'flex', alignItems: 'center' }}>
                <span style={{
                    position: 'absolute',
                    left: 12,
                    color: 'var(--color-text-tertiary)',
                    pointerEvents: 'none',
                    display: 'flex',
                }}>
                    <Icon name="search" size={16} />
                </span>
                <input
                    type="text"
                    placeholder={t('search.placeholder')}
                    value={keywords}
                    onChange={(e) => setKeywords(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && handleSubmit()}
                    style={{
                        width:        '100%',
                        height:       42,
                        padding:      '0 14px 0 38px',
                        fontSize:     'var(--text-md)',
                        fontFamily:   'inherit',
                        color:        'var(--color-text-primary)',
                        background:   'var(--color-bg-surface)',
                        border:       '1px solid var(--color-border-default)',
                        borderRadius: 'var(--radius-md)',
                        outline:      'none',
                        transition:   'border-color var(--transition-fast), box-shadow var(--transition-fast)',
                    }}
                    onFocus={(e) => {
                        e.currentTarget.style.borderColor = 'var(--color-primary-500)'
                        e.currentTarget.style.boxShadow   = '0 0 0 3px var(--color-primary-100)'
                    }}
                    onBlur={(e) => {
                        e.currentTarget.style.borderColor = 'var(--color-border-default)'
                        e.currentTarget.style.boxShadow   = 'none'
                    }}
                />
            </div>
            <button
                onClick={handleSubmit}
                disabled={isLoading || !keywords.trim()}
                style={{
                    height:       42,
                    padding:      '0 22px',
                    fontSize:     'var(--text-md)',
                    fontWeight:   'var(--font-weight-medium)' as unknown as number,
                    borderRadius: 'var(--radius-md)',
                    background:   'var(--color-primary-600)',
                    color:        '#fff',
                    border:       'none',
                    cursor:       isLoading || !keywords.trim() ? 'not-allowed' : 'pointer',
                    opacity:      isLoading || !keywords.trim() ? 0.6 : 1,
                    fontFamily:   'inherit',
                    transition:   'opacity var(--transition-fast)',
                    whiteSpace:   'nowrap',
                }}
            >
                {isLoading ? t('search.searching') : t('search.button')}
            </button>
        </div>
    )
}

export default SearchBar

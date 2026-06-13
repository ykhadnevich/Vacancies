import { useState } from 'react'
import { useT } from '../i18n/useT'
import Icon from './ui/Icon'
import type { Country } from '../types/job'

interface Props {
    onSearch:         (keywords: string, location: string | null, country: Country) => void
    isLoading:        boolean
    country?:         Country
    onCountryChange?: (country: Country) => void
}

function SearchBar({ onSearch, isLoading, country, onCountryChange }: Props) {
    const [keywords, setKeywords] = useState('')
    const [internalCountry, setInternalCountry] = useState<Country>('Ukraine')
    const t = useT()

    const currentCountry: Country = country ?? internalCountry

    const COUNTRIES: { value: Country; label: string }[] = [
        { value: 'All',            label: t('country.all')           },
        { value: 'Ukraine',        label: t('country.ukraine')       },
        { value: 'UnitedStates',   label: t('country.unitedStates')  },
        { value: 'UnitedKingdom',  label: t('country.unitedKingdom') },
        { value: 'Germany',        label: t('country.germany')       },
        { value: 'Poland',         label: t('country.poland')        },
    ]

    const handleCountryChange = (next: Country) => {
        if (onCountryChange) onCountryChange(next)
        else setInternalCountry(next)
    }

    const handleSubmit = () => {
        if (keywords.trim()) onSearch(keywords.trim(), null, currentCountry)
    }

    return (
        <div style={{ display: 'flex', gap: 8, width: '100%' }}>
            <div style={{ position: 'relative', flexShrink: 0 }}>
                <select
                    aria-label={t('search.country')}
                    value={currentCountry}
                    onChange={(e) => handleCountryChange(e.target.value as Country)}
                    style={{
                        height:            42,
                        width:             195,
                        padding:           '0 36px 0 12px',
                        fontSize:          'var(--text-md)',
                        fontFamily:        'inherit',
                        color:             'var(--color-text-primary)',
                        background:        'var(--color-bg-surface)',
                        border:            '1px solid var(--color-border-default)',
                        borderRadius:      'var(--radius-md)',
                        outline:           'none',
                        cursor:            'pointer',
                        appearance:        'none',
                        WebkitAppearance:  'none',
                        MozAppearance:     'none',
                    }}
                >
                    {COUNTRIES.map((c) => (
                        <option key={c.value} value={c.value}>{c.label}</option>
                    ))}
                </select>
                <span style={{
                    position:      'absolute',
                    right:         12,
                    top:           '50%',
                    transform:     'translateY(-50%)',
                    pointerEvents: 'none',
                    color:         'var(--color-text-tertiary)',
                    display:       'flex',
                }}>
                    <Icon name="chevron-down" size={14} />
                </span>
            </div>
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

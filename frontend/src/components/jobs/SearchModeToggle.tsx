import { useT } from '../../i18n/useT'
import Icon from '../ui/Icon'

export type SearchMode = 'analyzed' | 'raw'

interface Props {
    value:    SearchMode
    onChange: (mode: SearchMode) => void
    disabled?: boolean
}

const BUTTON_WIDTH = 200


function SearchModeToggle({ value, onChange, disabled = false }: Props) {
    const t = useT()
    const OPTIONS: { value: SearchMode; label: string; hint: string; icon: 'sparkle' | 'search' }[] = [
        { value: 'analyzed', label: t('search.modeAnalyzed'), hint: t('search.modeAnalyzedHint'), icon: 'sparkle' },
        { value: 'raw',      label: t('search.modeRaw'),      hint: t('search.modeRawHint'),      icon: 'search'  },
    ]
    return (
        <div style={{ display: 'inline-flex', gap: 8 }}>
            {OPTIONS.map((opt) => {
                const active = opt.value === value
                return (
                    <button
                        key={opt.value}
                        onClick={() => onChange(opt.value)}
                        disabled={disabled}
                        title={`${opt.label} · ${opt.hint}`}
                        style={{
                            display:        'inline-flex',
                            alignItems:     'center',
                            justifyContent: 'center',
                            gap:            10,
                            width:          BUTTON_WIDTH,
                            height:         38,
                            padding:        '0 18px',
                            border:         `1px solid ${active ? 'var(--color-primary-600)' : 'var(--color-border-default)'}`,
                            background:     active ? 'var(--color-primary-50)' : 'var(--color-bg-surface)',
                            color:          active ? 'var(--color-primary-700)' : 'var(--color-text-secondary)',
                            borderRadius:   'var(--radius-md)',
                            fontSize:       'var(--text-sm)',
                            fontWeight:     (active ? 'var(--font-weight-medium)' : 'var(--font-weight-regular)') as unknown as number,
                            cursor:         disabled ? 'not-allowed' : 'pointer',
                            opacity:        disabled ? 0.6 : 1,
                            fontFamily:     'inherit',
                            transition:     'all var(--transition-fast)',
                            whiteSpace:     'nowrap',
                        }}
                    >
                        <Icon name={opt.icon} size={14} />
                        <span>{opt.label}</span>
                        <span style={{
                            color:       active ? 'var(--color-primary-600)' : 'var(--color-text-tertiary)',
                            opacity:     0.85,
                            fontSize:    'var(--text-xs)',
                            marginLeft:  4,
                        }}>
                            · {opt.hint}
                        </span>
                    </button>
                )
            })}
        </div>
    )
}

export default SearchModeToggle

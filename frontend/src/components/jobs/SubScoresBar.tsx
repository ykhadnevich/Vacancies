import { SUB_SCORE_ORDER, type SubScores } from '../../types/jobV6'
import { useLanguage } from '../../i18n/LanguageContext'
import type { TranslationKey } from '../../i18n/translations'
import { useT } from '../../i18n/useT'

interface Props {
    subScores: SubScores

    compact?:  boolean
}

const SUB_SCORE_KEY_MAP: Record<string, TranslationKey> = {
    skill_match:       'subscore.skill',
    role_intent_match: 'subscore.role',
    seniority_match:   'subscore.seniority',
    experience_match:  'subscore.experience',
    domain_alignment:  'subscore.domain',
    language_match:    'subscore.language',
    education_match:   'subscore.education',
}

function barColor(value: number): string {
    if (value >= 0.75) return 'var(--color-success-500)'
    if (value >= 0.50) return 'var(--color-info-500)'
    if (value >= 0.25) return 'var(--color-warning-500)'
    return 'var(--color-danger-500)'
}

function SubScoresBar({ subScores, compact = false }: Props) {
    const keys = compact ? SUB_SCORE_ORDER.slice(0, 4) : SUB_SCORE_ORDER
    const t = useT()
    useLanguage()

    return (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: 8 }}>
            {keys.map((key) => {
                const value = subScores[key] ?? 0
                const pct   = Math.round(value * 100)
                return (
                    <div key={key} style={{
                        display:             'grid',
                        gridTemplateColumns: '88px 1fr 40px',
                        alignItems:          'center',
                        gap:                 12,
                    }}>
                        <span style={{
                            fontSize: 'var(--text-sm)',
                            color:    'var(--color-text-secondary)',
                        }}>
                            {SUB_SCORE_KEY_MAP[key] ? t(SUB_SCORE_KEY_MAP[key]) : key}
                        </span>
                        <div style={{
                            position:     'relative',
                            height:       6,
                            background:   'var(--color-bg-muted)',
                            borderRadius: 'var(--radius-pill)',
                            overflow:     'hidden',
                        }}>
                            <div style={{
                                position:     'absolute',
                                inset:        0,
                                width:        `${pct}%`,
                                background:   barColor(value),
                                borderRadius: 'var(--radius-pill)',
                                transition:   'width var(--transition-slow)',
                            }} />
                        </div>
                    </div>
                )
            })}
        </div>
    )
}

export default SubScoresBar

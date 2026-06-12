import { useT } from '../../i18n/useT'
import type { TranslationKey } from '../../i18n/translations'

interface Props {
    subScores: Record<string, number>
}

const SUB_SCORE_ORDER: string[] = [
    'skill_match',
    'role_intent_match',
    'seniority_match',
    'experience_match',
    'domain_alignment',
    'language_match',
    'education_match',
]

const KEY_MAP: Record<string, TranslationKey> = {
    skill_match:       'subscore.skill',
    role_intent_match: 'subscore.role',
    seniority_match:   'subscore.seniority',
    experience_match:  'subscore.experience',
    domain_alignment:  'subscore.domain',
    language_match:    'subscore.language',
    education_match:   'subscore.education',
}

const TOOLTIP_MAP: Record<string, TranslationKey> = {
    skill_match:       'subscore.tooltip.skill',
    role_intent_match: 'subscore.tooltip.role',
    seniority_match:   'subscore.tooltip.seniority',
    experience_match:  'subscore.tooltip.experience',
    domain_alignment:  'subscore.tooltip.domain',
    language_match:    'subscore.tooltip.language',
    education_match:   'subscore.tooltip.education',
}

function barColor(value: number): string {
    if (value >= 0.75) return 'var(--color-success-500)'
    if (value >= 0.5)  return 'var(--color-info-500)'
    if (value >= 0.25) return 'var(--color-warning-500)'
    return 'var(--color-danger-500)'
}

/** Compact sub-score visualisation reused inside the candidate drawer. */
function RecruiterSubScoresBar({ subScores }: Props) {
    const t = useT()
    return (
        <div style={{ display: 'grid', gap: 8 }}>
            {SUB_SCORE_ORDER.map((key) => {
                const value = subScores[key] ?? 0
                const pct = Math.round(value * 100)
                const tooltip = TOOLTIP_MAP[key] ? t(TOOLTIP_MAP[key]) : undefined
                return (
                    <div
                        key={key}
                        title={tooltip}
                        style={{
                            display: 'grid',
                            gridTemplateColumns: '100px 1fr 44px',
                            alignItems: 'center',
                            gap: 12,
                            cursor: tooltip ? 'help' : 'default',
                        }}
                    >
                        <span
                            style={{
                                fontSize: 'var(--text-sm)',
                                color: 'var(--color-text-secondary)',
                                textDecoration: tooltip ? 'underline dotted var(--color-border)' : 'none',
                                textUnderlineOffset: 3,
                            }}
                        >
                            {KEY_MAP[key] ? t(KEY_MAP[key]) : key}
                        </span>
                        <div
                            style={{
                                position: 'relative',
                                height: 6,
                                background: 'var(--color-bg-muted)',
                                borderRadius: 'var(--radius-pill)',
                                overflow: 'hidden',
                            }}
                        >
                            <div
                                style={{
                                    position: 'absolute',
                                    inset: 0,
                                    width: `${pct}%`,
                                    background: barColor(value),
                                    borderRadius: 'var(--radius-pill)',
                                    transition: 'width var(--transition-slow)',
                                }}
                            />
                        </div>
                        <span
                            style={{
                                fontSize: 'var(--text-xs)',
                                color: 'var(--color-text-tertiary)',
                                fontVariantNumeric: 'tabular-nums',
                                textAlign: 'right',
                            }}
                        >
                            {pct}%
                        </span>
                    </div>
                )
            })}
        </div>
    )
}

export default RecruiterSubScoresBar

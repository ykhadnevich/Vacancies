import { useEffect, useState } from 'react'
import { useT } from '../../i18n/useT'
import type { TranslationKey } from '../../i18n/translations'
import { verdictColor, type VerdictColor } from '../../utils/verdict'

interface VerdictMeta {
    color:    VerdictColor
    labelKey: TranslationKey
}

function verdictMeta(verdict: string): VerdictMeta {
    const color = verdictColor(verdict)
    const v = verdict.toLowerCase()
    if (v.startsWith('strong'))
        return { color, labelKey: 'scoring.verdict.strong'      }
    if (v.startsWith('partial'))
        return { color, labelKey: 'scoring.verdict.partial'     }
    if (v.startsWith('weak'))
        return { color, labelKey: 'scoring.verdict.weak'        }
    if (v.includes('mismatch') || v.includes('notrelevant') || v.includes('not_relevant'))
        return { color, labelKey: 'scoring.verdict.notRelevant' }
    return     { color, labelKey: 'scoring.verdict.notRelevant' }
}

const LABEL_COLOR: Record<VerdictColor, string> = {
    success: 'var(--color-success-700)',
    warning: 'var(--color-warning-700)',
    danger:  'var(--color-danger-700)',
    neutral: 'var(--color-text-tertiary)',
}

interface ScoreWidgetProps {
    score:       number
    verdict:     string
    confidence?: number
}

function ScoreWidget({ score, verdict, confidence }: ScoreWidgetProps) {
    const t = useT()
    const [filled, setFilled] = useState(false)
    useEffect(() => { setFilled(true) }, [])

    const meta          = verdictMeta(verdict)
    const lowConfidence = typeof confidence === 'number' && confidence < 0.7

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 8 }}>
                <span style={{
                    fontSize:      'var(--text-xs)',
                    fontWeight:    600,
                    letterSpacing: '0.08em',
                    textTransform: 'uppercase',
                    color:         LABEL_COLOR[meta.color],
                }}>
                    {t(meta.labelKey)}
                </span>
                <span style={{
                    fontFamily:         'var(--font-mono)',
                    fontSize:           'var(--text-xl)',
                    fontWeight:         700,
                    fontVariantNumeric: 'tabular-nums',
                    lineHeight:         1,
                    color: lowConfidence
                        ? 'var(--color-text-secondary)'
                        : 'var(--color-text-primary)',
                }}>
                    {(score * 100).toFixed(1)}%
                </span>
            </div>

            <div style={{
                height:       6,
                borderRadius: 3,
                background:   'var(--score-bar-track)',
                overflow:     'hidden',
            }}>
                <div style={{
                    height:     '100%',
                    width:      filled ? `${score * 100}%` : '0%',
                    borderRadius: 3,
                    background: 'linear-gradient(to right, var(--score-bar-fill-start), var(--score-bar-fill-end))',
                    transition: `width var(--transition-score)`,
                }} />
            </div>
        </div>
    )
}

export default ScoreWidget

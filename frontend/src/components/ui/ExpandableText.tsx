import { useState, type CSSProperties } from 'react'
import { useT } from '../../i18n/useT'

/**
 * Long-form text with a "Show more / Show less" toggle.
 *
 * Used on candidate-card reason text where a single Gemini-generated
 * paragraph can run 6–12 lines and crowds the rest of the drawer.
 * Below the threshold we render the full content with no toggle, so
 * short reasons are unaffected.
 */
interface Props {
    text:        string
    /** Word count above which the text is collapsed by default. Default 50 words ≈ 4 lines at typical drawer width. */
    threshold?:  number
    style?:      CSSProperties
}

function wordCount(s: string): number {
    return s.trim().split(/\s+/).length
}

function ExpandableText({ text, threshold = 50, style }: Props) {
    const t = useT()
    const [expanded, setExpanded] = useState(false)
    const long = wordCount(text) > threshold

    return (
        <div
            style={{
                fontSize:   'var(--text-md)',
                color:      'var(--color-text-primary)',
                lineHeight: 1.55,
                ...style,
            }}
        >
            <p
                style={{
                    margin: 0,
                    display:           expanded || !long ? 'block' : '-webkit-box',
                    WebkitLineClamp:   expanded || !long ? undefined : 4,
                    WebkitBoxOrient:   'vertical' as unknown as undefined,
                    overflow:          expanded || !long ? 'visible' : 'hidden',
                }}
            >
                {text}
            </p>
            {long && (
                <button
                    onClick={() => setExpanded((v) => !v)}
                    style={{
                        marginTop:    6,
                        padding:      0,
                        background:   'transparent',
                        border:       'none',
                        cursor:       'pointer',
                        color:        'var(--color-primary-600)',
                        fontSize:     'var(--text-sm)',
                        fontWeight:   'var(--font-weight-medium)' as unknown as number,
                        fontFamily:   'inherit',
                    }}
                >
                    {expanded ? t('common.showLess') : t('common.showMore')}
                </button>
            )}
        </div>
    )
}

export default ExpandableText

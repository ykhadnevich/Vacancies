import { useLanguage } from '../../i18n/LanguageContext'

interface Section {
    en: string | null
    uk: string | null
}

interface Props {
    strengths:      Section
    gaps:           Section
    recommendation: Section

    flat?: Section
}


function BilingualReason({ strengths, gaps, recommendation, flat }: Props) {
    const { language } = useLanguage()

    const pick = (s: Section) => (language === 'uk' ? s.uk : s.en) ?? (language === 'uk' ? s.en : s.uk)

    const s = pick(strengths)
    const g = pick(gaps)
    const r = pick(recommendation)
    const f = flat ? pick(flat) : null


    const hasStructured = !!(s || g || r)

    if (!hasStructured && f) {
        return (
            <p style={{
                fontSize: 'var(--text-md)',
                lineHeight: 'var(--line-height-relaxed)',
                color: 'var(--color-text-secondary)',
                fontStyle: 'italic',
                margin: 0,
            }}>
                {f}
            </p>
        )
    }

    if (!hasStructured) return null

    const sectionStyle: React.CSSProperties = {
        display: 'flex',
        flexDirection: 'column',
        gap: 4,
    }

    const labelStyle: React.CSSProperties = {
        fontSize: 'var(--text-xs)',
        textTransform: 'uppercase',
        letterSpacing: '0.04em',
        fontWeight: 'var(--font-weight-medium)' as unknown as number,
    }

    const bodyStyle: React.CSSProperties = {
        fontSize: 'var(--text-md)',
        lineHeight: 'var(--line-height-relaxed)',
        color: 'var(--color-text-primary)',
    }

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            {s && (
                <div style={sectionStyle}>
                    <span style={{ ...labelStyle, color: 'var(--color-success-600)' }}>
                        {language === 'uk' ? 'Переваги' : 'Strengths'}
                    </span>
                    <span style={bodyStyle}>{s}</span>
                </div>
            )}
            {g && (
                <div style={sectionStyle}>
                    <span style={{ ...labelStyle, color: 'var(--color-danger-600)' }}>
                        {language === 'uk' ? 'Прогалини' : 'Gaps'}
                    </span>
                    <span style={bodyStyle}>{g}</span>
                </div>
            )}
            {r && (
                <div style={sectionStyle}>
                    <span style={{ ...labelStyle, color: 'var(--color-primary-600)' }}>
                        {language === 'uk' ? 'Рекомендація' : 'Recommendation'}
                    </span>
                    <span style={bodyStyle}>{r}</span>
                </div>
            )}
        </div>
    )
}

export default BilingualReason

import { useT } from '../../i18n/useT'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Icon from '../../components/ui/Icon'

const AXIS_KEYS = [
    'about.section.howScoring.axis.skill',
    'about.section.howScoring.axis.role',
    'about.section.howScoring.axis.seniority',
    'about.section.howScoring.axis.experience',
    'about.section.howScoring.axis.domain',
    'about.section.howScoring.axis.language',
    'about.section.howScoring.axis.education',
] as const

const eyebrow: React.CSSProperties = {
    fontFamily: 'var(--font-sans)', fontSize: 'var(--text-xs)', fontWeight: 600,
    letterSpacing: '0.14em', textTransform: 'uppercase', color: 'var(--color-primary-600)',
    marginBottom: 10,
}
const h2: React.CSSProperties = {
    fontFamily: 'var(--font-serif)', fontSize: 'var(--text-2xl)', fontWeight: 600,
    letterSpacing: '-0.01em', margin: 0, color: 'var(--color-text-primary)',
}
const body: React.CSSProperties = {
    margin: '10px 0 0', fontSize: 'var(--text-md)', lineHeight: 1.65,
    color: 'var(--color-text-secondary)', maxWidth: 660,
}

function AboutPage() {
    const t = useT()
    return (
        <main style={{ maxWidth: 'var(--max-width-content)', margin: '0 auto', padding: '52px 24px 80px', color: 'var(--color-text-primary)' }}>
            <header style={{ marginBottom: 40, borderBottom: '1px solid var(--color-border-default)', paddingBottom: 32 }}>
                <div style={{ ...eyebrow, color: 'var(--color-text-tertiary)' }}>Methodology · public</div>
                <h1 style={{ margin: 0, fontSize: 'var(--display-lg)', lineHeight: 1.05, maxWidth: 720 }}>
                    {t('about.title')}
                </h1>
                <p style={{ margin: '18px 0 0', fontFamily: 'var(--font-serif)', fontSize: 'var(--text-xl)', fontStyle: 'italic', lineHeight: 1.5, color: 'var(--color-text-secondary)', maxWidth: 640 }}>
                    {t('about.subtitle')}
                </p>
            </header>

            <section style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
                <Card padding="lg">
                    <div style={eyebrow}>01 — {t('about.section.howScoring.title')}</div>
                    <h2 style={h2}>{t('about.section.howScoring.title')}</h2>
                    <p style={body}>{t('about.section.howScoring.body')}</p>
                    <div style={{ marginTop: 20 }}>
                        {AXIS_KEYS.map((k, i) => (
                            <div key={k} style={{ display: 'grid', gridTemplateColumns: '34px 1fr', gap: 14, alignItems: 'baseline', padding: '11px 0', borderTop: '1px solid var(--color-border-subtle)' }}>
                                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-sm)', fontVariantNumeric: 'tabular-nums', color: 'var(--color-text-tertiary)' }}>
                                    {String(i + 1).padStart(2, '0')}
                                </span>
                                <span style={{ fontSize: 'var(--text-sm)', lineHeight: 1.55, color: 'var(--color-text-secondary)' }}>
                                    {t(k)}
                                </span>
                            </div>
                        ))}
                    </div>
                </Card>

                <Card padding="lg">
                    <div style={eyebrow}>02 — {t('about.section.calibration.title')}</div>
                    <h2 style={h2}>{t('about.section.calibration.title')}</h2>
                    <p style={body}>{t('about.section.calibration.body')}</p>
                    <div style={{ marginTop: 16, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                        <Badge color="success" size="md">
                            <Icon name="check-circle" size={12} /> ECE 0.141 → 0.025
                        </Badge>
                        <Badge color="info" size="md">N = 398 held-out pairs</Badge>
                        <Badge color="info" size="md">Isotonic regression (PAV)</Badge>
                    </div>
                </Card>

                <Card padding="lg">
                    <div style={eyebrow}>03 — {t('about.section.evidence.title')}</div>
                    <h2 style={h2}>{t('about.section.evidence.title')}</h2>
                    <p style={body}>{t('about.section.evidence.body')}</p>
                    <div style={{ marginTop: 20, display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 12 }}>
                        <Stat label={t('about.metric.spearman')} value="0.65" hint="[0.58, 0.72]" />
                        <Stat label={t('about.metric.ndcg5')}    value="0.82" hint="per-CV avg" />
                        <Stat label={t('about.metric.midrange')} value="0.74" hint="n = 110" />
                        <Stat label={t('about.metric.ece')}      value="0.025" hint="5-fold CV" />
                    </div>
                </Card>

                <Card padding="lg" style={{ background: 'var(--color-bg-muted)' }}>
                    <div style={eyebrow}>04 — {t('about.section.transparency.title')}</div>
                    <h2 style={h2}>{t('about.section.transparency.title')}</h2>
                    <p style={{ ...body, fontFamily: 'var(--font-serif)', fontSize: 'var(--text-xl)', fontStyle: 'italic', color: 'var(--color-text-primary)', maxWidth: 680 }}>
                        {t('about.section.transparency.body')}
                    </p>
                </Card>
            </section>

            <footer style={{ marginTop: 32, paddingTop: 18, borderTop: '1px solid var(--color-border-default)', fontSize: 'var(--text-sm)', color: 'var(--color-text-tertiary)', lineHeight: 1.6, maxWidth: 680 }}>
                {t('about.footer')}
            </footer>
        </main>
    )
}

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
    return (
        <div style={{ padding: '16px 18px', background: 'var(--color-bg-elevated)', border: '1px solid var(--color-border-default)', borderRadius: 'var(--radius-md)', boxShadow: 'var(--shadow-xs)' }}>
            <div style={{ fontSize: 'var(--text-xs)', letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--color-text-tertiary)' }}>
                {label}
            </div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 32, fontWeight: 700, fontVariantNumeric: 'tabular-nums', letterSpacing: '-0.02em', color: 'var(--color-primary-600)', marginTop: 6, lineHeight: 1 }}>
                {value}
            </div>
            {hint && (
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-xs)', color: 'var(--color-text-tertiary)', marginTop: 5 }}>
                    {hint}
                </div>
            )}
        </div>
    )
}

export default AboutPage

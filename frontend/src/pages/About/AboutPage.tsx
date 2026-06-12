import { useT } from '../../i18n/useT'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Icon from '../../components/ui/Icon'

/**
 * Public methodology / "how it works" page.
 *
 * Three readers: candidates (build trust before they upload a CV), recruiters
 * (build trust before they create a vacancy + candidate list), and academic
 * reviewers reading the thesis (gives a one-page summary they can cite).
 *
 * Content is intentionally light on jargon and heavy on transparency about
 * what the system measures and how. Numbers are exact, sourced from the
 * 398-pair held-out evaluation documented in `thesis/template/main/06-validation.typ`.
 */
function AboutPage() {
    const t = useT()
    return (
        <main
            style={{
                maxWidth: 920,
                margin: '0 auto',
                padding: '40px 24px 80px',
                color: 'var(--color-text-primary)',
            }}
        >
            <header style={{ marginBottom: 32 }}>
                <h1 style={{ margin: 0, fontSize: 'var(--text-3xl)', lineHeight: 1.2 }}>
                    {t('about.title')}
                </h1>
                <p
                    style={{
                        marginTop: 12,
                        fontSize: 'var(--text-md)',
                        color: 'var(--color-text-secondary)',
                        maxWidth: 680,
                        lineHeight: 1.6,
                    }}
                >
                    {t('about.subtitle')}
                </p>
            </header>

            <section style={{ display: 'grid', gap: 16, marginBottom: 32 }}>
                <Card>
                    <div style={{ padding: '20px 24px' }}>
                        <h2 style={{ margin: 0, fontSize: 'var(--text-lg)' }}>
                            {t('about.section.howScoring.title')}
                        </h2>
                        <p
                            style={{
                                marginTop: 8,
                                fontSize: 'var(--text-md)',
                                color: 'var(--color-text-secondary)',
                                lineHeight: 1.6,
                            }}
                        >
                            {t('about.section.howScoring.body')}
                        </p>
                        <ul
                            style={{
                                marginTop: 12,
                                paddingLeft: 18,
                                color: 'var(--color-text-secondary)',
                                lineHeight: 1.8,
                            }}
                        >
                            <li>{t('about.section.howScoring.axis.skill')}</li>
                            <li>{t('about.section.howScoring.axis.role')}</li>
                            <li>{t('about.section.howScoring.axis.seniority')}</li>
                            <li>{t('about.section.howScoring.axis.experience')}</li>
                            <li>{t('about.section.howScoring.axis.domain')}</li>
                            <li>{t('about.section.howScoring.axis.language')}</li>
                            <li>{t('about.section.howScoring.axis.education')}</li>
                        </ul>
                    </div>
                </Card>

                <Card>
                    <div style={{ padding: '20px 24px' }}>
                        <h2 style={{ margin: 0, fontSize: 'var(--text-lg)' }}>
                            {t('about.section.calibration.title')}
                        </h2>
                        <p
                            style={{
                                marginTop: 8,
                                fontSize: 'var(--text-md)',
                                color: 'var(--color-text-secondary)',
                                lineHeight: 1.6,
                            }}
                        >
                            {t('about.section.calibration.body')}
                        </p>
                        <div style={{ marginTop: 12, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                            <Badge color="success" size="md">
                                <Icon name="check-circle" size={12} /> ECE 0.141 → 0.025
                            </Badge>
                            <Badge color="info" size="md">N = 398 held-out pairs</Badge>
                            <Badge color="info" size="md">Isotonic regression (PAV)</Badge>
                        </div>
                    </div>
                </Card>

                <Card>
                    <div style={{ padding: '20px 24px' }}>
                        <h2 style={{ margin: 0, fontSize: 'var(--text-lg)' }}>
                            {t('about.section.evidence.title')}
                        </h2>
                        <p
                            style={{
                                marginTop: 8,
                                fontSize: 'var(--text-md)',
                                color: 'var(--color-text-secondary)',
                                lineHeight: 1.6,
                            }}
                        >
                            {t('about.section.evidence.body')}
                        </p>
                        <div
                            style={{
                                marginTop: 16,
                                display: 'grid',
                                gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
                                gap: 12,
                            }}
                        >
                            <Stat label={t('about.metric.spearman')} value="0.65" hint="[0.58, 0.72]" />
                            <Stat label={t('about.metric.ndcg5')} value="0.82" hint="per-CV avg" />
                            <Stat label={t('about.metric.midrange')} value="0.74" hint="n = 110" />
                            <Stat label={t('about.metric.ece')} value="0.025" hint="5-fold CV" />
                        </div>
                    </div>
                </Card>

                <Card>
                    <div style={{ padding: '20px 24px' }}>
                        <h2 style={{ margin: 0, fontSize: 'var(--text-lg)' }}>
                            {t('about.section.transparency.title')}
                        </h2>
                        <p
                            style={{
                                marginTop: 8,
                                fontSize: 'var(--text-md)',
                                color: 'var(--color-text-secondary)',
                                lineHeight: 1.6,
                            }}
                        >
                            {t('about.section.transparency.body')}
                        </p>
                    </div>
                </Card>
            </section>

            <footer
                style={{
                    fontSize: 'var(--text-sm)',
                    color: 'var(--color-text-tertiary)',
                    borderTop: '1px solid var(--color-border)',
                    paddingTop: 16,
                }}
            >
                {t('about.footer')}
            </footer>
        </main>
    )
}

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
    return (
        <div
            style={{
                padding: '12px 16px',
                background: 'var(--color-bg-muted)',
                borderRadius: 'var(--radius-md)',
            }}
        >
            <div style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-tertiary)' }}>
                {label}
            </div>
            <div
                style={{
                    fontSize: 'var(--text-xl)',
                    fontWeight: 600,
                    fontVariantNumeric: 'tabular-nums',
                    marginTop: 4,
                }}
            >
                {value}
            </div>
            {hint && (
                <div style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-tertiary)', marginTop: 2 }}>
                    {hint}
                </div>
            )}
        </div>
    )
}

export default AboutPage

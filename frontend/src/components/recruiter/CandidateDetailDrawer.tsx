import { useEffect } from 'react'
import { useLanguage } from '../../i18n/LanguageContext'
import { useT } from '../../i18n/useT'
import type { CandidateAnalysisResultDto } from '../../types/recruiter'
import Badge from '../ui/Badge'
import Icon from '../ui/Icon'
import RecruiterSubScoresBar from './RecruiterSubScoresBar'
import { formatRelative, formatAbsolute } from '../../utils/formatDate'
import ExpandableText from '../ui/ExpandableText'
import { verdictColor } from '../../utils/verdict'

interface Props {
    result: CandidateAnalysisResultDto | null
    onClose: () => void
}

function CandidateDetailDrawer({ result, onClose }: Props) {
    const { language } = useLanguage()
    const t = useT()

    useEffect(() => {
        if (!result) return
        const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
        window.addEventListener('keydown', onKey)
        const prev = document.body.style.overflow
        document.body.style.overflow = 'hidden'
        return () => {
            window.removeEventListener('keydown', onKey)
            document.body.style.overflow = prev
        }
    }, [result, onClose])

    const isOpen = !!result

    return (
        <>
            <div
                onClick={onClose}
                style={{
                    position: 'fixed',
                    inset: 0,
                    background: 'rgba(26, 31, 54, 0.32)',
                    opacity: isOpen ? 1 : 0,
                    pointerEvents: isOpen ? 'auto' : 'none',
                    transition: 'opacity var(--transition-base)',
                    zIndex: 'var(--z-drawer)' as unknown as number,
                }}
                aria-hidden="true"
            />

            <aside
                role="dialog"
                aria-modal="true"
                style={{
                    position: 'fixed',
                    top: 0,
                    right: 0,
                    bottom: 0,
                    width: 'min(560px, 100vw)',
                    background: 'var(--color-bg-surface)',
                    boxShadow: 'var(--shadow-xl)',
                    transform: isOpen ? 'translateX(0)' : 'translateX(100%)',
                    transition: 'transform var(--transition-slow)',
                    zIndex: 'calc(var(--z-drawer) + 1)' as unknown as number,
                    display: 'flex',
                    flexDirection: 'column',
                    overflowY: 'auto',
                }}
            >
                {result && (
                    <>
                        <header
                            style={{
                                position: 'sticky',
                                top: 0,
                                background: 'var(--color-bg-surface)',
                                borderBottom: '0.5px solid var(--color-border-default)',
                                padding: '20px 24px 16px',
                                display: 'flex',
                                flexDirection: 'column',
                                gap: 10,
                            }}
                        >
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
                                <h2 style={{ fontSize: 'var(--text-xl)', margin: 0, lineHeight: 'var(--line-height-tight)' }}>
                                    {result.candidateName || result.candidateId.slice(0, 8)}
                                </h2>
                                <button
                                    onClick={onClose}
                                    aria-label={t('common.close')}
                                    style={{
                                        background: 'transparent',
                                        border: 'none',
                                        cursor: 'pointer',
                                        padding: 6,
                                        borderRadius: 'var(--radius-md)',
                                        color: 'var(--color-text-secondary)',
                                        display: 'flex',
                                    }}
                                >
                                    <Icon name="close" size={18} />
                                </button>
                            </div>
                            <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                                <Badge color={verdictColor(result.verdict)} size="md">
                                    <span style={{ fontVariantNumeric: 'tabular-nums' }}>
                                        {(result.score * 100).toFixed(1)}%
                                    </span>
                                </Badge>
                                {typeof result.confidence === 'number' && result.confidence < 0.7 && (
                                    <Badge
                                        color={result.confidence < 0.5 ? 'danger' : 'warning'}
                                        size="sm"
                                        title={t('recruiter.confidence.tooltip')}
                                    >
                                        <Icon name="alert-circle" size={11} />
                                        {' '}{t('recruiter.confidence.label')} {(result.confidence * 100).toFixed(0)}%
                                    </Badge>
                                )}
                                {result.antiFlagPenalty < 1 && (
                                    <Badge color="danger" size="sm" title={`anti-flag penalty: ${result.antiFlagPenalty}`}>
                                        <Icon name="alert-circle" size={11} /> ×{result.antiFlagPenalty.toFixed(1)}
                                    </Badge>
                                )}
                                <Badge color="neutral" size="sm" title="Gemini tokens: input → output">
                                    <span style={{ fontVariantNumeric: 'tabular-nums' }}>
                                        {result.inputTokens.toLocaleString()} → {result.outputTokens.toLocaleString()} tok
                                    </span>
                                </Badge>
                                <Badge color="neutral" size="sm" title="Estimated Gemini cost (input $0.30/M + output $2.50/M)">
                                    <span style={{ fontVariantNumeric: 'tabular-nums' }}>
                                        ${result.estimatedCostUsd.toFixed(4)}
                                    </span>
                                </Badge>
                                {/* Calibrated-score trust badge — shown only when the production scoring
                                    service has applied the post-hoc isotonic calibrator (raw composite ⇒
                                    held-out-calibrated percentage). The marker is the "+cal:" suffix that
                                    RecruiterMonolithicScoringService appends to ModelVersion when
                                    IScoreCalibrator.IsEnabled returns true. */}
                                {result.modelVersion?.includes('+cal:') && (
                                    <Badge
                                        color="success"
                                        size="sm"
                                        title={t('recruiter.calibratedTooltip')}
                                    >
                                        <Icon name="check-circle" size={11} />
                                        {' '}{t('recruiter.calibratedBadge')}
                                    </Badge>
                                )}
                            </div>
                            <div
                                title={formatAbsolute(result.scoredAt, language)}
                                style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-tertiary)' }}
                            >
                                {result.modelVersion} · {formatRelative(result.scoredAt, language)}
                            </div>
                        </header>

                        <div style={{ padding: '20px 24px', display: 'flex', flexDirection: 'column', gap: 18 }}>
                            {(language === 'uk' ? result.reasonUk : result.reasonEn) && (
                                <section>
                                    <ExpandableText
                                        text={(language === 'uk' ? result.reasonUk : result.reasonEn) ?? ''}
                                        threshold={50}
                                    />
                                </section>
                            )}

                            <section>
                                <h3 style={sectionTitle}>{t('reason.scoreBreakdown')}</h3>
                                <RecruiterSubScoresBar subScores={result.subScores} />
                            </section>

                            {result.matchedSkills.length > 0 && (
                                <section>
                                    <h3 style={sectionTitle}>{t('recruiter.results.matched')}</h3>
                                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                                        {result.matchedSkills.map((s) => (
                                            <Badge key={`m-${s}`} color="success" size="sm">{s}</Badge>
                                        ))}
                                    </div>
                                </section>
                            )}

                            {result.missingMustHaves.length > 0 && (
                                <section>
                                    <h3 style={sectionTitle}>{t('recruiter.results.missing')}</h3>
                                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                                        {result.missingMustHaves.map((s) => (
                                            <Badge key={`x-${s}`} color="warning" size="sm">— {s}</Badge>
                                        ))}
                                    </div>
                                </section>
                            )}

                            {result.triggeredAntiFlags.length > 0 && (
                                <section>
                                    <h3 style={sectionTitle}>Anti-flags</h3>
                                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                                        {result.triggeredAntiFlags.map((s) => (
                                            <Badge key={`a-${s}`} color="danger" size="sm">{s}</Badge>
                                        ))}
                                    </div>
                                </section>
                            )}
                        </div>
                    </>
                )}
            </aside>
        </>
    )
}

const sectionTitle: React.CSSProperties = {
    margin: '0 0 8px',
    fontSize: 'var(--text-xs)',
    textTransform: 'uppercase',
    letterSpacing: '0.06em',
    color: 'var(--color-text-tertiary)',
    fontWeight: 600,
}

export default CandidateDetailDrawer

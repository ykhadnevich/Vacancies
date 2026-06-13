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
                    background: 'rgba(20, 17, 11, 0.42)',
                    backdropFilter: 'blur(1.5px)',
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
                    width: 'clamp(min(100vw, 440px), 50vw, 860px)',
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
                                borderBottom: '1px solid var(--color-border-default)',
                                padding: '22px 28px 18px',
                                display: 'flex',
                                flexDirection: 'column',
                                gap: 12,
                            }}
                        >
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
                                <div style={{ minWidth: 0 }}>
                                    <span style={{
                                        fontFamily: 'var(--font-sans)', fontSize: 'var(--text-xs)', fontWeight: 600,
                                        letterSpacing: '0.08em', textTransform: 'uppercase',
                                        color: `var(--color-${verdictColor(result.verdict)}-700)`,
                                    }}>
                                        {(result.score * 100).toFixed(1)}%
                                    </span>
                                    <h2 style={{
                                        fontFamily: 'var(--font-serif)', fontSize: 'var(--text-3xl)', margin: '4px 0 0',
                                        lineHeight: 1.1, letterSpacing: '-0.02em',
                                    }}>
                                        {result.candidateName || result.candidateId.slice(0, 8)}
                                    </h2>
                                </div>
                                <button
                                    onClick={onClose}
                                    aria-label={t('common.close')}
                                    style={{
                                        background: 'transparent', border: '1px solid var(--color-border-default)',
                                        cursor: 'pointer', padding: 8, borderRadius: 'var(--radius-md)',
                                        color: 'var(--color-text-secondary)', display: 'flex', flexShrink: 0,
                                    }}
                                >
                                    <Icon name="close" size={18} />
                                </button>
                            </div>
                            <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                                {typeof result.confidence === 'number' && result.confidence < 0.7 && (
                                    <Badge color={result.confidence < 0.5 ? 'danger' : 'warning'} size="sm" title={t('recruiter.confidence.tooltip')}>
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
                                    <span style={{ fontVariantNumeric: 'tabular-nums', fontFamily: 'var(--font-mono)' }}>
                                        {result.inputTokens.toLocaleString()} → {result.outputTokens.toLocaleString()} tok
                                    </span>
                                </Badge>
                                <Badge color="neutral" size="sm" title="Estimated Gemini cost (input $0.30/M + output $2.50/M)">
                                    <span style={{ fontVariantNumeric: 'tabular-nums', fontFamily: 'var(--font-mono)' }}>
                                        ${result.estimatedCostUsd.toFixed(4)}
                                    </span>
                                </Badge>
                                {result.modelVersion?.includes('+cal:') && (
                                    <Badge color="success" size="sm" title={t('recruiter.calibratedTooltip')}>
                                        <Icon name="check-circle" size={11} />
                                        {' '}{t('recruiter.calibratedBadge')}
                                    </Badge>
                                )}
                            </div>
                            <div title={formatAbsolute(result.scoredAt, language)} style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono)' }}>
                                {result.modelVersion} · {formatRelative(result.scoredAt, language)}
                            </div>
                        </header>

                        <div style={{ padding: '22px 28px 40px', display: 'flex', flexDirection: 'column', gap: 22 }}>
                            {(language === 'uk' ? result.reasonUk : result.reasonEn) && (
                                <section style={{
                                    fontFamily: 'var(--font-serif)', fontSize: 'var(--text-xl)', lineHeight: 1.55,
                                    color: 'var(--color-text-primary)', letterSpacing: '-0.01em',
                                }}>
                                    <ExpandableText text={(language === 'uk' ? result.reasonUk : result.reasonEn) ?? ''} threshold={50} />
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
                                        {result.matchedSkills.map((s) => (<Badge key={`m-${s}`} color="success" size="sm">{s}</Badge>))}
                                    </div>
                                </section>
                            )}

                            {result.missingMustHaves.length > 0 && (
                                <section>
                                    <h3 style={sectionTitle}>{t('recruiter.results.missing')}</h3>
                                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                                        {result.missingMustHaves.map((s) => (<Badge key={`x-${s}`} color="warning" size="sm">— {s}</Badge>))}
                                    </div>
                                </section>
                            )}

                            {result.triggeredAntiFlags.length > 0 && (
                                <section>
                                    <h3 style={sectionTitle}>Anti-flags</h3>
                                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                                        {result.triggeredAntiFlags.map((s) => (<Badge key={`a-${s}`} color="danger" size="sm">{s}</Badge>))}
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
    margin: '0 0 10px',
    fontSize: 'var(--text-xs)',
    textTransform: 'uppercase',
    letterSpacing: '0.1em',
    color: 'var(--color-text-tertiary)',
    fontWeight: 600,
}

export default CandidateDetailDrawer

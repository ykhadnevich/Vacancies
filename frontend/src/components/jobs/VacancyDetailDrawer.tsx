import { useEffect } from 'react'
import SubScoresBar from './SubScoresBar'
import EvidenceChips from './EvidenceChips'
import BilingualReason from './BilingualReason'
import VerdictBadge from './VerdictBadge'
import Icon from '../ui/Icon'
import { useLanguage } from '../../i18n/LanguageContext'
import { useT } from '../../i18n/useT'
import { type JobVacancyV6, primaryUrlOf } from '../../types/jobV6'

interface Props {
    job:     JobVacancyV6 | null
    onClose: () => void
}

function VacancyDetailDrawer({ job, onClose }: Props) {
    const { toggle } = useLanguage()
    const t = useT()

    useEffect(() => {
        if (!job) return
        const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
        window.addEventListener('keydown', onKey)
        return () => window.removeEventListener('keydown', onKey)
    }, [job, onClose])

    useEffect(() => {
        if (!job) return
        const prev = document.body.style.overflow
        document.body.style.overflow = 'hidden'
        return () => { document.body.style.overflow = prev }
    }, [job])

    const isOpen = !!job

    return (
        <>
            <div
                onClick={onClose}
                style={{
                    position:      'fixed',
                    inset:         0,
                    background:    'rgba(26, 31, 54, 0.32)',
                    opacity:       isOpen ? 1 : 0,
                    pointerEvents: isOpen ? 'auto' : 'none',
                    transition:    'opacity var(--transition-base)',
                    zIndex:        'var(--z-drawer)' as unknown as number,
                }}
                aria-hidden="true"
            />

            <aside
                role="dialog"
                aria-modal="true"
                aria-label={t('vacancy.details.aria')}
                style={{
                    position:      'fixed',
                    top:           0,
                    right:         0,
                    bottom:        0,
                    width:         'min(560px, 100vw)',
                    background:    'var(--color-bg-surface)',
                    boxShadow:     'var(--shadow-xl)',
                    transform:     isOpen ? 'translateX(0)' : 'translateX(100%)',
                    transition:    'transform var(--transition-slow)',
                    zIndex:        'calc(var(--z-drawer) + 1)' as unknown as number,
                    display:       'flex',
                    flexDirection: 'column',
                    overflowY:     'auto',
                }}
            >
                {job && (
                    <>
                        <header style={{
                            position:      'sticky',
                            top:           0,
                            background:    'var(--color-bg-surface)',
                            borderBottom:  '0.5px solid var(--color-border-default)',
                            padding:       '20px 24px 16px',
                            display:       'flex',
                            flexDirection: 'column',
                            gap:           12,
                        }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
                                <div style={{ minWidth: 0 }}>
                                    <h2 style={{
                                        fontSize:   'var(--text-xl)',
                                        fontWeight: 'var(--font-weight-semibold)' as unknown as number,
                                        margin:     0,
                                        lineHeight: 'var(--line-height-tight)',
                                    }}>
                                        {job.title}
                                    </h2>
                                    <p style={{
                                        fontSize: 'var(--text-md)',
                                        color:    'var(--color-text-secondary)',
                                        margin:   '4px 0 0',
                                    }}>
                                        {job.company} {job.location ? `· ${job.location}` : ''}
                                    </p>
                                </div>
                                <button
                                    onClick={onClose}
                                    aria-label={t('common.close')}
                                    style={{
                                        background:   'transparent',
                                        border:       'none',
                                        cursor:       'pointer',
                                        color:        'var(--color-text-secondary)',
                                        padding:      6,
                                        borderRadius: 'var(--radius-md)',
                                        display:      'flex',
                                        fontFamily:   'inherit',
                                    }}
                                >
                                    <Icon name="close" size={20} />
                                </button>
                            </div>

                            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
                                <VerdictBadge verdict={job.verdict} score={job.score} />
                                <button
                                    onClick={toggle}
                                    title={t('nav.langSwitchTitle')}
                                    style={{
                                        background:    'var(--color-bg-muted)',
                                        border:        'none',
                                        color:         'var(--color-text-secondary)',
                                        fontSize:      'var(--text-xs)',
                                        textTransform: 'uppercase',
                                        letterSpacing: '0.06em',
                                        padding:       '4px 10px',
                                        borderRadius:  'var(--radius-pill)',
                                        cursor:        'pointer',
                                        fontWeight:    'var(--font-weight-medium)' as unknown as number,
                                        fontFamily:    'inherit',
                                    }}
                                >
                                    {t('nav.langSwitch')}
                                </button>
                            </div>
                        </header>

                        <div style={{ padding: '20px 24px 32px', display: 'flex', flexDirection: 'column', gap: 28 }}>

                            <section>
                                <h3 style={{
                                    fontSize:      'var(--text-xs)',
                                    textTransform: 'uppercase',
                                    letterSpacing: '0.06em',
                                    color:         'var(--color-text-tertiary)',
                                    margin:        '0 0 12px',
                                    fontWeight:    'var(--font-weight-medium)' as unknown as number,
                                }}>
                                    {t('reason.whySuchMatch')}
                                </h3>
                                <BilingualReason
                                    strengths={{      en: job.strengthsEn ?? null,      uk: job.strengthsUk ?? null }}
                                    gaps={{           en: job.gapsEn ?? null,           uk: job.gapsUk ?? null }}
                                    recommendation={{ en: job.recommendationEn ?? null, uk: job.recommendationUk ?? null }}
                                    flat={{           en: job.reasonEn ?? null,         uk: job.reasonUk ?? null }}
                                />
                            </section>

                            <section>
                                <h3 style={{
                                    fontSize:      'var(--text-xs)',
                                    textTransform: 'uppercase',
                                    letterSpacing: '0.06em',
                                    color:         'var(--color-text-tertiary)',
                                    margin:        '0 0 12px',
                                    fontWeight:    'var(--font-weight-medium)' as unknown as number,
                                }}>
                                    {t('reason.scoreBreakdown')}
                                </h3>
                                <SubScoresBar subScores={job.subScores ?? {}} />
                            </section>

                            {(((job.matchedSkills?.length ?? 0) > 0) ||
                              ((job.missingMustHaves?.length ?? 0) > 0)) && (
                                <section>
                                    <h3 style={{
                                        fontSize:      'var(--text-xs)',
                                        textTransform: 'uppercase',
                                        letterSpacing: '0.06em',
                                        color:         'var(--color-text-tertiary)',
                                        margin:        '0 0 12px',
                                        fontWeight:    'var(--font-weight-medium)' as unknown as number,
                                    }}>
                                        {t('reason.evidence')}
                                    </h3>
                                    <EvidenceChips
                                        matched={job.matchedSkills ?? []}
                                        missing={job.missingMustHaves ?? []}
                                        antiFlags={job.triggeredAntiFlags ?? []}
                                        limit={6}
                                        score={job.score}
                                    />
                                </section>
                            )}

                            <section>
                                <a
                                    href={primaryUrlOf(job)}
                                    target="_blank"
                                    rel="noreferrer"
                                    style={{
                                        display:        'inline-flex',
                                        alignItems:     'center',
                                        gap:            6,
                                        padding:        '10px 16px',
                                        background:     'var(--color-primary-600)',
                                        color:          '#fff',
                                        borderRadius:   'var(--radius-md)',
                                        fontSize:       'var(--text-md)',
                                        fontWeight:     'var(--font-weight-medium)' as unknown as number,
                                        textDecoration: 'none',
                                    }}
                                >
                                    {t('reason.openOnSource')}
                                    <Icon name="arrow-up-right" size={14} />
                                </a>
                            </section>

                        </div>
                    </>
                )}
            </aside>
        </>
    )
}

export default VacancyDetailDrawer

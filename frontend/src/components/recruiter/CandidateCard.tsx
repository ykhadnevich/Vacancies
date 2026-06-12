import type { CandidateAnalysisResultDto } from '../../types/recruiter'
import type { Language } from '../../i18n/translations'
import { verdictColor, type VerdictColor } from '../../utils/verdict'
import ScoreWidget from './ScoreWidget'
import Badge from '../ui/Badge'
import Icon from '../ui/Icon'
import { useT } from '../../i18n/useT'

const ACCENT: Record<VerdictColor, string> = {
    success: 'var(--color-success-500)',
    warning: 'var(--color-warning-500)',
    danger:  'var(--color-danger-500)',
    neutral: 'var(--color-border-strong)',
}

interface CandidateCardProps {
    result:   CandidateAnalysisResultDto
    rank:     number
    onClick:  () => void
    language: Language
    style?:   React.CSSProperties
}

function CandidateCard({ result, rank, onClick, language, style }: CandidateCardProps) {
    const t   = useT()
    const vc  = verdictColor(result.verdict)
    const conf = result.confidence ?? undefined
    const reason = language === 'uk' ? result.reasonUk : result.reasonEn
    const rankStr = String(rank).padStart(2, '0')

    return (
        <div
            onClick={onClick}
            style={{
                position:     'relative',
                overflow:     'hidden',
                borderRadius: 'var(--radius-lg)',
                border:       '0.5px solid var(--color-border-default)',
                boxShadow:    'var(--shadow-xs)',
                background:   'var(--color-bg-surface)',
                cursor:       'pointer',
                transition:   'box-shadow var(--transition-fast), transform var(--transition-fast)',
                ...style,
            }}
            onMouseEnter={(e) => {
                e.currentTarget.style.boxShadow = 'var(--shadow-card-hover)'
                e.currentTarget.style.transform  = 'translateY(-1px)'
            }}
            onMouseLeave={(e) => {
                e.currentTarget.style.boxShadow = 'var(--shadow-xs)'
                e.currentTarget.style.transform  = 'translateY(0)'
            }}
        >
            <div style={{
                position:   'absolute',
                left:       0,
                top:        0,
                bottom:     0,
                width:      3,
                background: ACCENT[vc],
            }} />

            <div style={{
                padding:       '14px 18px',
                paddingLeft:   20,
                display:       'flex',
                flexDirection: 'column',
                gap:           10,
            }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <span style={{
                        fontFamily:         'var(--font-mono)',
                        fontSize:           'var(--rank-font-size)',
                        fontWeight:         700,
                        fontVariantNumeric: 'tabular-nums',
                        color:              'var(--color-border-default)',
                        lineHeight:         1,
                        flexShrink:         0,
                        userSelect:         'none',
                        minWidth:           40,
                    }}>
                        {rankStr}
                    </span>

                    <span style={{
                        fontSize:     'var(--text-md)',
                        fontWeight:   600,
                        color:        'var(--color-text-primary)',
                        flex:         1,
                        overflow:     'hidden',
                        textOverflow: 'ellipsis',
                        whiteSpace:   'nowrap',
                    }}>
                        {result.candidateName || result.candidateId.slice(0, 8)}
                    </span>

                    {typeof conf === 'number' && conf < 0.7 && (
                        <Badge
                            color={conf < 0.5 ? 'danger' : 'warning'}
                            size="sm"
                            title={t('recruiter.confidence.tooltip')}
                        >
                            <Icon name="alert-circle" size={11} />
                            {' '}{(conf * 100).toFixed(0)}%
                        </Badge>
                    )}
                </div>

                <ScoreWidget
                    score={result.score}
                    verdict={result.verdict}
                    confidence={conf}
                />

                {reason && (
                    <p style={{
                        margin:          0,
                        fontSize:        'var(--text-sm)',
                        color:           'var(--color-text-secondary)',
                        lineHeight:      'var(--line-height-normal)',
                        display:         '-webkit-box',
                        WebkitLineClamp: 2,
                        WebkitBoxOrient: 'vertical',
                        overflow:        'hidden',
                    }}>
                        {reason}
                    </p>
                )}

                {(result.matchedSkills.length > 0 || result.missingMustHaves.length > 0) && (
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                        {result.matchedSkills.slice(0, 6).map((s) => (
                            <Badge key={`m-${s}`} color="success" size="sm">{s}</Badge>
                        ))}
                        {result.missingMustHaves.slice(0, 4).map((s) => (
                            <Badge key={`x-${s}`} color="warning" size="sm">— {s}</Badge>
                        ))}
                    </div>
                )}
            </div>
        </div>
    )
}

export default CandidateCard

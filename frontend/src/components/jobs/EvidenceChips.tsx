import { useState } from 'react'
import Badge from '../ui/Badge'
import { useT } from '../../i18n/useT'
import { classifyTier } from './evidenceTier'

interface Props {
    matched:    string[]
    missing:    string[]
    antiFlags:  string[]
    limit?:     number
    showAntiFlags?: boolean
    score?: number
}


const SPECIFIC_TOP_N    = 3
const LOWFIT_INITIAL_N  = 10

const FILTER_THRESHOLD = 0.50

function EvidenceChips({ matched, missing, antiFlags, limit, showAntiFlags = false, score }: Props) {
    const t = useT()
    const [expanded, setExpanded] = useState(false)
    const [matchedExpanded, setMatchedExpanded] = useState(false)


    const isFiltering = score !== undefined && score >= FILTER_THRESHOLD
    const specificMissing = isFiltering
        ? missing.filter((s) => classifyTier(s) <= 2)
        : missing
    const conceptMissing = isFiltering
        ? missing.filter((s) => classifyTier(s) === 3)
        : []


    const initialN     = isFiltering ? SPECIFIC_TOP_N : LOWFIT_INITIAL_N
    const initialShown = specificMissing.slice(0, initialN)
    const moreSpecific = specificMissing.slice(initialN)
    const hiddenCount  = moreSpecific.length + conceptMissing.length


    const m = limit && !matchedExpanded ? matched.slice(0, limit) : matched
    const af = limit ? antiFlags.slice(0, limit) : antiFlags
    const mExtra  = limit && matched.length   > limit ? matched.length   - limit : 0
    const afExtra = limit && antiFlags.length > limit ? antiFlags.length - limit : 0

    const labelStyle: React.CSSProperties = {
        fontSize: 'var(--text-xs)',
        color:    'var(--color-text-tertiary)',
        minWidth: 60,
    }
    const counterStyle: React.CSSProperties = {
        fontSize: 'var(--text-xs)',
        color:    'var(--color-text-secondary)',
        cursor:   hiddenCount > 0 ? 'pointer' : 'default',
        textDecoration: hiddenCount > 0 ? 'underline dotted' : 'none',
        userSelect: 'none',
    }

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {m.length > 0 && (
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                    <span style={labelStyle}>{t('card.skillsMatched')}</span>
                    {m.map((s) => <Badge key={s} color="success" size="sm">{s}</Badge>)}
                    {mExtra > 0 && !matchedExpanded && (
                        <span
                            style={counterStyle}
                            onClick={() => setMatchedExpanded(true)}
                            role="button"
                            tabIndex={0}
                            onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') setMatchedExpanded(true) }}
                        >
                            +{mExtra}
                        </span>
                    )}
                    {matchedExpanded && mExtra > 0 && (
                        <span
                            style={counterStyle}
                            onClick={() => setMatchedExpanded(false)}
                            role="button"
                            tabIndex={0}
                            onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') setMatchedExpanded(false) }}
                        >
                            {t('common.showLess')}
                        </span>
                    )}
                </div>
            )}
            {(initialShown.length > 0 || hiddenCount > 0) && (
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                    <span style={labelStyle}>{t('card.skillsMissing')}</span>
                    {initialShown.map((s) => <Badge key={s} color="danger" size="sm">{s}</Badge>)}
                    {expanded && moreSpecific.map((s) => <Badge key={s} color="danger" size="sm">{s}</Badge>)}
                    {expanded && conceptMissing.map((s) => <Badge key={s} color="neutral" size="sm">{s}</Badge>)}
                    {hiddenCount > 0 && (
                        <span
                            style={counterStyle}
                            onClick={() => setExpanded(!expanded)}
                            role="button"
                            tabIndex={0}
                            onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') setExpanded(!expanded) }}
                        >
                            {expanded ? t('common.showLess') : `+${hiddenCount}`}
                        </span>
                    )}
                </div>
            )}
            {showAntiFlags && af.length > 0 && (
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                    <span style={labelStyle}>{t('card.skillsMissing')}</span>
                    {af.map((s) => <Badge key={s} color="warning" size="sm">{s}</Badge>)}
                    {afExtra > 0 && <Badge color="neutral" size="sm">+{afExtra}</Badge>}
                </div>
            )}
        </div>
    )
}

export default EvidenceChips

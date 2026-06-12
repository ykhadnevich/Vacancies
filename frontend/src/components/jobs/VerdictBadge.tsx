import Badge from '../ui/Badge'
import type { Verdict } from '../../types/jobV6'
import { VERDICT_META } from './verdictMeta'

interface Props {
    verdict:    Verdict
    score:      number
    showScore?: boolean
}


function VerdictBadge({ verdict, score, showScore = true }: Props) {
    const meta = VERDICT_META[verdict]
    return (
        <Badge color={meta?.color ?? 'neutral'} size="md">
            {meta?.labelUk ?? verdict}
            {showScore && (
                <>
                    <span style={{ opacity: 0.5, margin: '0 2px' }}>·</span>
                    <span style={{ fontVariantNumeric: 'tabular-nums' }}>
                        {((score ?? 0) * 100).toFixed(1)}%
                    </span>
                </>
            )}
        </Badge>
    )
}

export default VerdictBadge

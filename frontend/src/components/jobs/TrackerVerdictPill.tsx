import Badge from '../ui/Badge'
import { useT } from '../../i18n/useT'
import type { TranslationKey } from '../../i18n/translations'

type VerdictString = string

const COLOR: Record<string, 'success' | 'info' | 'warning' | 'danger'> = {
    Strong:   'success',
    Partial:  'info',
    Weak:     'warning',
    Mismatch: 'danger',
}

const VERDICT_KEY: Record<string, TranslationKey> = {
    Strong:   'verdict.strong',
    Partial:  'verdict.partial',
    Weak:     'verdict.weak',
    Mismatch: 'verdict.notRelevant',
}

interface Props {
    verdict: VerdictString | null | undefined
    score:   number | null | undefined
    size?:   'sm' | 'md'
}

function TrackerVerdictPill({ verdict, score, size = 'sm' }: Props) {
    const t = useT()
    if (!verdict || score == null) return null
    const key = VERDICT_KEY[verdict]
    return (
        <Badge color={COLOR[verdict] ?? 'neutral'} size={size}>
            {key ? t(key) : verdict}
            <span style={{ opacity: 0.55, margin: '0 2px' }}>·</span>
            <span style={{ fontVariantNumeric: 'tabular-nums' }}>
                {(score * 100).toFixed(1)}%
            </span>
        </Badge>
    )
}

export default TrackerVerdictPill

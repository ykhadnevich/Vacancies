export type VerdictColor = 'success' | 'warning' | 'danger' | 'neutral'

export function verdictColor(verdict: string): VerdictColor {
    const v = verdict.toLowerCase()
    if (v.startsWith('strong'))  return 'success'
    if (v.startsWith('partial') || v.startsWith('weak')) return 'warning'
    if (v.includes('mismatch') || v.includes('notrelevant') || v.includes('not_relevant')) return 'danger'
    return 'neutral'
}

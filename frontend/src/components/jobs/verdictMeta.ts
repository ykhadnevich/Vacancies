import type { Verdict } from '../../types/jobV6'


export interface VerdictMeta {
    labelEn: string
    labelUk: string
    color:   'success' | 'info' | 'warning' | 'danger'
}

export const VERDICT_META: Record<Verdict, VerdictMeta> = {
    Strong:   { labelEn: 'Strong',   labelUk: 'Сильна',          color: 'success' },
    Partial:  { labelEn: 'Partial',  labelUk: 'Часткова',        color: 'info'    },
    Weak:     { labelEn: 'Weak',     labelUk: 'Слабка',          color: 'warning' },
    Mismatch: { labelEn: 'Mismatch', labelUk: 'Невідповідність', color: 'danger'  },
}

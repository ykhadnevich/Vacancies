import { useLanguage } from './LanguageContext'
import { useT } from './useT'
import type { TranslationKey } from './translations'

const cache = new Map<string, Intl.PluralRules>()

function rulesFor(language: string): Intl.PluralRules {
    let r = cache.get(language)
    if (!r) {
        r = new Intl.PluralRules(language)
        cache.set(language, r)
    }
    return r
}

export function usePlural() {
    const { language } = useLanguage()
    const t = useT()
    return function tp(
        baseKey: string,
        count: number,
        extra?: Record<string, string | number>,
    ): string {
        const form = rulesFor(language).select(count)
        const key  = `${baseKey}.${form}` as TranslationKey
        return t(key, { count, ...extra })
    }
}

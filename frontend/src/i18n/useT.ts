import { useLanguage } from './LanguageContext'
import { translations, type TranslationKey } from './translations'

const PLACEHOLDER = /\{(\w+)\}/g

export function useT() {
    const { language } = useLanguage()
    return function t(
        key: TranslationKey,
        vars?: Record<string, string | number>,
    ): string {
        const raw = (translations[language] as Record<string, string>)[key] ?? key
        if (!vars) return raw
        return raw.replace(PLACEHOLDER, (_match, name) =>
            name in vars ? String(vars[name]) : `{${name}}`,
        )
    }
}

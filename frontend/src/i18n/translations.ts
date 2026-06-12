import { uk } from './locales/uk'
import { en } from './locales/en'

export const translations = { uk, en } as const

export type Language       = keyof typeof translations
export type TranslationKey = keyof typeof uk


const _shapeCheck: Record<keyof typeof uk, string> = en
void _shapeCheck

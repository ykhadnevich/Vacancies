type Lang = 'uk' | 'en'

interface Unit {
    secs: number
    one:  { uk: string; en: string }
    few:  { uk: string; en: string }
    many: { uk: string; en: string }
}

const UNITS: Unit[] = [
    { secs: 60,          one: { uk: 'секунду',  en: 'second' },  few: { uk: 'секунди', en: 'seconds' }, many: { uk: 'секунд',  en: 'seconds' } },
    { secs: 3600,        one: { uk: 'хвилину',  en: 'minute' },  few: { uk: 'хвилини', en: 'minutes' }, many: { uk: 'хвилин',  en: 'minutes' } },
    { secs: 86400,       one: { uk: 'годину',   en: 'hour'   },  few: { uk: 'години',  en: 'hours'   }, many: { uk: 'годин',   en: 'hours'   } },
    { secs: 86400 * 30,  one: { uk: 'день',     en: 'day'    },  few: { uk: 'дні',     en: 'days'    }, many: { uk: 'днів',    en: 'days'    } },
    { secs: 86400 * 365, one: { uk: 'місяць',   en: 'month'  },  few: { uk: 'місяці',  en: 'months'  }, many: { uk: 'місяців', en: 'months'  } },
]

function ukPlural(n: number, one: string, few: string, many: string): string {
    const mod10 = n % 10
    const mod100 = n % 100
    if (mod10 === 1 && mod100 !== 11) return one
    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return few
    return many
}

export function formatRelative(input: string | Date, lang: Lang = 'uk'): string {
    const d = typeof input === 'string' ? new Date(input) : input
    if (Number.isNaN(d.getTime())) return ''
    const diffSecs = Math.max(0, Math.round((Date.now() - d.getTime()) / 1000))
    if (diffSecs < 5) return lang === 'uk' ? 'щойно' : 'just now'

    // 5-59 s: emit "less than a minute ago" instead of the literally-correct but odd "59 seconds ago".
    if (diffSecs < 60) return lang === 'uk' ? 'менш ніж хвилину тому' : 'less than a minute ago'

    // Largest unit whose `secs` exceeds the diff.
    let chosenIdx = UNITS.length - 1
    for (let i = 0; i < UNITS.length; i++) {
        if (diffSecs < UNITS[i].secs) { chosenIdx = i; break }
    }
    const unit = UNITS[chosenIdx]
    const stepSecs = chosenIdx === 0 ? 1 : UNITS[chosenIdx - 1].secs
    const count = Math.max(1, Math.floor(diffSecs / stepSecs))

    const word = lang === 'uk'
        ? ukPlural(count, unit.one.uk, unit.few.uk, unit.many.uk)
        : (count === 1 ? unit.one.en : unit.many.en)

    return lang === 'uk' ? `${count} ${word} тому` : `${count} ${word} ago`
}

export function formatAbsolute(input: string | Date, lang: Lang = 'uk'): string {
    const d = typeof input === 'string' ? new Date(input) : input
    if (Number.isNaN(d.getTime())) return ''
    return d.toLocaleString(lang === 'uk' ? 'uk-UA' : 'en-GB', {
        year:   'numeric',
        month:  '2-digit',
        day:    '2-digit',
        hour:   '2-digit',
        minute: '2-digit',
    })
}

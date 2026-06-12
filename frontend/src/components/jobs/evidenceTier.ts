const LOWERCASE_BRAND_TOOLS = new Set<string>([
    'helm', 'ansible', 'pytest', 'istio', 'kafka', 'nginx', 'redis',
    'docker', 'kubectl', 'npm', 'pip', 'yarn', 'terraform', 'prometheus',
    'grafana', 'elasticsearch', 'mongodb', 'postgres', 'mysql', 'mariadb',
    'sqlite', 'rabbitmq', 'memcached', 'celery', 'airflow', 'dagster',
    'kotlin', 'rust', 'golang', 'scala', 'erlang', 'elixir', 'haskell',
    'jenkins', 'circleci', 'argocd', 'flux', 'consul', 'vault', 'tomcat',
    'webpack', 'vite', 'rollup', 'esbuild', 'babel', 'eslint', 'prettier',
    'jest', 'mocha', 'cypress', 'playwright', 'selenium', 'puppeteer',
    'nestjs', 'fastapi', 'flask', 'django', 'rails', 'laravel', 'symfony',
    'rxjs', 'redux', 'zustand', 'mobx', 'tailwind', 'sass', 'less',
    'bash', 'zsh', 'curl', 'wget', 'jq', 'sed', 'awk', 'grep',
    'kafka-streams', 'spark', 'hadoop', 'hive', 'presto', 'trino',
])


const GENERIC_SUFFIXES = [
    'mindset', 'thinking', 'approach', 'loops', 'tooling',
    'fundamentals', 'development', 'management', 'strategy',
    'culture', 'practices', 'principles', 'awareness',
]


const DEPARTMENT_LABELS = new Set<string>([
    'engineering', 'sales', 'marketing', 'product', 'design',
    'operations', 'finance', 'hr', 'legal', 'support',
])


const IMPLICIT_PM_METRICS = new Set<string>([
    'ltv', 'cac', 'dau', 'mau', 'wau', 'roi', 'roas',
    'nps', 'csat', 'aov', 'cvr', 'ctr', 'cogs',
    'mrr', 'arr',
    'kpi', 'kpis', 'okr', 'okrs',
    'churn', 'churn rate', 'retention', 'engagement',
    'funnel', 'conversion', 'acquisition', 'activation',
    'revenue', 'growth',
    'attribution', 'cohort', 'segmentation',
])

export type EvidenceTier = 1 | 2 | 3

export function classifyTier(term: string): EvidenceTier {
    const raw = term.trim()
    if (!raw) return 3
    const lower = raw.toLowerCase()


    if (IMPLICIT_PM_METRICS.has(lower)) return 3


    if (LOWERCASE_BRAND_TOOLS.has(lower)) return 1


    if (DEPARTMENT_LABELS.has(lower)) return 3
    if (lower.endsWith(' teams') || lower.endsWith(' team')) return 3


    for (const suffix of GENERIC_SUFFIXES) {
        if (lower.endsWith(' ' + suffix) || lower === suffix) return 3
    }

    const hasSpace = raw.includes(' ')


    if (!hasSpace) {

        if (raw.length <= 5 && /^[A-Z][A-Z0-9]*$/.test(raw)) return 1

        if (/[A-Z]/.test(raw) && /[0-9]/.test(raw)) return 1

        if (/[A-Za-z][./\-+#][A-Za-z0-9]/.test(raw)) return 1

        if (/^[A-Z][a-zA-Z0-9]*$/.test(raw)) return 1

        if (raw === lower) return 3

        return 2
    }


    const tokens = raw.split(/\s+/)
    const hasBrandLikeToken = tokens.some((t) => {
        if (LOWERCASE_BRAND_TOOLS.has(t.toLowerCase())) return true
        if (t.length > 0 && /^[A-Z]/.test(t)) return true
        if (t.length <= 5 && /^[A-Z][A-Z0-9]*$/.test(t)) return true
        return false
    })

    if (hasBrandLikeToken) return 2

    return 3
}

export function isHighInfoGap(term: string): boolean {
    return classifyTier(term) <= 2
}

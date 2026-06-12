import type { ReactNode } from 'react'

type BadgeColor = 'success' | 'warning' | 'danger' | 'info' | 'primary' | 'neutral'
type BadgeSize = 'sm' | 'md'

interface BadgeProps {
    color?: BadgeColor
    size?: BadgeSize
    children: ReactNode
    title?: string
    style?: React.CSSProperties
}

const COLOR_STYLES: Record<BadgeColor, React.CSSProperties> = {
    success: { background: 'var(--color-success-50)',  color: 'var(--color-success-700)' },
    warning: { background: 'var(--color-warning-50)',  color: 'var(--color-warning-700)' },
    danger:  { background: 'var(--color-danger-50)',   color: 'var(--color-danger-700)'  },
    info:    { background: 'var(--color-info-50)',     color: 'var(--color-info-700)'    },
    primary: { background: 'var(--color-primary-50)',  color: 'var(--color-primary-700)' },
    neutral: { background: 'var(--color-bg-muted)',    color: 'var(--color-text-secondary)' },
}

const SIZE_STYLES: Record<BadgeSize, React.CSSProperties> = {
    sm: { fontSize: 'var(--text-xs)', padding: '2px 7px' },
    md: { fontSize: 'var(--text-sm)', padding: '3px 10px' },
}

function Badge({ color = 'neutral', size = 'sm', children, title, style }: BadgeProps) {
    const baseStyle: React.CSSProperties = {
        display: 'inline-flex',
        alignItems: 'center',
        gap: '4px',
        borderRadius: 'var(--radius-pill)',
        fontWeight: 'var(--font-weight-medium)' as unknown as number,
        whiteSpace: 'nowrap',
        lineHeight: 1.4,
        ...COLOR_STYLES[color],
        ...SIZE_STYLES[size],
        ...style,
    }
    return <span title={title} style={baseStyle}>{children}</span>
}

export default Badge

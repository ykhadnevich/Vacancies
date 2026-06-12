import type { HTMLAttributes, ReactNode } from 'react'

type CardVariant = 'default' | 'elevated' | 'flat'

interface CardProps extends HTMLAttributes<HTMLDivElement> {
    variant?: CardVariant
    padding?: 'none' | 'sm' | 'md' | 'lg'
    children: ReactNode
    interactive?: boolean
}

const PADDINGS = {
    none: '0',
    sm:   '12px',
    md:   '16px 18px',
    lg:   '20px 24px',
}

function Card({
    variant = 'default',
    padding = 'md',
    interactive = false,
    children,
    style,
    ...rest
}: CardProps) {
    const baseStyle: React.CSSProperties = {
        background: 'var(--color-bg-surface)',
        borderRadius: 'var(--radius-lg)',
        padding: PADDINGS[padding],
        border: variant === 'flat' ? 'none' : '0.5px solid var(--color-border-default)',
        boxShadow: variant === 'elevated' ? 'var(--shadow-md)' : variant === 'default' ? 'var(--shadow-xs)' : 'none',
        transition: interactive ? 'all var(--transition-base)' : undefined,
        cursor: interactive ? 'pointer' : undefined,
        ...style,
    }

    return (
        <div
            style={baseStyle}
            onMouseEnter={interactive ? (e) => {
                e.currentTarget.style.boxShadow = 'var(--shadow-md)'
                e.currentTarget.style.borderColor = 'var(--color-border-strong)'
            } : undefined}
            onMouseLeave={interactive ? (e) => {
                e.currentTarget.style.boxShadow = variant === 'elevated' ? 'var(--shadow-md)' : 'var(--shadow-xs)'
                e.currentTarget.style.borderColor = 'var(--color-border-default)'
            } : undefined}
            {...rest}
        >
            {children}
        </div>
    )
}

export default Card

import type { ButtonHTMLAttributes, ReactNode } from 'react'

type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger'
type ButtonSize = 'sm' | 'md' | 'lg'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
    variant?: ButtonVariant
    size?: ButtonSize
    fullWidth?: boolean
    leftIcon?: ReactNode
    rightIcon?: ReactNode
    isLoading?: boolean
}

const VARIANT_STYLES: Record<ButtonVariant, React.CSSProperties> = {
    primary: {
        background: 'var(--color-primary-600)',
        color: '#fff',
        border: '1px solid var(--color-primary-600)',
    },
    secondary: {
        background: 'var(--color-bg-surface)',
        color: 'var(--color-text-primary)',
        border: '1px solid var(--color-border-default)',
    },
    ghost: {
        background: 'transparent',
        color: 'var(--color-text-secondary)',
        border: '1px solid transparent',
    },
    danger: {
        background: 'var(--color-danger-50)',
        color: 'var(--color-danger-700)',
        border: '1px solid var(--color-danger-100)',
    },
}

const SIZE_STYLES: Record<ButtonSize, React.CSSProperties> = {
    sm: { padding: '6px 12px', fontSize: 'var(--text-sm)', borderRadius: 'var(--radius-md)' },
    md: { padding: '8px 16px', fontSize: 'var(--text-md)', borderRadius: 'var(--radius-md)' },
    lg: { padding: '10px 20px', fontSize: 'var(--text-lg)', borderRadius: 'var(--radius-lg)' },
}

function Button({
    variant = 'primary',
    size = 'md',
    fullWidth = false,
    leftIcon,
    rightIcon,
    isLoading = false,
    disabled,
    children,
    style,
    ...rest
}: ButtonProps) {
    const baseStyle: React.CSSProperties = {
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: '6px',
        fontWeight: 'var(--font-weight-medium)' as unknown as number,
        cursor: disabled || isLoading ? 'not-allowed' : 'pointer',
        opacity: disabled || isLoading ? 0.6 : 1,
        transition: 'all var(--transition-fast)',
        whiteSpace: 'nowrap',
        width: fullWidth ? '100%' : 'auto',
        ...VARIANT_STYLES[variant],
        ...SIZE_STYLES[size],
        ...style,
    }

    return (
        <button
            disabled={disabled || isLoading}
            style={baseStyle}
            {...rest}
        >
            {isLoading ? (
                <span style={{ display: 'inline-block', width: 14, height: 14, border: '2px solid currentColor', borderTopColor: 'transparent', borderRadius: '50%', animation: 'spin 0.6s linear infinite' }} />
            ) : leftIcon}
            {children}
            {rightIcon}
            <style>{`
                @keyframes spin {
                    to { transform: rotate(360deg); }
                }
                button:hover:not(:disabled) {
                    filter: brightness(0.95);
                }
                button:active:not(:disabled) {
                    transform: scale(0.98);
                }
            `}</style>
        </button>
    )
}

export default Button

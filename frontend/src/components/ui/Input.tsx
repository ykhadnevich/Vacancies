import type { InputHTMLAttributes } from 'react'

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
    label?: string
    hint?: string
    error?: string
    leftIcon?: React.ReactNode
}

function Input({ label, hint, error, leftIcon, style, id, ...rest }: InputProps) {
    const inputId = id || rest.name
    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
            {label && (
                <label htmlFor={inputId} style={{
                    fontSize: 'var(--text-sm)',
                    fontWeight: 'var(--font-weight-medium)',
                    color: 'var(--color-text-secondary)',
                }}>
                    {label}
                </label>
            )}
            <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
                {leftIcon && (
                    <span style={{
                        position: 'absolute',
                        left: 12,
                        color: 'var(--color-text-tertiary)',
                        display: 'flex',
                        alignItems: 'center',
                        pointerEvents: 'none',
                    }}>
                        {leftIcon}
                    </span>
                )}
                <input
                    id={inputId}
                    style={{
                        width: '100%',
                        padding: leftIcon ? '10px 14px 10px 38px' : '10px 14px',
                        fontSize: 'var(--text-md)',
                        fontFamily: 'inherit',
                        color: 'var(--color-text-primary)',
                        background: 'var(--color-bg-surface)',
                        border: `1px solid ${error ? 'var(--color-danger-500)' : 'var(--color-border-default)'}`,
                        borderRadius: 'var(--radius-md)',
                        outline: 'none',
                        transition: 'border-color var(--transition-fast), box-shadow var(--transition-fast)',
                        ...style,
                    }}
                    onFocus={(e) => {
                        e.currentTarget.style.borderColor = error ? 'var(--color-danger-500)' : 'var(--color-primary-500)'
                        e.currentTarget.style.boxShadow = `0 0 0 3px ${error ? 'var(--color-danger-100)' : 'var(--color-primary-100)'}`
                    }}
                    onBlur={(e) => {
                        e.currentTarget.style.borderColor = error ? 'var(--color-danger-500)' : 'var(--color-border-default)'
                        e.currentTarget.style.boxShadow = 'none'
                    }}
                    {...rest}
                />
            </div>
            {(hint || error) && (
                <span style={{
                    fontSize: 'var(--text-xs)',
                    color: error ? 'var(--color-danger-600)' : 'var(--color-text-tertiary)',
                }}>
                    {error || hint}
                </span>
            )}
        </div>
    )
}

export default Input

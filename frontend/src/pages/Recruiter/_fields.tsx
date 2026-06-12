import React from 'react'

export function FieldRow({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <label style={{ fontSize: 'var(--text-sm)', fontWeight: 500, color: 'var(--color-text-secondary)' }}>
                {label}
            </label>
            {children}
            {hint && (<span style={{ fontSize: 'var(--text-xs)', color: 'var(--color-text-tertiary)' }}>{hint}</span>)}
        </div>
    )
}

export function BareInput({ value, onChange, placeholder, type = 'text' }: {
    value: string; onChange: (v: string) => void; placeholder?: string; type?: string
}) {
    return (
        <input
            type={type}
            value={value}
            onChange={(e) => onChange(e.target.value)}
            placeholder={placeholder}
            style={{
                width: '100%', padding: '10px 14px', fontSize: 'var(--text-md)',
                fontFamily: 'inherit', color: 'var(--color-text-primary)',
                background: 'var(--color-bg-surface)', border: '1px solid var(--color-border-default)',
                borderRadius: 'var(--radius-md)', outline: 'none',
            }}
        />
    )
}

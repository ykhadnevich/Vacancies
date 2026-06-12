import type React from 'react'

export const pageWrap: React.CSSProperties = {
    width: '100%', maxWidth: 'var(--max-width-content)', margin: '0 auto',
    padding: '32px 16px', display: 'flex', flexDirection: 'column', gap: 20,
}
export const pageHeader: React.CSSProperties = {
    display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 4,
}
export const pageTitle: React.CSSProperties = { fontSize: 'var(--text-2xl)', margin: 0 }
export const mutedText: React.CSSProperties = { color: 'var(--color-text-tertiary)', fontSize: 'var(--text-md)', margin: 0 }
export const textareaStyle: React.CSSProperties = {
    width: '100%', padding: '10px 14px', fontSize: 'var(--text-md)',
    fontFamily: 'inherit', color: 'var(--color-text-primary)',
    background: 'var(--color-bg-surface)', border: '1px solid var(--color-border-default)',
    borderRadius: 'var(--radius-md)', outline: 'none', resize: 'vertical',
}

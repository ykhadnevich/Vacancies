import type React from 'react'

/* ── Page measures ──────────────────────────────────────────────────────── */
export const wideWrap: React.CSSProperties = {
    width: '100%', maxWidth: 'var(--max-width-wide)', margin: '0 auto',
    padding: '28px 24px 72px',
}
export const contentWrap: React.CSSProperties = {
    width: '100%', maxWidth: 'var(--max-width-content)', margin: '0 auto',
    padding: '28px 24px 72px',
}

/* ── Sticky page header (title row + actions) ───────────────────────────── */
export const stickyHeader: React.CSSProperties = {
    position: 'sticky', top: 0, zIndex: 20,
    background: 'color-mix(in srgb, var(--color-bg-page) 86%, transparent)',
    backdropFilter: 'blur(8px)',
    borderBottom: '1px solid var(--color-border-default)',
    margin: '0 0 24px',
}
export const stickyHeaderInner: React.CSSProperties = {
    width: '100%', maxWidth: 'var(--max-width-wide)', margin: '0 auto',
    padding: '18px 24px', display: 'flex', alignItems: 'flex-end',
    justifyContent: 'space-between', gap: 20, flexWrap: 'wrap',
}

/* ── Editorial type helpers ─────────────────────────────────────────────── */
export const eyebrow: React.CSSProperties = {
    fontFamily: 'var(--font-sans)', fontSize: 'var(--text-xs)', fontWeight: 600,
    letterSpacing: '0.14em', textTransform: 'uppercase',
    color: 'var(--color-text-tertiary)',
}
export const serifTitle: React.CSSProperties = {
    fontFamily: 'var(--font-serif)', fontSize: 'var(--display-sm)', fontWeight: 600,
    letterSpacing: '-0.02em', lineHeight: 1.1, margin: 0,
    color: 'var(--color-text-primary)',
}
export const serifTitleLg: React.CSSProperties = {
    ...serifTitle, fontSize: 'var(--display-md)',
}
export const sectionHead: React.CSSProperties = {
    fontFamily: 'var(--font-sans)', fontSize: 'var(--text-xs)', fontWeight: 600,
    letterSpacing: '0.1em', textTransform: 'uppercase',
    color: 'var(--color-text-tertiary)', margin: '0 0 12px',
}
export const mono: React.CSSProperties = {
    fontFamily: 'var(--font-mono)', fontVariantNumeric: 'tabular-nums',
}

/* ── Sidebar ────────────────────────────────────────────────────────────── */
export const sidebarCard: React.CSSProperties = {
    background: 'var(--color-bg-surface)', border: '1px solid var(--color-border-default)',
    borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-xs)',
    padding: 16, display: 'flex', flexDirection: 'column', gap: 16,
}
export const sidebarSticky: React.CSSProperties = {
    position: 'sticky', top: 24,
}

/* ── Data table (CSS-table / real-table shared cells) ───────────────────── */
export const tableShell: React.CSSProperties = {
    width: '100%', borderCollapse: 'separate', borderSpacing: 0,
    background: 'var(--color-bg-surface)', borderRadius: 'var(--radius-lg)',
    border: '1px solid var(--color-border-default)', overflow: 'hidden',
}
export const th: React.CSSProperties = {
    textAlign: 'left', padding: '12px 16px',
    fontFamily: 'var(--font-sans)', fontSize: 'var(--text-xs)', fontWeight: 600,
    letterSpacing: '0.08em', textTransform: 'uppercase',
    color: 'var(--color-text-tertiary)',
    background: 'var(--color-bg-muted)',
    borderBottom: '1px solid var(--color-border-default)',
    position: 'sticky', top: 0, zIndex: 1, whiteSpace: 'nowrap',
}
export const td: React.CSSProperties = {
    padding: '14px 16px', fontSize: 'var(--text-md)',
    color: 'var(--color-text-primary)', verticalAlign: 'middle',
    borderBottom: '1px solid var(--color-border-subtle)',
}
export const tdMono: React.CSSProperties = {
    ...td, fontFamily: 'var(--font-mono)', fontVariantNumeric: 'tabular-nums',
}

/* ── Misc ───────────────────────────────────────────────────────────────── */
export const rowActions: React.CSSProperties = {
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
    width: 32, height: 32, background: 'transparent',
    border: '1px solid var(--color-border-default)', borderRadius: 'var(--radius-md)',
    cursor: 'pointer', color: 'var(--color-text-tertiary)', fontFamily: 'inherit',
}

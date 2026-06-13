import React from 'react'
import { useIsMobile } from '../../hooks/useViewport'
import {
    wideWrap, stickyHeader, stickyHeaderInner, eyebrow, serifTitle,
    sidebarCard, sidebarSticky, sectionHead,
} from './_layout'

export function PageHeader({ eyebrowText, title, subtitle, actions, sticky = true }: {
    eyebrowText?: string
    title:        React.ReactNode
    subtitle?:    React.ReactNode
    actions?:     React.ReactNode
    sticky?:      boolean
}) {
    const inner = (
        <div style={sticky ? stickyHeaderInner : { ...stickyHeaderInner, padding: '4px 0 0' }}>
            <div style={{ minWidth: 0 }}>
                {eyebrowText && <div style={{ ...eyebrow, marginBottom: 8 }}>{eyebrowText}</div>}
                <h1 style={serifTitle}>{title}</h1>
                {subtitle && (
                    <p style={{ margin: '6px 0 0', color: 'var(--color-text-secondary)', fontSize: 'var(--text-md)' }}>
                        {subtitle}
                    </p>
                )}
            </div>
            {actions && <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>{actions}</div>}
        </div>
    )
    return sticky ? <div style={stickyHeader}>{inner}</div> : inner
}

export function SectionHead({ children }: { children: React.ReactNode }) {
    return <div style={sectionHead}>{children}</div>
}

export function WideShell({ sidebar, sidebarWidth = 300, collapseAt = 1024, children }: {
    sidebar:       React.ReactNode
    sidebarWidth?: number
    collapseAt?:   number
    children:      React.ReactNode
}) {
    const narrow = useIsMobile(collapseAt)
    return (
        <div style={wideWrap}>
            <div style={{
                display: 'grid',
                gridTemplateColumns: narrow ? '1fr' : `${sidebarWidth}px minmax(0, 1fr)`,
                gap: narrow ? 16 : 28,
                alignItems: 'start',
            }}>
                <aside style={narrow ? undefined : sidebarSticky}>{sidebar}</aside>
                <main style={{ minWidth: 0 }}>{children}</main>
            </div>
        </div>
    )
}

export function SidebarCard({ children, style }: { children: React.ReactNode; style?: React.CSSProperties }) {
    return <div style={{ ...sidebarCard, ...style }}>{children}</div>
}

export function TableScroll({ children }: { children: React.ReactNode }) {
    return <div style={{ width: '100%', overflowX: 'auto' }}>{children}</div>
}

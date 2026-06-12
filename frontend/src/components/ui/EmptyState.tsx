import type { ReactNode } from 'react'
import Icon, { type IconName } from './Icon'

/**
 * Empty-state block — used when a list / page has no data to show.
 *
 * Replaces the previous "(nothing)" placeholders or plain "Empty" labels.
 * Renders a centred icon + title + description + optional CTA, all using
 * the same token surface as every other component.
 */
interface Props {
    icon?:       IconName
    title:       string
    description?: string
    action?:     ReactNode
}

function EmptyState({ icon = 'sparkle', title, description, action }: Props) {
    return (
        <div
            style={{
                display:        'flex',
                flexDirection:  'column',
                alignItems:     'center',
                gap:            12,
                padding:        '48px 24px',
                textAlign:      'center',
            }}
        >
            <div
                style={{
                    width:         48,
                    height:        48,
                    display:       'flex',
                    alignItems:    'center',
                    justifyContent:'center',
                    borderRadius:  'var(--radius-pill)',
                    background:    'var(--color-bg-muted)',
                    color:         'var(--color-text-tertiary)',
                }}
            >
                <Icon name={icon} size={22} />
            </div>
            <div
                style={{
                    fontSize:    'var(--text-md)',
                    fontWeight:  'var(--font-weight-medium)' as unknown as number,
                    color:       'var(--color-text-primary)',
                }}
            >
                {title}
            </div>
            {description && (
                <div
                    style={{
                        fontSize:  'var(--text-sm)',
                        color:     'var(--color-text-secondary)',
                        maxWidth:  340,
                        lineHeight: 1.5,
                    }}
                >
                    {description}
                </div>
            )}
            {action && <div style={{ marginTop: 4 }}>{action}</div>}
        </div>
    )
}

export default EmptyState

import type { CSSProperties } from 'react'

/**
 * Animated placeholder block used while async data is loading.
 *
 * Perceived-speed primitive — replaces blank space or generic spinners
 * during query loading states. Renders a single shimmering rectangle.
 * Compose multiple instances to mimic the final layout (one for header,
 * three for rows, etc.).
 */
interface Props {
    width?:        number | string
    height?:       number | string
    radius?:       number | string
    style?:        CSSProperties
}

function Skeleton({ width = '100%', height = 14, radius = 6, style }: Props) {
    return (
        <span
            aria-hidden="true"
            style={{
                display:      'inline-block',
                width,
                height,
                borderRadius: radius,
                background:   'linear-gradient(90deg, var(--color-bg-muted) 0%, var(--color-border-default) 50%, var(--color-bg-muted) 100%)',
                backgroundSize: '200% 100%',
                animation:    'skeleton-shimmer 1.4s ease-in-out infinite',
                ...style,
            }}
        />
    )
}

export default Skeleton

/* Keyframes are injected once via index.css — see the `@keyframes skeleton-shimmer` block. */

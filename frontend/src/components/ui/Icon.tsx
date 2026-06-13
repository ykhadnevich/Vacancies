export type IconName =
    | 'search' | 'close' | 'check' | 'plus' | 'minus'
    | 'pin' | 'calendar' | 'chevron-down' | 'chevron-up'
    | 'arrow-right' | 'arrow-up-right' | 'refresh'
    | 'upload' | 'file-text' | 'briefcase'
    | 'sparkle' | 'alert-circle' | 'info' | 'check-circle' | 'circle'
    | 'logout' | 'user' | 'globe' | 'trash'
    | 'sun' | 'moon'

interface Props {
    name:    IconName
    size?:   number
    color?:  string
    style?:  React.CSSProperties
    title?:  string
}

const PATHS: Record<IconName, React.ReactNode> = {
    'search':         <><circle cx="11" cy="11" r="8" /><path d="m21 21-4.3-4.3" /></>,
    'close':          <><path d="M18 6 6 18" /><path d="m6 6 12 12" /></>,
    'check':          <path d="M20 6 9 17l-5-5" />,
    'plus':           <><path d="M5 12h14" /><path d="M12 5v14" /></>,
    'minus':          <path d="M5 12h14" />,
    'pin':            <><path d="M20 10c0 7-8 12-8 12s-8-5-8-12a8 8 0 0 1 16 0" /><circle cx="12" cy="10" r="3" /></>,
    'calendar':       <><rect width="18" height="18" x="3" y="4" rx="2" /><path d="M16 2v4" /><path d="M8 2v4" /><path d="M3 10h18" /></>,
    'chevron-down':   <path d="m6 9 6 6 6-6" />,
    'chevron-up':     <path d="m18 15-6-6-6 6" />,
    'arrow-right':    <><path d="M5 12h14" /><path d="m12 5 7 7-7 7" /></>,
    'arrow-up-right': <><path d="M7 7h10v10" /><path d="M7 17 17 7" /></>,
    'refresh':        <><path d="M3 12a9 9 0 0 1 15-6.7L21 8" /><path d="M21 3v5h-5" /><path d="M21 12a9 9 0 0 1-15 6.7L3 16" /><path d="M3 21v-5h5" /></>,
    'upload':         <><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" /><path d="M17 8l-5-5-5 5" /><path d="M12 3v12" /></>,
    'file-text':      <><path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z" /><path d="M14 2v5h5" /><path d="M16 13H8" /><path d="M16 17H8" /><path d="M10 9H8" /></>,
    'briefcase':      <><rect width="20" height="14" x="2" y="7" rx="2" /><path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16" /></>,
    'sparkle':        <><path d="M12 3v18" /><path d="M3 12h18" /><path d="m5 5 14 14" /><path d="m19 5-14 14" /></>,
    'alert-circle':   <><circle cx="12" cy="12" r="10" /><path d="M12 8v4" /><path d="M12 16h.01" /></>,
    'info':           <><circle cx="12" cy="12" r="10" /><path d="M12 16v-4" /><path d="M12 8h.01" /></>,
    'check-circle':   <><circle cx="12" cy="12" r="10" /><path d="m9 12 2 2 4-4" /></>,
    'circle':         <circle cx="12" cy="12" r="10" />,
    'logout':         <><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" /><path d="m16 17 5-5-5-5" /><path d="M21 12H9" /></>,
    'user':           <><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" /><circle cx="12" cy="7" r="4" /></>,
    'globe':          <><circle cx="12" cy="12" r="10" /><path d="M12 2a14.5 14.5 0 0 0 0 20a14.5 14.5 0 0 0 0-20" /><path d="M2 12h20" /></>,
    'trash':          <><path d="M3 6h18" /><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" /><path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" /></>,
    'sun':            <><circle cx="12" cy="12" r="4" /><path d="M12 2v2" /><path d="M12 20v2" /><path d="m4.93 4.93 1.41 1.41" /><path d="m17.66 17.66 1.41 1.41" /><path d="M2 12h2" /><path d="M20 12h2" /><path d="m6.34 17.66-1.41 1.41" /><path d="m19.07 4.93-1.41 1.41" /></>,
    'moon':           <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />,
}

function Icon({ name, size = 16, color, style, title }: Props) {
    return (
        <svg
            xmlns="http://www.w3.org/2000/svg"
            width={size}
            height={size}
            viewBox="0 0 24 24"
            fill="none"
            stroke={color ?? 'currentColor'}
            strokeWidth="1.75"
            strokeLinecap="round"
            strokeLinejoin="round"
            style={{ display: 'inline-block', verticalAlign: 'middle', flexShrink: 0, ...style }}
            aria-hidden={title ? undefined : true}
            role={title ? 'img' : undefined}
        >
            {title && <title>{title}</title>}
            {PATHS[name]}
        </svg>
    )
}

export default Icon

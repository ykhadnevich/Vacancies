import { useEffect, type ReactNode } from 'react'
import Icon from './Icon'
import { useT } from '../../i18n/useT'

interface Props {
    open:      boolean
    onClose:   () => void
    title?:    string
    children:  ReactNode

    width?:    'sm' | 'md' | 'lg'
}

const WIDTHS = { sm: 360, md: 480, lg: 640 } as const


function Modal({ open, onClose, title, children, width = 'md' }: Props) {
    const t = useT()
    useEffect(() => {
        if (!open) return
        const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
        window.addEventListener('keydown', onKey)
        const prev = document.body.style.overflow
        document.body.style.overflow = 'hidden'
        return () => {
            window.removeEventListener('keydown', onKey)
            document.body.style.overflow = prev
        }
    }, [open, onClose])

    if (!open) return null

    return (
        <>
            <div
                onClick={onClose}
                style={{
                    position:   'fixed',
                    inset:      0,
                    background: 'rgba(26, 31, 54, 0.32)',
                    zIndex:     'var(--z-modal)' as unknown as number,
                }}
                aria-hidden="true"
            />
            <div
                role="dialog"
                aria-modal="true"
                aria-labelledby={title ? 'modal-title' : undefined}
                style={{
                    position:      'fixed',
                    top:           '50%',
                    left:          '50%',
                    transform:     'translate(-50%, -50%)',
                    width:         '92vw',
                    maxWidth:      WIDTHS[width],
                    maxHeight:     '85vh',
                    overflowY:     'auto',
                    background:    'var(--color-bg-surface)',
                    borderRadius:  'var(--radius-lg)',
                    boxShadow:     'var(--shadow-xl)',
                    zIndex:        'calc(var(--z-modal) + 1)' as unknown as number,
                    display:       'flex',
                    flexDirection: 'column',
                }}
            >
                {title && (
                    <header style={{
                        display:        'flex',
                        alignItems:     'center',
                        justifyContent: 'space-between',
                        padding:        '16px 20px',
                        borderBottom:   '0.5px solid var(--color-border-default)',
                    }}>
                        <h3 id="modal-title" style={{
                            margin:     0,
                            fontSize:   'var(--text-lg)',
                            fontWeight: 'var(--font-weight-semibold)' as unknown as number,
                        }}>
                            {title}
                        </h3>
                        <button
                            onClick={onClose}
                            aria-label={t('common.close')}
                            style={{
                                background: 'transparent',
                                border:     'none',
                                cursor:     'pointer',
                                padding:    6,
                                borderRadius: 'var(--radius-md)',
                                color:      'var(--color-text-secondary)',
                                display:    'flex',
                                fontFamily: 'inherit',
                            }}
                        >
                            <Icon name="close" size={18} />
                        </button>
                    </header>
                )}
                <div style={{ padding: 20 }}>
                    {children}
                </div>
            </div>
        </>
    )
}

export default Modal

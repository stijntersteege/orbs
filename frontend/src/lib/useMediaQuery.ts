import { useSyncExternalStore } from 'react'

const supported = () => typeof window !== 'undefined' && typeof window.matchMedia === 'function'

/** True while the CSS media query matches, such as "(max-width: 1099px)". False where media queries are not available. */
export function useMediaQuery(query: string): boolean {
  return useSyncExternalStore(
    (onChange) => {
      if (!supported()) return () => {}
      const list = window.matchMedia(query)
      list.addEventListener('change', onChange)
      return () => list.removeEventListener('change', onChange)
    },
    () => supported() && window.matchMedia(query).matches,
    () => false,
  )
}

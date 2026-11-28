import { lazy, Suspense } from 'react'
import styles from './markdown.module.css'

// The renderer and its code highlighter are big, so they load on first use instead of with the app.
const MarkdownRenderer = lazy(() => import('./MarkdownRenderer'))

/** Markdown from orbs or people. Shows the plain text until the renderer has loaded. */
export function Markdown({ children, className }: { children: string; className?: string }) {
  return (
    <Suspense
      fallback={<div className={[styles.plain, className].filter(Boolean).join(' ')}>{children}</div>}
    >
      <MarkdownRenderer className={className}>{children}</MarkdownRenderer>
    </Suspense>
  )
}

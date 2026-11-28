import { useRef, useState, type ComponentPropsWithoutRef, type ReactNode } from 'react'
import ReactMarkdown, { type Components } from 'react-markdown'
import rehypeHighlight from 'rehype-highlight'
import remarkGfm from 'remark-gfm'
import styles from './markdown.module.css'

/** A code block with its language and a button that copies the code. */
function CodeBlock({ children, ...props }: ComponentPropsWithoutRef<'pre'>) {
  const ref = useRef<HTMLPreElement>(null)
  const [copied, setCopied] = useState(false)
  const code = (children as { props?: { className?: string } } | undefined)?.props
  const language = /language-([\w+-]+)/.exec(code?.className ?? '')?.[1]
  return (
    <div className={styles.codeBlock}>
      <div className={styles.codeBar}>
        <span>{language ?? 'code'}</span>
        <button
          type="button"
          className={styles.copy}
          onClick={() => {
            void navigator.clipboard?.writeText(ref.current?.textContent ?? '')
            setCopied(true)
            setTimeout(() => setCopied(false), 1500)
          }}
        >
          {copied ? 'Copied' : 'Copy'}
        </button>
      </div>
      <pre ref={ref} {...props}>
        {children}
      </pre>
    </div>
  )
}

const components: Components = {
  pre: ({ node: _node, ...props }) => <CodeBlock {...props} />,
  // Links open in a new tab and never tell the other site where they came from.
  a: ({ node: _node, children, href, ...props }) => (
    <a {...props} href={href} target="_blank" rel="noopener noreferrer nofollow">
      {children}
    </a>
  ),
  // Pictures from replies are links, not loaded: no tracking pixels, and the strict CSP stays as it is.
  img: ({ src, alt }) =>
    typeof src === 'string' ? (
      <a href={src} target="_blank" rel="noopener noreferrer nofollow">
        {alt || src}
      </a>
    ) : null,
  table: ({ node: _node, ...props }) => (
    <div className={styles.tableWrap}>
      <table {...props} />
    </div>
  ),
}

/**
 * Renders Markdown written by orbs or people: headings, lists, tables, task lists, quotes and highlighted code.
 * Raw HTML in the text is shown as text, never run, and unsafe link targets such as javascript: are dropped.
 */
export default function MarkdownRenderer({
  children,
  className,
}: {
  children: string
  className?: string
}): ReactNode {
  return (
    <div className={[styles.markdown, className].filter(Boolean).join(' ')}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[[rehypeHighlight, { detect: false }]]}
        components={components}
      >
        {children}
      </ReactMarkdown>
    </div>
  )
}

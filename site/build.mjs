// Builds the project website for GitHub Pages: landing page, changelog and 404 page.
// No dependencies. Usage: node site/build.mjs <output-dir> [demo-dir]
// The live demo (frontend `pnpm build:demo`) is copied to <output-dir>/demo when given.

import { cpSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const repoRoot = join(here, '..')
const out = process.argv[2] ?? join(repoRoot, '_site')
const demo = process.argv[3]
const repo = 'https://github.com/stijntersteege/orbs'
const fonts = join(repoRoot, 'frontend/node_modules/@fontsource-variable')

function escapeHtml(text) {
  return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;')
}

/** Inline Markdown: code, links, bold. Enough for a Keep a Changelog file. */
function inline(text) {
  return escapeHtml(text)
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, '<a href="$2">$1</a>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
}

/** Renders CHANGELOG.md: headings, paragraphs and bullet lists. Version headings link to their release. */
export function renderChangelog(markdown) {
  const references = Object.fromEntries(
    [...markdown.matchAll(/^\[([^\]]+)\]:\s*(\S+)\s*$/gm)].map((m) => [m[1], m[2]]),
  )
  const html = []
  let list = false
  const closeList = () => {
    if (list) html.push('</ul>')
    list = false
  }

  for (const line of markdown.split('\n')) {
    if (/^\[[^\]]+\]:\s*\S+/.test(line)) continue
    const heading = line.match(/^(#{1,3})\s+(.*)$/)
    if (heading) {
      closeList()
      const level = heading[1].length
      if (level === 1) continue
      let text = heading[2]
      if (level === 2 && text.trim() === '[Unreleased]') {
        html.push('<!-- unreleased -->')
        continue
      }
      const version = text.match(/^\[([^\]]+)\](.*)$/)
      text = version
        ? `${references[version[1]] ? `<a href="${references[version[1]]}">${escapeHtml(version[1])}</a>` : escapeHtml(version[1])}${escapeHtml(version[2])}`
        : inline(text)
      html.push(`<h${level}>${text}</h${level}>`)
    } else if (/^[-*]\s+/.test(line)) {
      if (!list) html.push('<ul>')
      list = true
      html.push(`<li>${inline(line.replace(/^[-*]\s+/, ''))}</li>`)
    } else if (line.trim()) {
      closeList()
      html.push(`<p>${inline(line.trim())}</p>`)
    } else {
      closeList()
    }
  }
  closeList()
  // Drop an empty Unreleased section; keep it when it has entries.
  return html.join('\n').replace(/<!-- unreleased -->\n?(?=<h2>|$)/, '').replace('<!-- unreleased -->', '<h2>Unreleased</h2>')
}

function layout({ title, description, body, root }) {
  const fill = (text) => text.replaceAll('{{root}}', root).replaceAll('{{repo}}', repo)
  return fill(`<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="description" content="${escapeHtml(description)}" />
    <meta name="theme-color" content="#0F0C1D" />
    <link rel="icon" type="image/svg+xml" href="{{root}}favicon.svg" />
    <link rel="stylesheet" href="{{root}}style.css" />
    <title>${escapeHtml(title)}</title>
  </head>
  <body>
    <div class="wrap">
      <header class="top">
        <a class="brand" href="{{root}}"><img src="{{root}}favicon.svg" width="32" height="32" alt="" />orbs</a>
        <nav class="links" aria-label="Main">
          <a href="{{root}}demo/">Demo</a>
          <a href="{{root}}changelog/">Changelog</a>
          <a href="{{repo}}">GitHub</a>
        </nav>
      </header>
      <main>
${body}
      </main>
      <footer>
        Orbs is open source under the <a href="{{repo}}/blob/main/LICENSE">MIT license</a>.
        <a href="{{repo}}/blob/main/CONTRIBUTING.md">Contribute</a> ·
        <a href="{{repo}}/blob/main/SECURITY.md">Security</a>
      </footer>
    </div>
  </body>
</html>
`)
}

function write(path, html) {
  mkdirSync(dirname(path), { recursive: true })
  writeFileSync(path, html)
}

function build() {
  mkdirSync(out, { recursive: true })
  const description = 'Orbs: open-source, self-hosted AI companions. Any model, any context, your own little orbs.'

  write(
    join(out, 'index.html'),
    layout({ title: 'Orbs', description, root: './', body: readFileSync(join(here, 'pages/index.html'), 'utf8') }),
  )
  write(
    join(out, 'changelog/index.html'),
    layout({
      title: 'Changelog · Orbs',
      description: 'Every notable change to Orbs.',
      root: '../',
      body: `<article class="changelog"><h1>Changelog</h1>\n${renderChangelog(readFileSync(join(repoRoot, 'CHANGELOG.md'), 'utf8'))}</article>`,
    }),
  )
  // GitHub Pages serves 404.html for any missing path, so its links must work from every depth.
  const base = process.env.SITE_BASE_PATH ?? '/'
  write(
    join(out, '404.html'),
    layout({ title: 'Not found · Orbs', description, root: base, body: readFileSync(join(here, 'pages/404.html'), 'utf8') }),
  )

  cpSync(join(here, 'style.css'), join(out, 'style.css'))
  cpSync(join(repoRoot, 'frontend/public/favicon.svg'), join(out, 'favicon.svg'))
  mkdirSync(join(out, 'fonts'), { recursive: true })
  for (const [name, file] of [
    ['unbounded', 'unbounded/files/unbounded-latin-wght-normal.woff2'],
    ['figtree', 'figtree/files/figtree-latin-wght-normal.woff2'],
  ]) {
    if (existsSync(join(fonts, file))) cpSync(join(fonts, file), join(out, `fonts/${name}.woff2`))
  }
  if (demo) cpSync(demo, join(out, 'demo'), { recursive: true })
  writeFileSync(join(out, '.nojekyll'), '')
}

if (process.argv[1] === fileURLToPath(import.meta.url)) build()

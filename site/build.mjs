// Builds the project website for GitHub Pages from the repository itself:
// landing page, docs (everything in docs/), changelog (CHANGELOG.md), 404 page and the live demo.
//
//   node site/build.mjs <output-dir> [demo-dir] [--check]
//
// The live demo (frontend `pnpm build:demo`) is copied to <output-dir>/demo when given.
// The build fails when a relative link in the docs or changelog points at a file that does not exist,
// so broken links are caught in CI before they reach the website.

import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { dirname, join, posix, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { Marked } from 'marked'

const here = dirname(fileURLToPath(import.meta.url))
const repoRoot = join(here, '..')
const args = process.argv.slice(2).filter((a) => !a.startsWith('--'))
const out = args[0] ? resolve(args[0]) : join(repoRoot, '_site')
const demo = args[1] ? resolve(args[1]) : undefined
const repo = 'https://github.com/stijntersteege/orbs'
const branch = 'main'
const fonts = join(repoRoot, 'frontend/node_modules/@fontsource-variable')
const mermaidBundle = join(here, 'node_modules/mermaid/dist/mermaid.min.js')

// ---------------------------------------------------------------------------
// Pages: every Markdown file that becomes a page, keyed by its repository path.

/** Repository path (posix) -> site path (posix, no leading slash, ends with "/" or is ""). */
const pages = new Map([['CHANGELOG.md', 'changelog/']])
const docFiles = walk(join(repoRoot, 'docs'))
  .filter((file) => file.endsWith('.md'))
  .map((file) => posix.join('docs', relative(join(repoRoot, 'docs'), file).split('\\').join('/')))
for (const file of docFiles) pages.set(file, sitePathFor(file))

function walk(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    return statSync(path).isDirectory() ? walk(path) : [path]
  })
}

/** docs/README.md -> docs/, docs/guides/testing.md -> docs/guides/testing/ */
function sitePathFor(file) {
  const withoutExt = file.replace(/\.md$/, '')
  return withoutExt.endsWith('/README') || withoutExt === 'docs/README'
    ? `${withoutExt.slice(0, -'README'.length)}`
    : `${withoutExt}/`
}

// ---------------------------------------------------------------------------
// Markdown

const brokenLinks = []

function escapeHtml(text) {
  return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;')
}

/** GitHub-style heading anchors, so links like `guide.md#adding-a-preset` keep working. */
function slugify(text, seen) {
  const base = text
    .toLowerCase()
    .trim()
    .replace(/<[^>]+>/g, '')
    .replace(/[^\p{L}\p{N}\s_-]/gu, '')
    .replace(/\s/g, '-')
  const count = seen.get(base) ?? 0
  seen.set(base, count + 1)
  return count ? `${base}-${count}` : base
}

/**
 * Rewrites a link found in `source` (a repository path) for the page at `sitePath`.
 * Markdown pages become website links; other repository files link to GitHub.
 */
function rewriteLink(href, source, sitePath) {
  if (/^([a-z]+:|#|\/\/)/i.test(href)) return href
  const [path, hash] = href.split('#')
  const target = posix.normalize(posix.join(posix.dirname(source), decodeURI(path)))
  if (target.startsWith('..') || !existsSync(join(repoRoot, target))) {
    brokenLinks.push(`${source}: ${href}`)
    return href
  }
  const anchor = hash ? `#${hash}` : ''
  const page = pages.get(target) ?? (statSync(join(repoRoot, target)).isDirectory() && pages.get(`${target}/README.md`))
  if (page !== undefined && page !== false) {
    return `${posix.relative(sitePath || '.', page || '.') || '.'}/${anchor}`.replace(/\/+(#|$)/, '/$1')
  }
  const kind = statSync(join(repoRoot, target)).isDirectory() ? 'tree' : 'blob'
  return `${repo}/${kind}/${branch}/${target}${anchor}`
}

/** Renders one repository Markdown file for the page at `sitePath`. */
function renderMarkdown(markdown, source, sitePath) {
  const seen = new Map()
  const state = { mermaid: false }
  const marked = new Marked({ gfm: true })
  marked.use({
    renderer: {
      heading({ tokens, depth }) {
        const text = this.parser.parseInline(tokens)
        const plain = this.parser.parseInline(tokens, this.parser.textRenderer)
        const id = slugify(plain, seen)
        return `<h${depth} id="${id}"><a class="anchor" href="#${id}" aria-hidden="true">#</a>${text}</h${depth}>\n`
      },
      link({ href, title, tokens }) {
        const text = this.parser.parseInline(tokens)
        const titleAttr = title ? ` title="${escapeHtml(title)}"` : ''
        return `<a href="${escapeHtml(rewriteLink(href, source, sitePath))}"${titleAttr}>${text}</a>`
      },
      image({ href, title, text }) {
        const local = !/^([a-z]+:|\/\/)/i.test(href)
        const src = local
          ? `${repo}/raw/${branch}/${posix.normalize(posix.join(posix.dirname(source), href))}`
          : href
        const titleAttr = title ? ` title="${escapeHtml(title)}"` : ''
        return `<img src="${escapeHtml(src)}" alt="${escapeHtml(text)}"${titleAttr} />`
      },
      code({ text, lang }) {
        if (lang === 'mermaid') {
          state.mermaid = true
          return `<pre class="mermaid">${escapeHtml(text)}</pre>\n`
        }
        const cls = lang ? ` class="language-${escapeHtml(lang)}"` : ''
        return `<pre><code${cls}>${escapeHtml(text)}</code></pre>\n`
      },
      table(token) {
        // Wrap tables so wide ones scroll on phones instead of widening the page.
        return `<div class="table">${marked.Renderer.prototype.table.call(this, token)}</div>\n`
      },
    },
  })
  const html = marked.parse(markdown)
  const title = markdown.match(/^#\s+(.+)$/m)?.[1]?.replace(/[*_`]/g, '') ?? 'Orbs'
  return { html, title, mermaid: state.mermaid }
}

/** Keep a Changelog: hide the [Unreleased] section on the website while it is empty. */
export function prepareChangelog(markdown) {
  const lines = markdown.split('\n')
  const start = lines.findIndex((line) => /^## \[?Unreleased\]?\s*$/i.test(line))
  if (start === -1) return markdown
  let end = start + 1
  while (end < lines.length && !/^## /.test(lines[end]) && !/^\[[^\]]+\]:\s/.test(lines[end])) end++
  const empty = lines.slice(start + 1, end).every((line) => !line.trim())
  return empty ? [...lines.slice(0, start), ...lines.slice(end)].join('\n') : markdown
}

// ---------------------------------------------------------------------------
// Layout

const DOC_SECTIONS = [
  { title: 'Guides', dir: 'docs/guides/' },
  { title: 'Architecture (arc42)', dir: 'docs/architecture/' },
  { title: 'Decisions (ADRs)', dir: 'docs/adr/' },
]

function titleOf(file) {
  return readFileSync(join(repoRoot, file), 'utf8').match(/^#\s+(.+)$/m)?.[1]?.replace(/[*_`]/g, '') ?? file
}

/** Sidebar for the docs: overview, then each folder in file-name order (its README first). */
function docsNav(currentFile, root) {
  const link = (file, label) =>
    `<a href="${root}${pages.get(file)}"${file === currentFile ? ' aria-current="page"' : ''}>${escapeHtml(label)}</a>`
  const sections = DOC_SECTIONS.map(({ title, dir }) => {
    const files = docFiles
      .filter((f) => f.startsWith(dir) && !f.slice(dir.length).includes('/') && !f.endsWith('/template.md'))
      .sort((a, b) => (a.endsWith('README.md') ? -1 : b.endsWith('README.md') ? 1 : a.localeCompare(b)))
    if (files.length === 0) return ''
    const items = files.map((f) => `<li>${link(f, f.endsWith('README.md') ? 'Overview' : titleOf(f))}</li>`)
    return `<details${files.includes(currentFile) ? ' open' : ''}><summary>${escapeHtml(title)}</summary><ul>${items.join('')}</ul></details>`
  })
  return `<nav class="docs-nav" aria-label="Documentation">${link('docs/README.md', 'Overview')}${sections.join('')}</nav>`
}

function layout({ title, description, body, root, mermaid = false }) {
  const fill = (text) => text.replaceAll('{{root}}', root).replaceAll('{{repo}}', repo)
  const mermaidScript = mermaid
    ? `<script src="{{root}}assets/mermaid.min.js"></script>
    <script>
      mermaid.initialize({
        startOnLoad: true,
        theme: matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'neutral',
      })
    </script>`
    : ''
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
          <a href="{{root}}docs/">Docs</a>
          <a href="{{root}}changelog/">Changelog</a>
          <a href="{{repo}}">GitHub</a>
        </nav>
      </header>
      <main>
${body}
      </main>
      <footer>
        Orbs is open source under the <a href="{{repo}}/blob/${branch}/LICENSE">MIT license</a>.
        <a href="{{repo}}/blob/${branch}/CONTRIBUTING.md">Contribute</a> ·
        <a href="{{repo}}/blob/${branch}/SECURITY.md">Security</a>
      </footer>
    </div>
    ${mermaidScript}
  </body>
</html>
`)
}

/** Relative path from a site page back to the site root, e.g. "../../". */
function rootFor(sitePath) {
  const depth = sitePath.split('/').filter(Boolean).length
  return depth ? '../'.repeat(depth) : './'
}

// ---------------------------------------------------------------------------
// Build

function write(path, html) {
  mkdirSync(dirname(path), { recursive: true })
  writeFileSync(path, html)
}

function build() {
  const check = process.argv.includes('--check')
  // Only ever clear an earlier site build, never a folder with other content (like the repository).
  if (existsSync(out) && readdirSync(out).length > 0 && !existsSync(join(out, '.nojekyll'))) {
    console.error(`Refusing to overwrite ${out}: it is not empty and not an earlier site build.`)
    process.exit(1)
  }
  rmSync(out, { recursive: true, force: true })
  mkdirSync(out, { recursive: true })
  const description = 'Orbs: open-source, self-hosted AI companions. Any model, any context, your own little orbs.'

  write(
    join(out, 'index.html'),
    layout({ title: 'Orbs', description, root: './', body: readFileSync(join(here, 'pages/index.html'), 'utf8') }),
  )

  const changelog = renderMarkdown(
    prepareChangelog(readFileSync(join(repoRoot, 'CHANGELOG.md'), 'utf8')),
    'CHANGELOG.md',
    'changelog/',
  )
  write(
    join(out, 'changelog/index.html'),
    layout({
      title: 'Changelog · Orbs',
      description: 'Every notable change to Orbs.',
      root: '../',
      body: `<article class="prose">${changelog.html}</article>`,
    }),
  )

  for (const file of docFiles) {
    const sitePath = pages.get(file)
    const root = rootFor(sitePath)
    const doc = renderMarkdown(readFileSync(join(repoRoot, file), 'utf8'), file, sitePath)
    write(
      join(out, sitePath, 'index.html'),
      layout({
        title: `${doc.title} · Orbs docs`,
        description: `${doc.title}: Orbs documentation.`,
        root,
        mermaid: doc.mermaid,
        body: `<div class="docs">
${docsNav(file, root)}
<article class="prose">
${doc.html}
<p class="edit"><a href="${repo}/edit/${branch}/${file}">Edit this page on GitHub</a></p>
</article>
</div>`,
      }),
    )
  }

  // GitHub Pages serves 404.html for any missing path, so its links must work from every depth.
  const base = process.env.SITE_BASE_PATH ?? '/'
  write(
    join(out, '404.html'),
    layout({ title: 'Not found · Orbs', description, root: base, body: readFileSync(join(here, 'pages/404.html'), 'utf8') }),
  )

  if (brokenLinks.length) {
    console.error(`Broken links:\n  ${brokenLinks.join('\n  ')}`)
    process.exit(1)
  }
  writeFileSync(join(out, '.nojekyll'), '')
  if (check) {
    console.log(`Site OK: ${docFiles.length} docs pages, changelog and landing page.`)
    return
  }

  cpSync(join(here, 'style.css'), join(out, 'style.css'))
  cpSync(join(repoRoot, 'frontend/public/favicon.svg'), join(out, 'favicon.svg'))
  mkdirSync(join(out, 'assets'), { recursive: true })
  if (existsSync(mermaidBundle)) cpSync(mermaidBundle, join(out, 'assets/mermaid.min.js'))
  mkdirSync(join(out, 'fonts'), { recursive: true })
  for (const [name, file] of [
    ['unbounded', 'unbounded/files/unbounded-latin-wght-normal.woff2'],
    ['figtree', 'figtree/files/figtree-latin-wght-normal.woff2'],
  ]) {
    if (existsSync(join(fonts, file))) cpSync(join(fonts, file), join(out, `fonts/${name}.woff2`))
  }
  if (demo) cpSync(demo, join(out, 'demo'), { recursive: true })
  console.log(`Site built in ${out}`)
}

if (process.argv[1] === fileURLToPath(import.meta.url)) build()

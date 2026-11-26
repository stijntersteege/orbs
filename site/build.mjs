// Builds the project website for GitHub Pages from the repository itself:
// landing, Get Orbs and Open source pages (site/pages), the docs (everything in docs/), the changelog
// (CHANGELOG.md), the policies (PRIVACY.md, SECURITY.md, CODE_OF_CONDUCT.md, CONTRIBUTING.md, LICENSE),
// a 404 page and the live demo.
//
//   node site/build.mjs <output-dir> [demo-dir] [--check]
//
// The live demo (frontend `pnpm build:demo`) is copied to <output-dir>/demo when given.
// The build fails when a relative link in the docs or changelog points at a file that does not exist,
// so broken links are caught in CI before they reach the website.

import { execFileSync } from 'node:child_process'
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { dirname, join, posix, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { Marked } from 'marked'
import { BRANCH as branch, escapeHtml, layout, policyNav, REPO as repo } from './lib/layout.mjs'
import { get } from './pages/get.mjs'
import { home } from './pages/home.mjs'
import { licenseBody, notFound } from './pages/misc.mjs'
import { openSource } from './pages/open-source.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const repoRoot = join(here, '..')
const args = process.argv.slice(2).filter((a) => !a.startsWith('--'))
const out = args[0] ? resolve(args[0]) : join(repoRoot, '_site')
const demo = args[1] ? resolve(args[1]) : undefined
const fontFiles = [
  ['geist', 'frontend/node_modules/@fontsource-variable/geist/files/geist-latin-wght-normal.woff2'],
  ['geist-mono', 'frontend/node_modules/@fontsource-variable/geist-mono/files/geist-mono-latin-wght-normal.woff2'],
]
const mermaidBundle = join(here, 'node_modules/mermaid/dist/mermaid.min.js')

// ---------------------------------------------------------------------------
// Pages: every Markdown file that becomes a page, keyed by its repository path.

/** Repository path (posix) -> site path (posix, no leading slash, ends with "/" or is ""). */
const POLICY_FILES = [
  ['PRIVACY.md', 'privacy/', 'privacy'],
  ['SECURITY.md', 'security/', 'security'],
  ['CODE_OF_CONDUCT.md', 'code-of-conduct/', 'code-of-conduct'],
  ['CONTRIBUTING.md', 'contributing/', 'contributing'],
]
const pages = new Map([
  ['CHANGELOG.md', 'changelog/'],
  ['LICENSE', 'license/'],
  ...POLICY_FILES.map(([file, path]) => [file, path]),
])
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
// Docs navigation

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
  return `<nav class="side-nav docs-nav" aria-label="Documentation"><span class="side-title">Documentation</span>${link('docs/README.md', 'Overview')}${sections.join('')}</nav>`
}

/** Relative path from a site page back to the site root, e.g. "../../". */
function rootFor(sitePath) {
  const depth = sitePath.split('/').filter(Boolean).length
  return depth ? '../'.repeat(depth) : './'
}

/** Two columns: a sidebar and an article. */
function withSidebar(nav, article, footer = '') {
  return `<div class="wrap with-sidebar">
${nav}
<article class="prose">
${article}
${footer}
</article>
</div>`
}

/** Shows when a repository file last changed, from Git when available. */
function lastUpdated(file) {
  try {
    const iso = execFileSync('git', ['log', '-1', '--format=%cs', '--', file], { cwd: repoRoot, encoding: 'utf8' }).trim()
    if (!iso) return ''
    const date = new Date(`${iso}T00:00:00Z`).toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' })
    return `<p class="updated">Last updated ${date}</p>`
  } catch {
    return ''
  }
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

  // Hand-made pages
  write(join(out, 'index.html'), layout({ title: 'Orbs · Little orbs. Big context.', description, root: './', current: 'home', body: home({ root: './' }) }))
  write(
    join(out, 'get/index.html'),
    layout({ title: 'Get Orbs', description: 'Install Orbs with Docker or from source in about two minutes.', root: '../', current: 'get/', body: get({ root: '../' }) }),
  )
  write(
    join(out, 'open-source/index.html'),
    layout({ title: 'Open source · Orbs', description: 'Orbs is MIT licensed. What that means, and how to help.', root: '../', current: 'open-source/', body: openSource({ root: '../' }) }),
  )

  // Changelog
  const changelog = renderMarkdown(prepareChangelog(readFileSync(join(repoRoot, 'CHANGELOG.md'), 'utf8')), 'CHANGELOG.md', 'changelog/')
  write(
    join(out, 'changelog/index.html'),
    layout({
      title: 'Changelog · Orbs',
      description: 'Every notable change to Orbs.',
      root: '../',
      current: 'changelog/',
      body: `<div class="wrap narrow-page"><article class="prose">${changelog.html}</article></div>`,
    }),
  )

  // Policies, from the files in the repository root
  for (const [file, sitePath, id] of POLICY_FILES) {
    const doc = renderMarkdown(readFileSync(join(repoRoot, file), 'utf8'), file, sitePath)
    write(
      join(out, sitePath, 'index.html'),
      layout({
        title: `${doc.title} · Orbs`,
        description: `${doc.title} for Orbs, a personal open-source hobby project.`,
        root: '../',
        body: withSidebar(
          policyNav('../', id),
          `${lastUpdated(file)}\n${doc.html}`,
          `<p class="edit"><a href="${repo}/blob/${branch}/${file}">View ${file} on GitHub</a></p>`,
        ),
      }),
    )
  }
  write(
    join(out, 'license/index.html'),
    layout({
      title: 'License · Orbs',
      description: 'Orbs is released under the MIT License.',
      root: '../',
      body: withSidebar(
        policyNav('../', 'license'),
        licenseBody(readFileSync(join(repoRoot, 'LICENSE'), 'utf8')),
        `<p class="edit"><a href="${repo}/blob/${branch}/LICENSE">View LICENSE on GitHub</a></p>`,
      ),
    }),
  )

  // Docs
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
        current: 'docs/',
        mermaid: doc.mermaid,
        body: withSidebar(docsNav(file, root), doc.html, `<p class="edit"><a href="${repo}/edit/${branch}/${file}">Edit this page on GitHub</a></p>`),
      }),
    )
  }

  // GitHub Pages serves 404.html for any missing path, so its links must work from every depth.
  const base = process.env.SITE_BASE_PATH ?? '/'
  write(join(out, '404.html'), layout({ title: 'Not found · Orbs', description, root: base, body: notFound({ root: base }) }))

  if (brokenLinks.length) {
    console.error(`Broken links:\n  ${brokenLinks.join('\n  ')}`)
    process.exit(1)
  }
  writeFileSync(join(out, '.nojekyll'), '')
  if (check) {
    console.log(`Site OK: ${docFiles.length} docs pages, ${POLICY_FILES.length + 1} policy pages, changelog and landing pages.`)
    return
  }

  cpSync(join(here, 'style.css'), join(out, 'style.css'))
  cpSync(join(here, 'theme.js'), join(out, 'theme.js'))
  cpSync(join(repoRoot, 'frontend/public/favicon.svg'), join(out, 'favicon.svg'))
  mkdirSync(join(out, 'assets'), { recursive: true })
  if (existsSync(mermaidBundle)) cpSync(mermaidBundle, join(out, 'assets/mermaid.min.js'))
  mkdirSync(join(out, 'fonts'), { recursive: true })
  for (const [name, file] of fontFiles) {
    if (existsSync(join(repoRoot, file))) cpSync(join(repoRoot, file), join(out, `fonts/${name}.woff2`))
  }
  if (demo) cpSync(demo, join(out, 'demo'), { recursive: true })
  console.log(`Site built in ${out}`)
}

if (process.argv[1] === fileURLToPath(import.meta.url)) build()

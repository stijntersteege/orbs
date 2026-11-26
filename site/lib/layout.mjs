// Shared page chrome for the website: header, footer, policy sidebar and the HTML document.
import { orb } from './orb.mjs'

export const REPO = 'https://github.com/stijntersteege/orbs'
export const BRANCH = 'main'
export const EMAIL = 'mail@stijntersteege.com'

export function escapeHtml(text) {
  return String(text)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
}

const ICONS = {
  star: '<path d="M12 3l2.8 5.6 6.2.9-4.5 4.4 1 6.1L12 17.1 6.5 20l1-6.1L3 9.5l6.2-.9z"/>',
  menu: '<path d="M4 7h16M4 12h16M4 17h16"/>',
  arrow: '<path d="M5 12h14M13 6l6 6-6 6"/>',
  up: '<path d="M12 19V5M5 12l7-7 7 7"/>',
  check: '<path d="M5 12.5l4.5 4.5L19 7.5"/>',
  server: '<path d="M3 5h18v11H3z M8 20h8 M12 16v4"/>',
  lock: '<path d="M5 11h14v10H5z M8 11V7a4 4 0 0 1 8 0v4"/>',
  eye: '<path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z"/>',
  shield: '<path d="M12 3l8 4v5c0 5-3.5 8-8 9-4.5-1-8-4-8-9V7z"/>',
  sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>',
  moon: '<path d="M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5z"/>',
}

/** An inline stroke icon. Decorative unless a label is given. */
export function icon(name, size = 18) {
  return `<svg class="icon" width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${ICONS[name]}</svg>`
}

const NAV = [
  ['Models', '#models', '#models'],
  ['Context', '#context', '#context'],
  ['Use cases', '#jobs', '#jobs'],
  ['Docs', 'docs/'],
  ['Open source', 'open-source/'],
]

function header(root, current) {
  const links = NAV.map(([label, href, homeHref]) => {
    const target = current === 'home' && homeHref ? homeHref : `${root}${href}`
    const active = current && current !== 'home' && href.startsWith(current) ? ' aria-current="page"' : ''
    return `<a href="${target}"${active}>${label}</a>`
  }).join('')
  return `<a class="announce" href="${root}changelog/">Orbs 0.1 is here: any model, any context, open source. <span>Read the changelog →</span></a>
<header class="site-header">
  <div class="header-inner">
    <a class="brand" href="${root}">${orb({ eyes: 'dots', accessory: 'none', blush: false, glow: false, size: 30 })}<span>orbs</span></a>
    <nav class="main-nav" aria-label="Main">${links}</nav>
    <div class="header-actions">
      <button type="button" class="theme-toggle" data-theme-toggle aria-label="Switch between light and dark mode">${icon('sun', 18).replace('class="icon"', 'class="icon icon-sun"')}${icon('moon', 18).replace('class="icon"', 'class="icon icon-moon"')}</button>
      <a class="v2-btn v2-btn-small v2-btn-quiet ghost" href="${REPO}">${icon('star', 15)}Star on GitHub</a>
      <a class="v2-btn v2-btn-small v2-btn-solid" href="${root}get/">Get started</a>
    </div>
  </div>
</header>`
}

function footer(root) {
  const col = (title, links) =>
    `<nav class="footer-col" aria-label="${title}"><span class="footer-title">${title}</span>${links
      .map(([label, href]) => `<a href="${href.startsWith('http') ? href : root + href}">${label}</a>`)
      .join('')}</nav>`
  return `<footer class="site-footer">
  <div class="footer-inner">
    <div class="footer-cols">
      <div class="footer-about">
        <span class="footer-brand">orbs</span>
        <p>The open-source home for AI companions.</p>
      </div>
      ${col('Product', [
        ['Get started', 'get/'],
        ['Models', '#models'],
        ['Context', '#context'],
        ['Live demo', 'demo/'],
        ['Changelog', 'changelog/'],
      ])}
      ${col('Open source', [
        ['GitHub', REPO],
        ['Contribute', 'open-source/'],
        ['Docs', 'docs/'],
        ['License', 'license/'],
        ['Security', 'security/'],
      ])}
      ${col('Legal', [
        ['Privacy', 'privacy/'],
        ['Code of conduct', 'code-of-conduct/'],
        ['Contributing', 'contributing/'],
      ])}
    </div>
    <div class="footer-bottom">
      <span>© 2026 Orbs contributors · <a href="${root}license/">MIT License</a></span>
      <span>Made with <span role="img" aria-label="love">❤️</span> by <a href="https://stijntersteege.nl/">Stijn ter Steege</a></span>
    </div>
  </div>
</footer>`
}

export const POLICIES = [
  ['privacy', 'Privacy', 'privacy/'],
  ['security', 'Security', 'security/'],
  ['code-of-conduct', 'Code of conduct', 'code-of-conduct/'],
  ['contributing', 'Contributing', 'contributing/'],
  ['license', 'License (MIT)', 'license/'],
]

/** Sidebar for the policy pages, with a contact box. */
export function policyNav(root, active) {
  const links = POLICIES.map(
    ([id, label, href]) =>
      `<a href="${root}${href}"${id === active ? ' aria-current="page"' : ''}>${label}</a>`,
  ).join('')
  return `<nav class="side-nav policy-nav" aria-label="Policies">
  <span class="side-title">Policies</span>${links}
  <div class="side-box"><strong>Questions?</strong><a href="mailto:${EMAIL}">${EMAIL}</a></div>
</nav>`
}

/** A whole HTML document. `current` marks the active main-nav item ('home', 'docs/', ...). */
export function layout({ title, description, body, root, current, mermaid = false }) {
  // Diagrams render in the current theme and re-render when the switch changes it.
  const mermaidScript = mermaid
    ? `<script src="${root}assets/mermaid.min.js"></script>
    <script>
      ;(() => {
        const nodes = [...document.querySelectorAll('pre.mermaid')]
        for (const node of nodes) node.dataset.source = node.textContent
        const isLight = () =>
          (document.documentElement.dataset.theme || (matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark')) === 'light'
        const draw = () => {
          mermaid.initialize({ startOnLoad: false, theme: isLight() ? 'neutral' : 'dark' })
          for (const node of nodes) {
            node.removeAttribute('data-processed')
            node.textContent = node.dataset.source
          }
          mermaid.run({ nodes })
        }
        draw()
        document.addEventListener('orbs:theme', draw)
      })()
    </script>`
    : ''
  return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="description" content="${escapeHtml(description)}" />
    <meta name="theme-color" content="#0B0A12" />
    <script>
      // Apply a stored light/dark choice before the page paints (shared with the live demo).
      try {
        const theme = JSON.parse(localStorage.getItem('orbs.settings.v1') || '{}').theme
        if (theme === 'light' || theme === 'dark') document.documentElement.dataset.theme = theme
      } catch {}
    </script>
    <meta property="og:title" content="${escapeHtml(title)}" />
    <meta property="og:description" content="${escapeHtml(description)}" />
    <meta property="og:type" content="website" />
    <link rel="icon" type="image/svg+xml" href="${root}favicon.svg" />
    <link rel="stylesheet" href="${root}style.css" />
    <title>${escapeHtml(title)}</title>
  </head>
  <body>
    <a class="skip" href="#main">Skip to content</a>
    ${header(root, current)}
    <main id="main">
${body}
    </main>
    ${footer(root)}
    <script src="${root}theme.js" defer></script>
    ${mermaidScript}
  </body>
</html>
`
}

// Light/dark switch for the website. The choice is stored like the app stores it (orbs.settings.v1 → theme),
// so the website and the live demo, which share this origin, always follow the same setting.
// With no choice stored, the site follows the system preference.
;(() => {
  const KEY = 'orbs.settings.v1'
  const root = document.documentElement
  const lightQuery = matchMedia('(prefers-color-scheme: light)')

  const effective = () => root.dataset.theme || (lightQuery.matches ? 'light' : 'dark')

  function save(theme) {
    try {
      const settings = JSON.parse(localStorage.getItem(KEY) || '{}')
      localStorage.setItem(KEY, JSON.stringify({ ...settings, theme }))
    } catch {
      // Storage blocked: the switch still works for this page.
    }
  }

  function render() {
    const theme = effective()
    for (const button of document.querySelectorAll('[data-theme-toggle]')) {
      const label = theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'
      button.setAttribute('aria-label', label)
      button.title = label
    }
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', theme === 'dark' ? '#0F0C1D' : '#FFF8EE')
  }

  function apply(theme) {
    root.dataset.theme = theme
    save(theme)
    render()
    // The embedded demo reads the setting when it loads.
    for (const frame of document.querySelectorAll('iframe[data-follows-theme]')) {
      try {
        frame.contentWindow.location.reload()
      } catch {
        frame.src = frame.src
      }
    }
    document.dispatchEvent(new CustomEvent('orbs:theme', { detail: theme }))
  }

  document.addEventListener('click', (event) => {
    if (event.target.closest('[data-theme-toggle]')) apply(effective() === 'dark' ? 'light' : 'dark')
  })
  lightQuery.addEventListener('change', render)
  render()
})()

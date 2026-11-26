import { useState } from 'react'
import { Link, NavLink, Outlet, useMatch } from 'react-router'
import { useContextSources, useOrbs } from '@/api/queries'
import type { ContextSource, Orb as OrbModel } from '@/api/types'
import { Orb } from '@/components/orb/Orb'
import { Icon, type IconName } from '@/components/ui/icons'
import { DemoBanner } from '@/demo/DemoBanner'
import { useSettings } from '@/app/SettingsContext'
import { usePopover } from '@/lib/usePopover'
import styles from './AppShell.module.css'

/** Everything that is not chatting with an orb lives behind one menu. */
export const MENU: { to: string; label: string; hint: string; icon: IconName }[] = [
  { to: '/models', label: 'Models', hint: 'Providers and API keys', icon: 'models' },
  { to: '/context', label: 'Context', hint: 'Notes and web pages', icon: 'context' },
  { to: '/orbs', label: 'Manage orbs', hint: 'Faces, instructions, delete', icon: 'orbs' },
  { to: '/settings', label: 'Settings', hint: 'Theme, motion and server', icon: 'settings' },
]

type OrbStatus = { tone: 'ok' | 'warn' | 'err'; label: string }

/** Green when everything the orb knows is ready, amber while a source syncs, red when one failed. */
function statusOf(orb: OrbModel, sources: ContextSource[] | undefined): OrbStatus {
  const linked = sources?.filter((s) => orb.contextSourceIds.includes(s.id)) ?? []
  if (linked.some((s) => s.status === 'failed')) return { tone: 'err', label: 'A source failed to sync' }
  if (linked.some((s) => s.status === 'pending')) return { tone: 'warn', label: 'A source is syncing' }
  return { tone: 'ok', label: 'Ready' }
}

function SettingsMenu() {
  const { open, setOpen, ref, toggle } = usePopover()

  return (
    <div className={styles.menuWrap} ref={ref}>
      {open && (
        <nav aria-label="Menu" className={styles.menu}>
          {MENU.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end
              className={styles.menuItem}
              onClick={() => setOpen(false)}
            >
              <Icon name={item.icon} />
              <span className={styles.menuText}>
                <span>{item.label}</span>
                <span className={styles.menuHint}>{item.hint}</span>
              </span>
            </NavLink>
          ))}
        </nav>
      )}
      <button
        type="button"
        className={styles.menuButton}
        aria-expanded={open}
        aria-haspopup="true"
        onClick={toggle}
      >
        <Icon name="settings" />
        <span>Settings and more</span>
      </button>
    </div>
  )
}

/** Your orbs are always in the sidebar; pick one to talk to it. On phones the sidebar slides in. */
export function AppShell() {
  const { data: orbs } = useOrbs()
  const { data: sources } = useContextSources()
  const { settings } = useSettings()
  // The layout sits above the chat route, so it reads the orb from the URL itself.
  const orbId = useMatch('/orbs/:orbId/*')?.params.orbId
  const [drawer, setDrawer] = useState(false)

  // On the home screen the chat opens with the orb you used last.
  const activeId = orbId ?? (orbs?.find((o) => o.id === settings.lastOrbId) ?? orbs?.[0])?.id

  return (
    <div className={styles.shell}>
      <a href="#main" className={styles.skip}>
        Skip to content
      </a>

      <header className={styles.topBar}>
        <button
          type="button"
          className={styles.iconButton}
          aria-label="Open menu"
          aria-expanded={drawer}
          onClick={() => setDrawer(true)}
        >
          <Icon name="menu" />
        </button>
        <Link to="/" className={styles.brand}>
          <span className={styles.wordmark}>orbs</span>
        </Link>
        <Link to="/orbs/new" className={styles.iconButton} aria-label="New orb">
          <Icon name="plus" />
        </Link>
      </header>

      {drawer && <div className={styles.scrim} onClick={() => setDrawer(false)} aria-hidden="true" />}

      {/* Following a link inside the drawer closes it. */}
      <aside
        className={[styles.sidebar, drawer && styles.open].filter(Boolean).join(' ')}
        onClickCapture={(e) => {
          if ((e.target as Element).closest('a')) setDrawer(false)
        }}
      >
        <div className={styles.sidebarHead}>
          <Link to="/" className={styles.brand}>
            <Orb
              size={28}
              appearance={{ eyes: 'dots', accessory: 'none', blush: false, glow: false, floating: false }}
            />
            <span className={styles.wordmark}>orbs</span>
          </Link>
          <button
            type="button"
            className={[styles.iconButton, styles.closeDrawer].join(' ')}
            aria-label="Close menu"
            onClick={() => setDrawer(false)}
          >
            <Icon name="close" />
          </button>
        </div>

        <nav aria-label="Your orbs" className={styles.roster}>
          <span className={styles.sectionLabel}>Your orbs</span>
          {orbs?.map((orb) => {
            const status = statusOf(orb, sources)
            const active = orb.id === activeId
            return (
              <Link
                key={orb.id}
                to={`/orbs/${orb.id}/chat`}
                className={[styles.orbItem, active && styles.active].filter(Boolean).join(' ')}
                aria-current={active ? 'page' : undefined}
              >
                <Orb
                  size={32}
                  appearance={{ ...orb.appearance, glow: false, floating: false, accessory: 'none' }}
                />
                <span className={styles.orbText}>
                  <span className={styles.orbName}>{orb.name}</span>
                  {orb.description && <span className={styles.orbDescription}>{orb.description}</span>}
                </span>
                <span className={styles.dot} data-tone={status.tone} title={status.label}>
                  <span className="visually-hidden">{status.label}</span>
                </span>
              </Link>
            )
          })}
          <Link to="/orbs/new" className={styles.newOrb}>
            <span className={styles.newOrbIcon}>
              <Icon name="plus" size={16} />
            </span>
            New orb
          </Link>
        </nav>

        <SettingsMenu />
      </aside>

      <main id="main" className={styles.main}>
        <DemoBanner />
        <Outlet />
      </main>
    </div>
  )
}

/** Padding and a readable width for the pages behind the menu. */
export function PageLayout() {
  return (
    <div className={styles.page}>
      <Outlet />
    </div>
  )
}

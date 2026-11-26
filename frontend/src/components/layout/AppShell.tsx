import { useState } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router'
import { useConversations, useOrbs } from '@/api/queries'
import { Orb } from '@/components/orb/Orb'
import { Icon, type IconName } from '@/components/ui/icons'
import { DemoBanner } from '@/demo/DemoBanner'
import { useSettings } from '@/app/SettingsContext'
import { usePopover } from '@/lib/usePopover'
import styles from './AppShell.module.css'

/** Everything that is not chatting lives behind one menu. */
export const MENU: { to: string; label: string; hint: string; icon: IconName }[] = [
  { to: '/models', label: 'Models', hint: 'Providers and API keys', icon: 'models' },
  { to: '/orbs', label: 'Orbs', hint: 'Names, faces and instructions', icon: 'orbs' },
  { to: '/context', label: 'Context', hint: 'Notes and web pages', icon: 'context' },
  { to: '/settings', label: 'Settings', hint: 'Theme, motion and server', icon: 'settings' },
]

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

/** A quiet sidebar with your chats, and one menu for everything else. On phones it slides in. */
export function AppShell() {
  const { data: orbs } = useOrbs()
  const { data: conversations } = useConversations()
  const { settings } = useSettings()
  const navigate = useNavigate()
  const [drawer, setDrawer] = useState(false)

  const orbOf = (id: string) => orbs?.find((o) => o.id === id)
  const startOrb = orbs?.find((o) => o.id === settings.lastOrbId) ?? orbs?.[0]
  const newChat = () => {
    setDrawer(false)
    navigate(startOrb ? `/orbs/${startOrb.id}/chat` : '/')
  }

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
        <button type="button" className={styles.iconButton} aria-label="New chat" onClick={newChat}>
          <Icon name="plus" />
        </button>
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

        <button type="button" className={styles.newChat} onClick={newChat}>
          <Icon name="edit" size={18} /> New chat
        </button>

        <nav aria-label="Chats" className={styles.chats}>
          {conversations && conversations.length > 0 ? (
            <>
              <span className={styles.sectionLabel}>Chats</span>
              {conversations.map((c) => {
                const orb = orbOf(c.orbId)
                return (
                  <NavLink key={c.id} to={`/orbs/${c.orbId}/chat/${c.id}`} className={styles.chatItem}>
                    {orb && (
                      <Orb
                        size={20}
                        appearance={{ ...orb.appearance, glow: false, floating: false, accessory: 'none' }}
                      />
                    )}
                    <span className={styles.chatTitle}>{c.title}</span>
                  </NavLink>
                )
              })}
            </>
          ) : (
            <p className={styles.noChats}>Your chats show up here.</p>
          )}
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

import { Link, NavLink, Outlet } from 'react-router'
import { useOrbs } from '@/api/queries'
import { Orb } from '@/components/orb/Orb'
import { Icon, type IconName } from '@/components/ui/icons'
import { DemoBanner } from '@/demo/DemoBanner'
import styles from './AppShell.module.css'

const NAV: { to: string; label: string; icon: IconName; end?: boolean }[] = [
  { to: '/', label: 'Orbs', icon: 'orbs', end: true },
  { to: '/context', label: 'Context', icon: 'context' },
  { to: '/models', label: 'Models', icon: 'models' },
  { to: '/settings', label: 'Settings', icon: 'settings' },
]

const navClass = ({ isActive }: { isActive: boolean }) =>
  [styles.navItem, isActive && styles.active].filter(Boolean).join(' ')

/** Sidebar on wide screens, bottom tab bar on phones. Same routes, same components. */
export function AppShell() {
  const { data: orbs } = useOrbs()

  return (
    <div className={styles.shell}>
      <a href="#main" className={styles.skip}>
        Skip to content
      </a>
      <aside className={styles.sidebar}>
        <Link to="/" className={styles.brand}>
          <Orb
            size={34}
            appearance={{ eyes: 'dots', accessory: 'none', blush: false, glow: false, floating: false }}
          />
          <span className={styles.wordmark}>orbs</span>
        </Link>
        <Link to="/orbs/new" className={styles.newOrb}>
          <Icon name="plus" size={18} /> New orb
        </Link>
        <nav aria-label="Main" className={styles.nav}>
          {NAV.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.end} className={navClass}>
              <Icon name={item.icon} /> {item.label}
            </NavLink>
          ))}
        </nav>
        {orbs && orbs.length > 0 && (
          <div className={styles.orbList}>
            <span className={styles.sectionLabel}>Your orbs</span>
            {orbs.map((orb) => (
              <NavLink key={orb.id} to={`/orbs/${orb.id}/chat`} className={navClass}>
                <Orb
                  size={28}
                  appearance={{ ...orb.appearance, glow: false, floating: false, accessory: 'none' }}
                />
                <span className={styles.orbName}>{orb.name}</span>
              </NavLink>
            ))}
          </div>
        )}
        <div className={styles.footer}>
          <span className={styles.footerTitle}>Self-hosted</span>
          <span className="mono">Orbs v0.1 · open source</span>
        </div>
      </aside>

      <main id="main" className={styles.main}>
        <DemoBanner />
        <Outlet />
      </main>

      <nav aria-label="Main" className={styles.tabBar}>
        {NAV.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.end} className={navClass}>
            <Icon name={item.icon} />
            <span>{item.label}</span>
          </NavLink>
        ))}
      </nav>
    </div>
  )
}

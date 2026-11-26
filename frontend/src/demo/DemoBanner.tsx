import { DEMO_STORAGE_KEY, IS_DEMO, REPOSITORY_URL } from './mode'
import styles from './DemoBanner.module.css'

function reset() {
  try {
    localStorage.removeItem(DEMO_STORAGE_KEY)
  } catch {
    // Storage blocked: a reload still starts from the sample data.
  }
  location.reload()
}

/** Tells people they are in the live demo. Renders nothing in real installs. */
export function DemoBanner() {
  if (!IS_DEMO) return null
  return (
    <div className={styles.banner} role="note">
      <span>
        <strong>Live demo.</strong> Sample data, replies from the Echo provider, and everything stays in your
        browser.
      </span>
      <span className={styles.actions}>
        <button type="button" className={styles.link} onClick={reset}>
          Reset demo
        </button>
        <a className={styles.link} href={REPOSITORY_URL}>
          Get Orbs
        </a>
      </span>
    </div>
  )
}

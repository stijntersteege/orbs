import { Link } from 'react-router'
import { useContextSources, useOrbs, useProviders } from '@/api/queries'
import type { Orb as OrbModel } from '@/api/types'
import { Orb } from '@/components/orb/Orb'
import { ORB_WASH } from '@/components/orb/palette'
import { ButtonLink, Chip, EmptyState, ErrorNotice, PageHeader, Spinner } from '@/components/ui'
import { Icon } from '@/components/ui/icons'
import { plural } from '@/lib/format'
import styles from './orbs.module.css'

function OrbCard({ orb }: { orb: OrbModel }) {
  return (
    <li className={styles.card}>
      <Link to={`/orbs/${orb.id}/chat`} className={styles.cardLink}>
        <span className={styles.cardTile} style={{ background: ORB_WASH[orb.appearance.color] }}>
          <Orb appearance={orb.appearance} size={88} />
        </span>
        <span className={styles.cardBody}>
          <span className={styles.cardName}>{orb.name}</span>
          {orb.description && <span className={styles.cardDescription}>{orb.description}</span>}
        </span>
      </Link>
      <div className={styles.cardMeta}>
        <Chip tone={orb.contextSourceIds.length > 0 ? 'ok' : 'neutral'}>
          {plural(orb.contextSourceIds.length, 'source')}
        </Chip>
        <Chip tone="mono">{orb.model?.model ?? 'auto'}</Chip>
        <Link to={`/orbs/${orb.id}`} className={styles.editLink} aria-label={`Set up ${orb.name}`}>
          <Icon name="edit" size={18} />
        </Link>
      </div>
    </li>
  )
}

/** Home: every orb at a glance, plus a quick way to make a new one. */
export function OrbsPage() {
  const orbs = useOrbs()
  const providers = useProviders()
  const sources = useContextSources()

  const summary = [
    plural(orbs.data?.length ?? 0, 'orb'),
    `${plural(sources.data?.length ?? 0, 'source')} connected`,
    plural(providers.data?.length ?? 0, 'provider'),
  ].join(' · ')

  return (
    <>
      <PageHeader
        title="Your orbs"
        subtitle={summary}
        actions={
          <ButtonLink to="/orbs/new" variant="primary">
            <Icon name="plus" size={18} /> New orb
          </ButtonLink>
        }
      />

      {providers.data && providers.data.length === 0 && (
        <div className={styles.banner}>
          <div>
            <h2>Give your orbs a brain</h2>
            <p>Connect a model provider: a hosted API, a local runtime or any OpenAI-compatible endpoint.</p>
          </div>
          <ButtonLink to="/models" variant="primary">
            Connect a model
          </ButtonLink>
        </div>
      )}

      <ErrorNotice error={orbs.error} />
      {orbs.isPending && <Spinner />}

      {orbs.data?.length === 0 && (
        <EmptyState>
          <Orb size={96} appearance={{ eyes: 'sleepy' }} />
          <p>No orbs yet. Pick what it knows, a brain and a face. Takes a minute.</p>
          <ButtonLink to="/orbs/new" variant="primary">
            Create your first orb
          </ButtonLink>
        </EmptyState>
      )}

      {orbs.data && orbs.data.length > 0 && (
        <ul className={styles.grid} aria-label="Orbs">
          {orbs.data.map((orb) => (
            <OrbCard key={orb.id} orb={orb} />
          ))}
          <li>
            <Link to="/orbs/new" className={styles.newCard}>
              <Icon name="plus" size={28} />
              <span className={styles.cardName}>New orb</span>
              <span className={styles.cardDescription}>Pick what it knows, a brain and a face.</span>
            </Link>
          </li>
        </ul>
      )}
    </>
  )
}

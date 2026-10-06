import { useEffect, useState } from 'react';
import { userApi, type ExternalIdentityDto } from '@rsgo/core';
import { ProviderMark } from '../../components/sso/SsoComponents';

function formatDate(isoString: string): string {
  return new Date(isoString).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}

/**
 * Linked single sign-on accounts. Without a local password "Unlink" is disabled: it could be the
 * only way to sign in (design frames 177:4834, 177:5913; plan E22).
 */
export default function ConnectedAccounts({
  hasPassword = true,
  onIdentities,
}: {
  hasPassword?: boolean;
  onIdentities?: (identities: ExternalIdentityDto[]) => void;
}) {
  const [identities, setIdentities] = useState<ExternalIdentityDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => {
    userApi
      .getExternalIdentities()
      .then((list) => {
        setIdentities(list);
        onIdentities?.(list);
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load connected accounts'))
      .finally(() => setLoading(false));
  }, [onIdentities]);

  const handleUnlink = async (provider: string) => {
    setError('');
    setBusy(provider);
    try {
      await userApi.unlinkExternalIdentity(provider);
      setIdentities((list) => list.filter((i) => i.provider !== provider));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to unlink account');
    } finally {
      setBusy(null);
    }
  };

  return (
    <section className="rounded-2xl border border-line bg-surface" data-testid="connected-accounts">
      <div className="border-b border-line px-6 py-[18px]">
        <h3 className="text-[17px] font-semibold text-fg">Connected accounts</h3>
        <p className="mt-0.5 text-[13px] text-fg-muted">Single sign-on (OIDC) providers linked to your account.</p>
      </div>
      <div className="px-6 py-5">
        {error && (
          <div role="alert" className="mb-4 rounded-xl border border-status-unhealthy/35 bg-status-unhealthy-bg px-4 py-3 text-sm text-fg">
            {error}
          </div>
        )}

        {loading ? (
          <p className="text-sm text-fg-muted">Loading…</p>
        ) : identities.length === 0 ? (
          <p className="text-sm text-fg-muted">No connected single sign-on accounts.</p>
        ) : (
          <ul className="divide-y divide-line">
            {identities.map((i) => (
              <li key={i.provider} className="flex flex-col gap-2 py-3" data-testid={`linked-${i.provider}`}>
                <div className="flex items-center gap-3.5">
                  <ProviderMark iconUrl={i.iconUrl} />
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium text-fg">{i.displayName || i.provider}</p>
                    <p className="text-xs text-fg-muted">Linked {formatDate(i.linkedAt)}</p>
                  </div>
                  <button
                    onClick={() => handleUnlink(i.provider)}
                    disabled={!hasPassword || busy === i.provider}
                    className="text-sm font-medium text-status-unhealthy hover:underline disabled:cursor-not-allowed disabled:text-fg-muted disabled:no-underline disabled:opacity-60"
                    data-testid={`unlink-${i.provider}`}
                  >
                    {busy === i.provider ? 'Unlinking…' : 'Unlink'}
                  </button>
                </div>
                {!hasPassword && (
                  <p className="text-xs text-fg-muted">
                    You can't unlink {i.displayName || i.provider} while you have no local password, otherwise you could no longer sign in.
                    Set a local password first.
                  </p>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}

import { useEffect, useState } from 'react';
import { userApi, type ExternalIdentityDto } from '@rsgo/core';

function formatDate(isoString: string): string {
  return new Date(isoString).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}

export default function ConnectedAccounts() {
  const [identities, setIdentities] = useState<ExternalIdentityDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => {
    userApi
      .getExternalIdentities()
      .then(setIdentities)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load connected accounts'))
      .finally(() => setLoading(false));
  }, []);

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
    <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
      <div className="border-b border-gray-200 px-6 py-5 dark:border-gray-700">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Connected accounts</h3>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Single sign-on (OIDC) providers linked to your account.
        </p>
      </div>
      <div className="px-6 py-5">
        {error && (
          <div className="mb-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800 dark:border-red-800 dark:bg-red-900/20 dark:text-red-400">
            {error}
          </div>
        )}

        {loading ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">Loading…</p>
        ) : identities.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No connected single sign-on accounts.</p>
        ) : (
          <ul className="divide-y divide-gray-100 dark:divide-gray-800">
            {identities.map((i) => (
              <li key={i.provider} className="flex items-center justify-between py-3">
                <div>
                  <p className="text-sm font-medium text-gray-900 dark:text-white">{i.provider}</p>
                  <p className="text-xs text-gray-500 dark:text-gray-400">Linked {formatDate(i.linkedAt)}</p>
                </div>
                <button
                  onClick={() => handleUnlink(i.provider)}
                  disabled={busy === i.provider}
                  className="text-sm text-red-600 hover:text-red-700 disabled:opacity-50"
                >
                  {busy === i.provider ? 'Unlinking…' : 'Unlink'}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

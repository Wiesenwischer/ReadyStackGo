import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import {
  getOidcSettings,
  saveOidcSettings,
  type OidcProviderSettingsDto,
} from '@rsgo/core';

const newProvider = (): OidcProviderSettingsDto => ({
  name: '',
  displayName: '',
  authority: '',
  clientId: '',
  clientSecret: '',
  hasClientSecret: false,
  scopes: 'openid email profile',
  enabled: true,
});

const inputClass =
  'w-full h-11 px-4 py-2.5 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:focus:border-brand-600';
const labelClass = 'block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300';

export default function OidcSettingsPage() {
  const [providers, setProviders] = useState<OidcProviderSettingsDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  useEffect(() => {
    getOidcSettings()
      .then((s) => setProviders(s.providers.map((p) => ({ ...p, clientSecret: '' }))))
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load settings'))
      .finally(() => setLoading(false));
  }, []);

  const updateProvider = (index: number, patch: Partial<OidcProviderSettingsDto>) =>
    setProviders((ps) => ps.map((p, i) => (i === index ? { ...p, ...patch } : p)));

  const removeProvider = (index: number) =>
    setProviders((ps) => ps.filter((_, i) => i !== index));

  const handleSave = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');
    setSaving(true);
    try {
      await saveOidcSettings({ providers });
      setSuccess('Providers saved.');
      setProviders((ps) => ps.map((p) => ({ ...p, clientSecret: '', hasClientSecret: p.hasClientSecret || !!p.clientSecret })));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save providers');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <div className="p-6 text-gray-500 dark:text-gray-400">Loading…</div>;
  }

  return (
    <div className="max-w-3xl p-6 mx-auto">
      <div className="mb-6">
        <Link to="/settings" className="text-sm text-gray-500 hover:text-brand-600">← Settings</Link>
        <h2 className="mt-2 text-2xl font-bold text-black dark:text-white">Single Sign-On (OIDC)</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Configure generic OpenID Connect providers. Users can sign in only if they already have an account or a pending invitation.
        </p>
      </div>

      {error && (
        <div className="mb-4 p-4 text-sm border rounded-lg border-red-300 bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
          {error}
        </div>
      )}
      {success && (
        <div className="mb-4 p-4 text-sm border rounded-lg border-green-300 bg-green-50 text-green-800 dark:bg-green-900/20 dark:border-green-800 dark:text-green-400">
          {success}
        </div>
      )}

      <form onSubmit={handleSave} className="space-y-6">
        {providers.map((p, i) => (
          <div key={i} className="p-5 border rounded-2xl border-gray-200 dark:border-gray-800 dark:bg-white/[0.03]">
            <div className="flex items-center justify-between mb-4">
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={p.enabled} onChange={(e) => updateProvider(i, { enabled: e.target.checked })} />
                <span className="text-sm font-medium text-gray-700 dark:text-gray-300">Enabled</span>
              </label>
              <button type="button" onClick={() => removeProvider(i)} className="text-sm text-red-600 hover:text-red-700">
                Remove
              </button>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className={labelClass}>Name (url-safe id)</label>
                <input className={inputClass} value={p.name} onChange={(e) => updateProvider(i, { name: e.target.value })} placeholder="identityaccess" />
              </div>
              <div>
                <label className={labelClass}>Display name</label>
                <input className={inputClass} value={p.displayName} onChange={(e) => updateProvider(i, { displayName: e.target.value })} placeholder="IdentityAccess" />
              </div>
            </div>

            <div className="mt-4">
              <label className={labelClass}>Authority (issuer URL)</label>
              <input className={inputClass} value={p.authority} onChange={(e) => updateProvider(i, { authority: e.target.value })} placeholder="https://idp.example.com" />
            </div>

            <div className="grid grid-cols-2 gap-4 mt-4">
              <div>
                <label className={labelClass}>Client ID</label>
                <input className={inputClass} value={p.clientId} onChange={(e) => updateProvider(i, { clientId: e.target.value })} />
              </div>
              <div>
                <label className={labelClass}>Client secret</label>
                <input
                  className={inputClass}
                  type="password"
                  value={p.clientSecret ?? ''}
                  onChange={(e) => updateProvider(i, { clientSecret: e.target.value })}
                  placeholder={p.hasClientSecret ? '•••••••• (unchanged)' : ''}
                  autoComplete="new-password"
                />
              </div>
            </div>

            <div className="mt-4">
              <label className={labelClass}>Scopes</label>
              <input className={inputClass} value={p.scopes} onChange={(e) => updateProvider(i, { scopes: e.target.value })} />
            </div>
          </div>
        ))}

        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => setProviders((ps) => [...ps, newProvider()])}
            className="px-5 py-2.5 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-white/90 dark:hover:bg-white/5"
          >
            + Add provider
          </button>
          <button
            type="submit"
            disabled={saving}
            className="px-5 py-2.5 text-sm font-medium text-white rounded-lg bg-brand-600 hover:bg-brand-700 disabled:opacity-50"
          >
            {saving ? 'Saving…' : 'Save providers'}
          </button>
        </div>
      </form>
    </div>
  );
}

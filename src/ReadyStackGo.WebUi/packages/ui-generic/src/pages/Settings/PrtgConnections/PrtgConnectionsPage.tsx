import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  listPrtgConnections,
  createPrtgConnection,
  updatePrtgConnection,
  deletePrtgConnection,
  type PrtgConnectionDto,
} from '@rsgo/core';

export default function PrtgConnectionsPage() {
  const [connections, setConnections] = useState<PrtgConnectionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<PrtgConnectionDto | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  const reload = async () => {
    try {
      setLoading(true);
      const list = await listPrtgConnections();
      setConnections(list);
      setError(null);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void reload(); }, []);

  const onDelete = async (c: PrtgConnectionDto) => {
    if (!confirm(`Delete PRTG connection '${c.name}'?\n\nProductDeployments that reference it will lose the link and stop auto-syncing to PRTG.`)) return;
    try {
      await deletePrtgConnection(c.id);
      await reload();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10 space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <Link to="/settings" className="text-sm text-gray-500 hover:text-brand-600">← Settings</Link>
          <h2 className="mt-2 text-2xl font-bold text-black dark:text-white">PRTG Connections</h2>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Manage reusable PRTG Network Monitor connections. ProductDeployments can link to
            one, then RSGO auto-registers the deployment as a PRTG device when it goes live and
            auto-deregisters when it is removed.
          </p>
        </div>
        <button
          onClick={() => { setEditing(null); setShowCreate(true); }}
          className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
        >
          + Add connection
        </button>
      </div>

      {error && (
        <div className="rounded-md bg-red-50 p-4 text-sm text-red-700 dark:bg-red-900/30 dark:text-red-300">{error}</div>
      )}

      {(showCreate || editing !== null) && (
        <ConnectionForm
          initial={editing ?? undefined}
          onCancel={() => { setShowCreate(false); setEditing(null); }}
          onSaved={async () => { setShowCreate(false); setEditing(null); await reload(); }}
        />
      )}

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        {loading ? (
          <p className="p-6 text-sm text-gray-500">Loading PRTG connections…</p>
        ) : connections.length === 0 ? (
          <div className="p-8 text-center">
            <p className="text-sm text-gray-500 dark:text-gray-400">
              No PRTG connections yet.
            </p>
            <p className="mt-2 text-xs text-gray-400 dark:text-gray-500">
              Add one to enable auto-register on ProductDeployments. The connection stores
              the PRTG URL, an API token (encrypted at rest) and an optional template-device
              id used to clone sensors per deployment.
            </p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-left text-xs uppercase text-gray-600 dark:bg-gray-800/50 dark:text-gray-400">
              <tr>
                <th className="px-4 py-3">Name</th>
                <th className="px-4 py-3">URL</th>
                <th className="px-4 py-3">Template Device</th>
                <th className="px-4 py-3">TLS</th>
                <th className="px-4 py-3">Last used</th>
                <th className="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-800">
              {connections.map((c) => (
                <tr key={c.id} className="hover:bg-gray-50 dark:hover:bg-gray-900/50">
                  <td className="px-4 py-2 font-medium text-gray-900 dark:text-white">{c.name}</td>
                  <td className="px-4 py-2 font-mono text-xs text-gray-600 dark:text-gray-300">{c.url}</td>
                  <td className="px-4 py-2 text-gray-700 dark:text-gray-200">
                    {c.templateDeviceId ?? <span className="italic text-gray-400">(none)</span>}
                  </td>
                  <td className="px-4 py-2 text-xs">
                    {c.verifyTls
                      ? <span className="text-green-700 dark:text-green-400">verified</span>
                      : <span className="text-amber-600 dark:text-amber-400">no-verify</span>}
                  </td>
                  <td className="px-4 py-2 text-xs text-gray-500">
                    {c.lastUsedAt ? new Date(c.lastUsedAt).toLocaleString() : <span className="italic">never</span>}
                  </td>
                  <td className="px-4 py-2 text-right">
                    <button
                      onClick={() => { setShowCreate(false); setEditing(c); }}
                      className="rounded bg-gray-100 px-3 py-1 text-xs text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-200 dark:hover:bg-gray-600 mr-2"
                    >Edit</button>
                    <button
                      onClick={() => onDelete(c)}
                      className="rounded bg-red-100 px-3 py-1 text-xs text-red-700 hover:bg-red-200 dark:bg-red-900/30 dark:text-red-300"
                    >Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

function ConnectionForm({
  initial,
  onCancel,
  onSaved,
}: {
  initial?: PrtgConnectionDto;
  onCancel: () => void;
  onSaved: () => Promise<void>;
}) {
  const [form, setForm] = useState({
    name: initial?.name ?? '',
    url: initial?.url ?? 'https://',
    apiToken: '',
    templateDeviceId: initial?.templateDeviceId ?? '',
    verifyTls: initial?.verifyTls ?? true,
  });
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const isEdit = initial !== undefined;

  const submit = async () => {
    setBusy(true);
    setErr(null);
    try {
      const payload = {
        name: form.name,
        url: form.url,
        apiToken: form.apiToken || undefined,
        templateDeviceId: form.templateDeviceId === '' ? null : Number(form.templateDeviceId),
        verifyTls: form.verifyTls,
      };
      const response = isEdit
        ? await updatePrtgConnection(initial!.id, { ...payload, apiToken: payload.apiToken ?? null })
        : await createPrtgConnection({
            name: form.name,
            url: form.url,
            apiToken: form.apiToken,
            templateDeviceId: payload.templateDeviceId,
            verifyTls: form.verifyTls,
          });
      if (!response.success) {
        setErr(response.error ?? 'Save failed.');
        return;
      }
      await onSaved();
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
      <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
        {isEdit ? `Edit '${initial!.name}'` : 'New PRTG connection'}
      </h3>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-gray-500 dark:text-gray-400">Name</span>
          <input
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            className="w-full px-3 py-2 border border-gray-300 rounded-md dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
            placeholder="prod-prtg"
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-gray-500 dark:text-gray-400">URL</span>
          <input
            value={form.url}
            onChange={(e) => setForm({ ...form, url: e.target.value })}
            className="w-full px-3 py-2 border border-gray-300 rounded-md font-mono text-xs dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
            placeholder="https://prtg.example.local"
          />
        </label>
        <label className="flex flex-col gap-1 text-sm md:col-span-2">
          <span className="text-gray-500 dark:text-gray-400">
            API token / passhash {isEdit && <span className="text-xs italic">(leave empty to keep existing)</span>}
          </span>
          <input
            type="password"
            value={form.apiToken}
            onChange={(e) => setForm({ ...form, apiToken: e.target.value })}
            className="w-full px-3 py-2 border border-gray-300 rounded-md font-mono text-xs dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
            placeholder={isEdit ? '••••••••••' : 'PRTG API token or passhash'}
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-gray-500 dark:text-gray-400">Template Device ID (optional)</span>
          <input
            type="number"
            value={form.templateDeviceId}
            onChange={(e) => setForm({ ...form, templateDeviceId: e.target.value })}
            className="w-full px-3 py-2 border border-gray-300 rounded-md dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
            placeholder="e.g. 4221"
          />
          <span className="text-xs text-gray-400">PRTG device-id RSGO duplicates per deployment. Leave empty to disable auto-register.</span>
        </label>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={form.verifyTls}
            onChange={(e) => setForm({ ...form, verifyTls: e.target.checked })}
            className="rounded text-brand-600 focus:ring-brand-500"
          />
          <span className="text-gray-700 dark:text-gray-200">Verify TLS certificate</span>
          <span className="text-xs text-gray-400">(uncheck for self-signed PRTG certs)</span>
        </label>
      </div>

      <div className="mt-4 flex items-center gap-3">
        <button
          onClick={submit}
          disabled={busy || !form.name || !form.url || (!isEdit && !form.apiToken)}
          className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
        >
          {busy ? 'Saving…' : (isEdit ? 'Save changes' : 'Create')}
        </button>
        <button
          onClick={onCancel}
          className="rounded-md bg-gray-100 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-200 dark:hover:bg-gray-600"
        >
          Cancel
        </button>
        {err && <span className="text-sm text-red-600 dark:text-red-400">{err}</span>}
      </div>
    </div>
  );
}

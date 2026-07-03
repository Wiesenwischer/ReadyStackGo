import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  getSnmpStatus,
  getOidReference,
  getMibDownloadUrl,
  getPrtgBundleDownloadUrl,
  getPrtgSensorUrl,
  getSnmpSettings,
  updateSnmpSettings,
  listV3Users,
  addV3User,
  deleteV3User,
} from '@rsgo/core';
import type {
  SnmpStatus,
  OidReference,
  OidReferenceEnvironment,
  OidReferenceProduct,
  OidReferenceStack,
  OidReferenceService,
  OidReferenceColumn,
  SnmpSettings,
  SnmpV3User,
  SnmpAuthProtocol,
  SnmpPrivProtocol,
} from '@rsgo/core';

export default function SnmpSettingsPage() {
  const [status, setStatus] = useState<SnmpStatus | null>(null);
  const [reference, setReference] = useState<OidReference | null>(null);
  const [settings, setSettings] = useState<SnmpSettings | null>(null);
  const [users, setUsers] = useState<SnmpV3User[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = async () => {
    const [st, r, s, u] = await Promise.all([
      getSnmpStatus(),
      getOidReference(),
      getSnmpSettings(),
      listV3Users(),
    ]);
    setStatus(st);
    setReference(r);
    setSettings(s);
    setUsers(u);
  };

  useEffect(() => {
    (async () => {
      try {
        setLoading(true);
        await reload();
      } catch (e: unknown) {
        setError(e instanceof Error ? e.message : String(e));
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  if (loading) return <div className="p-6 text-sm text-gray-500">Loading SNMP status…</div>;
  if (error) {
    return (
      <div className="p-6">
        <div className="rounded-md bg-red-50 p-4 text-sm text-red-700 dark:bg-red-900/30 dark:text-red-300">{error}</div>
      </div>
    );
  }
  if (!status || !reference || !settings) return null;

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10 space-y-6">
      <div>
        <Link to="/settings" className="text-sm text-gray-500 hover:text-brand-600">← Settings</Link>
        <h2 className="mt-2 text-2xl font-bold text-black dark:text-white">SNMP Monitoring</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Configure ReadyStackGo's SNMP agent and review the OIDs you can poll from your monitoring tool.
        </p>
      </div>

      <SettingsForm settings={settings} onSaved={reload} />

      <V3UsersCard users={users} onChanged={reload} />

      <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white">MIB file</h3>
            <p className="text-sm text-gray-500 dark:text-gray-400">
              Import <code>READYSTACKGO-MIB.txt</code> into your MIB browser so the OIDs below resolve to symbolic names.
            </p>
          </div>
          <a href={getMibDownloadUrl()} className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700" download>
            Download MIB
          </a>
        </div>
      </div>

      <PrtgHttpSensorCard />

      <PrtgIntegrationCard />

      <OidReferenceTree reference={reference} />
    </div>
  );
}

function SettingsForm({ settings, onSaved }: { settings: SnmpSettings; onSaved: () => Promise<void> }) {
  const [form, setForm] = useState({
    enabled: settings.enabled,
    port: settings.port,
    listenAddress: settings.listenAddress,
    rootOid: settings.rootOid,
    community: settings.community === '***' ? '' : settings.community,
    trapReceivers: settings.trapReceivers,
  });
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const save = async () => {
    setSaving(true);
    setMsg(null);
    setErr(null);
    try {
      await updateSnmpSettings({
        enabled: form.enabled,
        port: form.port,
        listenAddress: form.listenAddress,
        rootOid: form.rootOid,
        community: form.community,
        trapReceivers: form.trapReceivers,
      });
      setMsg('Saved. Agent reloads automatically.');
      await onSaved();
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : String(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
      <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Agent configuration</h3>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Toggle label="Enabled" value={form.enabled} onChange={(v) => setForm({ ...form, enabled: v })} />
        <TextField label="Listen address" value={form.listenAddress} onChange={(v) => setForm({ ...form, listenAddress: v })} mono />
        <NumberField label="Port" value={form.port} onChange={(v) => setForm({ ...form, port: v })} />
        <TextField label="Root OID" value={form.rootOid} onChange={(v) => setForm({ ...form, rootOid: v })} mono />
        <TextField label="SNMPv2c community" value={form.community} onChange={(v) => setForm({ ...form, community: v })} placeholder="leave empty to disable v2c" type="password" />
        <TextField label="Trap receivers" value={form.trapReceivers} onChange={(v) => setForm({ ...form, trapReceivers: v })} placeholder="host[:port], comma-separated (traps ship in next phase)" />
      </div>
      <div className="mt-4 flex items-center gap-3">
        <button onClick={save} disabled={saving} className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50">
          {saving ? 'Saving…' : 'Save'}
        </button>
        {msg && <span className="text-sm text-green-600 dark:text-green-400">{msg}</span>}
        {err && <span className="text-sm text-red-600 dark:text-red-400">{err}</span>}
      </div>
    </div>
  );
}

function Toggle({ label, value, onChange }: { label: string; value: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="flex flex-col gap-1 text-sm">
      <span className="text-gray-500 dark:text-gray-400">{label}</span>
      <button
        type="button"
        role="switch"
        aria-checked={value}
        onClick={() => onChange(!value)}
        className={`relative inline-flex h-7 w-14 items-center rounded-full border-2 transition-colors ${
          value ? 'bg-brand-600 border-brand-600' : 'bg-gray-300 border-gray-400 dark:bg-gray-600 dark:border-gray-500'
        }`}
      >
        <span className={`inline-block h-5 w-5 rounded-full bg-white shadow-md ring-1 ring-black/10 transition-transform ${value ? 'translate-x-7' : 'translate-x-0.5'}`} />
      </button>
    </label>
  );
}

function TextField({ label, value, onChange, mono, placeholder, type }: { label: string; value: string; onChange: (v: string) => void; mono?: boolean; placeholder?: string; type?: string }) {
  return (
    <label className="flex flex-col gap-1 text-sm">
      <span className="text-gray-500 dark:text-gray-400">{label}</span>
      <input
        type={type ?? 'text'}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className={`w-full px-3 py-2 border border-gray-300 rounded-md dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500 ${mono ? 'font-mono text-xs' : ''}`}
      />
    </label>
  );
}

function NumberField({ label, value, onChange }: { label: string; value: number; onChange: (v: number) => void }) {
  return (
    <label className="flex flex-col gap-1 text-sm">
      <span className="text-gray-500 dark:text-gray-400">{label}</span>
      <input
        type="number"
        min={1}
        max={65535}
        value={value}
        onChange={(e) => onChange(parseInt(e.target.value, 10) || 0)}
        className="w-full px-3 py-2 border border-gray-300 rounded-md dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
      />
    </label>
  );
}

function V3UsersCard({ users, onChanged }: { users: SnmpV3User[]; onChanged: () => Promise<void> }) {
  const [showForm, setShowForm] = useState(false);
  return (
    <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
      <div className="flex items-center justify-between mb-3">
        <div>
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">SNMPv3 users</h3>
          <p className="text-sm text-gray-500 dark:text-gray-400">User-based security model (USM) entries for SNMPv3 polling.</p>
        </div>
        <button onClick={() => setShowForm((s) => !s)} className="rounded-md bg-brand-600 px-3 py-2 text-sm text-white hover:bg-brand-700">
          {showForm ? 'Cancel' : 'Add user'}
        </button>
      </div>

      {showForm && <V3UserForm onAdded={async () => { setShowForm(false); await onChanged(); }} />}

      <ul className="mt-4 divide-y divide-gray-100 dark:divide-gray-800">
        {users.length === 0 && <li className="py-3 text-sm text-gray-500 italic">No SNMPv3 users configured.</li>}
        {users.map((u) => (
          <li key={u.id} className="flex items-center justify-between py-2 text-sm">
            <div>
              <span className="font-medium text-gray-900 dark:text-white">{u.name}</span>
              <span className="ml-3 text-xs text-gray-500">auth: {u.authProtocol} · priv: {u.privProtocol}</span>
            </div>
            <button
              onClick={async () => {
                if (!confirm(`Delete SNMPv3 user '${u.name}'?`)) return;
                await deleteV3User(u.id);
                await onChanged();
              }}
              className="text-xs text-red-600 hover:text-red-800"
            >
              Delete
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

const AUTH_PROTOS: SnmpAuthProtocol[] = ['None', 'Md5', 'Sha1', 'Sha256', 'Sha384', 'Sha512'];
const PRIV_PROTOS: SnmpPrivProtocol[] = ['None', 'Des', 'Aes128', 'Aes192', 'Aes256'];

function V3UserForm({ onAdded }: { onAdded: () => Promise<void> }) {
  const [form, setForm] = useState({
    name: '',
    authProtocol: 'Sha256' as SnmpAuthProtocol,
    authPassphrase: '',
    privProtocol: 'Aes128' as SnmpPrivProtocol,
    privPassphrase: '',
  });
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const submit = async () => {
    setBusy(true);
    setErr(null);
    try {
      await addV3User(form);
      await onAdded();
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="border border-gray-200 dark:border-gray-700 rounded-lg p-4 grid grid-cols-1 md:grid-cols-2 gap-3 mt-3">
      <TextField label="Name" value={form.name} onChange={(v) => setForm({ ...form, name: v })} />
      <Select label="Auth protocol" value={form.authProtocol} options={AUTH_PROTOS} onChange={(v) => setForm({ ...form, authProtocol: v as SnmpAuthProtocol })} />
      {form.authProtocol !== 'None' && (
        <TextField label="Auth passphrase" value={form.authPassphrase} onChange={(v) => setForm({ ...form, authPassphrase: v })} type="password" />
      )}
      <Select label="Priv protocol" value={form.privProtocol} options={PRIV_PROTOS} onChange={(v) => setForm({ ...form, privProtocol: v as SnmpPrivProtocol })} />
      {form.privProtocol !== 'None' && (
        <TextField label="Priv passphrase" value={form.privPassphrase} onChange={(v) => setForm({ ...form, privPassphrase: v })} type="password" />
      )}
      <div className="md:col-span-2 flex items-center gap-3">
        <button onClick={submit} disabled={busy || !form.name} className="rounded-md bg-brand-600 px-4 py-2 text-sm text-white hover:bg-brand-700 disabled:opacity-50">
          {busy ? 'Adding…' : 'Add user'}
        </button>
        {err && <span className="text-sm text-red-600 dark:text-red-400">{err}</span>}
      </div>
    </div>
  );
}

function Select({ label, value, options, onChange }: { label: string; value: string; options: string[]; onChange: (v: string) => void }) {
  return (
    <label className="flex flex-col gap-1 text-sm">
      <span className="text-gray-500 dark:text-gray-400">{label}</span>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full px-3 py-2 border border-gray-300 rounded-md dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
      >
        {options.map((o) => <option key={o} value={o}>{o}</option>)}
      </select>
    </label>
  );
}

function PrtgHttpSensorCard() {
  const url = getPrtgSensorUrl('');
  const [copied, setCopied] = useState(false);

  const onCopy = async () => {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      /* silent — clipboard may be blocked in some browsers */
    }
  };

  return (
    <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
      <div className="flex flex-col gap-4">
        <div>
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
            PRTG HTTP sensor <span className="ml-2 inline-flex items-center rounded bg-green-100 px-2 py-0.5 text-xs font-medium text-green-700 dark:bg-green-900/30 dark:text-green-300">5-min setup</span>
          </h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            One PRTG sensor, one URL, no file copies and no probe restart. Paste the URL below
            into a PRTG <strong>HTTP Data Advanced</strong> sensor — PRTG polls it on its
            regular interval and renders aggregated channels (Healthy / Failed / Maintenance /
            DB health, …).
          </p>
        </div>

        <ol className="list-decimal pl-5 text-sm text-gray-600 dark:text-gray-300 space-y-1">
          <li>
            Create an API key with the <strong>Read Status (PRTG)</strong> permission via{' '}
            <Link to="/settings/cicd" className="text-brand-600 hover:underline">
              CI/CD Integration
            </Link>{' '}
            (name it e.g. <em>"prtg-sensor"</em>).
          </li>
          <li>
            Copy the URL below and replace <code className="text-xs">YOUR_API_KEY</code> with the
            generated key.
          </li>
          <li>
            In PRTG: <em>Add Sensor → HTTP Data Advanced</em> → paste the URL → done.
          </li>
        </ol>

        <div className="flex items-center gap-2 rounded-md bg-gray-50 px-3 py-2 font-mono text-xs text-gray-800 dark:bg-gray-900/60 dark:text-gray-200">
          <span className="truncate" title={url}>{url}</span>
          <button
            type="button"
            onClick={onCopy}
            className="ml-auto shrink-0 rounded bg-gray-200 px-2 py-1 text-xs font-medium text-gray-700 hover:bg-gray-300 dark:bg-gray-700 dark:text-gray-200 dark:hover:bg-gray-600"
          >
            {copied ? 'Copied ✓' : 'Copy'}
          </button>
        </div>

        <p className="text-xs text-gray-500 dark:text-gray-400">
          The query-string API key is the only way PRTG accepts an authenticated URL in a single
          field. Treat the resulting sensor URL like a password — anyone with the URL can read
          the same status the sensor sees.
        </p>
      </div>
    </div>
  );
}

function PrtgIntegrationCard() {
  return (
    <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
      <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
        <div className="md:flex-1">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">PRTG integration</h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Download a ready-to-use bundle for <strong>PRTG Network Monitor</strong>.
            Includes a device template, the MIB and value lookups so PRTG renders
            statuses (<span className="text-green-600 dark:text-green-400">Running</span>,
            <span className="text-amber-600 dark:text-amber-400"> PartiallyRunning</span>,
            <span className="text-red-600 dark:text-red-400"> Failed</span>) instead of
            raw integers.
          </p>
          <ol className="mt-3 list-decimal pl-5 text-sm text-gray-600 dark:text-gray-300 space-y-1">
            <li>
              Stop the <em>PRTG Probe</em> service (or reload templates from the PRTG web UI
              after copying the files).
            </li>
            <li>
              Unpack the ZIP into <code className="text-xs">C:\Program Files (x86)\PRTG Network Monitor\</code>.
              The folders inside match PRTG's layout so files land in the right place.
            </li>
            <li>
              Import the MIB via <em>Paessler MIB Importer → File → Save for PRTG</em>.
            </li>
            <li>
              Start the probe and run <em>Auto-Discovery (with template)</em> on your
              ReadyStackGo device — pick "ReadyStackGo Deployment".
            </li>
          </ol>
        </div>
        <a
          href={getPrtgBundleDownloadUrl()}
          className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 self-start"
          download
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v2a2 2 0 002 2h12a2 2 0 002-2v-2M7 10l5 5 5-5M12 15V3" />
          </svg>
          Download PRTG bundle
        </a>
      </div>
    </div>
  );
}

function OidReferenceTree({ reference }: { reference: OidReference }) {
  return (
    <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
      <h3 className="text-lg font-semibold text-gray-900 dark:text-white">OID reference</h3>
      <p className="text-sm text-gray-500 dark:text-gray-400 mb-4">
        Concrete OIDs you can paste into Nagios, Zabbix, PRTG, etc. Expand a node to see every column with its current value.
      </p>

      <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 uppercase mt-4">System scalars</h4>
      <ul className="mt-2 space-y-1 text-sm">
        {reference.system.map((s) => (
          <li key={s.oid} className="flex items-baseline gap-3">
            <CopyOid oid={s.oid} />
            <span className="text-gray-700 dark:text-gray-300">{s.symbol}</span>
            <span className="text-xs text-gray-400">[{s.type}]</span>
            <span className="ml-auto text-xs text-gray-500 dark:text-gray-400 truncate max-w-xs" title={s.currentValue}>= {s.currentValue}</span>
          </li>
        ))}
      </ul>

      <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 uppercase mt-6">Environments / Products / Stacks</h4>
      {reference.environments.length === 0 && <p className="text-sm text-gray-500 italic mt-2">No environments configured.</p>}
      <ul className="mt-2 space-y-2">
        {reference.environments.map((env) => <EnvironmentNode key={env.environmentIndex} env={env} />)}
      </ul>
    </div>
  );
}

function ColumnsTable({ columns }: { columns: OidReferenceColumn[] }) {
  return (
    <div className="mt-2 rounded-md border border-gray-100 dark:border-gray-800 bg-gray-50/60 dark:bg-white/[0.02] p-2">
      <table className="w-full text-xs">
        <thead>
          <tr className="text-left text-gray-400">
            <th className="font-normal pb-1 pr-3">Symbol</th>
            <th className="font-normal pb-1 pr-3">Type</th>
            <th className="font-normal pb-1 pr-3">OID</th>
            <th className="font-normal pb-1">Value</th>
          </tr>
        </thead>
        <tbody>
          {columns.map((c) => (
            <tr key={c.oid} className="border-t border-gray-100 dark:border-gray-800">
              <td className="py-1 pr-3 text-gray-700 dark:text-gray-300 whitespace-nowrap">{c.symbol}</td>
              <td className="py-1 pr-3 text-gray-400 whitespace-nowrap">{c.type}</td>
              <td className="py-1 pr-3"><CopyOid oid={c.oid} small /></td>
              <td className="py-1 text-gray-500 dark:text-gray-400 truncate max-w-xs" title={c.currentValue}>= {c.currentValue}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function EnvironmentNode({ env }: { env: OidReferenceEnvironment }) {
  const [open, setOpen] = useState(true);
  const [showColumns, setShowColumns] = useState(false);
  return (
    <li className="border border-gray-100 dark:border-gray-800 rounded-lg p-3">
      <div className="flex items-center gap-2 text-sm font-medium text-gray-900 dark:text-white">
        <button onClick={() => setOpen(!open)} className="flex items-center gap-2 flex-1 text-left">
          <span>{open ? '▾' : '▸'}</span>
          <span>{env.name}</span>
          <span className="text-xs text-gray-500">envIdx {env.environmentIndex}</span>
        </button>
        <ColumnsToggle open={showColumns} onClick={() => setShowColumns(!showColumns)} count={env.columns.length} />
      </div>
      {showColumns && <ColumnsTable columns={env.columns} />}
      {open && (
        <ul className="mt-2 ml-5 space-y-2">
          {env.products.map((p) => <ProductNode key={p.productIndex} product={p} />)}
          {env.products.length === 0 && <li className="text-xs text-gray-500 italic">No active products in this environment.</li>}
        </ul>
      )}
    </li>
  );
}

function ProductNode({ product }: { product: OidReferenceProduct }) {
  const [open, setOpen] = useState(true);
  const [showColumns, setShowColumns] = useState(false);
  return (
    <li>
      <div className="flex items-center gap-2 text-sm text-gray-900 dark:text-white">
        <button onClick={() => setOpen(!open)} className="flex items-center gap-2 flex-1 text-left">
          <span>{open ? '▾' : '▸'}</span>
          <span className="font-medium">{product.name}</span>
          <span className="text-xs text-gray-500">v{product.version}</span>
          <StatusBadge status={product.statusText} />
        </button>
        <ColumnsToggle open={showColumns} onClick={() => setShowColumns(!showColumns)} count={product.columns.length} />
      </div>
      {showColumns && <ColumnsTable columns={product.columns} />}
      {open && (
        <ul className="mt-1 ml-5 space-y-1">
          {product.stacks.map((s) => <StackNode key={s.stackIndex} stack={s} />)}
        </ul>
      )}
    </li>
  );
}

function StackNode({ stack }: { stack: OidReferenceStack }) {
  const [showColumns, setShowColumns] = useState(false);
  return (
    <li className="text-sm text-gray-700 dark:text-gray-300">
      <div className="flex items-center gap-2">
        <span className="text-gray-400">└</span>
        <span>{stack.name}</span>
        <StatusBadge status={stack.statusText} />
        <ColumnsToggle open={showColumns} onClick={() => setShowColumns(!showColumns)} count={stack.columns.length} className="ml-auto" />
      </div>
      {showColumns && <ColumnsTable columns={stack.columns} />}
      {stack.services.length > 0 && (
        <ul className="ml-6 mt-1 space-y-0.5">
          {stack.services.map((sv) => <ServiceNode key={sv.serviceIndex} service={sv} />)}
        </ul>
      )}
    </li>
  );
}

function ServiceNode({ service }: { service: OidReferenceService }) {
  const [showColumns, setShowColumns] = useState(false);
  return (
    <li className="text-xs text-gray-500">
      <div className="flex items-center gap-2">
        <span>·</span>
        <span>{service.name}</span>
        <span className={`px-1.5 rounded ${service.running ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300' : 'bg-gray-100 text-gray-500'}`}>
          {service.running ? 'running' : 'stopped'}
        </span>
        <ColumnsToggle open={showColumns} onClick={() => setShowColumns(!showColumns)} count={service.columns.length} className="ml-auto" />
      </div>
      {showColumns && <ColumnsTable columns={service.columns} />}
    </li>
  );
}

function ColumnsToggle({ open, onClick, count, className }: { open: boolean; onClick: () => void; count: number; className?: string }) {
  return (
    <button
      onClick={(e) => { e.stopPropagation(); onClick(); }}
      className={`text-xs px-2 py-0.5 rounded border border-gray-200 dark:border-gray-700 text-gray-500 hover:text-brand-600 hover:border-brand-600 whitespace-nowrap ${className ?? ''}`}
      title={open ? 'Hide column OIDs' : 'Show column OIDs'}
    >
      {open ? '− columns' : `+ ${count} columns`}
    </button>
  );
}

function StatusBadge({ status }: { status: string }) {
  const lower = status.toLowerCase();
  const color = lower === 'running' ? 'bg-green-100 text-green-700 dark:bg-green-900/30'
    : lower === 'failed' ? 'bg-red-100 text-red-700 dark:bg-red-900/30'
      : lower === 'partiallyrunning' ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/30'
        : 'bg-gray-100 text-gray-700 dark:bg-gray-900/30';
  return <span className={`text-xs px-1.5 py-0.5 rounded ${color}`}>{status}</span>;
}

function CopyOid({ oid, small }: { oid: string; small?: boolean }) {
  const [copied, setCopied] = useState(false);
  const onClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    navigator.clipboard.writeText(oid).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1200);
    });
  };
  return (
    <button onClick={onClick} className={`group flex items-center gap-1 font-mono ${small ? 'text-xs' : 'text-sm'} text-gray-500 hover:text-brand-600`} title="Copy OID">
      <span className="truncate max-w-md">{oid}</span>
      <span className="opacity-0 group-hover:opacity-100 text-xs">{copied ? '✓' : '⧉'}</span>
    </button>
  );
}

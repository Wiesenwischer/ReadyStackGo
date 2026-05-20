import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getSnmpStatus, getOidReference, getMibDownloadUrl } from '@rsgo/core';
import type { SnmpStatus, OidReference, OidReferenceEnvironment, OidReferenceProduct, OidReferenceStack } from '@rsgo/core';

export default function SnmpSettingsPage() {
  const [status, setStatus] = useState<SnmpStatus | null>(null);
  const [reference, setReference] = useState<OidReference | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        setLoading(true);
        const [s, r] = await Promise.all([getSnmpStatus(), getOidReference()]);
        setStatus(s);
        setReference(r);
      } catch (e: unknown) {
        setError(e instanceof Error ? e.message : String(e));
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  if (loading) {
    return <div className="p-6 text-sm text-gray-500">Loading SNMP status…</div>;
  }
  if (error) {
    return (
      <div className="p-6">
        <div className="rounded-md bg-red-50 p-4 text-sm text-red-700 dark:bg-red-900/30 dark:text-red-300">
          {error}
        </div>
      </div>
    );
  }
  if (!status || !reference) {
    return null;
  }

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10 space-y-6">
      <div>
        <Link to="/settings" className="text-sm text-gray-500 hover:text-brand-600">← Settings</Link>
        <h2 className="mt-2 text-2xl font-bold text-black dark:text-white">SNMP Monitoring</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          ReadyStackGo exposes deployment health via standard SNMP polling. Configure your monitoring tool against the OIDs listed below.
        </p>
      </div>

      <StatusCard status={status} />

      <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white">MIB file</h3>
            <p className="text-sm text-gray-500 dark:text-gray-400">
              Import <code>READYSTACKGO-MIB.txt</code> into your MIB browser so the OIDs below resolve to symbolic names.
            </p>
          </div>
          <a
            href={getMibDownloadUrl()}
            className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
            download
          >
            Download MIB
          </a>
        </div>
      </div>

      <OidReferenceTree reference={reference} />
    </div>
  );
}

function StatusCard({ status }: { status: SnmpStatus }) {
  return (
    <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
      <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Agent status</h3>
      <dl className="grid grid-cols-1 md:grid-cols-2 gap-x-8 gap-y-3 text-sm">
        <Row label="Enabled" value={status.enabled ? 'Yes' : 'No — set Snmp:Enabled=true in appsettings.json'} highlight={!status.enabled} />
        <Row label="Listen address" value={`${status.listenAddress}:${status.port}/udp`} />
        <Row label="Root OID" value={status.rootOid} mono />
        <Row label="SNMPv2c" value={status.v2cConfigured ? 'configured' : 'not configured'} highlight={!status.v2cConfigured} />
        <Row label="SNMPv3 users" value={String(status.v3UserCount)} />
        <Row label="Editing" value="Read-only in v0.64 — managed via appsettings.json (container restart required)" />
      </dl>
    </div>
  );
}

function Row({ label, value, mono, highlight }: { label: string; value: string; mono?: boolean; highlight?: boolean }) {
  return (
    <>
      <dt className="text-gray-500 dark:text-gray-400">{label}</dt>
      <dd className={`${mono ? 'font-mono' : ''} ${highlight ? 'text-amber-600 dark:text-amber-400' : 'text-gray-900 dark:text-white'}`}>
        {value}
      </dd>
    </>
  );
}

function OidReferenceTree({ reference }: { reference: OidReference }) {
  return (
    <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
      <h3 className="text-lg font-semibold text-gray-900 dark:text-white">OID reference</h3>
      <p className="text-sm text-gray-500 dark:text-gray-400 mb-4">
        Concrete OIDs you can paste into Nagios, Zabbix, PRTG, etc. <code>&lt;column&gt;</code> is the column number per the MIB file (e.g. column 6 = <code>rsgoProductStatus</code>).
      </p>

      <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 uppercase mt-4">System scalars</h4>
      <ul className="mt-2 space-y-1 text-sm">
        {reference.system.map(s => (
          <li key={s.oid} className="flex items-baseline gap-3">
            <CopyOid oid={s.oid} />
            <span className="text-gray-700 dark:text-gray-300">{s.symbol}</span>
            <span className="text-xs text-gray-400">[{s.type}]</span>
            <span className="ml-auto text-xs text-gray-500 dark:text-gray-400 truncate max-w-xs" title={s.currentValue}>= {s.currentValue}</span>
          </li>
        ))}
      </ul>

      <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 uppercase mt-6">Environments / Products / Stacks</h4>
      {reference.environments.length === 0 && (
        <p className="text-sm text-gray-500 italic mt-2">No environments configured.</p>
      )}
      <ul className="mt-2 space-y-2">
        {reference.environments.map(env => (
          <EnvironmentNode key={env.environmentIndex} env={env} />
        ))}
      </ul>
    </div>
  );
}

function EnvironmentNode({ env }: { env: OidReferenceEnvironment }) {
  const [open, setOpen] = useState(true);
  return (
    <li className="border border-gray-100 dark:border-gray-800 rounded-lg p-3">
      <button onClick={() => setOpen(!open)} className="flex items-center gap-2 text-sm font-medium text-gray-900 dark:text-white w-full text-left">
        <span>{open ? '▾' : '▸'}</span>
        <span>{env.name}</span>
        <span className="text-xs text-gray-500">envIdx {env.environmentIndex}</span>
        <CopyOid oid={env.baseOid} small />
      </button>
      {open && (
        <ul className="mt-2 ml-5 space-y-2">
          {env.products.map(p => <ProductNode key={p.productIndex} product={p} />)}
          {env.products.length === 0 && <li className="text-xs text-gray-500 italic">No active products in this environment.</li>}
        </ul>
      )}
    </li>
  );
}

function ProductNode({ product }: { product: OidReferenceProduct }) {
  const [open, setOpen] = useState(true);
  return (
    <li>
      <button onClick={() => setOpen(!open)} className="flex items-center gap-2 text-sm text-gray-900 dark:text-white w-full text-left">
        <span>{open ? '▾' : '▸'}</span>
        <span className="font-medium">{product.name}</span>
        <span className="text-xs text-gray-500">v{product.version}</span>
        <StatusBadge status={product.statusText} />
        <CopyOid oid={product.baseOid} small />
      </button>
      {open && (
        <ul className="mt-1 ml-5 space-y-1">
          {product.stacks.map(s => <StackNode key={s.stackIndex} stack={s} />)}
        </ul>
      )}
    </li>
  );
}

function StackNode({ stack }: { stack: OidReferenceStack }) {
  return (
    <li className="text-sm text-gray-700 dark:text-gray-300">
      <div className="flex items-center gap-2">
        <span className="text-gray-400">└</span>
        <span>{stack.name}</span>
        <StatusBadge status={stack.statusText} />
        <CopyOid oid={stack.baseOid} small />
      </div>
      {stack.services.length > 0 && (
        <ul className="ml-6 mt-1 space-y-0.5">
          {stack.services.map(sv => (
            <li key={sv.serviceIndex} className="flex items-center gap-2 text-xs text-gray-500">
              <span>·</span>
              <span>{sv.name}</span>
              <span className={`px-1.5 rounded ${sv.running ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300' : 'bg-gray-100 text-gray-500'}`}>
                {sv.running ? 'running' : 'stopped'}
              </span>
              <CopyOid oid={sv.baseOid} small />
            </li>
          ))}
        </ul>
      )}
    </li>
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
    <button
      onClick={onClick}
      className={`group flex items-center gap-1 font-mono ${small ? 'text-xs' : 'text-sm'} text-gray-500 hover:text-brand-600`}
      title="Copy OID"
    >
      <span className="truncate max-w-md">{oid}</span>
      <span className="opacity-0 group-hover:opacity-100 text-xs">{copied ? '✓' : '⧉'}</span>
    </button>
  );
}

import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import {
  getSmtpSettings,
  saveSmtpSettings,
  testSmtpSettings,
  type SmtpSettingsDto,
} from '@rsgo/core';

const EMPTY: SmtpSettingsDto = {
  enabled: false,
  host: '',
  port: 587,
  useStartTls: true,
  username: '',
  fromAddress: '',
  fromName: 'ReadyStackGo',
  password: '',
  hasPassword: false,
};

const inputClass =
  'w-full h-11 px-4 py-2.5 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:focus:border-brand-600';
const labelClass = 'block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300';

export default function SmtpSettingsPage() {
  const [settings, setSettings] = useState<SmtpSettingsDto>(EMPTY);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [testTo, setTestTo] = useState('');
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState('');

  useEffect(() => {
    getSmtpSettings()
      .then((s) => setSettings({ ...s, password: '' }))
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load settings'))
      .finally(() => setLoading(false));
  }, []);

  const update = <K extends keyof SmtpSettingsDto>(key: K, value: SmtpSettingsDto[K]) =>
    setSettings((s) => ({ ...s, [key]: value }));

  const handleSave = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');
    setSaving(true);
    try {
      await saveSmtpSettings(settings);
      setSuccess('Settings saved.');
      setSettings((s) => ({ ...s, password: '', hasPassword: s.hasPassword || !!s.password }));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save settings');
    } finally {
      setSaving(false);
    }
  };

  const handleTest = async () => {
    setTestResult('');
    setTesting(true);
    try {
      const result = await testSmtpSettings({ ...settings, toAddress: testTo });
      setTestResult(result.success ? '✓ Test email sent successfully.' : `✗ ${result.error ?? 'Test failed.'}`);
    } catch (err) {
      setTestResult(`✗ ${err instanceof Error ? err.message : 'Test failed.'}`);
    } finally {
      setTesting(false);
    }
  };

  if (loading) {
    return <div className="p-6 text-gray-500 dark:text-gray-400">Loading…</div>;
  }

  return (
    <div className="max-w-2xl p-6 mx-auto">
      <div className="mb-6">
        <Link to="/settings" className="text-sm text-gray-500 hover:text-brand-600">← Settings</Link>
        <h2 className="mt-2 text-2xl font-bold text-black dark:text-white">Email (SMTP)</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Configure SMTP so ReadyStackGo can send invitations and verification emails.
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

      <form onSubmit={handleSave} className="space-y-5">
        <label className="flex items-center gap-3">
          <input type="checkbox" checked={settings.enabled} onChange={(e) => update('enabled', e.target.checked)} />
          <span className="text-sm font-medium text-gray-700 dark:text-gray-300">Enable email sending</span>
        </label>

        <div className="grid grid-cols-3 gap-4">
          <div className="col-span-2">
            <label className={labelClass}>SMTP host</label>
            <input className={inputClass} value={settings.host} onChange={(e) => update('host', e.target.value)} placeholder="smtp.example.com" />
          </div>
          <div>
            <label className={labelClass}>Port</label>
            <input className={inputClass} type="number" value={settings.port} onChange={(e) => update('port', Number(e.target.value))} />
          </div>
        </div>

        <label className="flex items-center gap-3">
          <input type="checkbox" checked={settings.useStartTls} onChange={(e) => update('useStartTls', e.target.checked)} />
          <span className="text-sm font-medium text-gray-700 dark:text-gray-300">Use STARTTLS</span>
        </label>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className={labelClass}>Username</label>
            <input className={inputClass} value={settings.username ?? ''} onChange={(e) => update('username', e.target.value)} autoComplete="off" />
          </div>
          <div>
            <label className={labelClass}>Password</label>
            <input
              className={inputClass}
              type="password"
              value={settings.password ?? ''}
              onChange={(e) => update('password', e.target.value)}
              placeholder={settings.hasPassword ? '•••••••• (unchanged)' : ''}
              autoComplete="new-password"
            />
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className={labelClass}>From address</label>
            <input className={inputClass} value={settings.fromAddress} onChange={(e) => update('fromAddress', e.target.value)} placeholder="noreply@example.com" />
          </div>
          <div>
            <label className={labelClass}>From name</label>
            <input className={inputClass} value={settings.fromName} onChange={(e) => update('fromName', e.target.value)} />
          </div>
        </div>

        <button
          type="submit"
          disabled={saving}
          className="px-5 py-2.5 text-sm font-medium text-white rounded-lg bg-brand-600 hover:bg-brand-700 disabled:opacity-50"
        >
          {saving ? 'Saving…' : 'Save settings'}
        </button>
      </form>

      <div className="pt-6 mt-6 border-t border-gray-200 dark:border-gray-700">
        <h3 className="mb-2 text-lg font-semibold text-black dark:text-white">Send test email</h3>
        <p className="mb-3 text-sm text-gray-500 dark:text-gray-400">
          Sends a test message using the values above (saving first is recommended).
        </p>
        <div className="flex items-end gap-3">
          <div className="flex-1">
            <label className={labelClass}>Recipient</label>
            <input className={inputClass} value={testTo} onChange={(e) => setTestTo(e.target.value)} placeholder="you@example.com" />
          </div>
          <button
            type="button"
            onClick={handleTest}
            disabled={testing || !testTo}
            className="px-5 py-2.5 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 disabled:opacity-50 dark:border-gray-700 dark:text-white/90 dark:hover:bg-white/5"
          >
            {testing ? 'Sending…' : 'Send test'}
          </button>
        </div>
        {testResult && <p className="mt-3 text-sm text-gray-700 dark:text-gray-300">{testResult}</p>}
      </div>
    </div>
  );
}

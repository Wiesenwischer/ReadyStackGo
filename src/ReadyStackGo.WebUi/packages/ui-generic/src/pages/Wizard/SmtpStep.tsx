import { useState } from 'react';
import { saveSmtpSettings, testSmtpSettings, type SmtpSettingsDto } from '@rsgo/core';
import SmtpFields from '../../components/settings/SmtpFields';

interface SmtpStepProps {
  /** Completes the wizard (install + navigate). Called after saving or skipping. */
  onComplete: () => Promise<void>;
}

const EMPTY: SmtpSettingsDto = {
  enabled: true,
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

export default function SmtpStep({ onComplete }: SmtpStepProps) {
  const [settings, setSettings] = useState<SmtpSettingsDto>(EMPTY);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [testTo, setTestTo] = useState('');
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState('');

  const finish = async (action: () => Promise<void>) => {
    setError('');
    setBusy(true);
    try {
      await action();
      await onComplete();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong');
      setBusy(false);
    }
  };

  const handleSaveAndContinue = () =>
    finish(async () => {
      if (!settings.host.trim() || !settings.fromAddress.trim()) {
        throw new Error('SMTP host and from address are required to enable email.');
      }
      await saveSmtpSettings({ ...settings, enabled: true });
    });

  const handleSkip = () => finish(async () => { /* skip: leave email disabled */ });

  const handleTest = async () => {
    setTestResult('');
    setTesting(true);
    try {
      const result = await testSmtpSettings({ ...settings, enabled: true, toAddress: testTo });
      setTestResult(result.success ? '✓ Test email sent successfully.' : `✗ ${result.error ?? 'Test failed.'}`);
    } catch (err) {
      setTestResult(`✗ ${err instanceof Error ? err.message : 'Test failed.'}`);
    } finally {
      setTesting(false);
    }
  };

  return (
    <div>
      <div className="mb-6">
        <h2 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">Configure email (optional)</h2>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Set up SMTP so ReadyStackGo can send invitations and verification emails. You can skip
          this and configure it later under Settings → Email.
        </p>
      </div>

      {error && (
        <div className="p-4 mb-5 text-sm border rounded-lg border-red-300 bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
          {error}
        </div>
      )}

      <SmtpFields value={settings} onChange={(patch) => setSettings((s) => ({ ...s, ...patch }))} showEnableToggle={false} />

      <div className="flex items-end gap-3 pt-5 mt-5 border-t border-gray-200 dark:border-gray-700">
        <div className="flex-1">
          <label className="block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">Send a test email (optional)</label>
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

      <div className="flex items-center gap-3 pt-6">
        <button
          type="button"
          onClick={handleSaveAndContinue}
          disabled={busy}
          className="inline-flex items-center justify-center py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 disabled:opacity-50 px-7"
        >
          {busy ? 'Finishing…' : 'Save & continue'}
        </button>
        <button
          type="button"
          onClick={handleSkip}
          disabled={busy}
          className="py-3 text-sm font-medium text-gray-600 hover:text-gray-800 disabled:opacity-50 dark:text-gray-400 dark:hover:text-white px-3"
        >
          Skip for now
        </button>
      </div>
    </div>
  );
}

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
  'h-11 w-full rounded-[10px] border border-line-strong/55 bg-surface px-3.5 text-sm text-fg outline-none placeholder:text-fg-muted focus:border-primary focus:ring-1 focus:ring-primary';

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
        <h2 className="mb-1.5 text-xl font-semibold text-fg">Configure email (optional)</h2>
        <p className="text-sm text-fg-secondary">
          Set up SMTP so ReadyStackGo can send invitations and verification emails. You can skip
          this and configure it later under Settings → Email.
        </p>
      </div>

      {error && (
        <div role="alert" className="mb-5 rounded-xl border border-status-unhealthy/35 bg-status-unhealthy-bg px-4 py-3 text-sm text-fg">
          {error}
        </div>
      )}

      <SmtpFields value={settings} onChange={(patch) => setSettings((s) => ({ ...s, ...patch }))} showEnableToggle={false} />

      <div className="flex items-end gap-3 pt-5 mt-5 border-t border-line">
        <div className="flex-1">
          <label className="mb-1.5 block text-sm font-medium text-fg">Send a test email (optional)</label>
          <input className={inputClass} value={testTo} onChange={(e) => setTestTo(e.target.value)} placeholder="you@example.com" />
        </div>
        <button
          type="button"
          onClick={handleTest}
          disabled={testing || !testTo}
          className="h-11 rounded-[10px] border border-line-strong bg-surface px-5 text-sm font-semibold text-fg hover:bg-raised disabled:opacity-45"
        >
          {testing ? 'Sending…' : 'Send test'}
        </button>
      </div>
      {testResult && <p className="mt-3 text-sm text-fg-secondary">{testResult}</p>}

      <div className="flex items-center gap-3 pt-6">
        <button
          type="button"
          onClick={handleSaveAndContinue}
          disabled={busy}
          className="inline-flex h-11 items-center justify-center rounded-[10px] bg-primary px-7 text-sm font-semibold text-on-primary transition-colors hover:bg-primary-hover disabled:opacity-45"
        >
          {busy ? 'Finishing…' : 'Save & continue'}
        </button>
        <button
          type="button"
          onClick={handleSkip}
          disabled={busy}
          className="px-3 py-3 text-sm font-medium text-fg-secondary hover:text-fg disabled:opacity-45"
        >
          Skip for now
        </button>
      </div>
    </div>
  );
}

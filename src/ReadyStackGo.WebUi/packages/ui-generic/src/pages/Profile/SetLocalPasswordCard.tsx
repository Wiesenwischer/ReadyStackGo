import { useState, type FormEvent } from 'react';
import { userApi } from '@rsgo/core';
import { Alert } from '../../components/ui/Alert';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/FormControls';

// "Set a local password" for accounts without a password (design frames 177:4834, 177:5913).

export default function SetLocalPasswordCard({
  username,
  providerName,
  onDone,
}: {
  username: string;
  providerName: string;
  onDone: () => void;
}) {
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (password !== confirm) {
      setError('The passwords do not match.');
      return;
    }
    setBusy(true);
    try {
      await userApi.setPassword(password);
      onDone();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'The password could not be set.');
      setBusy(false);
    }
  };

  return (
    <section className="rounded-2xl border border-line bg-surface" data-testid="set-local-password">
      <div className="border-b border-line px-6 py-[18px]">
        <h3 className="text-[17px] font-semibold text-fg">Set a local password</h3>
        <p className="mt-0.5 text-[13px] text-fg-muted">
          You sign in with {providerName}. A local password lets you also sign in with your username {username}, for example when{' '}
          {providerName} is unavailable.
        </p>
      </div>
      <form onSubmit={submit} className="flex max-w-[448px] flex-col gap-4 px-6 py-5">
        <TextField
          label="New password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
          minLength={8}
          autoComplete="new-password"
          hint="Minimum 8 characters with at least one uppercase letter, one lowercase letter, and one digit."
          testId="local-password"
        />
        <TextField
          label="Confirm new password"
          type="password"
          value={confirm}
          onChange={(e) => setConfirm(e.target.value)}
          required
          minLength={8}
          autoComplete="new-password"
          testId="local-password-confirm"
        />
        {error && (
          <Alert tone="error" title="Password not set">
            {error}
          </Alert>
        )}
        <div>
          <Button type="submit" disabled={busy || !password || !confirm}>
            {busy ? 'Setting…' : 'Set password'}
          </Button>
        </div>
      </form>
    </section>
  );
}

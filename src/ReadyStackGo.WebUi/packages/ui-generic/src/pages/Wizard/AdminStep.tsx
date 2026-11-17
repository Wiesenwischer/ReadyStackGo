import { useState, type FormEvent } from 'react';

interface AdminStepProps {
  onNext: (data: { username: string; email: string; password: string }) => Promise<void>;
}

export default function AdminStep({ onNext }: AdminStepProps) {
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');

    // Validation
    if (username.length < 3) {
      setError('Username must be at least 3 characters long');
      return;
    }

    if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) {
      setError('Please enter a valid email address');
      return;
    }

    if (password.length < 8) {
      setError('Password must be at least 8 characters long');
      return;
    }

    if (password !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    setIsLoading(true);
    try {
      await onNext({ username, email, password });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create admin user');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div>
      <div className="mb-6">
        <h2 className="mb-1.5 text-xl font-semibold text-fg">
          Create Admin Account
        </h2>
        <p className="text-sm text-fg-secondary">
          This will be the primary administrator account for ReadyStackGo
        </p>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="space-y-5">
          {error && (
            <div role="alert" className="rounded-xl border border-status-unhealthy/35 bg-status-unhealthy-bg px-4 py-3 text-sm text-fg">
              {error}
            </div>
          )}

          <div>
            <label className="mb-1.5 block text-sm font-medium text-fg">
              Username <span className="text-status-unhealthy">*</span>
            </label>
            <input
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="admin"
              required
              minLength={3}
              autoFocus
              className="h-11 w-full rounded-[10px] border border-line-strong/55 bg-surface px-3.5 text-sm text-fg outline-none placeholder:text-fg-muted focus:border-primary focus:ring-1 focus:ring-primary"
            />
            <p className="mt-1.5 text-xs text-fg-muted">
              Minimum 3 characters
            </p>
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-fg">
              Email <span className="text-status-unhealthy">*</span>
            </label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="admin@example.com"
              required
              className="h-11 w-full rounded-[10px] border border-line-strong/55 bg-surface px-3.5 text-sm text-fg outline-none placeholder:text-fg-muted focus:border-primary focus:ring-1 focus:ring-primary"
            />
            <p className="mt-1.5 text-xs text-fg-muted">
              You can verify this address later once email is configured
            </p>
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-fg">
              Password <span className="text-status-unhealthy">*</span>
            </label>
            <div className="relative">
              <input
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Enter a strong password"
                required
                minLength={8}
                className="h-11 w-full rounded-[10px] border border-line-strong/55 bg-surface px-3.5 text-sm text-fg outline-none placeholder:text-fg-muted focus:border-primary focus:ring-1 focus:ring-primary"
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="absolute z-30 -translate-y-1/2 cursor-pointer right-4 top-1/2"
              >
                {showPassword ? (
                  <svg className="w-5 h-5 fill-fg-muted" viewBox="0 0 20 20" fill="currentColor">
                    <path d="M10 12a2 2 0 100-4 2 2 0 000 4z" />
                    <path fillRule="evenodd" d="M.458 10C1.732 5.943 5.522 3 10 3s8.268 2.943 9.542 7c-1.274 4.057-5.064 7-9.542 7S1.732 14.057.458 10zM14 10a4 4 0 11-8 0 4 4 0 018 0z" clipRule="evenodd" />
                  </svg>
                ) : (
                  <svg className="w-5 h-5 fill-fg-muted" viewBox="0 0 20 20" fill="currentColor">
                    <path fillRule="evenodd" d="M3.707 2.293a1 1 0 00-1.414 1.414l14 14a1 1 0 001.414-1.414l-1.473-1.473A10.014 10.014 0 0019.542 10C18.268 5.943 14.478 3 10 3a9.958 9.958 0 00-4.512 1.074l-1.78-1.781zm4.261 4.26l1.514 1.515a2.003 2.003 0 012.45 2.45l1.514 1.514a4 4 0 00-5.478-5.478z" clipRule="evenodd" />
                    <path d="M12.454 16.697L9.75 13.992a4 4 0 01-3.742-3.741L2.335 6.578A9.98 9.98 0 00.458 10c1.274 4.057 5.065 7 9.542 7 .847 0 1.669-.105 2.454-.303z" />
                  </svg>
                )}
              </button>
            </div>
            <p className="mt-1.5 text-xs text-fg-muted">
              Minimum 8 characters
            </p>
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-fg">
              Confirm Password <span className="text-status-unhealthy">*</span>
            </label>
            <input
              type={showPassword ? 'text' : 'password'}
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              placeholder="Re-enter your password"
              required
              className="h-11 w-full rounded-[10px] border border-line-strong/55 bg-surface px-3.5 text-sm text-fg outline-none placeholder:text-fg-muted focus:border-primary focus:ring-1 focus:ring-primary"
            />
          </div>

          <div className="pt-4">
            <button
              type="submit"
              disabled={isLoading}
              className="inline-flex h-11 w-full items-center justify-center rounded-[10px] bg-primary px-7 text-sm font-semibold text-on-primary transition-colors hover:bg-primary-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus disabled:cursor-not-allowed disabled:opacity-45"
            >
              {isLoading ? 'Creating...' : 'Continue'}
            </button>
          </div>
        </div>
      </form>
    </div>
  );
}

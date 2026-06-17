import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  getInvitationInfo,
  acceptInvitation,
  decodeAuthFromToken,
  type InvitationInfoResponse,
} from '@rsgo/core';
import { useAuth } from '../../context/AuthContext';

export default function AcceptInvitationPage() {
  const [token, setToken] = useState('');
  const [info, setInfo] = useState<InvitationInfoResponse | null>(null);
  const [loadingInfo, setLoadingInfo] = useState(true);
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { setAuthDirectly } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    const t = new URLSearchParams(window.location.search).get('token') ?? '';
    setToken(t);

    if (!t) {
      setInfo({ valid: false });
      setLoadingInfo(false);
      return;
    }

    getInvitationInfo(t)
      .then(setInfo)
      .catch(() => setInfo({ valid: false }))
      .finally(() => setLoadingInfo(false));
  }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');

    if (password.length < 8) {
      setError('Password must be at least 8 characters long');
      return;
    }
    if (password !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    setIsSubmitting(true);
    try {
      const response = await acceptInvitation({ token, password });
      if (response.success && response.token) {
        const { username, role } = decodeAuthFromToken(response.token);
        setAuthDirectly(response.token, response.username ?? username, role);
        navigate('/', { replace: true });
      } else {
        setError(response.message ?? 'Failed to accept the invitation.');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to accept the invitation.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="flex items-center justify-center min-h-screen bg-white dark:bg-gray-900">
      <div className="w-full max-w-md p-8">
        <h1 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">Accept invitation</h1>

        {loadingInfo && <p className="text-gray-600 dark:text-gray-400">Validating invitation…</p>}

        {!loadingInfo && !info?.valid && (
          <div className="p-4 text-sm border rounded-lg border-red-300 bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
            This invitation is invalid or has expired. Ask an administrator to send a new one.
          </div>
        )}

        {!loadingInfo && info?.valid && (
          <>
            <p className="mb-6 text-sm text-gray-500 dark:text-gray-400">
              Set a password to activate your account for <strong>{info.email}</strong>.
            </p>

            <form onSubmit={handleSubmit} className="space-y-5">
              {error && (
                <div className="p-4 text-sm border rounded-lg border-red-300 bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
                  {error}
                </div>
              )}

              <div>
                <label className="block mb-2.5 text-sm font-medium text-gray-700 dark:text-gray-300">
                  Password <span className="text-error-500">*</span>
                </label>
                <input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Choose a strong password"
                  required
                  minLength={8}
                  className="w-full h-12.5 px-4 py-3 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:focus:border-brand-600"
                />
                <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">Minimum 8 characters</p>
              </div>

              <div>
                <label className="block mb-2.5 text-sm font-medium text-gray-700 dark:text-gray-300">
                  Confirm password <span className="text-error-500">*</span>
                </label>
                <input
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="Re-enter your password"
                  required
                  className="w-full h-12.5 px-4 py-3 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:focus:border-brand-600"
                />
              </div>

              <button
                type="submit"
                disabled={isSubmitting}
                className="inline-flex items-center justify-center w-full py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed px-7"
              >
                {isSubmitting ? 'Activating…' : 'Activate account'}
              </button>
            </form>
          </>
        )}
      </div>
    </div>
  );
}

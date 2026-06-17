import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { requestPasswordReset } from '@rsgo/core';

export default function ForgotPasswordPage() {
  const [identifier, setIdentifier] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    try {
      await requestPasswordReset(identifier);
    } catch {
      // Intentionally ignore errors — the response is generic to avoid account enumeration.
    } finally {
      setIsLoading(false);
      setSubmitted(true);
    }
  };

  return (
    <div className="flex items-center justify-center min-h-screen bg-white dark:bg-gray-900">
      <div className="w-full max-w-md p-8">
        <h1 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">Reset your password</h1>

        {submitted ? (
          <>
            <div className="p-4 mb-6 text-sm border rounded-lg border-green-300 bg-green-50 text-green-800 dark:bg-green-900/20 dark:border-green-800 dark:text-green-400">
              If an account exists for that identifier, a reset email has been sent.
            </div>
            <Link to="/login" className="text-sm text-brand-600 hover:text-brand-700">← Back to sign in</Link>
          </>
        ) : (
          <>
            <p className="mb-6 text-sm text-gray-500 dark:text-gray-400">
              Enter your email address or username and we'll send you a link to reset your password.
            </p>
            <form onSubmit={handleSubmit} className="space-y-5">
              <div>
                <label className="block mb-2.5 text-sm font-medium text-gray-700 dark:text-gray-300">
                  Email or username <span className="text-error-500">*</span>
                </label>
                <input
                  type="text"
                  value={identifier}
                  onChange={(e) => setIdentifier(e.target.value)}
                  placeholder="admin@example.com"
                  required
                  className="w-full h-12.5 px-4 py-3 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:focus:border-brand-600"
                />
              </div>
              <button
                type="submit"
                disabled={isLoading}
                className="inline-flex items-center justify-center w-full py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed px-7"
              >
                {isLoading ? 'Sending…' : 'Send reset link'}
              </button>
              <div className="text-center">
                <Link to="/login" className="text-sm text-brand-600 hover:text-brand-700">← Back to sign in</Link>
              </div>
            </form>
          </>
        )}
      </div>
    </div>
  );
}

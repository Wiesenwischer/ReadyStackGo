import { useEffect, useState } from 'react';
import { userApi, requestEmailVerification } from '@rsgo/core';

/**
 * Shows a dismissible-style prompt when the current user's email is not verified and SMTP
 * is configured (so a verification email can actually be sent).
 */
export default function EmailVerificationBanner() {
  const [show, setShow] = useState(false);
  const [sending, setSending] = useState(false);
  const [message, setMessage] = useState('');

  useEffect(() => {
    userApi
      .getProfile()
      .then((p) => setShow(!p.emailVerified && p.smtpEnabled))
      .catch(() => setShow(false));
  }, []);

  if (!show) return null;

  const handleSend = async () => {
    setSending(true);
    setMessage('');
    try {
      const result = await requestEmailVerification();
      setMessage(result.success ? 'Verification email sent. Check your inbox.' : result.message ?? 'Could not send email.');
    } catch {
      setMessage('Could not send verification email.');
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="px-4 py-3 border-b bg-amber-50 border-amber-200 dark:bg-amber-900/20 dark:border-amber-800 md:px-6">
      <div className="flex flex-wrap items-center gap-3 mx-auto max-w-(--breakpoint-2xl)">
        <svg className="w-5 h-5 text-amber-600 dark:text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <p className="flex-1 text-sm font-medium text-amber-900 dark:text-amber-200">
          Your email address is not verified.
        </p>
        {message ? (
          <span className="text-sm text-amber-800 dark:text-amber-300">{message}</span>
        ) : (
          <button
            onClick={handleSend}
            disabled={sending}
            className="text-sm font-medium text-amber-700 hover:text-amber-900 disabled:opacity-50 dark:text-amber-400"
          >
            {sending ? 'Sending…' : 'Send verification email'}
          </button>
        )}
      </div>
    </div>
  );
}

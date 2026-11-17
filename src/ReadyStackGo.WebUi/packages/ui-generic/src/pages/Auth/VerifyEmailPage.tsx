import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { verifyEmail } from '@rsgo/core';

type Status = 'verifying' | 'success' | 'error';

export default function VerifyEmailPage() {
  const [status, setStatus] = useState<Status>('verifying');
  const [message, setMessage] = useState('');

  useEffect(() => {
    const token = new URLSearchParams(window.location.search).get('token');
    if (!token) {
      setStatus('error');
      setMessage('Verification link is missing a token.');
      return;
    }

    verifyEmail(token)
      .then((result) => {
        if (result.success) {
          setStatus('success');
        } else {
          setStatus('error');
          setMessage(result.message ?? 'Verification failed.');
        }
      })
      .catch(() => {
        setStatus('error');
        setMessage('Invalid or expired verification link.');
      });
  }, []);

  return (
    <div className="flex items-center justify-center min-h-screen bg-white dark:bg-gray-900">
      <div className="w-full max-w-md p-8 text-center">
        <h1 className="mb-3 text-2xl font-semibold text-gray-800 dark:text-white">Email verification</h1>

        {status === 'verifying' && (
          <p className="text-gray-600 dark:text-gray-400">Verifying your email address…</p>
        )}

        {status === 'success' && (
          <>
            <div className="p-4 mb-6 text-sm border rounded-lg border-green-300 bg-green-50 text-green-800 dark:bg-green-900/20 dark:border-green-800 dark:text-green-400">
              Your email address has been verified.
            </div>
            <Link to="/" className="inline-flex items-center justify-center px-7 py-3 text-sm font-medium text-white rounded-lg bg-brand-600 hover:bg-brand-700">
              Go to dashboard
            </Link>
          </>
        )}

        {status === 'error' && (
          <>
            <div className="p-4 mb-6 text-sm border rounded-lg border-red-300 bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
              {message}
            </div>
            <Link to="/" className="inline-flex items-center justify-center px-7 py-3 text-sm font-medium text-white rounded-lg bg-brand-600 hover:bg-brand-700">
              Go to dashboard
            </Link>
          </>
        )}
      </div>
    </div>
  );
}

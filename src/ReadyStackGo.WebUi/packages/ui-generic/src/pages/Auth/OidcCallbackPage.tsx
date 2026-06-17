import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { decodeAuthFromToken } from '@rsgo/core';
import { useAuth } from '../../context/AuthContext';

/**
 * Receives the ReadyStackGo session token from the OIDC callback redirect (passed in the
 * URL fragment so it is never sent to the server) and seeds the auth state.
 */
export default function OidcCallbackPage() {
  const { setAuthDirectly } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    const hash = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : window.location.hash;
    const params = new URLSearchParams(hash);
    const token = params.get('token');

    if (!token) {
      navigate('/login?error=oidc_failed', { replace: true });
      return;
    }

    const { username, role } = decodeAuthFromToken(token);
    setAuthDirectly(token, username, role);
    navigate('/', { replace: true });
  }, [navigate, setAuthDirectly]);

  return (
    <div className="flex items-center justify-center min-h-screen bg-white dark:bg-gray-900">
      <div className="text-center">
        <svg className="animate-spin h-10 w-10 mx-auto text-brand-600 dark:text-brand-400 mb-4" fill="none" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
        </svg>
        <p className="text-gray-600 dark:text-gray-400">Signing you in…</p>
      </div>
    </div>
  );
}

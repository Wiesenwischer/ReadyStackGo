import { useState, useEffect, useCallback } from 'react';
import { systemApi, type TlsConfig, type LetsEncryptStatus, type ReverseProxySslMode } from '../api/system';

export interface UseTlsStoreReturn {
  config: TlsConfig | null;
  leStatus: LetsEncryptStatus | null;
  loading: boolean;
  actionLoading: boolean;
  error: string | null;
  success: string | null;
  showCertificateSettings: boolean;
  refresh: () => Promise<void>;
  handleHttpToggle: () => Promise<void>;
  handleReverseProxyToggle: () => Promise<void>;
  handleSslModeChange: (sslMode: ReverseProxySslMode) => Promise<void>;
  clearError: () => void;
  clearSuccess: () => void;
  formatDate: (dateString: string) => string;
}

export function useTlsStore(): UseTlsStoreReturn {
  const [config, setConfig] = useState<TlsConfig | null>(null);
  const [leStatus, setLeStatus] = useState<LetsEncryptStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      setLoading(true);
      const [tlsConfig, leStatusData] = await Promise.all([
        systemApi.getTlsConfig(),
        systemApi.getLetsEncryptStatus().catch(() => null),
      ]);
      setConfig(tlsConfig);
      setLeStatus(leStatusData);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load TLS configuration');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const handleHttpToggle = useCallback(async () => {
    if (!config) return;

    try {
      setActionLoading(true);
      setError(null);
      setSuccess(null);

      const response = await systemApi.updateTlsConfig({
        httpEnabled: !config.httpEnabled,
      });

      if (response.success) {
        setSuccess(response.message || 'HTTP setting updated');
        await refresh();
      } else {
        setError(response.message || 'Failed to update HTTP setting');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update HTTP setting');
    } finally {
      setActionLoading(false);
    }
  }, [config, refresh]);

  const handleReverseProxyToggle = useCallback(async () => {
    if (!config) return;

    try {
      setActionLoading(true);
      setError(null);
      setSuccess(null);

      const currentEnabled = config.reverseProxy?.enabled ?? false;
      const response = await systemApi.updateTlsConfig({
        reverseProxy: { enabled: !currentEnabled },
      });

      if (response.success) {
        setSuccess(response.message || 'Reverse proxy setting updated');
        await refresh();
      } else {
        setError(response.message || 'Failed to update reverse proxy setting');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update reverse proxy setting');
    } finally {
      setActionLoading(false);
    }
  }, [config, refresh]);

  const handleSslModeChange = useCallback(async (sslMode: ReverseProxySslMode) => {
    if (!config) return;

    try {
      setActionLoading(true);
      setError(null);
      setSuccess(null);

      const response = await systemApi.updateTlsConfig({
        reverseProxy: { sslMode },
      });

      if (response.success) {
        setSuccess(response.message || 'SSL mode updated');
        await refresh();
      } else {
        setError(response.message || 'Failed to update SSL mode');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update SSL mode');
    } finally {
      setActionLoading(false);
    }
  }, [config, refresh]);

  const showCertificateSettings = !config?.reverseProxy?.enabled ||
    config.reverseProxy.sslMode !== 'Termination';

  const clearError = useCallback(() => setError(null), []);
  const clearSuccess = useCallback(() => setSuccess(null), []);

  const formatDate = useCallback((dateString: string) => {
    return new Date(dateString).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  }, []);

  return {
    config,
    leStatus,
    loading,
    actionLoading,
    error,
    success,
    showCertificateSettings,
    refresh,
    handleHttpToggle,
    handleReverseProxyToggle,
    handleSslModeChange,
    clearError,
    clearSuccess,
    formatDate,
  };
}

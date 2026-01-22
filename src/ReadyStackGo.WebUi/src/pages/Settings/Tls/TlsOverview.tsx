import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { systemApi, type TlsConfig, type LetsEncryptStatus, type ReverseProxySslMode } from "../../../api/system";

export default function TlsOverview() {
  const [config, setConfig] = useState<TlsConfig | null>(null);
  const [leStatus, setLeStatus] = useState<LetsEncryptStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const loadData = async () => {
    try {
      setLoading(true);
      const [tlsConfig, leStatusData] = await Promise.all([
        systemApi.getTlsConfig(),
        systemApi.getLetsEncryptStatus().catch(() => null),
      ]);
      setConfig(tlsConfig);
      setLeStatus(leStatusData);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load TLS configuration");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleHttpToggle = async () => {
    if (!config) return;

    try {
      setActionLoading(true);
      setError(null);
      setSuccess(null);

      const response = await systemApi.updateTlsConfig({
        httpEnabled: !config.httpEnabled,
      });

      if (response.success) {
        setSuccess(response.message || "HTTP setting updated");
        await loadData();
      } else {
        setError(response.message || "Failed to update HTTP setting");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update HTTP setting");
    } finally {
      setActionLoading(false);
    }
  };

  const handleReverseProxyToggle = async () => {
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
        setSuccess(response.message || "Reverse proxy setting updated");
        await loadData();
      } else {
        setError(response.message || "Failed to update reverse proxy setting");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update reverse proxy setting");
    } finally {
      setActionLoading(false);
    }
  };

  const handleSslModeChange = async (sslMode: ReverseProxySslMode) => {
    if (!config) return;

    try {
      setActionLoading(true);
      setError(null);
      setSuccess(null);

      const response = await systemApi.updateTlsConfig({
        reverseProxy: { sslMode },
      });

      if (response.success) {
        setSuccess(response.message || "SSL mode updated");
        await loadData();
      } else {
        setError(response.message || "Failed to update SSL mode");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update SSL mode");
    } finally {
      setActionLoading(false);
    }
  };

  // Determine if certificate settings should be shown
  // Hide when reverse proxy is enabled with SSL Termination (proxy handles all SSL)
  const showCertificateSettings = !config?.reverseProxy?.enabled ||
    config.reverseProxy.sslMode !== "Termination";

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString(undefined, {
      year: "numeric",
      month: "long",
      day: "numeric",
    });
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <nav className="mb-6">
        <ol className="flex items-center gap-2 text-sm">
          <li>
            <Link
              to="/settings"
              className="text-gray-500 hover:text-brand-600 dark:text-gray-400 dark:hover:text-brand-400"
            >
              Settings
            </Link>
          </li>
          <li className="text-gray-400 dark:text-gray-500">/</li>
          <li className="text-gray-900 dark:text-white font-medium">
            TLS / Certificates
          </li>
        </ol>
      </nav>

      {/* Header */}
      <div className="mb-8 flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-black dark:text-white">
            TLS / Certificates
          </h2>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Manage HTTPS certificates for secure connections
          </p>
        </div>
        {showCertificateSettings && (
          <Link
            to="/settings/tls/configure"
            className="inline-flex items-center gap-2 rounded-lg bg-brand-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-brand-700 transition-colors"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
            Configure Certificate
          </Link>
        )}
      </div>

      {/* Error */}
      {error && (
        <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <div className="flex justify-between items-start">
            <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
            <button onClick={() => setError(null)} className="text-red-500 hover:text-red-600">
              <svg className="h-4 w-4" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
              </svg>
            </button>
          </div>
        </div>
      )}

      {/* Success */}
      {success && (
        <div className="mb-6 rounded-md bg-green-50 p-4 dark:bg-green-900/20">
          <div className="flex justify-between items-start">
            <p className="text-sm text-green-800 dark:text-green-200">{success}</p>
            <button onClick={() => setSuccess(null)} className="text-green-500 hover:text-green-600">
              <svg className="h-4 w-4" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
              </svg>
            </button>
          </div>
        </div>
      )}

      {/* Loading */}
      {loading ? (
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <p className="text-center text-sm text-gray-600 dark:text-gray-400">
            Loading TLS configuration...
          </p>
        </div>
      ) : (
        <div className="space-y-6">
          {/* Current Certificate Card - hidden when using SSL Termination behind proxy */}
          {showCertificateSettings && (
          <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
            <div className="px-6 py-5 border-b border-gray-200 dark:border-gray-700">
              <div className="flex items-center justify-between">
                <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                  Current Certificate
                </h3>
                <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-medium ${
                  config?.certificateInfo?.isExpired
                    ? "bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400"
                    : config?.certificateInfo?.isExpiringSoon
                    ? "bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400"
                    : "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400"
                }`}>
                  {config?.certificateInfo?.isExpired ? (
                    <>
                      <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
                        <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
                      </svg>
                      Expired
                    </>
                  ) : config?.certificateInfo?.isExpiringSoon ? (
                    <>
                      <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
                        <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
                      </svg>
                      Expiring Soon
                    </>
                  ) : (
                    <>
                      <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
                        <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                      </svg>
                      Valid
                    </>
                  )}
                </span>
              </div>
            </div>
            <div className="px-6 py-5">
              {config?.certificateInfo ? (
                <dl className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <dt className="text-sm text-gray-500 dark:text-gray-400">Type</dt>
                    <dd className="mt-1 text-sm font-medium text-gray-900 dark:text-white flex items-center gap-2">
                      {config.mode}
                      {config.certificateInfo.isSelfSigned && (
                        <span className="inline-flex rounded-full bg-yellow-100 px-2 py-0.5 text-xs font-medium text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-200">
                          Self-Signed
                        </span>
                      )}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-sm text-gray-500 dark:text-gray-400">Subject</dt>
                    <dd className="mt-1 text-sm font-mono text-gray-900 dark:text-white">
                      {config.certificateInfo.subject}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-sm text-gray-500 dark:text-gray-400">Issuer</dt>
                    <dd className="mt-1 text-sm font-mono text-gray-900 dark:text-white">
                      {config.certificateInfo.issuer}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-sm text-gray-500 dark:text-gray-400">Expires</dt>
                    <dd className={`mt-1 text-sm font-medium ${
                      config.certificateInfo.isExpired
                        ? "text-red-600 dark:text-red-400"
                        : config.certificateInfo.isExpiringSoon
                        ? "text-yellow-600 dark:text-yellow-400"
                        : "text-gray-900 dark:text-white"
                    }`}>
                      {formatDate(config.certificateInfo.expiresAt)}
                    </dd>
                  </div>
                  <div className="sm:col-span-2">
                    <dt className="text-sm text-gray-500 dark:text-gray-400">Thumbprint</dt>
                    <dd className="mt-1 text-xs font-mono text-gray-600 dark:text-gray-400 break-all">
                      {config.certificateInfo.thumbprint}
                    </dd>
                  </div>
                </dl>
              ) : (
                <p className="text-sm text-gray-500 dark:text-gray-400">
                  No certificate information available
                </p>
              )}
            </div>
          </div>
          )}

          {/* Let's Encrypt Status (if configured) - also hidden when using SSL Termination */}
          {showCertificateSettings && leStatus?.isConfigured && (
            <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
              <div className="px-6 py-5 border-b border-gray-200 dark:border-gray-700">
                <div className="flex items-center justify-between">
                  <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                    Let's Encrypt
                  </h3>
                  {leStatus.isActive && (
                    <span className="inline-flex items-center gap-1.5 rounded-full bg-green-100 px-3 py-1 text-xs font-medium text-green-700 dark:bg-green-900/30 dark:text-green-400">
                      <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
                        <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                      </svg>
                      Active
                    </span>
                  )}
                </div>
              </div>
              <div className="px-6 py-5">
                <dl className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <dt className="text-sm text-gray-500 dark:text-gray-400">Domains</dt>
                    <dd className="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                      {leStatus.domains.join(", ")}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-sm text-gray-500 dark:text-gray-400">Challenge Type</dt>
                    <dd className="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                      {leStatus.challengeType}
                    </dd>
                  </div>
                  {leStatus.certificateExpiresAt && (
                    <div>
                      <dt className="text-sm text-gray-500 dark:text-gray-400">Certificate Expires</dt>
                      <dd className="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                        {formatDate(leStatus.certificateExpiresAt)}
                      </dd>
                    </div>
                  )}
                  {leStatus.isUsingStaging && (
                    <div className="sm:col-span-2">
                      <span className="inline-flex items-center gap-1.5 rounded-full bg-yellow-100 px-3 py-1 text-xs font-medium text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400">
                        <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
                        </svg>
                        Using Staging Environment (test certificates)
                      </span>
                    </div>
                  )}
                </dl>
                {leStatus.lastError && (
                  <div className="mt-4 rounded-md bg-red-50 p-3 dark:bg-red-900/20">
                    <p className="text-sm text-red-700 dark:text-red-300">
                      Last error: {leStatus.lastError}
                    </p>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Pending DNS Challenges - also hidden when using SSL Termination */}
          {showCertificateSettings && leStatus?.pendingDnsChallenges && leStatus.pendingDnsChallenges.length > 0 && (
            <div className="rounded-2xl border border-blue-200 bg-blue-50 dark:border-blue-800 dark:bg-blue-900/20">
              <div className="px-6 py-5 border-b border-blue-200 dark:border-blue-700">
                <h3 className="text-lg font-semibold text-blue-900 dark:text-blue-100">
                  Pending DNS Challenges
                </h3>
                <p className="mt-1 text-sm text-blue-700 dark:text-blue-300">
                  Create the following TXT records in your DNS provider
                </p>
              </div>
              <div className="px-6 py-5 space-y-3">
                {leStatus.pendingDnsChallenges.map((challenge, idx) => (
                  <div key={idx} className="p-3 bg-white dark:bg-gray-800 rounded-lg text-sm font-mono">
                    <div className="flex flex-col gap-1">
                      <span className="text-gray-500 dark:text-gray-400">Name:</span>
                      <span className="text-gray-900 dark:text-white break-all">{challenge.txtRecordName}</span>
                    </div>
                    <div className="flex flex-col gap-1 mt-2">
                      <span className="text-gray-500 dark:text-gray-400">Value:</span>
                      <span className="text-gray-900 dark:text-white break-all">{challenge.txtValue}</span>
                    </div>
                  </div>
                ))}
                <Link
                  to="/settings/tls/letsencrypt/confirm"
                  className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-blue-700 transition-colors"
                >
                  Confirm DNS Records
                </Link>
              </div>
            </div>
          )}

          {/* HTTP Configuration */}
          <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
            <div className="px-6 py-5">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                    HTTP Access
                  </h3>
                  <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
                    {config?.httpEnabled
                      ? "Both HTTP (port 8080) and HTTPS are available"
                      : "Only HTTPS is available, HTTP access is disabled"}
                  </p>
                </div>
                <button
                  onClick={handleHttpToggle}
                  disabled={actionLoading}
                  className={`relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed ${
                    config?.httpEnabled ? "bg-brand-600" : "bg-gray-200 dark:bg-gray-700"
                  }`}
                  role="switch"
                  aria-checked={config?.httpEnabled}
                >
                  <span
                    className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
                      config?.httpEnabled ? "translate-x-5" : "translate-x-0"
                    }`}
                  />
                </button>
              </div>
            </div>
          </div>

          {/* Reverse Proxy Configuration */}
          <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
            <div className="px-6 py-5 border-b border-gray-200 dark:border-gray-700">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                    Reverse Proxy Mode
                  </h3>
                  <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
                    {config?.reverseProxy?.enabled
                      ? "Running behind a reverse proxy (nginx, Traefik, etc.)"
                      : "Direct connection mode (no reverse proxy)"}
                  </p>
                </div>
                <button
                  onClick={handleReverseProxyToggle}
                  disabled={actionLoading}
                  className={`relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed ${
                    config?.reverseProxy?.enabled ? "bg-brand-600" : "bg-gray-200 dark:bg-gray-700"
                  }`}
                  role="switch"
                  aria-checked={config?.reverseProxy?.enabled}
                >
                  <span
                    className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
                      config?.reverseProxy?.enabled ? "translate-x-5" : "translate-x-0"
                    }`}
                  />
                </button>
              </div>
            </div>
            {config?.reverseProxy?.enabled && (
              <div className="px-6 py-5">
                {/* SSL Mode Selection */}
                <div className="mb-6">
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">
                    SSL Handling Mode
                  </label>
                  <div className="space-y-3">
                    <label className={`flex items-start p-3 rounded-lg border cursor-pointer transition-colors ${
                      config.reverseProxy.sslMode === "Termination"
                        ? "border-brand-500 bg-brand-50 dark:bg-brand-900/20"
                        : "border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600"
                    }`}>
                      <input
                        type="radio"
                        name="sslMode"
                        value="Termination"
                        checked={config.reverseProxy.sslMode === "Termination"}
                        onChange={() => handleSslModeChange("Termination")}
                        disabled={actionLoading}
                        className="mt-0.5 h-4 w-4 text-brand-600 border-gray-300 focus:ring-brand-500"
                      />
                      <div className="ml-3">
                        <span className="block text-sm font-medium text-gray-900 dark:text-white">
                          SSL Termination
                        </span>
                        <span className="block text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                          Proxy handles HTTPS, sends HTTP to backend. No certificate needed here.
                        </span>
                      </div>
                    </label>
                    <label className={`flex items-start p-3 rounded-lg border cursor-pointer transition-colors ${
                      config.reverseProxy.sslMode === "Passthrough"
                        ? "border-brand-500 bg-brand-50 dark:bg-brand-900/20"
                        : "border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600"
                    }`}>
                      <input
                        type="radio"
                        name="sslMode"
                        value="Passthrough"
                        checked={config.reverseProxy.sslMode === "Passthrough"}
                        onChange={() => handleSslModeChange("Passthrough")}
                        disabled={actionLoading}
                        className="mt-0.5 h-4 w-4 text-brand-600 border-gray-300 focus:ring-brand-500"
                      />
                      <div className="ml-3">
                        <span className="block text-sm font-medium text-gray-900 dark:text-white">
                          SSL Passthrough
                        </span>
                        <span className="block text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                          Proxy forwards encrypted traffic. Backend handles TLS and needs a certificate.
                        </span>
                      </div>
                    </label>
                    <label className={`flex items-start p-3 rounded-lg border cursor-pointer transition-colors ${
                      config.reverseProxy.sslMode === "ReEncryption"
                        ? "border-brand-500 bg-brand-50 dark:bg-brand-900/20"
                        : "border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600"
                    }`}>
                      <input
                        type="radio"
                        name="sslMode"
                        value="ReEncryption"
                        checked={config.reverseProxy.sslMode === "ReEncryption"}
                        onChange={() => handleSslModeChange("ReEncryption")}
                        disabled={actionLoading}
                        className="mt-0.5 h-4 w-4 text-brand-600 border-gray-300 focus:ring-brand-500"
                      />
                      <div className="ml-3">
                        <span className="block text-sm font-medium text-gray-900 dark:text-white">
                          Re-Encryption
                        </span>
                        <span className="block text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                          Proxy terminates SSL, then re-encrypts to backend. Both need certificates.
                        </span>
                      </div>
                    </label>
                  </div>
                </div>

                {/* Forwarded Headers Status - only relevant for Termination and ReEncryption */}
                {config.reverseProxy.sslMode !== "Passthrough" && (
                  <>
                    <div className="border-t border-gray-200 dark:border-gray-700 pt-4 mb-4">
                      <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">
                        Trusted Headers
                      </h4>
                      <div className="space-y-2">
                        <div className="flex items-center gap-2 text-sm">
                          <svg className={`w-4 h-4 ${config.reverseProxy.trustForwardedFor ? "text-green-500" : "text-gray-400"}`} fill="currentColor" viewBox="0 0 20 20">
                            {config.reverseProxy.trustForwardedFor ? (
                              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                            ) : (
                              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
                            )}
                          </svg>
                          <span className="text-gray-700 dark:text-gray-300">
                            X-Forwarded-For (Client IP)
                          </span>
                        </div>
                        <div className="flex items-center gap-2 text-sm">
                          <svg className={`w-4 h-4 ${config.reverseProxy.trustForwardedProto ? "text-green-500" : "text-gray-400"}`} fill="currentColor" viewBox="0 0 20 20">
                            {config.reverseProxy.trustForwardedProto ? (
                              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                            ) : (
                              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
                            )}
                          </svg>
                          <span className="text-gray-700 dark:text-gray-300">
                            X-Forwarded-Proto (HTTPS detection)
                          </span>
                        </div>
                        <div className="flex items-center gap-2 text-sm">
                          <svg className={`w-4 h-4 ${config.reverseProxy.trustForwardedHost ? "text-green-500" : "text-gray-400"}`} fill="currentColor" viewBox="0 0 20 20">
                            {config.reverseProxy.trustForwardedHost ? (
                              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                            ) : (
                              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
                            )}
                          </svg>
                          <span className="text-gray-700 dark:text-gray-300">
                            X-Forwarded-Host (Host detection)
                          </span>
                        </div>
                      </div>
                    </div>
                  </>
                )}

                {config.reverseProxy.pathBase && (
                  <div className="flex items-center gap-2 text-sm mb-4">
                    <svg className="w-4 h-4 text-blue-500" fill="currentColor" viewBox="0 0 20 20">
                      <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
                    </svg>
                    <span className="text-gray-700 dark:text-gray-300">
                      Path Base: <code className="text-xs bg-gray-100 dark:bg-gray-800 px-1 py-0.5 rounded">{config.reverseProxy.pathBase}</code>
                    </span>
                  </div>
                )}

                {/* Info box based on SSL mode */}
                <div className={`mt-4 rounded-md p-3 ${
                  config.reverseProxy.sslMode === "Termination"
                    ? "bg-blue-50 dark:bg-blue-900/20"
                    : "bg-yellow-50 dark:bg-yellow-900/20"
                }`}>
                  <div className="flex">
                    <svg className={`h-5 w-5 flex-shrink-0 ${
                      config.reverseProxy.sslMode === "Termination"
                        ? "text-blue-400"
                        : "text-yellow-400"
                    }`} viewBox="0 0 20 20" fill="currentColor">
                      <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
                    </svg>
                    <p className={`ml-3 text-sm ${
                      config.reverseProxy.sslMode === "Termination"
                        ? "text-blue-700 dark:text-blue-300"
                        : "text-yellow-700 dark:text-yellow-300"
                    }`}>
                      {config.reverseProxy.sslMode === "Termination" && (
                        "The proxy handles all SSL/TLS. This backend receives unencrypted HTTP traffic. The HTTPS port is not used in this mode. Certificate settings are hidden since they're not needed."
                      )}
                      {config.reverseProxy.sslMode === "Passthrough" && (
                        "The proxy forwards encrypted traffic directly to the HTTPS port. This backend needs a valid certificate trusted by your clients to handle HTTPS connections."
                      )}
                      {config.reverseProxy.sslMode === "ReEncryption" && (
                        "The proxy terminates client SSL and creates a new encrypted connection to this backend's HTTPS port. A self-signed certificate is often acceptable here since the connection is internal."
                      )}
                    </p>
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Info Box */}
          <div className="rounded-md bg-blue-50 p-4 dark:bg-blue-900/20">
            <div className="flex">
              <svg className="h-5 w-5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
              </svg>
              <p className="ml-3 text-sm text-blue-700 dark:text-blue-300">
                Application restart may be required after certificate changes. The new certificate will be loaded on next startup.
              </p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

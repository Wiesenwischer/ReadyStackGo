import { useEffect, useState, useRef } from "react";
import {
  systemApi,
  type TlsConfig,
  type UpdateTlsConfigRequest,
  type LetsEncryptStatus,
  type LetsEncryptChallengeType,
  type LetsEncryptDnsProviderType,
} from "../../api/system";

type CertFormat = "pfx" | "pem";

export default function TlsSettings() {
  const [config, setConfig] = useState<TlsConfig | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [confirmReset, setConfirmReset] = useState(false);

  // Certificate upload state
  const [certFormat, setCertFormat] = useState<CertFormat>("pfx");
  const [pfxFile, setPfxFile] = useState<File | null>(null);
  const [pfxPassword, setPfxPassword] = useState("");
  const [certPem, setCertPem] = useState("");
  const [keyPem, setKeyPem] = useState("");

  // Let's Encrypt state
  const [leStatus, setLeStatus] = useState<LetsEncryptStatus | null>(null);
  const [leLoading, setLeLoading] = useState(false);
  const [leDomains, setLeDomains] = useState("");
  const [leEmail, setLeEmail] = useState("");
  const [leUseStaging, setLeUseStaging] = useState(false);
  const [leChallengeType, setLeChallengeType] = useState<LetsEncryptChallengeType>("Http01");
  const [leDnsProvider, setLeDnsProvider] = useState<LetsEncryptDnsProviderType>("Manual");
  const [leCloudflareToken, setLeCloudflareToken] = useState("");
  const [leCloudflareZoneId, setLeCloudflareZoneId] = useState("");

  const fileInputRef = useRef<HTMLInputElement>(null);

  const loadConfig = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await systemApi.getTlsConfig();
      setConfig(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load TLS configuration");
    } finally {
      setLoading(false);
    }
  };

  const loadLeStatus = async () => {
    try {
      setLeLoading(true);
      const status = await systemApi.getLetsEncryptStatus();
      setLeStatus(status);
      // Pre-fill form if configured
      if (status.isConfigured && status.domains.length > 0) {
        setLeDomains(status.domains.join(", "));
      }
    } catch {
      // Let's Encrypt status is optional, don't show error
    } finally {
      setLeLoading(false);
    }
  };

  useEffect(() => {
    loadConfig();
    loadLeStatus();
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
        await loadConfig();
      } else {
        setError(response.message || "Failed to update HTTP setting");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update HTTP setting");
    } finally {
      setActionLoading(false);
    }
  };

  const handleResetToSelfSigned = async () => {
    try {
      setConfirmReset(false);
      setActionLoading(true);
      setError(null);
      setSuccess(null);

      const response = await systemApi.updateTlsConfig({
        resetToSelfSigned: true,
      });

      if (response.success) {
        setSuccess(response.message || "Reset to self-signed certificate");
        await loadConfig();
      } else {
        setError(response.message || "Failed to reset certificate");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to reset certificate");
    } finally {
      setActionLoading(false);
    }
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setPfxFile(file);
    }
  };

  const handleUploadCertificate = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    try {
      setActionLoading(true);

      let request: UpdateTlsConfigRequest;

      if (certFormat === "pfx") {
        if (!pfxFile) {
          setError("Please select a PFX file");
          return;
        }

        // Read file as base64
        const base64 = await fileToBase64(pfxFile);
        request = {
          pfxBase64: base64,
          pfxPassword: pfxPassword,
        };
      } else {
        if (!certPem || !keyPem) {
          setError("Please provide both certificate and private key PEM");
          return;
        }

        request = {
          certificatePem: certPem,
          privateKeyPem: keyPem,
        };
      }

      const response = await systemApi.updateTlsConfig(request);

      if (response.success) {
        setSuccess(response.message || "Certificate uploaded successfully");
        // Reset form
        setPfxFile(null);
        setPfxPassword("");
        setCertPem("");
        setKeyPem("");
        if (fileInputRef.current) {
          fileInputRef.current.value = "";
        }
        await loadConfig();
      } else {
        setError(response.message || "Failed to upload certificate");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to upload certificate");
    } finally {
      setActionLoading(false);
    }
  };

  const handleConfigureLetsEncrypt = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    const domains = leDomains.split(",").map(d => d.trim()).filter(d => d);
    if (domains.length === 0) {
      setError("Please enter at least one domain");
      return;
    }
    if (!leEmail) {
      setError("Please enter an email address");
      return;
    }

    try {
      setActionLoading(true);

      const response = await systemApi.configureLetsEncrypt({
        domains,
        email: leEmail,
        useStaging: leUseStaging,
        challengeType: leChallengeType,
        dnsProvider: leChallengeType === "Dns01" ? {
          type: leDnsProvider,
          cloudflareApiToken: leDnsProvider === "Cloudflare" ? leCloudflareToken : undefined,
          cloudflareZoneId: leDnsProvider === "Cloudflare" ? leCloudflareZoneId : undefined,
        } : undefined,
      });

      if (response.success) {
        setSuccess(response.message || "Let's Encrypt certificate configured successfully");
        await loadConfig();
        await loadLeStatus();
      } else if (response.awaitingManualDnsChallenge) {
        setSuccess(response.message || "DNS challenges created. Please create the TXT records shown below.");
        await loadLeStatus();
      } else {
        setError(response.message || "Failed to configure Let's Encrypt");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to configure Let's Encrypt");
    } finally {
      setActionLoading(false);
    }
  };

  const handleConfirmDnsChallenge = async () => {
    try {
      setActionLoading(true);
      setError(null);
      setSuccess(null);

      const response = await systemApi.confirmLetsEncryptDns();

      if (response.success) {
        setSuccess(response.message || "DNS challenges confirmed, certificate issued successfully");
        await loadConfig();
        await loadLeStatus();
      } else {
        setError(response.message || "DNS validation failed");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to confirm DNS challenges");
    } finally {
      setActionLoading(false);
    }
  };

  const fileToBase64 = (file: File): Promise<string> => {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.readAsDataURL(file);
      reader.onload = () => {
        const result = reader.result as string;
        // Remove data URL prefix (e.g., "data:application/x-pkcs12;base64,")
        const base64 = result.split(",")[1];
        resolve(base64);
      };
      reader.onerror = (error) => reject(error);
    });
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString(undefined, {
      year: "numeric",
      month: "long",
      day: "numeric",
    });
  };

  if (loading) {
    return (
      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-4 py-8">
          <p className="text-center text-sm text-gray-600 dark:text-gray-400">
            Loading TLS configuration...
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Error/Success Messages */}
      {error && (
        <div className="rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <div className="flex">
            <div className="flex-shrink-0">
              <svg className="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
              </svg>
            </div>
            <div className="ml-3 flex-1">
              <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
            </div>
            <button onClick={() => setError(null)} className="text-red-500 hover:text-red-600">
              <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
              </svg>
            </button>
          </div>
        </div>
      )}

      {success && (
        <div className="rounded-md bg-green-50 p-4 dark:bg-green-900/20">
          <div className="flex">
            <div className="flex-shrink-0">
              <svg className="h-5 w-5 text-green-400" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
              </svg>
            </div>
            <div className="ml-3 flex-1">
              <p className="text-sm text-green-800 dark:text-green-200">{success}</p>
            </div>
            <button onClick={() => setSuccess(null)} className="text-green-500 hover:text-green-600">
              <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
              </svg>
            </button>
          </div>
        </div>
      )}

      {/* Current Certificate Status */}
      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-4 py-6 md:px-6">
          <h4 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
            Current Certificate Status
          </h4>

          {config?.certificateInfo ? (
            <div className="space-y-3">
              <div className="flex items-center gap-2">
                <span className="text-sm text-gray-500 dark:text-gray-400 w-24">Type:</span>
                <span className="text-sm font-medium text-gray-900 dark:text-white">
                  {config.mode}
                </span>
                {config.certificateInfo.isSelfSigned && (
                  <span className="inline-flex rounded-full bg-yellow-100 px-2 py-0.5 text-xs font-medium text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200">
                    Self-Signed
                  </span>
                )}
              </div>
              <div className="flex items-center gap-2">
                <span className="text-sm text-gray-500 dark:text-gray-400 w-24">Subject:</span>
                <span className="text-sm font-mono text-gray-900 dark:text-white">
                  {config.certificateInfo.subject}
                </span>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-sm text-gray-500 dark:text-gray-400 w-24">Issuer:</span>
                <span className="text-sm font-mono text-gray-900 dark:text-white">
                  {config.certificateInfo.issuer}
                </span>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-sm text-gray-500 dark:text-gray-400 w-24">Expires:</span>
                <span className={`text-sm font-medium ${
                  config.certificateInfo.isExpired
                    ? "text-red-600 dark:text-red-400"
                    : config.certificateInfo.isExpiringSoon
                    ? "text-yellow-600 dark:text-yellow-400"
                    : "text-green-600 dark:text-green-400"
                }`}>
                  {formatDate(config.certificateInfo.expiresAt)}
                  {config.certificateInfo.isExpired && " (Expired)"}
                  {config.certificateInfo.isExpiringSoon && !config.certificateInfo.isExpired && " (Expiring Soon)"}
                </span>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-sm text-gray-500 dark:text-gray-400 w-24">Thumbprint:</span>
                <span className="text-xs font-mono text-gray-600 dark:text-gray-400">
                  {config.certificateInfo.thumbprint}
                </span>
              </div>
            </div>
          ) : (
            <p className="text-sm text-gray-500 dark:text-gray-400">
              No certificate information available
            </p>
          )}
        </div>
      </div>

      {/* HTTP Configuration */}
      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-4 py-6 md:px-6">
          <h4 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
            HTTP Configuration
          </h4>

          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium text-gray-900 dark:text-white">
                Enable HTTP access (port 8080)
              </p>
              <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                Both HTTP and HTTPS will be available when enabled.
              </p>
            </div>
            <button
              onClick={handleHttpToggle}
              disabled={actionLoading}
              className={`relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2 disabled:opacity-50 ${
                config?.httpEnabled ? "bg-brand-600" : "bg-gray-200 dark:bg-gray-700"
              }`}
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

      {/* Let's Encrypt Configuration */}
      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-4 py-6 md:px-6">
          <div className="flex items-center justify-between mb-4">
            <h4 className="text-lg font-semibold text-gray-900 dark:text-white">
              Let's Encrypt
            </h4>
            {leStatus?.isActive && (
              <span className="inline-flex rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900 dark:text-green-200">
                Active
              </span>
            )}
          </div>

          {/* Let's Encrypt Status */}
          {leStatus?.isConfigured && (
            <div className="mb-4 p-3 rounded-md bg-gray-50 dark:bg-gray-800/50">
              <div className="space-y-2 text-sm">
                <div className="flex items-center gap-2">
                  <span className="text-gray-500 dark:text-gray-400">Domains:</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {leStatus.domains.join(", ")}
                  </span>
                </div>
                {leStatus.certificateExpiresAt && (
                  <div className="flex items-center gap-2">
                    <span className="text-gray-500 dark:text-gray-400">Expires:</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {formatDate(leStatus.certificateExpiresAt)}
                    </span>
                  </div>
                )}
                {leStatus.isUsingStaging && (
                  <div className="text-yellow-600 dark:text-yellow-400 text-xs">
                    Using Staging Environment (test certificates)
                  </div>
                )}
                {leStatus.lastError && (
                  <div className="text-red-600 dark:text-red-400 text-xs">
                    Last error: {leStatus.lastError}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Pending DNS Challenges */}
          {leStatus?.pendingDnsChallenges && leStatus.pendingDnsChallenges.length > 0 && (
            <div className="mb-4 p-3 rounded-md bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800">
              <h5 className="text-sm font-medium text-blue-800 dark:text-blue-200 mb-2">
                Pending DNS Challenges
              </h5>
              <p className="text-xs text-blue-700 dark:text-blue-300 mb-3">
                Create the following TXT records in your DNS provider, then click "Confirm DNS Records".
              </p>
              <div className="space-y-2">
                {leStatus.pendingDnsChallenges.map((challenge, idx) => (
                  <div key={idx} className="p-2 bg-white dark:bg-gray-800 rounded text-xs font-mono">
                    <div><span className="text-gray-500">Name:</span> {challenge.txtRecordName}</div>
                    <div><span className="text-gray-500">Value:</span> {challenge.txtValue}</div>
                  </div>
                ))}
              </div>
              <button
                onClick={handleConfirmDnsChallenge}
                disabled={actionLoading}
                className="mt-3 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {actionLoading ? "Confirming..." : "Confirm DNS Records"}
              </button>
            </div>
          )}

          {/* Let's Encrypt Configuration Form */}
          <form onSubmit={handleConfigureLetsEncrypt} className="space-y-4">
            {/* Domains */}
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Domains
              </label>
              <input
                type="text"
                value={leDomains}
                onChange={(e) => setLeDomains(e.target.value)}
                placeholder="example.com, www.example.com"
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                Comma-separated list of domains
              </p>
            </div>

            {/* Email */}
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Email (for expiry notices)
              </label>
              <input
                type="email"
                value={leEmail}
                onChange={(e) => setLeEmail(e.target.value)}
                placeholder="admin@example.com"
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
            </div>

            {/* Challenge Type */}
            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Challenge Type
              </label>
              <div className="flex gap-4">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    name="leChallengeType"
                    value="Http01"
                    checked={leChallengeType === "Http01"}
                    onChange={() => setLeChallengeType("Http01")}
                    className="h-4 w-4 text-brand-600 focus:ring-brand-500"
                  />
                  <span className="text-sm text-gray-700 dark:text-gray-300">HTTP-01</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    name="leChallengeType"
                    value="Dns01"
                    checked={leChallengeType === "Dns01"}
                    onChange={() => setLeChallengeType("Dns01")}
                    className="h-4 w-4 text-brand-600 focus:ring-brand-500"
                  />
                  <span className="text-sm text-gray-700 dark:text-gray-300">DNS-01</span>
                </label>
              </div>
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                {leChallengeType === "Http01"
                  ? "Requires port 80 to be accessible from the internet"
                  : "Requires DNS TXT record creation (supports wildcard certificates)"}
              </p>
            </div>

            {/* DNS Provider (only for DNS-01) */}
            {leChallengeType === "Dns01" && (
              <>
                <div>
                  <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    DNS Provider
                  </label>
                  <div className="flex gap-4">
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="radio"
                        name="leDnsProvider"
                        value="Manual"
                        checked={leDnsProvider === "Manual"}
                        onChange={() => setLeDnsProvider("Manual")}
                        className="h-4 w-4 text-brand-600 focus:ring-brand-500"
                      />
                      <span className="text-sm text-gray-700 dark:text-gray-300">Manual</span>
                    </label>
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="radio"
                        name="leDnsProvider"
                        value="Cloudflare"
                        checked={leDnsProvider === "Cloudflare"}
                        onChange={() => setLeDnsProvider("Cloudflare")}
                        className="h-4 w-4 text-brand-600 focus:ring-brand-500"
                      />
                      <span className="text-sm text-gray-700 dark:text-gray-300">Cloudflare</span>
                    </label>
                  </div>
                </div>

                {/* Cloudflare Configuration */}
                {leDnsProvider === "Cloudflare" && (
                  <>
                    <div>
                      <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                        Cloudflare API Token
                      </label>
                      <input
                        type="password"
                        value={leCloudflareToken}
                        onChange={(e) => setLeCloudflareToken(e.target.value)}
                        placeholder="Your Cloudflare API token"
                        className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                      />
                      <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                        Token needs Zone:DNS:Edit permissions
                      </p>
                    </div>
                    <div>
                      <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                        Cloudflare Zone ID
                      </label>
                      <input
                        type="text"
                        value={leCloudflareZoneId}
                        onChange={(e) => setLeCloudflareZoneId(e.target.value)}
                        placeholder="Zone ID from Cloudflare dashboard"
                        className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                      />
                    </div>
                  </>
                )}
              </>
            )}

            {/* Use Staging */}
            <div className="flex items-center gap-3">
              <input
                type="checkbox"
                id="leUseStaging"
                checked={leUseStaging}
                onChange={(e) => setLeUseStaging(e.target.checked)}
                className="h-4 w-4 rounded border-gray-300 text-brand-600 focus:ring-brand-500"
              />
              <label htmlFor="leUseStaging" className="text-sm text-gray-700 dark:text-gray-300">
                Use Staging Environment (for testing)
              </label>
            </div>

            <div className="pt-2">
              <button
                type="submit"
                disabled={actionLoading || leLoading}
                className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {actionLoading ? "Configuring..." : "Request Certificate"}
              </button>
            </div>
          </form>

          <div className="mt-4 rounded-md bg-blue-50 p-3 dark:bg-blue-900/20">
            <div className="flex">
              <svg className="h-5 w-5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
              </svg>
              <p className="ml-3 text-xs text-blue-700 dark:text-blue-300">
                Let's Encrypt certificates are free and auto-renew every 90 days. Ensure your domains point to this server.
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Upload Custom Certificate */}
      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-4 py-6 md:px-6">
          <h4 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
            Upload Custom Certificate
          </h4>

          <form onSubmit={handleUploadCertificate} className="space-y-4">
            {/* Format Selection */}
            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Certificate Format
              </label>
              <div className="flex gap-4">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    name="certFormat"
                    value="pfx"
                    checked={certFormat === "pfx"}
                    onChange={() => setCertFormat("pfx")}
                    className="h-4 w-4 text-brand-600 focus:ring-brand-500"
                  />
                  <span className="text-sm text-gray-700 dark:text-gray-300">PFX / PKCS#12</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    name="certFormat"
                    value="pem"
                    checked={certFormat === "pem"}
                    onChange={() => setCertFormat("pem")}
                    className="h-4 w-4 text-brand-600 focus:ring-brand-500"
                  />
                  <span className="text-sm text-gray-700 dark:text-gray-300">PEM</span>
                </label>
              </div>
            </div>

            {certFormat === "pfx" ? (
              <>
                {/* PFX File Upload */}
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    PFX File
                  </label>
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept=".pfx,.p12"
                    onChange={handleFileSelect}
                    className="block w-full text-sm text-gray-500 dark:text-gray-400
                      file:mr-4 file:py-2 file:px-4
                      file:rounded-lg file:border-0
                      file:text-sm file:font-medium
                      file:bg-brand-50 file:text-brand-700
                      hover:file:bg-brand-100
                      dark:file:bg-brand-900/30 dark:file:text-brand-300"
                  />
                  {pfxFile && (
                    <p className="mt-1 text-xs text-gray-500">
                      Selected: {pfxFile.name}
                    </p>
                  )}
                </div>

                {/* PFX Password */}
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Certificate Password
                  </label>
                  <input
                    type="password"
                    value={pfxPassword}
                    onChange={(e) => setPfxPassword(e.target.value)}
                    placeholder="Enter certificate password"
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                </div>
              </>
            ) : (
              <>
                {/* PEM Certificate */}
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Certificate (PEM)
                  </label>
                  <textarea
                    value={certPem}
                    onChange={(e) => setCertPem(e.target.value)}
                    placeholder="-----BEGIN CERTIFICATE-----&#10;...&#10;-----END CERTIFICATE-----"
                    rows={5}
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                </div>

                {/* PEM Private Key */}
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Private Key (PEM)
                  </label>
                  <textarea
                    value={keyPem}
                    onChange={(e) => setKeyPem(e.target.value)}
                    placeholder="-----BEGIN PRIVATE KEY-----&#10;...&#10;-----END PRIVATE KEY-----"
                    rows={5}
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                </div>
              </>
            )}

            <div className="flex items-center justify-between pt-2">
              {confirmReset ? (
                <div className="flex items-center gap-2">
                  <span className="text-xs text-gray-500 dark:text-gray-400">Reset to self-signed?</span>
                  <button
                    type="button"
                    onClick={handleResetToSelfSigned}
                    disabled={actionLoading}
                    className="rounded bg-red-600 px-2 py-1 text-xs font-medium text-white hover:bg-red-700 disabled:opacity-50"
                  >
                    {actionLoading ? "..." : "Confirm"}
                  </button>
                  <button
                    type="button"
                    onClick={() => setConfirmReset(false)}
                    className="rounded bg-gray-200 px-2 py-1 text-xs font-medium text-gray-700 hover:bg-gray-300 dark:bg-gray-700 dark:text-gray-300"
                  >
                    Cancel
                  </button>
                </div>
              ) : (
                <button
                  type="button"
                  onClick={() => setConfirmReset(true)}
                  disabled={actionLoading || config?.mode === "SelfSigned"}
                  className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  Reset to Self-Signed
                </button>
              )}
              <button
                type="submit"
                disabled={actionLoading}
                className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {actionLoading ? "Uploading..." : "Upload Certificate"}
              </button>
            </div>
          </form>

          <div className="mt-4 rounded-md bg-yellow-50 p-3 dark:bg-yellow-900/20">
            <div className="flex">
              <svg className="h-5 w-5 text-yellow-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
              </svg>
              <p className="ml-3 text-xs text-yellow-700 dark:text-yellow-300">
                Application restart is required after certificate changes. The new certificate will be loaded on next startup.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

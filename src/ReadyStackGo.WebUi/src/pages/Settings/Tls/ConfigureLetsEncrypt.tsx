import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  systemApi,
  type LetsEncryptChallengeType,
  type LetsEncryptDnsProviderType,
} from "../../../api/system";

export default function ConfigureLetsEncrypt() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [domains, setDomains] = useState("");
  const [email, setEmail] = useState("");
  const [useStaging, setUseStaging] = useState(false);
  const [challengeType, setChallengeType] = useState<LetsEncryptChallengeType>("Http01");
  const [dnsProvider, setDnsProvider] = useState<LetsEncryptDnsProviderType>("Manual");
  const [cloudflareToken, setCloudflareToken] = useState("");
  const [cloudflareZoneId, setCloudflareZoneId] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const domainList = domains.split(",").map(d => d.trim()).filter(d => d);
    if (domainList.length === 0) {
      setError("Please enter at least one domain");
      return;
    }
    if (!email) {
      setError("Please enter an email address");
      return;
    }

    try {
      setLoading(true);

      const response = await systemApi.configureLetsEncrypt({
        domains: domainList,
        email,
        useStaging,
        challengeType,
        dnsProvider: challengeType === "Dns01" ? {
          type: dnsProvider,
          cloudflareApiToken: dnsProvider === "Cloudflare" ? cloudflareToken : undefined,
          cloudflareZoneId: dnsProvider === "Cloudflare" ? cloudflareZoneId : undefined,
        } : undefined,
      });

      if (response.success) {
        navigate("/settings/tls", {
          state: { message: response.message || "Let's Encrypt certificate configured successfully" }
        });
      } else if (response.awaitingManualDnsChallenge) {
        navigate("/settings/tls", {
          state: { message: "DNS challenges created. Please create the TXT records shown below." }
        });
      } else {
        setError(response.message || "Failed to configure Let's Encrypt");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to configure Let's Encrypt");
    } finally {
      setLoading(false);
    }
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
          <li>
            <Link
              to="/settings/tls"
              className="text-gray-500 hover:text-brand-600 dark:text-gray-400 dark:hover:text-brand-400"
            >
              TLS / Certificates
            </Link>
          </li>
          <li className="text-gray-400 dark:text-gray-500">/</li>
          <li className="text-gray-900 dark:text-white font-medium">
            Let's Encrypt
          </li>
        </ol>
      </nav>

      {/* Header */}
      <div className="mb-8">
        <h2 className="text-2xl font-bold text-black dark:text-white">
          Configure Let's Encrypt
        </h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Obtain a free, automatically renewed certificate from Let's Encrypt
        </p>
      </div>

      {/* Error */}
      {error && (
        <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <div className="flex">
            <svg className="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
            </svg>
            <p className="ml-3 text-sm text-red-800 dark:text-red-200">{error}</p>
          </div>
        </div>
      )}

      {/* Form */}
      <form onSubmit={handleSubmit}>
        <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="px-6 py-5 border-b border-gray-200 dark:border-gray-700">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
              Certificate Details
            </h3>
          </div>
          <div className="px-6 py-5 space-y-6">
            {/* Domains */}
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                Domains <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={domains}
                onChange={(e) => setDomains(e.target.value)}
                placeholder="example.com, www.example.com"
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1.5 text-xs text-gray-500 dark:text-gray-400">
                Comma-separated list of domains. All domains must point to this server.
              </p>
            </div>

            {/* Email */}
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                Email Address <span className="text-red-500">*</span>
              </label>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="admin@example.com"
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1.5 text-xs text-gray-500 dark:text-gray-400">
                Used for certificate expiry notifications
              </p>
            </div>

            {/* Challenge Type */}
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">
                Challenge Type
              </label>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <label
                  className={`relative flex cursor-pointer rounded-lg border p-4 focus:outline-none ${
                    challengeType === "Http01"
                      ? "border-brand-500 bg-brand-50 dark:bg-brand-900/20"
                      : "border-gray-200 dark:border-gray-700"
                  }`}
                >
                  <input
                    type="radio"
                    name="challengeType"
                    value="Http01"
                    checked={challengeType === "Http01"}
                    onChange={() => setChallengeType("Http01")}
                    className="sr-only"
                  />
                  <div className="flex flex-col">
                    <span className={`block text-sm font-medium ${
                      challengeType === "Http01"
                        ? "text-brand-700 dark:text-brand-300"
                        : "text-gray-900 dark:text-white"
                    }`}>
                      HTTP-01
                    </span>
                    <span className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                      Requires port 80 to be accessible from the internet
                    </span>
                  </div>
                  {challengeType === "Http01" && (
                    <svg className="absolute top-4 right-4 h-5 w-5 text-brand-600" viewBox="0 0 20 20" fill="currentColor">
                      <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                    </svg>
                  )}
                </label>

                <label
                  className={`relative flex cursor-pointer rounded-lg border p-4 focus:outline-none ${
                    challengeType === "Dns01"
                      ? "border-brand-500 bg-brand-50 dark:bg-brand-900/20"
                      : "border-gray-200 dark:border-gray-700"
                  }`}
                >
                  <input
                    type="radio"
                    name="challengeType"
                    value="Dns01"
                    checked={challengeType === "Dns01"}
                    onChange={() => setChallengeType("Dns01")}
                    className="sr-only"
                  />
                  <div className="flex flex-col">
                    <span className={`block text-sm font-medium ${
                      challengeType === "Dns01"
                        ? "text-brand-700 dark:text-brand-300"
                        : "text-gray-900 dark:text-white"
                    }`}>
                      DNS-01
                    </span>
                    <span className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                      Requires DNS TXT record creation (supports wildcards)
                    </span>
                  </div>
                  {challengeType === "Dns01" && (
                    <svg className="absolute top-4 right-4 h-5 w-5 text-brand-600" viewBox="0 0 20 20" fill="currentColor">
                      <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                    </svg>
                  )}
                </label>
              </div>
            </div>

            {/* DNS Provider (only for DNS-01) */}
            {challengeType === "Dns01" && (
              <>
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">
                    DNS Provider
                  </label>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <label
                      className={`relative flex cursor-pointer rounded-lg border p-4 focus:outline-none ${
                        dnsProvider === "Manual"
                          ? "border-brand-500 bg-brand-50 dark:bg-brand-900/20"
                          : "border-gray-200 dark:border-gray-700"
                      }`}
                    >
                      <input
                        type="radio"
                        name="dnsProvider"
                        value="Manual"
                        checked={dnsProvider === "Manual"}
                        onChange={() => setDnsProvider("Manual")}
                        className="sr-only"
                      />
                      <div className="flex flex-col">
                        <span className={`block text-sm font-medium ${
                          dnsProvider === "Manual"
                            ? "text-brand-700 dark:text-brand-300"
                            : "text-gray-900 dark:text-white"
                        }`}>
                          Manual
                        </span>
                        <span className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                          Create TXT records manually in your DNS provider
                        </span>
                      </div>
                      {dnsProvider === "Manual" && (
                        <svg className="absolute top-4 right-4 h-5 w-5 text-brand-600" viewBox="0 0 20 20" fill="currentColor">
                          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                        </svg>
                      )}
                    </label>

                    <label
                      className={`relative flex cursor-pointer rounded-lg border p-4 focus:outline-none ${
                        dnsProvider === "Cloudflare"
                          ? "border-brand-500 bg-brand-50 dark:bg-brand-900/20"
                          : "border-gray-200 dark:border-gray-700"
                      }`}
                    >
                      <input
                        type="radio"
                        name="dnsProvider"
                        value="Cloudflare"
                        checked={dnsProvider === "Cloudflare"}
                        onChange={() => setDnsProvider("Cloudflare")}
                        className="sr-only"
                      />
                      <div className="flex flex-col">
                        <span className={`block text-sm font-medium ${
                          dnsProvider === "Cloudflare"
                            ? "text-brand-700 dark:text-brand-300"
                            : "text-gray-900 dark:text-white"
                        }`}>
                          Cloudflare
                        </span>
                        <span className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                          Automatic DNS record creation via Cloudflare API
                        </span>
                      </div>
                      {dnsProvider === "Cloudflare" && (
                        <svg className="absolute top-4 right-4 h-5 w-5 text-brand-600" viewBox="0 0 20 20" fill="currentColor">
                          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                        </svg>
                      )}
                    </label>
                  </div>
                </div>

                {/* Cloudflare Configuration */}
                {dnsProvider === "Cloudflare" && (
                  <div className="space-y-4 p-4 rounded-lg bg-gray-50 dark:bg-gray-800/50">
                    <div>
                      <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                        Cloudflare API Token <span className="text-red-500">*</span>
                      </label>
                      <input
                        type="password"
                        value={cloudflareToken}
                        onChange={(e) => setCloudflareToken(e.target.value)}
                        placeholder="Your Cloudflare API token"
                        className="w-full rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white"
                      />
                      <p className="mt-1.5 text-xs text-gray-500 dark:text-gray-400">
                        Token needs Zone:DNS:Edit permissions
                      </p>
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                        Cloudflare Zone ID <span className="text-red-500">*</span>
                      </label>
                      <input
                        type="text"
                        value={cloudflareZoneId}
                        onChange={(e) => setCloudflareZoneId(e.target.value)}
                        placeholder="Zone ID from Cloudflare dashboard"
                        className="w-full rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white"
                      />
                    </div>
                  </div>
                )}
              </>
            )}

            {/* Use Staging */}
            <div className="flex items-start gap-3">
              <input
                type="checkbox"
                id="useStaging"
                checked={useStaging}
                onChange={(e) => setUseStaging(e.target.checked)}
                className="mt-1 h-4 w-4 rounded border-gray-300 text-brand-600 focus:ring-brand-500"
              />
              <div>
                <label htmlFor="useStaging" className="text-sm font-medium text-gray-900 dark:text-white">
                  Use Staging Environment
                </label>
                <p className="text-xs text-gray-500 dark:text-gray-400">
                  For testing only. Staging certificates are not trusted by browsers.
                </p>
              </div>
            </div>
          </div>

          {/* Actions */}
          <div className="px-6 py-4 border-t border-gray-200 dark:border-gray-700 flex items-center justify-between">
            <Link
              to="/settings/tls/configure"
              className="text-sm font-medium text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white"
            >
              Back
            </Link>
            <button
              type="submit"
              disabled={loading}
              className="inline-flex items-center gap-2 rounded-lg bg-brand-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {loading ? (
                <>
                  <svg className="animate-spin h-4 w-4" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                  </svg>
                  Requesting Certificate...
                </>
              ) : (
                "Request Certificate"
              )}
            </button>
          </div>
        </div>
      </form>

      {/* Info Box */}
      <div className="mt-6 rounded-md bg-blue-50 p-4 dark:bg-blue-900/20">
        <div className="flex">
          <svg className="h-5 w-5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
          </svg>
          <p className="ml-3 text-sm text-blue-700 dark:text-blue-300">
            Let's Encrypt certificates are free and auto-renew every 90 days. Ensure your domains point to this server before requesting.
          </p>
        </div>
      </div>
    </div>
  );
}

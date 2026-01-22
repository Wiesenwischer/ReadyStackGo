import { useState, useRef } from "react";
import { Link, useNavigate } from "react-router-dom";
import { systemApi, type UpdateTlsConfigRequest } from "../../../api/system";

type CertFormat = "pfx" | "pem";

export default function UploadCertificate() {
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [certFormat, setCertFormat] = useState<CertFormat>("pfx");
  const [pfxFile, setPfxFile] = useState<File | null>(null);
  const [pfxPassword, setPfxPassword] = useState("");
  const [certPem, setCertPem] = useState("");
  const [keyPem, setKeyPem] = useState("");

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setPfxFile(file);
    }
  };

  const fileToBase64 = (file: File): Promise<string> => {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.readAsDataURL(file);
      reader.onload = () => {
        const result = reader.result as string;
        const base64 = result.split(",")[1];
        resolve(base64);
      };
      reader.onerror = (error) => reject(error);
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    try {
      setLoading(true);

      let request: UpdateTlsConfigRequest;

      if (certFormat === "pfx") {
        if (!pfxFile) {
          setError("Please select a PFX file");
          return;
        }

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
        navigate("/settings/tls", {
          state: { message: response.message || "Certificate uploaded successfully. Restart required." }
        });
      } else {
        setError(response.message || "Failed to upload certificate");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to upload certificate");
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
            Upload Certificate
          </li>
        </ol>
      </nav>

      {/* Header */}
      <div className="mb-8">
        <h2 className="text-2xl font-bold text-black dark:text-white">
          Upload Custom Certificate
        </h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Upload your own SSL/TLS certificate in PFX or PEM format
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
              Certificate Format
            </h3>
          </div>
          <div className="px-6 py-5 space-y-6">
            {/* Format Selection */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <label
                className={`relative flex cursor-pointer rounded-lg border p-4 focus:outline-none ${
                  certFormat === "pfx"
                    ? "border-brand-500 bg-brand-50 dark:bg-brand-900/20"
                    : "border-gray-200 dark:border-gray-700"
                }`}
              >
                <input
                  type="radio"
                  name="certFormat"
                  value="pfx"
                  checked={certFormat === "pfx"}
                  onChange={() => setCertFormat("pfx")}
                  className="sr-only"
                />
                <div className="flex flex-col">
                  <span className={`block text-sm font-medium ${
                    certFormat === "pfx"
                      ? "text-brand-700 dark:text-brand-300"
                      : "text-gray-900 dark:text-white"
                  }`}>
                    PFX / PKCS#12
                  </span>
                  <span className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                    Single file containing certificate and private key
                  </span>
                </div>
                {certFormat === "pfx" && (
                  <svg className="absolute top-4 right-4 h-5 w-5 text-brand-600" viewBox="0 0 20 20" fill="currentColor">
                    <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                  </svg>
                )}
              </label>

              <label
                className={`relative flex cursor-pointer rounded-lg border p-4 focus:outline-none ${
                  certFormat === "pem"
                    ? "border-brand-500 bg-brand-50 dark:bg-brand-900/20"
                    : "border-gray-200 dark:border-gray-700"
                }`}
              >
                <input
                  type="radio"
                  name="certFormat"
                  value="pem"
                  checked={certFormat === "pem"}
                  onChange={() => setCertFormat("pem")}
                  className="sr-only"
                />
                <div className="flex flex-col">
                  <span className={`block text-sm font-medium ${
                    certFormat === "pem"
                      ? "text-brand-700 dark:text-brand-300"
                      : "text-gray-900 dark:text-white"
                  }`}>
                    PEM
                  </span>
                  <span className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                    Separate certificate and private key in PEM format
                  </span>
                </div>
                {certFormat === "pem" && (
                  <svg className="absolute top-4 right-4 h-5 w-5 text-brand-600" viewBox="0 0 20 20" fill="currentColor">
                    <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                  </svg>
                )}
              </label>
            </div>

            {/* PFX Upload */}
            {certFormat === "pfx" && (
              <div className="space-y-4 p-4 rounded-lg bg-gray-50 dark:bg-gray-800/50">
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                    PFX File <span className="text-red-500">*</span>
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
                    <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">
                      Selected: {pfxFile.name}
                    </p>
                  )}
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                    Certificate Password
                  </label>
                  <input
                    type="password"
                    value={pfxPassword}
                    onChange={(e) => setPfxPassword(e.target.value)}
                    placeholder="Enter certificate password (if any)"
                    className="w-full rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white"
                  />
                  <p className="mt-1.5 text-xs text-gray-500 dark:text-gray-400">
                    Leave empty if the PFX file has no password
                  </p>
                </div>
              </div>
            )}

            {/* PEM Upload */}
            {certFormat === "pem" && (
              <div className="space-y-4 p-4 rounded-lg bg-gray-50 dark:bg-gray-800/50">
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                    Certificate (PEM) <span className="text-red-500">*</span>
                  </label>
                  <textarea
                    value={certPem}
                    onChange={(e) => setCertPem(e.target.value)}
                    placeholder="-----BEGIN CERTIFICATE-----&#10;...&#10;-----END CERTIFICATE-----"
                    rows={6}
                    className="w-full rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white"
                  />
                  <p className="mt-1.5 text-xs text-gray-500 dark:text-gray-400">
                    Include the full certificate chain if applicable
                  </p>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                    Private Key (PEM) <span className="text-red-500">*</span>
                  </label>
                  <textarea
                    value={keyPem}
                    onChange={(e) => setKeyPem(e.target.value)}
                    placeholder="-----BEGIN PRIVATE KEY-----&#10;...&#10;-----END PRIVATE KEY-----"
                    rows={6}
                    className="w-full rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white"
                  />
                </div>
              </div>
            )}
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
                  Uploading...
                </>
              ) : (
                <>
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                  </svg>
                  Upload Certificate
                </>
              )}
            </button>
          </div>
        </div>
      </form>

      {/* Warning Box */}
      <div className="mt-6 rounded-md bg-yellow-50 p-4 dark:bg-yellow-900/20">
        <div className="flex">
          <svg className="h-5 w-5 text-yellow-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
          </svg>
          <p className="ml-3 text-sm text-yellow-700 dark:text-yellow-300">
            Application restart is required after uploading a new certificate. The new certificate will be loaded on next startup.
          </p>
        </div>
      </div>
    </div>
  );
}

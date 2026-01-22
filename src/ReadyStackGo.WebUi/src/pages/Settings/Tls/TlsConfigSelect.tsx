import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { TypeSelector, type TypeOption } from "../../../components/ui/TypeSelector";

type CertificateType = "letsencrypt" | "custom" | "selfsigned";

const certificateOptions: TypeOption<CertificateType>[] = [
  {
    id: "letsencrypt",
    label: "Let's Encrypt",
    description: "Free, automated certificates with auto-renewal",
    icon: (
      <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
      </svg>
    ),
  },
  {
    id: "custom",
    label: "Custom Certificate",
    description: "Upload your own PFX or PEM certificate",
    icon: (
      <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
      </svg>
    ),
  },
  {
    id: "selfsigned",
    label: "Self-Signed Certificate",
    description: "Generate a new self-signed certificate",
    icon: (
      <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
      </svg>
    ),
  },
];

export default function TlsConfigSelect() {
  const navigate = useNavigate();
  const [selectedType, setSelectedType] = useState<CertificateType | null>(null);

  const handleContinue = () => {
    if (!selectedType) return;

    if (selectedType === "selfsigned") {
      navigate("/settings/tls/selfsigned");
    } else if (selectedType === "letsencrypt") {
      navigate("/settings/tls/letsencrypt");
    } else if (selectedType === "custom") {
      navigate("/settings/tls/upload");
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
            Configure
          </li>
        </ol>
      </nav>

      {/* Header */}
      <div className="mb-8">
        <h2 className="text-2xl font-bold text-black dark:text-white">
          Configure Certificate
        </h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Choose how you want to configure your HTTPS certificate
        </p>
      </div>

      {/* Type Selection */}
      <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
        <TypeSelector
          options={certificateOptions}
          value={selectedType}
          onChange={setSelectedType}
          columns={3}
        />

        {/* Action Buttons */}
        <div className="mt-8 flex items-center justify-between border-t border-gray-200 pt-6 dark:border-gray-700">
          <Link
            to="/settings/tls"
            className="text-sm font-medium text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white"
          >
            Cancel
          </Link>
          <button
            onClick={handleContinue}
            disabled={!selectedType}
            className="inline-flex items-center gap-2 rounded-lg bg-brand-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            Continue
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
            </svg>
          </button>
        </div>
      </div>
    </div>
  );
}

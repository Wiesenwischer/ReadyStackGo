import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { TypeSelector, type TypeOption } from "../../components/ui/TypeSelector";

type EnvironmentConnectionType = "docker-socket" | "ssh-tunnel";

const connectionTypeOptions: TypeOption<EnvironmentConnectionType>[] = [
  {
    id: "docker-socket",
    label: "Local Docker Socket",
    description: "Direct connection via Unix socket on the local server",
    icon: (
      <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01" />
      </svg>
    ),
  },
  {
    id: "ssh-tunnel",
    label: "SSH Tunnel",
    description: "Remote Docker host via encrypted SSH connection",
    icon: (
      <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
      </svg>
    ),
  },
];

export default function AddEnvironment() {
  const navigate = useNavigate();
  const [selectedType, setSelectedType] = useState<EnvironmentConnectionType | null>(null);

  const handleContinue = () => {
    if (selectedType) {
      navigate(`/environments/add/${selectedType}`);
    }
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center gap-2 text-sm">
        <Link to="/environments" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Environments
        </Link>
        <span className="text-gray-400">/</span>
        <span className="text-gray-900 dark:text-white">Add Environment</span>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-6 py-6 border-b border-gray-200 dark:border-gray-700">
          <h4 className="text-xl font-semibold text-black dark:text-white">
            Add Environment
          </h4>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Choose how to connect to a Docker daemon
          </p>
        </div>

        <div className="p-6">
          <TypeSelector
            options={connectionTypeOptions}
            value={selectedType}
            onChange={setSelectedType}
            columns={2}
          />

          <div className="mt-8 flex items-center justify-between border-t border-gray-200 dark:border-gray-700 pt-6">
            <Link
              to="/environments"
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
            >
              Cancel
            </Link>
            <button
              onClick={handleContinue}
              disabled={!selectedType}
              className="rounded-md bg-brand-600 px-6 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              Continue
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

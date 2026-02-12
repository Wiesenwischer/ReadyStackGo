import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { createStackSource, type CreateStackSourceRequest } from "../../../api/stackSources";

export default function AddGitSource() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formData, setFormData] = useState({
    id: "",
    name: "",
    gitUrl: "",
    branch: "main",
    path: "",
    filePattern: "*.yml;*.yaml",
    gitUsername: "",
    gitPassword: "",
    sslVerify: true,
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    try {
      setLoading(true);

      const request: CreateStackSourceRequest = {
        id: formData.id,
        name: formData.name,
        type: "GitRepository",
        gitUrl: formData.gitUrl,
        branch: formData.branch || "main",
        path: formData.path || undefined,
        filePattern: formData.filePattern || undefined,
        gitUsername: formData.gitUsername || undefined,
        gitPassword: formData.gitPassword || undefined,
        sslVerify: formData.sslVerify,
      };

      const response = await createStackSource(request);
      if (response.success) {
        navigate("/settings/stack-sources");
      } else {
        setError(response.message || "Failed to create stack source");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create stack source");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center gap-2 text-sm">
        <Link to="/settings" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Settings
        </Link>
        <span className="text-gray-400">/</span>
        <Link to="/settings/stack-sources" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Stack Sources
        </Link>
        <span className="text-gray-400">/</span>
        <Link to="/settings/stack-sources/add" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Add Source
        </Link>
        <span className="text-gray-400">/</span>
        <span className="text-gray-900 dark:text-white">Git Repository</span>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-6 py-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 flex items-center justify-center rounded-full bg-purple-100 text-purple-600 dark:bg-purple-900/30 dark:text-purple-400">
              <svg className="w-5 h-5" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z"/>
              </svg>
            </div>
            <div>
              <h4 className="text-xl font-semibold text-black dark:text-white">
                Git Repository Source
              </h4>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Clone and sync stack definitions from a Git repository
              </p>
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="p-6">
          {error && (
            <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
              <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
            </div>
          )}

          <div className="space-y-6 max-w-2xl">
            {/* Basic Info */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                  Source ID <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={formData.id}
                  onChange={(e) => setFormData({ ...formData, id: e.target.value })}
                  placeholder="my-git-stacks"
                  required
                  className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                />
                <p className="mt-1 text-xs text-gray-500">
                  Unique identifier for this source (lowercase, no spaces)
                </p>
              </div>
              <div>
                <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                  Display Name <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  placeholder="My Git Stacks"
                  required
                  className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                />
              </div>
            </div>

            {/* Repository URL */}
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Repository URL <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={formData.gitUrl}
                onChange={(e) => setFormData({ ...formData, gitUrl: e.target.value })}
                placeholder="https://github.com/org/stacks-repo.git"
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
            </div>

            {/* Branch and Sub-path */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                  Branch
                </label>
                <input
                  type="text"
                  value={formData.branch}
                  onChange={(e) => setFormData({ ...formData, branch: e.target.value })}
                  placeholder="main"
                  className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                />
              </div>
              <div>
                <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                  Sub-path
                </label>
                <input
                  type="text"
                  value={formData.path}
                  onChange={(e) => setFormData({ ...formData, path: e.target.value })}
                  placeholder="stacks/"
                  className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                />
                <p className="mt-1 text-xs text-gray-500">
                  Optional path within the repository
                </p>
              </div>
            </div>

            {/* File Pattern */}
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                File Pattern
              </label>
              <input
                type="text"
                value={formData.filePattern}
                onChange={(e) => setFormData({ ...formData, filePattern: e.target.value })}
                placeholder="*.yml;*.yaml"
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                Semicolon-separated patterns for stack files (default: *.yml;*.yaml)
              </p>
            </div>

            {/* Authentication Section */}
            <div className="rounded-lg border border-gray-200 dark:border-gray-700 p-4">
              <h5 className="text-sm font-medium text-gray-900 dark:text-white mb-3">
                Authentication
                <span className="ml-2 text-xs font-normal text-gray-500">(optional, for private repositories)</span>
              </h5>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Username
                  </label>
                  <input
                    type="text"
                    value={formData.gitUsername}
                    onChange={(e) => setFormData({ ...formData, gitUsername: e.target.value })}
                    placeholder="git-user"
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                </div>
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Password / Token
                  </label>
                  <input
                    type="password"
                    value={formData.gitPassword}
                    onChange={(e) => setFormData({ ...formData, gitPassword: e.target.value })}
                    placeholder="Enter password or token"
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                </div>
              </div>
              <p className="mt-3 text-xs text-gray-500">
                For GitHub/GitLab, use a Personal Access Token instead of your password.
                For TFS/Azure DevOps Server, use a Personal Access Token with &quot;pat&quot; as username.
              </p>

              <div className="mt-4 flex items-center gap-3">
                <label className="relative inline-flex items-center cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formData.sslVerify}
                    onChange={(e) => setFormData({ ...formData, sslVerify: e.target.checked })}
                    className="sr-only peer"
                  />
                  <div className="w-9 h-5 bg-gray-200 peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-brand-300 rounded-full peer dark:bg-gray-600 peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all dark:border-gray-500 peer-checked:bg-brand-600"></div>
                </label>
                <div>
                  <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
                    Verify SSL Certificate
                  </span>
                  <p className="text-xs text-gray-500">
                    Disable for servers with self-signed or expired certificates
                  </p>
                </div>
              </div>
            </div>
          </div>

          <div className="mt-8 flex items-center justify-between border-t border-gray-200 dark:border-gray-700 pt-6">
            <Link
              to="/settings/stack-sources/add"
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
            >
              Back
            </Link>
            <button
              type="submit"
              disabled={loading}
              className="rounded-md bg-brand-600 px-6 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {loading ? "Creating..." : "Create Source"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

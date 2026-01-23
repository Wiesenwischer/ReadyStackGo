import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  getRegistry,
  updateRegistry,
  type RegistryDto,
  type UpdateRegistryRequest,
} from "../../../api/registries";

const KNOWN_REGISTRIES = [
  { label: "Docker Hub", url: "https://index.docker.io/v1/" },
  { label: "GitHub Container Registry", url: "https://ghcr.io" },
  { label: "GitLab Container Registry", url: "https://registry.gitlab.com" },
  { label: "Quay.io", url: "https://quay.io" },
  { label: "Custom", url: "" },
];

export default function EditRegistry() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [registry, setRegistry] = useState<RegistryDto | null>(null);
  const [formData, setFormData] = useState({
    name: "",
    url: "",
    username: "",
    password: "",
  });
  const [patternsInput, setPatternsInput] = useState("");
  const [clearCredentials, setClearCredentials] = useState(false);
  const [selectedRegistry, setSelectedRegistry] = useState<string>("custom");

  const handleRegistryChange = (value: string) => {
    setSelectedRegistry(value);
    const registry = KNOWN_REGISTRIES.find((r) => r.url === value);
    if (registry && registry.url !== "") {
      setFormData({ ...formData, url: registry.url, name: registry.label });
    }
  };

  useEffect(() => {
    const loadRegistry = async () => {
      if (!id) return;

      try {
        setLoading(true);
        const response = await getRegistry(id);
        if (response.success && response.registry) {
          setRegistry(response.registry);
          setFormData({
            name: response.registry.name,
            url: response.registry.url,
            username: response.registry.username || "",
            password: "",
          });
          setPatternsInput(response.registry.imagePatterns.join("\n"));
        } else {
          setError(response.message || "Registry not found");
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load registry");
      } finally {
        setLoading(false);
      }
    };

    loadRegistry();
  }, [id]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!id || !registry) return;

    setError(null);

    const patterns = patternsInput
      .split("\n")
      .map((p) => p.trim())
      .filter((p) => p.length > 0);

    try {
      setSaving(true);

      const request: UpdateRegistryRequest = {
        name: formData.name !== registry.name ? formData.name : undefined,
        url: formData.url !== registry.url ? formData.url : undefined,
        username: formData.username || undefined,
        password: formData.password || undefined,
        clearCredentials: clearCredentials,
        imagePatterns: patterns,
      };

      const response = await updateRegistry(id, request);
      if (response.success) {
        navigate("/settings/registries");
      } else {
        setError(response.message || "Failed to update registry");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update registry");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="flex items-center justify-center py-16">
          <p className="text-gray-500 dark:text-gray-400">Loading registry...</p>
        </div>
      </div>
    );
  }

  if (!registry) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="flex flex-col items-center justify-center py-16">
          <p className="text-gray-500 dark:text-gray-400 mb-4">Registry not found</p>
          <Link
            to="/settings/registries"
            className="text-brand-600 hover:text-brand-700"
          >
            Back to registries
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center gap-2 text-sm">
        <Link to="/settings" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Settings
        </Link>
        <span className="text-gray-400">/</span>
        <Link to="/settings/registries" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Container Registries
        </Link>
        <span className="text-gray-400">/</span>
        <span className="text-gray-900 dark:text-white">Edit</span>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-6 py-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 flex items-center justify-center rounded-full bg-purple-100 text-purple-600 dark:bg-purple-900/30 dark:text-purple-400">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 8h14M5 8a2 2 0 110-4h14a2 2 0 110 4M5 8v10a2 2 0 002 2h10a2 2 0 002-2V8m-9 4h4" />
              </svg>
            </div>
            <div>
              <h4 className="text-xl font-semibold text-black dark:text-white">
                Edit Registry
              </h4>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                {registry.name}
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
            {/* Registry Selection */}
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Quick Select Registry
              </label>
              <select
                value={selectedRegistry}
                onChange={(e) => handleRegistryChange(e.target.value)}
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              >
                {KNOWN_REGISTRIES.map((registry) => (
                  <option key={registry.url || "custom"} value={registry.url}>
                    {registry.label}
                  </option>
                ))}
              </select>
              <p className="mt-1 text-xs text-gray-500">
                Quick fill from known registries or keep your custom configuration
              </p>
            </div>

            {/* Basic Info */}
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Registry Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                placeholder="Docker Hub"
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                Custom name for this registry
              </p>
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Registry URL <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={formData.url}
                onChange={(e) => setFormData({ ...formData, url: e.target.value })}
                placeholder="https://index.docker.io/v1/"
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                Registry URL (can be adjusted if needed)
              </p>
            </div>

            {/* Credentials */}
            <div className="rounded-lg border border-gray-200 dark:border-gray-700 p-4">
              <h5 className="text-sm font-medium text-gray-900 dark:text-white mb-3">
                Authentication
                {registry.hasCredentials && (
                  <span className="ml-2 inline-flex rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900 dark:text-green-200">
                    Configured
                  </span>
                )}
              </h5>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Username
                  </label>
                  <input
                    type="text"
                    value={formData.username}
                    onChange={(e) => setFormData({ ...formData, username: e.target.value })}
                    placeholder="username"
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                </div>
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Password / Token
                  </label>
                  <input
                    type="password"
                    value={formData.password}
                    onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                    placeholder="(unchanged)"
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                </div>
              </div>
              {registry.hasCredentials && (
                <div className="mt-3 flex items-center gap-2">
                  <input
                    type="checkbox"
                    id="clearCredentials"
                    checked={clearCredentials}
                    onChange={(e) => setClearCredentials(e.target.checked)}
                    className="h-4 w-4 rounded border-gray-300 text-brand-600 focus:ring-brand-500"
                  />
                  <label htmlFor="clearCredentials" className="text-sm text-gray-700 dark:text-gray-300">
                    Clear existing credentials
                  </label>
                </div>
              )}
            </div>

            {/* Image Patterns */}
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Image Patterns
              </label>
              <textarea
                value={patternsInput}
                onChange={(e) => setPatternsInput(e.target.value)}
                placeholder={"library/*\nmyorg/*\nghcr.io/myorg/**"}
                rows={4}
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                One pattern per line. Use * for single segment match, ** for multiple segments.
              </p>
            </div>
          </div>

          <div className="mt-8 flex items-center justify-between border-t border-gray-200 dark:border-gray-700 pt-6">
            <Link
              to="/settings/registries"
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
            >
              Cancel
            </Link>
            <button
              type="submit"
              disabled={saving}
              className="rounded-md bg-brand-600 px-6 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {saving ? "Saving..." : "Save Changes"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

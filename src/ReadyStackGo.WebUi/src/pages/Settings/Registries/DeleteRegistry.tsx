import { useEffect, useState } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { getRegistry, deleteRegistry, type RegistryDto } from "../../../api/registries";

type DeleteState = "loading" | "confirm" | "deleting" | "success" | "error";

export default function DeleteRegistry() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [state, setState] = useState<DeleteState>("loading");
  const [registry, setRegistry] = useState<RegistryDto | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!id) {
      setState("error");
      setError("No registry ID provided");
      return;
    }

    const loadRegistry = async () => {
      try {
        setState("loading");
        setError("");

        const response = await getRegistry(id);
        if (response.success && response.registry) {
          setRegistry(response.registry);
          setState("confirm");
        } else {
          setError(response.message || "Registry not found");
          setState("error");
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load registry");
        setState("error");
      }
    };

    loadRegistry();
  }, [id]);

  const handleDelete = async () => {
    if (!id) {
      setError("No registry to delete");
      return;
    }

    setState("deleting");
    setError("");

    try {
      const response = await deleteRegistry(id);

      if (response.success) {
        setState("success");
        // Navigate back after 2 seconds
        setTimeout(() => {
          navigate("/settings/registries");
        }, 2000);
      } else {
        setError(response.message || "Failed to delete registry");
        setState("error");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete registry");
      setState("error");
    }
  };

  // Loading state
  if (state === "loading") {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="flex items-center justify-center py-16">
          <p className="text-gray-500 dark:text-gray-400">Loading registry...</p>
        </div>
      </div>
    );
  }

  // Error state
  if (state === "error" && !registry) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6 flex items-center gap-2 text-sm">
          <Link to="/settings" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
            Settings
          </Link>
          <span className="text-gray-400">/</span>
          <Link to="/settings/registries" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
            Container Registries
          </Link>
          <span className="text-gray-400">/</span>
          <span className="text-gray-900 dark:text-white">Delete</span>
        </div>

        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center justify-center py-8">
            <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-red-100 dark:bg-red-900/30">
              <svg className="h-8 w-8 text-red-600 dark:text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">Error</h3>
            <p className="text-sm text-gray-600 dark:text-gray-400 mb-6">{error}</p>
            <Link
              to="/settings/registries"
              className="rounded-md bg-brand-600 px-6 py-2 text-sm font-medium text-white hover:bg-brand-700"
            >
              Back to Registries
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // Success state
  if (state === "success") {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6 flex items-center gap-2 text-sm">
          <Link to="/settings" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
            Settings
          </Link>
          <span className="text-gray-400">/</span>
          <Link to="/settings/registries" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
            Container Registries
          </Link>
          <span className="text-gray-400">/</span>
          <span className="text-gray-900 dark:text-white">Delete</span>
        </div>

        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center justify-center py-8">
            <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-green-100 dark:bg-green-900/30">
              <svg className="h-8 w-8 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
            </div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">Registry Deleted</h3>
            <p className="text-sm text-gray-600 dark:text-gray-400 mb-2">
              The registry has been deleted successfully.
            </p>
            <p className="text-xs text-gray-500 dark:text-gray-500">
              Redirecting back to registries...
            </p>
          </div>
        </div>
      </div>
    );
  }

  // Confirm state
  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6 flex items-center gap-2 text-sm">
        <Link to="/settings" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Settings
        </Link>
        <span className="text-gray-400">/</span>
        <Link to="/settings/registries" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Container Registries
        </Link>
        <span className="text-gray-400">/</span>
        <span className="text-gray-900 dark:text-white">Delete</span>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-6 py-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 flex items-center justify-center rounded-full bg-red-100 text-red-600 dark:bg-red-900/30 dark:text-red-400">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
            </div>
            <div>
              <h4 className="text-xl font-semibold text-black dark:text-white">
                Delete Registry
              </h4>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Confirm deletion of this container registry
              </p>
            </div>
          </div>
        </div>

        <div className="p-6">
          {error && (
            <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
              <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
            </div>
          )}

          <div className="mb-6 rounded-lg border border-yellow-200 bg-yellow-50 p-4 dark:border-yellow-800 dark:bg-yellow-900/20">
            <div className="flex gap-3">
              <svg className="h-5 w-5 flex-shrink-0 text-yellow-600 dark:text-yellow-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
              <div>
                <h3 className="text-sm font-medium text-yellow-800 dark:text-yellow-200">
                  Warning: This action cannot be undone
                </h3>
                <p className="mt-1 text-sm text-yellow-700 dark:text-yellow-300">
                  You are about to delete the registry. This will remove all configuration and credentials.
                </p>
              </div>
            </div>
          </div>

          {registry && (
            <div className="mb-6 rounded-lg border border-gray-200 bg-gray-50 p-4 dark:border-gray-700 dark:bg-gray-800/50">
              <dl className="space-y-2">
                <div>
                  <dt className="text-xs font-medium text-gray-500 dark:text-gray-400">Registry Name</dt>
                  <dd className="mt-1 text-sm text-gray-900 dark:text-white font-medium">{registry.name}</dd>
                </div>
                <div>
                  <dt className="text-xs font-medium text-gray-500 dark:text-gray-400">Registry URL</dt>
                  <dd className="mt-1 text-sm text-gray-900 dark:text-white font-mono">{registry.url}</dd>
                </div>
                {registry.isDefault && (
                  <div>
                    <span className="inline-flex items-center rounded-full bg-brand-100 px-2.5 py-0.5 text-xs font-medium text-brand-800 dark:bg-brand-900/30 dark:text-brand-300">
                      Default Registry
                    </span>
                  </div>
                )}
              </dl>
            </div>
          )}

          <div className="flex items-center justify-between border-t border-gray-200 dark:border-gray-700 pt-6">
            <Link
              to="/settings/registries"
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
            >
              Cancel
            </Link>
            <button
              onClick={handleDelete}
              disabled={state === "deleting"}
              className="rounded-md bg-red-600 px-6 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {state === "deleting" ? "Deleting..." : "Delete Registry"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

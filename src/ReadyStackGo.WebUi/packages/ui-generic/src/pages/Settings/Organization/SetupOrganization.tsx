import { Link, useNavigate } from 'react-router-dom';
import { useSetupOrganizationStore } from '@rsgo/core';

export default function SetupOrganization() {
  const navigate = useNavigate();
  const store = useSetupOrganizationStore();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const success = await store.submit();
    if (success) {
      navigate('/');
    }
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center gap-2 text-sm">
        <Link to="/settings" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Settings
        </Link>
        <span className="text-gray-400 dark:text-gray-500">/</span>
        <span className="text-gray-900 dark:text-white">Organization</span>
      </div>

      <div className="mb-8">
        <h2 className="text-2xl font-bold text-black dark:text-white">
          Set Up Organization
        </h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Create your organization to start managing environments, stack sources, and registries.
        </p>
      </div>

      <div className="max-w-lg">
        <form onSubmit={handleSubmit}>
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            {store.error && (
              <div className="mb-4 rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800 dark:border-red-900 dark:bg-red-900/30 dark:text-red-300">
                {store.error}
              </div>
            )}

            <div className="mb-6">
              <label
                htmlFor="org-name"
                className="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300"
              >
                Organization Name
              </label>
              <input
                id="org-name"
                type="text"
                value={store.name}
                onChange={(e) => store.setName(e.target.value)}
                placeholder="My Company"
                className="w-full rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm text-gray-900 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-800 dark:text-white dark:focus:border-brand-500"
                autoFocus
                disabled={store.loading}
              />
              <p className="mt-1.5 text-xs text-gray-500 dark:text-gray-400">
                This is the name of your company or team. You can change it later.
              </p>
            </div>

            <div className="flex items-center gap-3">
              <button
                type="submit"
                disabled={store.loading || !store.name.trim()}
                className="rounded-lg bg-brand-500 px-5 py-2.5 text-sm font-medium text-white hover:bg-brand-600 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {store.loading ? 'Creating...' : 'Create Organization'}
              </button>
              <Link
                to="/"
                className="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300"
              >
                Cancel
              </Link>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}

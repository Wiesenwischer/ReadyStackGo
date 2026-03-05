import { useEffect, useRef } from 'react';
import { useParams, Link, useSearchParams, useNavigate } from 'react-router';
import { useUpgradeStackStore } from '@rsgo/core';
import { useAuth } from '../../context/AuthContext';
import { useEnvironment } from '../../context/EnvironmentContext';
import VariableInput, { groupVariables } from '../../components/variables/VariableInput';

export default function UpgradeStack() {
  const { stackName } = useParams<{ stackName: string }>();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { token } = useAuth();
  const { activeEnvironment } = useEnvironment();
  const envFileInputRef = useRef<HTMLInputElement>(null);
  const logEndRef = useRef<HTMLDivElement>(null);

  // Get optional target version from URL params
  const targetVersionParam = searchParams.get('version');

  const store = useUpgradeStackStore(token, activeEnvironment?.id, stackName, targetVersionParam);

  // Auto-scroll init container logs to bottom
  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [store.initContainerLogs]);

  // Handle .env file import
  const handleEnvFileImport = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (event) => {
        const content = event.target?.result as string;
        store.handleEnvFileContent(content);
      };
      reader.readAsText(file);
    }
    if (envFileInputRef.current) {
      envFileInputRef.current.value = '';
    }
  };

  const getBackUrl = () => `/deployments/${encodeURIComponent(stackName || '')}`;

  if (store.state === 'loading') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="flex items-center justify-center py-12">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-600 mx-auto mb-4"></div>
            <p className="text-gray-600 dark:text-gray-400">Loading upgrade data...</p>
          </div>
        </div>
      </div>
    );
  }

  if (store.state === 'error' && !store.targetStack) {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6">
          <Link
            to={getBackUrl()}
            className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            Back to Deployment
          </Link>
        </div>
        <div className="rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm text-red-800 dark:text-red-200">{store.error}</p>
        </div>
      </div>
    );
  }

  if (store.state === 'success') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="flex items-center justify-center w-16 h-16 mb-6 bg-green-100 rounded-full dark:bg-green-900/30">
              <svg className="w-8 h-8 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
            </div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Upgrade Successful!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              {store.deployment?.stackName} has been upgraded to version {store.selectedVersion || store.upgradeInfo?.latestVersion}
            </p>

            <div className="flex gap-4">
              <Link
                to={getBackUrl()}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
              >
                View Deployment
              </Link>
              <Link
                to="/deployments"
                className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                All Deployments
              </Link>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (store.state === 'upgrading') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="w-16 h-16 mb-6 border-4 border-brand-600 border-t-transparent rounded-full animate-spin"></div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Upgrading Stack...
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Upgrading {store.deployment?.stackName} to version {store.selectedVersion || store.upgradeInfo?.latestVersion}
            </p>

            {/* Progress Section */}
            <div className="w-full max-w-md">
              {/* Progress Bar */}
              <div className="mb-4">
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-gray-600 dark:text-gray-400">
                    {store.formattedPhase || 'Initializing'}
                  </span>
                  <span className="text-gray-600 dark:text-gray-400">
                    {store.progressUpdate?.percentComplete ?? 0}%
                  </span>
                </div>
                <div className="h-3 bg-gray-200 rounded-full dark:bg-gray-700 overflow-hidden">
                  <div
                    className="h-full bg-brand-600 rounded-full transition-all duration-500 ease-out"
                    style={{ width: `${store.progressUpdate?.percentComplete ?? 0}%` }}
                  />
                </div>
              </div>

              {/* Status Message */}
              <div className="text-center">
                <p className="text-sm text-gray-700 dark:text-gray-300 font-medium">
                  {store.progressUpdate?.message || 'Starting upgrade...'}
                </p>

                {store.progressUpdate && (store.progressUpdate.totalServices > 0 || store.progressUpdate.totalInitContainers > 0) && (
                  <p className="text-xs text-gray-500 dark:text-gray-400 mt-2">
                    {store.progressUpdate.phase === 'PullingImages'
                      ? `Images: ${store.progressUpdate.completedServices} / ${store.progressUpdate.totalServices}`
                      : store.progressUpdate.phase === 'InitializingContainers'
                        ? `Init Containers: ${store.progressUpdate.completedInitContainers} / ${store.progressUpdate.totalInitContainers}`
                        : `Services: ${store.progressUpdate.completedServices} / ${store.progressUpdate.totalServices}`
                    }
                    {store.progressUpdate.currentService && (
                      <span className="ml-2">
                        (current: <span className="font-mono">{store.progressUpdate.currentService}</span>)
                      </span>
                    )}
                  </p>
                )}
              </div>

              {/* Connection Status */}
              <div className="mt-6 flex items-center justify-center gap-2 text-xs text-gray-500 dark:text-gray-400">
                <span className={`w-2 h-2 rounded-full ${
                  store.connectionState === 'connected' ? 'bg-green-500' :
                  store.connectionState === 'connecting' ? 'bg-yellow-500' :
                  store.connectionState === 'reconnecting' ? 'bg-yellow-500' :
                  'bg-red-500'
                }`} />
                {store.connectionState === 'connected' ? 'Live updates' :
                 store.connectionState === 'connecting' ? 'Connecting...' :
                 store.connectionState === 'reconnecting' ? 'Reconnecting...' :
                 'Updates unavailable'}
              </div>
            </div>

            {/* Init Container Logs - full width */}
            {Object.keys(store.initContainerLogs).length > 0 && (
              <div className="mt-6 w-full">
                <div className="px-3 py-2 text-xs font-medium text-gray-600 dark:text-gray-400 bg-gray-100 dark:bg-gray-800 rounded-t-lg">
                  Init Container Logs
                </div>
                <div className="bg-gray-900 rounded-b-lg p-3 max-h-80 overflow-y-auto">
                  {Object.entries(store.initContainerLogs).map(([name, lines]) => (
                    <div key={name} className="mb-2 last:mb-0">
                      <div className="text-xs font-bold text-blue-400 mb-1">{name}</div>
                      {lines.map((line, i) => (
                        <div key={i} className="font-mono text-xs text-green-400 whitespace-pre-wrap break-all leading-relaxed">{line}</div>
                      ))}
                    </div>
                  ))}
                  <div ref={logEndRef} />
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    );
  }

  // Configure state
  return (
    <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6">
        <Link
          to={getBackUrl()}
          className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back to Deployment
        </Link>
      </div>

      {/* Header */}
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
          Upgrade {store.deployment?.stackName}
        </h1>
        <p className="text-gray-600 dark:text-gray-400">
          Upgrade from version <span className="font-medium">{store.upgradeInfo?.currentVersion}</span> to{' '}
          <span className="font-medium">{store.selectedVersion || store.upgradeInfo?.latestVersion}</span>
        </p>
      </div>

      {/* Error Display */}
      {store.error && (
        <div className="mb-6 p-4 text-sm text-red-800 bg-red-100 rounded-lg dark:bg-red-900/30 dark:text-red-400">
          <p className="font-medium mb-1">Error</p>
          <p>{store.error}</p>
          <div className="mt-3 pt-3 border-t border-red-200 dark:border-red-800">
            <p className="text-xs text-red-700 dark:text-red-300 mb-2">
              If the upgrade failed, you may be able to rollback to the previous version.
            </p>
            <Link
              to={getBackUrl()}
              className="inline-flex items-center gap-1 text-sm font-medium text-red-700 hover:text-red-900 dark:text-red-300 dark:hover:text-red-100"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 17l-5-5m0 0l5-5m-5 5h12" />
              </svg>
              Go to Deployment Details for Rollback
            </Link>
          </div>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Main Configuration */}
        <div className="lg:col-span-2 space-y-6">
          {/* Version Selection */}
          {(store.upgradeInfo?.availableVersions?.length ?? 0) > 1 && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Target Version
              </h2>
              <div>
                <label className="block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                  Select target version
                </label>
                <select
                  value={store.selectedVersion ?? store.upgradeInfo?.latestVersion ?? ''}
                  onChange={(e) => store.handleVersionChange(e.target.value)}
                  className="w-full px-4 py-3 border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                >
                  {store.upgradeInfo?.availableVersions?.map((v) => (
                    <option key={v.version} value={v.version}>
                      {v.version}{v.version === store.upgradeInfo?.latestVersion ? ' (latest)' : ''}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          )}

          {/* Variable Changes Info */}
          {((store.upgradeInfo?.newVariables?.length ?? 0) > 0 || (store.upgradeInfo?.removedVariables?.length ?? 0) > 0) && (
            <div className="rounded-2xl border border-blue-200 bg-blue-50 p-6 dark:border-blue-800 dark:bg-blue-900/20">
              <h2 className="text-lg font-semibold text-blue-900 dark:text-blue-100 mb-4">
                Configuration Changes
              </h2>
              {(store.upgradeInfo?.newVariables?.length ?? 0) > 0 && (
                <div className="mb-3">
                  <p className="text-sm font-medium text-blue-800 dark:text-blue-200 mb-2">New Variables:</p>
                  <div className="flex flex-wrap gap-2">
                    {store.upgradeInfo?.newVariables?.map((v) => (
                      <span key={v} className="inline-flex items-center rounded-full bg-blue-100 px-3 py-1 text-xs font-medium text-blue-800 dark:bg-blue-800 dark:text-blue-200">
                        {v}
                      </span>
                    ))}
                  </div>
                </div>
              )}
              {(store.upgradeInfo?.removedVariables?.length ?? 0) > 0 && (
                <div>
                  <p className="text-sm font-medium text-amber-700 dark:text-amber-400 mb-2">Removed Variables:</p>
                  <div className="flex flex-wrap gap-2">
                    {store.upgradeInfo?.removedVariables?.map((v) => (
                      <span key={v} className="inline-flex items-center rounded-full bg-amber-100 px-3 py-1 text-xs font-medium text-amber-800 dark:bg-amber-800 dark:text-amber-200">
                        {v}
                      </span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Variables */}
          {store.targetStack && store.targetStack.variables.length > 0 && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Environment Variables
              </h2>
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-6">
                Review and update the environment variables for this upgrade. Required fields are marked with *.
              </p>

              {(() => {
                const groups = groupVariables(store.targetStack.variables);
                return Array.from(groups.entries()).map(([groupName, groupVars]) => (
                  <div key={groupName} className="mb-6 last:mb-0">
                    {groups.size > 1 && (
                      <h3 className="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-4 pb-2 border-b border-gray-200 dark:border-gray-700">
                        {groupName}
                      </h3>
                    )}
                    <div className="space-y-4">
                      {groupVars.map((v) => {
                        const isNew = store.upgradeInfo?.newVariables?.includes(v.name);
                        return (
                          <div key={v.name} className={isNew ? 'ring-2 ring-blue-300 dark:ring-blue-600 rounded-lg p-3 -m-3' : ''}>
                            {isNew && (
                              <span className="inline-block mb-2 text-xs font-medium text-blue-600 dark:text-blue-400 bg-blue-100 dark:bg-blue-900/50 px-2 py-0.5 rounded">
                                New in this version
                              </span>
                            )}
                            <VariableInput
                              variable={v}
                              value={store.variableValues[v.name] || ''}
                              onChange={(newValue) => store.setVariableValue(v.name, newValue)}
                            />
                          </div>
                        );
                      })}
                    </div>
                  </div>
                ));
              })()}
            </div>
          )}
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          {/* Import & Upgrade Actions */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            {/* Import .env Button */}
            {store.targetStack && store.targetStack.variables.length > 0 && (
              <>
                <input
                  ref={envFileInputRef}
                  type="file"
                  onChange={handleEnvFileImport}
                  className="hidden"
                  id="env-file-input"
                />
                <label
                  htmlFor="env-file-input"
                  className="w-full inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 cursor-pointer hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600 mb-3"
                >
                  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
                  </svg>
                  Import .env
                </label>
              </>
            )}

            {/* Upgrade Button */}
            <button
              onClick={store.handleUpgrade}
              disabled={!activeEnvironment}
              className="w-full inline-flex items-center justify-center gap-2 rounded-md bg-blue-600 px-6 py-3 text-center font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16V4m0 0L3 8m4-4l4 4m6 0v12m0 0l4-4m-4 4l-4-4" />
              </svg>
              Upgrade to {store.selectedVersion || store.upgradeInfo?.latestVersion}
            </button>
            <p className="mt-2 text-xs text-center text-gray-500 dark:text-gray-400">
              This will start the upgrade process
            </p>
          </div>

          {/* Upgrade Info */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Upgrade Info
            </h2>

            <div className="space-y-3">
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Current Version</span>
                <span className="font-medium text-gray-900 dark:text-white">
                  {store.upgradeInfo?.currentVersion}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Target Version</span>
                <span className="font-medium text-blue-600 dark:text-blue-400">
                  {store.selectedVersion || store.upgradeInfo?.latestVersion}
                </span>
              </div>
              {store.targetStack && (
                <>
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-500 dark:text-gray-400">Services</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {store.targetStack.services.length}
                    </span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-500 dark:text-gray-400">Variables</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {store.targetStack.variables.length}
                    </span>
                  </div>
                </>
              )}
            </div>

            {store.targetStack && store.targetStack.services.length > 0 && (
              <div className="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
                <p className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
                  Services
                </p>
                <div className="space-y-1">
                  {store.targetStack.services.map((service) => (
                    <div
                      key={service.name}
                      className="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300"
                    >
                      <svg className="w-3 h-3 text-green-500" fill="currentColor" viewBox="0 0 8 8">
                        <circle cx="4" cy="4" r="3" />
                      </svg>
                      {service.name}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>

          {/* Cancel Button */}
          <button
            onClick={() => navigate(getBackUrl())}
            className="w-full inline-flex items-center justify-center gap-2 rounded-md border border-gray-300 bg-white px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}

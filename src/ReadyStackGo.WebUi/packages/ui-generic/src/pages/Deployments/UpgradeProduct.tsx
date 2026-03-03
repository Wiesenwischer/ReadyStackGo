import { useEffect, useRef } from 'react';
import { useParams, Link, useSearchParams } from 'react-router';
import { useUpgradeProductStore } from '@rsgo/core';
import { useAuth } from '../../context/AuthContext';
import { useEnvironment } from '../../context/EnvironmentContext';
import VariableInput, { groupVariables } from '../../components/variables/VariableInput';

export default function UpgradeProduct() {
  const { productDeploymentId } = useParams<{ productDeploymentId: string }>();
  const [searchParams] = useSearchParams();
  const { token } = useAuth();
  const { activeEnvironment } = useEnvironment();
  // Get optional target version from URL params
  const targetVersionParam = searchParams.get('version');

  const store = useUpgradeProductStore(token, activeEnvironment?.id, productDeploymentId, targetVersionParam);

  const logEndRef = useRef<HTMLDivElement>(null);
  const envFileInputRef = useRef<HTMLInputElement>(null);

  // Auto-scroll init container logs
  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [store.initContainerLogs]);

  // Handle .env file import (reads file, then delegates to store)
  const handleEnvFileImport = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file && store.targetProduct) {
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

  // ─── Loading State ────────────────────────────────────────────────────────

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

  // ─── Error State (no product loaded) ──────────────────────────────────────

  if (store.state === 'error' && !store.targetProduct) {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6">
          <Link
            to="/deployments"
            className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            Back to Deployments
          </Link>
        </div>
        <div className="rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm text-red-800 dark:text-red-200">{store.error}</p>
        </div>
      </div>
    );
  }

  // ─── Success State ────────────────────────────────────────────────────────

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
              Product Upgraded Successfully!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-2">
              {store.productDeployment?.productName} has been upgraded to v{store.selectedVersion || store.upgradeInfo?.latestVersion}
            </p>
            {store.productDeployment?.productVersion && (
              <p className="text-sm text-gray-500 dark:text-gray-400 mb-6">
                Previous version: v{store.productDeployment.productVersion}
              </p>
            )}

            {/* Stack Results */}
            {store.stackResults.length > 0 && (
              <div className="w-full max-w-lg mb-6">
                <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
                  Stack Results
                </h3>
                <div className="space-y-2">
                  {store.stackResults.map((result) => (
                    <div
                      key={result.stackName}
                      className="flex items-center justify-between p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50"
                    >
                      <div className="flex items-center gap-2">
                        <svg className="w-4 h-4 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                        </svg>
                        <span className="text-sm font-medium text-gray-900 dark:text-white">
                          {result.stackDisplayName}
                        </span>
                        {result.isNewInUpgrade && (
                          <span className="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900/30 dark:text-green-300">
                            New
                          </span>
                        )}
                      </div>
                      <span className="text-xs text-gray-500 dark:text-gray-400">
                        {result.serviceCount} service{result.serviceCount !== 1 ? 's' : ''}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            <div className="flex gap-4">
              <Link
                to="/deployments"
                className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
              >
                View Deployments
              </Link>
              <Link
                to={store.getBackUrl()}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                Back to Product
              </Link>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // ─── Error State (after upgrade) ───────────────────────────────────────

  if (store.state === 'error' && store.targetProduct) {
    const successCount = store.stackResults.filter(r => r.success).length;
    const failedCount = store.stackResults.filter(r => !r.success).length;

    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="flex items-center justify-center w-16 h-16 mb-6 bg-red-100 rounded-full dark:bg-red-900/30">
              <svg className="w-8 h-8 text-red-600 dark:text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Upgrade {failedCount > 0 && successCount > 0 ? 'Partially' : ''} Failed
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-2">
              {store.productDeployment?.productName} — v{store.productDeployment?.productVersion} to v{store.selectedVersion || store.upgradeInfo?.latestVersion}
            </p>
            <p className="text-sm text-red-600 dark:text-red-400 mb-6 max-w-md text-center">
              {store.error}
            </p>

            {/* Stack Results */}
            {store.stackResults.length > 0 && (
              <div className="w-full max-w-lg mb-6">
                <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
                  Stack Results ({successCount} succeeded, {failedCount} failed)
                </h3>
                <div className="space-y-2">
                  {store.stackResults.map((result) => (
                    <div
                      key={result.stackName}
                      className={`flex items-center justify-between p-3 rounded-lg ${
                        result.success
                          ? 'bg-green-50 dark:bg-green-900/10'
                          : 'bg-red-50 dark:bg-red-900/10'
                      }`}
                    >
                      <div className="flex items-center gap-2">
                        {result.success ? (
                          <svg className="w-4 h-4 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                          </svg>
                        ) : (
                          <svg className="w-4 h-4 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                          </svg>
                        )}
                        <span className="text-sm font-medium text-gray-900 dark:text-white">
                          {result.stackDisplayName}
                        </span>
                        {result.isNewInUpgrade && (
                          <span className="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900/30 dark:text-green-300">
                            New
                          </span>
                        )}
                      </div>
                      {result.errorMessage && (
                        <span className="text-xs text-red-600 dark:text-red-400 max-w-xs truncate">
                          {result.errorMessage}
                        </span>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            <div className="flex gap-4">
              <Link
                to="/deployments"
                className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
              >
                View Deployments
              </Link>
              <Link
                to={store.getBackUrl()}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                Back to Product
              </Link>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // ─── Upgrading State ──────────────────────────────────────────────────────

  if (store.state === 'upgrading') {
    const totalStacks = store.targetProduct?.stacks.length || 0;
    const completedCount = Object.values(store.stackStatuses).filter(s => s === 'running').length;
    const failedCount = Object.values(store.stackStatuses).filter(s => s === 'failed').length;
    const overallPercent = totalStacks > 0
      ? Math.round(((completedCount + failedCount) / totalStacks) * 100)
      : store.progressUpdate?.percentComplete ?? 0;

    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="w-16 h-16 mb-6 border-4 border-brand-600 border-t-transparent rounded-full animate-spin"></div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Upgrading Product...
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Upgrading {store.productDeployment?.productName} from v{store.productDeployment?.productVersion} to v{store.selectedVersion || store.upgradeInfo?.latestVersion}
            </p>

            {/* Overall Progress */}
            <div className="w-full max-w-lg">
              <div className="mb-4">
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-gray-600 dark:text-gray-400">
                    {store.progressUpdate?.phase === 'ProductDeploy'
                      ? store.progressUpdate.message || 'Upgrading stacks...'
                      : store.currentUpgradingStack
                        ? `Upgrading: ${store.currentUpgradingStack}`
                        : store.formattedPhase || 'Initializing'}
                  </span>
                  <span className="text-gray-600 dark:text-gray-400">
                    {completedCount}/{totalStacks} stacks
                  </span>
                </div>
                <div className="h-3 bg-gray-200 rounded-full dark:bg-gray-700 overflow-hidden">
                  <div
                    className="h-full bg-brand-600 rounded-full transition-all duration-500 ease-out"
                    style={{ width: `${overallPercent}%` }}
                  />
                </div>
              </div>

              {/* Stack Status List */}
              {store.targetProduct && (
                <div className="mt-4 space-y-2">
                  {store.targetProduct.stacks.map((stack) => {
                    const status = store.stackStatuses[stack.name] || 'pending';
                    const isNew = store.isNewStack(stack.name);
                    return (
                      <div
                        key={stack.id}
                        className="flex items-center justify-between p-2 rounded-lg bg-gray-50 dark:bg-gray-800/50"
                      >
                        <div className="flex items-center gap-2">
                          {status === 'pending' && (
                            <span className="w-4 h-4 rounded-full border-2 border-gray-300 dark:border-gray-600" />
                          )}
                          {status === 'upgrading' && (
                            <div className="w-4 h-4 border-2 border-brand-600 border-t-transparent rounded-full animate-spin" />
                          )}
                          {status === 'running' && (
                            <svg className="w-4 h-4 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                            </svg>
                          )}
                          {status === 'failed' && (
                            <svg className="w-4 h-4 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                            </svg>
                          )}
                          <span className={`text-sm ${
                            status === 'upgrading' ? 'font-medium text-brand-600 dark:text-brand-400' :
                            status === 'running' ? 'text-green-700 dark:text-green-400' :
                            status === 'failed' ? 'text-red-700 dark:text-red-400' :
                            'text-gray-600 dark:text-gray-400'
                          }`}>
                            {stack.name}
                          </span>
                          {isNew && (
                            <span className="inline-flex items-center rounded-full bg-green-100 px-1.5 py-0.5 text-xs font-medium text-green-700 dark:bg-green-900/30 dark:text-green-300">
                              New
                            </span>
                          )}
                        </div>
                        <span className={`text-xs px-2 py-0.5 rounded-full ${
                          status === 'pending' ? 'bg-gray-100 text-gray-500 dark:bg-gray-700 dark:text-gray-400' :
                          status === 'upgrading' ? 'bg-brand-100 text-brand-700 dark:bg-brand-900/30 dark:text-brand-300' :
                          status === 'running' ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300' :
                          'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300'
                        }`}>
                          {status === 'pending' ? 'Pending' :
                           status === 'upgrading' ? 'Upgrading' :
                           status === 'running' ? 'Running' : 'Failed'}
                        </span>
                      </div>
                    );
                  })}
                </div>
              )}

              {/* Current Stack Detail Progress */}
              {store.progressUpdate && store.progressUpdate.phase !== 'ProductDeploy' && (
                <div className="mt-4 p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50">
                  <p className="text-xs text-gray-500 dark:text-gray-400 mb-1">
                    {store.formattedPhase}
                  </p>
                  <p className="text-sm text-gray-700 dark:text-gray-300">
                    {store.progressUpdate.message}
                  </p>
                  {(store.progressUpdate.totalServices > 0 || store.progressUpdate.totalInitContainers > 0) && (
                    <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                      {store.progressUpdate.phase === 'PullingImages'
                        ? `Images: ${store.progressUpdate.completedServices} / ${store.progressUpdate.totalServices}`
                        : store.progressUpdate.phase === 'InitializingContainers'
                          ? `Init Containers: ${store.progressUpdate.completedInitContainers} / ${store.progressUpdate.totalInitContainers}`
                          : `Services: ${store.progressUpdate.completedServices} / ${store.progressUpdate.totalServices}`
                      }
                    </p>
                  )}
                </div>
              )}

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

            {/* Init Container Logs */}
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

  // ─── Configure State ──────────────────────────────────────────────────────

  return (
    <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6">
        <Link
          to={store.getBackUrl()}
          className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back
        </Link>
      </div>

      {/* Header */}
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
          Upgrade {store.productDeployment?.productName}
        </h1>
        <p className="text-gray-600 dark:text-gray-400">
          Upgrade from version <span className="font-medium">{store.productDeployment?.productVersion}</span> to{' '}
          <span className="font-medium">{store.selectedVersion || store.upgradeInfo?.latestVersion}</span> on{' '}
          <span className="font-medium">{activeEnvironment?.name}</span>
        </p>
      </div>

      {/* Error Display */}
      {store.error && (
        <div className="mb-6 p-4 text-sm text-red-800 bg-red-100 rounded-lg dark:bg-red-900/30 dark:text-red-400">
          <p className="font-medium mb-1">Error</p>
          <p>{store.error}</p>
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
                      {v.version}{v.version === store.upgradeInfo?.latestVersion ? ' (latest)' : ''} — {v.stackCount} stack{v.stackCount !== 1 ? 's' : ''}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          )}

          {/* Configuration Changes Info */}
          {((store.upgradeInfo?.newStacks?.length ?? 0) > 0 || (store.upgradeInfo?.removedStacks?.length ?? 0) > 0) && (
            <div className="rounded-2xl border border-blue-200 bg-blue-50 p-6 dark:border-blue-800 dark:bg-blue-900/20">
              <h2 className="text-lg font-semibold text-blue-900 dark:text-blue-100 mb-4">
                Stack Changes
              </h2>
              {(store.upgradeInfo?.newStacks?.length ?? 0) > 0 && (
                <div className="mb-3">
                  <p className="text-sm font-medium text-blue-800 dark:text-blue-200 mb-2">New Stacks:</p>
                  <div className="flex flex-wrap gap-2">
                    {store.upgradeInfo?.newStacks?.map((s) => (
                      <span key={s} className="inline-flex items-center rounded-full bg-green-100 px-3 py-1 text-xs font-medium text-green-800 dark:bg-green-800 dark:text-green-200">
                        {s}
                      </span>
                    ))}
                  </div>
                </div>
              )}
              {(store.upgradeInfo?.removedStacks?.length ?? 0) > 0 && (
                <div>
                  <p className="text-sm font-medium text-amber-700 dark:text-amber-400 mb-2">Stacks no longer in target version:</p>
                  <div className="flex flex-wrap gap-2">
                    {store.upgradeInfo?.removedStacks?.map((s) => (
                      <span key={s} className="inline-flex items-center rounded-full bg-amber-100 px-3 py-1 text-xs font-medium text-amber-800 dark:bg-amber-800 dark:text-amber-200">
                        {s}
                      </span>
                    ))}
                  </div>
                  <p className="text-xs text-amber-600 dark:text-amber-400 mt-2">
                    These stacks will not be automatically removed. You can remove them manually after the upgrade.
                  </p>
                </div>
              )}
            </div>
          )}

          {/* Upgrade Configuration */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Upgrade Configuration
            </h2>

            {/* Continue on Error */}
            <div className="flex items-center gap-3">
              <input
                type="checkbox"
                id="continueOnError"
                checked={store.continueOnError}
                onChange={(e) => store.setContinueOnError(e.target.checked)}
                className="h-4 w-4 rounded border-gray-300 text-brand-600 focus:ring-brand-500"
              />
              <label htmlFor="continueOnError" className="text-sm text-gray-700 dark:text-gray-300">
                Continue upgrading remaining stacks if one fails
              </label>
            </div>
          </div>

          {/* Shared Variables */}
          {store.sharedVars.length > 0 && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">
                Shared Variables
              </h2>
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-6">
                These variables are used by multiple stacks. Current values are preserved where applicable.
              </p>

              {(() => {
                const groups = groupVariables(store.sharedVars);
                return Array.from(groups.entries()).map(([groupName, groupVars]) => (
                  <div key={groupName} className="mb-6 last:mb-0">
                    {groups.size > 1 && (
                      <h3 className="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-4 pb-2 border-b border-gray-200 dark:border-gray-700">
                        {groupName}
                      </h3>
                    )}
                    <div className="space-y-4">
                      {groupVars.map((v) => (
                        <VariableInput
                          key={v.name}
                          variable={v}
                          value={store.sharedVariableValues[v.name] || ''}
                          onChange={(newValue) =>
                            store.setSharedVariableValue(v.name, newValue)
                          }
                        />
                      ))}
                    </div>
                  </div>
                ));
              })()}
            </div>
          )}

          {/* Per-Stack Configuration (Accordion) */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">
              Stack Configuration
            </h2>
            <p className="text-sm text-gray-600 dark:text-gray-400 mb-6">
              Review and adjust stack-specific variables for the upgrade.
            </p>

            <div className="space-y-3">
              {store.targetProduct?.stacks.map((stack) => {
                const isExpanded = store.expandedStacks.has(stack.id);
                const stackSpecific = store.getStackSpecificVariables(stack);
                const isNew = store.isNewStack(stack.name);
                const hasRequiredMissing = stackSpecific.some(v =>
                  v.isRequired && !store.perStackVariableValues[stack.id]?.[v.name]
                );

                return (
                  <div
                    key={stack.id}
                    className={`border rounded-lg overflow-hidden ${
                      isNew
                        ? 'border-green-300 dark:border-green-700'
                        : 'border-gray-200 dark:border-gray-700'
                    }`}
                  >
                    {/* Accordion Header */}
                    <button
                      onClick={() => store.toggleStackExpanded(stack.id)}
                      className="w-full flex items-center justify-between p-4 text-left hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors"
                    >
                      <div className="flex items-center gap-3">
                        <svg
                          className={`w-4 h-4 text-gray-400 transition-transform ${isExpanded ? 'rotate-90' : ''}`}
                          fill="none"
                          stroke="currentColor"
                          viewBox="0 0 24 24"
                        >
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                        </svg>
                        <span className="font-medium text-gray-900 dark:text-white">
                          {stack.name}
                        </span>
                        {isNew ? (
                          <span className="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900/30 dark:text-green-300">
                            New Stack
                          </span>
                        ) : (
                          <span className="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-600 dark:bg-gray-700 dark:text-gray-400">
                            Existing
                          </span>
                        )}
                        <span className="inline-flex items-center rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-800 dark:bg-blue-900/30 dark:text-blue-300">
                          {stack.services.length} service{stack.services.length !== 1 ? 's' : ''}
                        </span>
                        {stackSpecific.length > 0 && (
                          <span className="inline-flex items-center rounded-full bg-purple-100 px-2 py-0.5 text-xs font-medium text-purple-800 dark:bg-purple-900/30 dark:text-purple-300">
                            {stackSpecific.length} variable{stackSpecific.length !== 1 ? 's' : ''}
                          </span>
                        )}
                        {hasRequiredMissing && (
                          <span className="inline-flex items-center rounded-full bg-red-100 px-2 py-0.5 text-xs font-medium text-red-800 dark:bg-red-900/30 dark:text-red-300">
                            required
                          </span>
                        )}
                      </div>
                    </button>

                    {/* Accordion Content */}
                    {isExpanded && (
                      <div className="border-t border-gray-200 dark:border-gray-700 p-4 space-y-4">
                        {/* Stack-Specific Variables */}
                        {stackSpecific.length > 0 && (
                          <div>
                            {(() => {
                              const groups = groupVariables(stackSpecific);
                              return Array.from(groups.entries()).map(([groupName, groupVars]) => (
                                <div key={groupName} className="mb-4 last:mb-0">
                                  {groups.size > 1 && (
                                    <h4 className="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3 pb-2 border-b border-gray-200 dark:border-gray-700">
                                      {groupName}
                                    </h4>
                                  )}
                                  <div className="space-y-4">
                                    {groupVars.map((v) => (
                                      <VariableInput
                                        key={v.name}
                                        variable={v}
                                        value={store.perStackVariableValues[stack.id]?.[v.name] || ''}
                                        onChange={(newValue) =>
                                          store.setPerStackVariableValue(stack.id, v.name, newValue)
                                        }
                                      />
                                    ))}
                                  </div>
                                </div>
                              ));
                            })()}
                          </div>
                        )}

                        {stackSpecific.length === 0 && (
                          <p className="text-sm text-gray-500 dark:text-gray-400 italic">
                            No stack-specific variables. All variables are shared.
                          </p>
                        )}

                        {/* Services */}
                        {stack.services.length > 0 && (
                          <div className="pt-3 border-t border-gray-100 dark:border-gray-700">
                            <p className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
                              Services
                            </p>
                            <div className="flex flex-wrap gap-2">
                              {stack.services.map((service) => (
                                <span
                                  key={service}
                                  className="inline-flex items-center gap-1 rounded bg-gray-100 px-2 py-1 text-xs text-gray-700 dark:bg-gray-700 dark:text-gray-300"
                                >
                                  <svg className="w-2.5 h-2.5 text-green-500" fill="currentColor" viewBox="0 0 8 8">
                                    <circle cx="4" cy="4" r="3" />
                                  </svg>
                                  {service}
                                </span>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          {/* Actions */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            {/* Import .env Button */}
            {store.targetProduct && store.targetProduct.totalVariables > 0 && (
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
              Upgrade All Stacks to {store.selectedVersion || store.upgradeInfo?.latestVersion}
            </button>
            <p className="mt-2 text-xs text-center text-gray-500 dark:text-gray-400">
              This will upgrade {store.targetProduct?.stacks.length} stack{(store.targetProduct?.stacks.length || 0) !== 1 ? 's' : ''} sequentially
            </p>
          </div>

          {/* Upgrade Info */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Upgrade Info
            </h2>

            <div className="space-y-3">
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Deployment Name</span>
                <span className="font-medium text-gray-900 dark:text-white font-mono text-xs">
                  {store.productDeployment?.deploymentName || '-'}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Current Version</span>
                <span className="font-medium text-gray-900 dark:text-white">
                  {store.productDeployment?.productVersion}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Target Version</span>
                <span className="font-medium text-blue-600 dark:text-blue-400">
                  {store.selectedVersion || store.upgradeInfo?.latestVersion}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Current Stacks</span>
                <span className="font-medium text-gray-900 dark:text-white">
                  {store.productDeployment?.totalStacks || 0}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Target Stacks</span>
                <span className="font-medium text-gray-900 dark:text-white">
                  {store.targetProduct?.stacks.length || 0}
                </span>
              </div>
              {store.targetProduct && (
                <>
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-500 dark:text-gray-400">Total Services</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {store.targetProduct.totalServices}
                    </span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-500 dark:text-gray-400">Total Variables</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {store.targetProduct.totalVariables}
                    </span>
                  </div>
                </>
              )}
              {store.sharedVars.length > 0 && (
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 dark:text-gray-400">Shared Variables</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {store.sharedVars.length}
                  </span>
                </div>
              )}
              {store.productDeployment && store.productDeployment.upgradeCount > 0 && (
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 dark:text-gray-400">Previous Upgrades</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {store.productDeployment.upgradeCount}
                  </span>
                </div>
              )}
            </div>

            {/* Stack List */}
            {store.targetProduct && store.targetProduct.stacks.length > 0 && (
              <div className="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
                <p className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
                  Target Stacks
                </p>
                <div className="space-y-1">
                  {store.targetProduct.stacks.map((stack) => {
                    const isNew = store.isNewStack(stack.name);
                    return (
                      <div
                        key={stack.id}
                        className="flex items-center justify-between text-sm text-gray-700 dark:text-gray-300"
                      >
                        <div className="flex items-center gap-2">
                          <svg className="w-3 h-3 text-green-500" fill="currentColor" viewBox="0 0 8 8">
                            <circle cx="4" cy="4" r="3" />
                          </svg>
                          {stack.name}
                          {isNew && (
                            <span className="text-xs text-green-600 dark:text-green-400">(new)</span>
                          )}
                        </div>
                        <span className="text-xs text-gray-400">
                          {stack.services.length} svc
                        </span>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>

          {/* Cancel Button */}
          <Link
            to={store.getBackUrl()}
            className="w-full inline-flex items-center justify-center gap-2 rounded-md border border-gray-300 bg-white px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700"
          >
            Cancel
          </Link>
        </div>
      </div>
    </div>
  );
}

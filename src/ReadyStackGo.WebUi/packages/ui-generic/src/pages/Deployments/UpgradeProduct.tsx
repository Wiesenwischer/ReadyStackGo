import { useRef } from 'react';
import { useParams, Link, useSearchParams } from 'react-router';
import { useUpgradeProductStore } from '@rsgo/core';
import { useAuth } from '../../context/AuthContext';
import { useEnvironment } from '../../context/EnvironmentContext';
import VariableInput, { groupVariables } from '../../components/variables/VariableInput';
import { DeploymentProgressPanel } from '../../components/deployments/DeploymentProgressPanel';

export default function UpgradeProduct() {
  const { productDeploymentId } = useParams<{ productDeploymentId: string }>();
  const [searchParams] = useSearchParams();
  const { token } = useAuth();
  const { activeEnvironment } = useEnvironment();
  // Get optional target version from URL params
  const targetVersionParam = searchParams.get('version');

  const store = useUpgradeProductStore(token, activeEnvironment?.id, productDeploymentId, targetVersionParam);

  const envFileInputRef = useRef<HTMLInputElement>(null);

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

    const selectedStatus = store.selectedStack ? (store.stackStatuses[store.selectedStack] || 'pending') : null;
    const selectedProgress = store.selectedStack ? (store.perStackProgress[store.selectedStack] || null) : null;
    const selectedLogs = store.selectedStack ? (store.perStackLogs[store.selectedStack] || {}) : {};

    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
          {/* Header */}
          <div className="flex items-center gap-4 mb-4">
            <div className="w-10 h-10 border-4 border-brand-600 border-t-transparent rounded-full animate-spin flex-shrink-0"></div>
            <div>
              <h1 className="text-xl font-bold text-gray-900 dark:text-white">
                Upgrading Product...
              </h1>
              <p className="text-sm text-gray-600 dark:text-gray-400">
                {store.productDeployment?.productName} — v{store.productDeployment?.productVersion} → v{store.selectedVersion || store.upgradeInfo?.latestVersion} · {completedCount}/{totalStacks} stacks completed
              </p>
            </div>
          </div>

          {/* Overall Progress Bar */}
          <div className="mb-6">
            <div className="h-2 bg-gray-200 rounded-full dark:bg-gray-700 overflow-hidden">
              <div
                className="h-full bg-brand-600 rounded-full transition-all duration-500 ease-out"
                style={{ width: `${overallPercent}%` }}
              />
            </div>
          </div>

          {/* Split View: Stack List (left) + Detail Panel (right) */}
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* LEFT: Stack Overview */}
            <div className="lg:col-span-1">
              <h3 className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
                Stacks
              </h3>
              <div className="space-y-1">
                {store.targetProduct?.stacks.map((stack) => {
                  const status = store.stackStatuses[stack.name] || 'pending';
                  const isSelected = store.selectedStack === stack.name;
                  const isNew = store.isNewStack(stack.name);
                  return (
                    <button
                      key={stack.id}
                      onClick={() => store.handleStackSelect(stack.name)}
                      className={`w-full flex items-center justify-between p-3 rounded-lg text-left transition-colors ${
                        isSelected
                          ? 'bg-brand-50 border-l-4 border-l-brand-600 dark:bg-brand-900/20'
                          : 'bg-gray-50 hover:bg-gray-100 dark:bg-gray-800/50 dark:hover:bg-gray-800'
                      }`}
                    >
                      <div className="flex items-center gap-2">
                        {status === 'pending' && (
                          <span className="w-4 h-4 rounded-full border-2 border-gray-300 dark:border-gray-600 flex-shrink-0" />
                        )}
                        {status === 'removing' && (
                          <div className="w-4 h-4 border-2 border-orange-500 border-t-transparent rounded-full animate-spin flex-shrink-0" />
                        )}
                        {status === 'upgrading' && (
                          <div className="w-4 h-4 border-2 border-brand-600 border-t-transparent rounded-full animate-spin flex-shrink-0" />
                        )}
                        {status === 'running' && (
                          <svg className="w-4 h-4 text-green-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                          </svg>
                        )}
                        {status === 'failed' && (
                          <svg className="w-4 h-4 text-red-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                          </svg>
                        )}
                        <span className={`text-sm ${
                          status === 'removing' ? 'font-medium text-orange-600 dark:text-orange-400' :
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
                      <span className={`text-xs px-2 py-0.5 rounded-full flex-shrink-0 ${
                        status === 'pending' ? 'bg-gray-100 text-gray-500 dark:bg-gray-700 dark:text-gray-400' :
                        status === 'removing' ? 'bg-orange-100 text-orange-700 dark:bg-orange-900/30 dark:text-orange-300' :
                        status === 'upgrading' ? 'bg-brand-100 text-brand-700 dark:bg-brand-900/30 dark:text-brand-300' :
                        status === 'running' ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300' :
                        'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300'
                      }`}>
                        {status === 'pending' ? 'Pending' :
                         status === 'removing' ? 'Removing' :
                         status === 'upgrading' ? 'Upgrading' :
                         status === 'running' ? 'Running' : 'Failed'}
                      </span>
                    </button>
                  );
                })}
              </div>
            </div>

            {/* RIGHT: Detail Panel */}
            <div className="lg:col-span-2">
              {!store.selectedStack && (
                <div className="flex flex-col items-center justify-center h-full py-12 text-gray-400 dark:text-gray-500">
                  <svg className="w-12 h-12 mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
                  </svg>
                  <p className="text-sm">Waiting for upgrade to start...</p>
                </div>
              )}

              {store.selectedStack && selectedStatus === 'pending' && (
                <div className="flex flex-col items-center justify-center h-full py-12 text-gray-400 dark:text-gray-500">
                  <span className="w-12 h-12 rounded-full border-2 border-gray-300 dark:border-gray-600 mb-3" />
                  <p className="text-sm font-medium text-gray-600 dark:text-gray-400">{store.selectedStack}</p>
                  <p className="text-sm">Waiting to upgrade...</p>
                </div>
              )}

              {store.selectedStack && selectedStatus === 'removing' && (
                <div>
                  <h3 className="text-sm font-medium text-gray-900 dark:text-white mb-4">
                    {store.selectedStack}
                    <span className="ml-2 text-xs font-normal text-orange-600 dark:text-orange-400">Removing</span>
                  </h3>
                  <div className="flex items-center gap-3 p-4 rounded-lg bg-orange-50 dark:bg-orange-900/10">
                    <div className="w-5 h-5 border-2 border-orange-500 border-t-transparent rounded-full animate-spin flex-shrink-0" />
                    <p className="text-sm text-orange-700 dark:text-orange-300">
                      Removing existing stack before deploying the new version...
                    </p>
                  </div>
                </div>
              )}

              {store.selectedStack && selectedStatus === 'upgrading' && (
                <div>
                  <h3 className="text-sm font-medium text-gray-900 dark:text-white mb-4">
                    {store.selectedStack}
                    <span className="ml-2 text-xs font-normal text-brand-600 dark:text-brand-400">Upgrading</span>
                  </h3>
                  <DeploymentProgressPanel
                    progressUpdate={selectedProgress}
                    initContainerLogs={selectedLogs}
                    connectionState={store.connectionState}
                    defaultMessage={`Upgrading ${store.selectedStack}...`}
                  />
                </div>
              )}

              {store.selectedStack && selectedStatus === 'running' && (
                <div>
                  <h3 className="text-sm font-medium text-gray-900 dark:text-white mb-4">
                    {store.selectedStack}
                    <span className="ml-2 text-xs font-normal text-green-600 dark:text-green-400">Upgraded</span>
                  </h3>
                  <div className="flex items-center gap-3 mb-4 p-4 rounded-lg bg-green-50 dark:bg-green-900/10">
                    <svg className="w-6 h-6 text-green-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                    </svg>
                    <p className="text-sm text-green-700 dark:text-green-300">
                      Stack upgraded successfully
                    </p>
                  </div>
                  {selectedProgress && (
                    <DeploymentProgressPanel
                      progressUpdate={selectedProgress}
                      initContainerLogs={selectedLogs}
                      connectionState={store.connectionState}
                    />
                  )}
                </div>
              )}

              {store.selectedStack && selectedStatus === 'failed' && (
                <div>
                  <h3 className="text-sm font-medium text-gray-900 dark:text-white mb-4">
                    {store.selectedStack}
                    <span className="ml-2 text-xs font-normal text-red-600 dark:text-red-400">Failed</span>
                  </h3>
                  <div className="flex items-center gap-3 mb-4 p-4 rounded-lg bg-red-50 dark:bg-red-900/10">
                    <svg className="w-6 h-6 text-red-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                    </svg>
                    <p className="text-sm text-red-700 dark:text-red-300">
                      Stack upgrade failed
                    </p>
                  </div>
                  {selectedProgress && (
                    <DeploymentProgressPanel
                      progressUpdate={selectedProgress}
                      initContainerLogs={selectedLogs}
                      connectionState={store.connectionState}
                    />
                  )}
                </div>
              )}
            </div>
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

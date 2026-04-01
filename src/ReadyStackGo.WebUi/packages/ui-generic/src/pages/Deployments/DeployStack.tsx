import { useRef } from 'react';
import { useParams, Link, useNavigate } from 'react-router';
import { useDeployStackStore } from '@rsgo/core';
import { useAuth } from '../../context/AuthContext';
import { useEnvironment } from '../../context/EnvironmentContext';
import VariableInput, { groupVariables } from '../../components/variables/VariableInput';
import { DeploymentProgressPanel } from '../../components/deployments/DeploymentProgressPanel';

export default function DeployStack() {
  const { stackId } = useParams<{ stackId: string }>();
  const { token } = useAuth();
  const { activeEnvironment } = useEnvironment();
  const envFileInputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();

  const store = useDeployStackStore(token, activeEnvironment?.id, stackId);

  // Handle .env file import (file reading is UI concern, parsing delegated to store)
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
    // Reset input so same file can be selected again
    if (envFileInputRef.current) {
      envFileInputRef.current.value = '';
    }
  };

  if (store.state === 'loading') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="flex items-center justify-center py-12">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-600 mx-auto mb-4"></div>
            <p className="text-gray-600 dark:text-gray-400">Loading stack...</p>
          </div>
        </div>
      </div>
    );
  }

  if (store.state === 'error' && !store.stack) {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6">
          <Link
            to="/catalog"
            className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            Back to Catalog
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
              Stack Deployed Successfully!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              {store.stackName} has been deployed to {activeEnvironment?.name}
            </p>

            {store.deployWarnings.length > 0 && (
              <div className="w-full max-w-md mb-6 p-4 text-sm text-yellow-800 bg-yellow-100 rounded-lg dark:bg-yellow-900/30 dark:text-yellow-400">
                <p className="font-medium mb-2">Warnings:</p>
                <ul className="ml-4 list-disc">
                  {store.deployWarnings.map((w, i) => (
                    <li key={i}>{w}</li>
                  ))}
                </ul>
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
                to={store.backUrl}
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

  if (store.state === 'deploying') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="w-16 h-16 mb-6 border-4 border-brand-600 border-t-transparent rounded-full animate-spin"></div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Deploying Stack...
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Deploying {store.stackName} to {activeEnvironment?.name}
            </p>

            {/* Progress Section */}
            <div className="w-full max-w-md">
              <DeploymentProgressPanel
                progressUpdate={store.progressUpdate}
                initContainerLogs={store.initContainerLogs}
                connectionState={store.connectionState}
              />
            </div>
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
          to={store.backUrl}
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
          {store.isCustomDeploy ? 'Deploy Custom Stack' : `Deploy ${store.stack?.name}`}
        </h1>
        <p className="text-gray-600 dark:text-gray-400">
          {store.isCustomDeploy
            ? 'Deploy a custom docker-compose stack'
            : `Configure and deploy this stack to `}
          {!store.isCustomDeploy && <span className="font-medium">{activeEnvironment?.name}</span>}
        </p>
      </div>

      {/* Error Display - Directly under header for visibility */}
      {store.error && (
        <div className="mb-6 p-4 text-sm text-red-800 bg-red-100 rounded-lg dark:bg-red-900/30 dark:text-red-400">
          <p className="font-medium mb-1">Error</p>
          <p>{store.error}</p>
        </div>
      )}

      {!activeEnvironment && (
        <div className="mb-6 rounded-md bg-yellow-50 p-4 dark:bg-yellow-900/20">
          <p className="text-sm text-yellow-800 dark:text-yellow-200">
            No environment selected. Please select an environment to deploy.
          </p>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Main Configuration */}
        <div className="lg:col-span-2 space-y-6">
          {/* Stack Name & Version */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Stack Configuration
            </h2>

            {/* Version Selection (only if multiple versions available) */}
            {!store.isCustomDeploy && store.product?.availableVersions && store.product.availableVersions.length > 1 && (
              <div className="mb-4">
                <label className="block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                  Version
                </label>
                <select
                  value={store.stack?.version || ''}
                  onChange={(e) => {
                    const version = store.product!.availableVersions?.find(v => v.version === e.target.value);
                    if (version) store.handleVersionChange(version);
                  }}
                  className="w-full px-4 py-3 border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                >
                  {store.product.availableVersions.map((v) => (
                    <option key={v.productId} value={v.version}>
                      {v.version}{v.isCurrent ? ' (current)' : ''}
                    </option>
                  ))}
                </select>
                <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                  {store.product.availableVersions.length} versions available
                </p>
              </div>
            )}

            <div>
              <label className="block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                Stack Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={store.stackName}
                onChange={(e) => store.setStackName(e.target.value)}
                className="w-full px-4 py-3 border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                placeholder="my-stack"
              />
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                This name will be used to identify the deployment
              </p>
            </div>
          </div>

          {/* Custom YAML Input (for custom deploy) */}
          {store.isCustomDeploy && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Docker Compose YAML
              </h2>
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
                Paste your docker-compose.yml content below.
              </p>
              <textarea
                value={store.yamlContent}
                onChange={(e) => store.setYamlContent(e.target.value)}
                rows={15}
                className="w-full px-4 py-3 font-mono text-sm border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                placeholder={`version: "3.8"
services:
  web:
    image: nginx:alpine
    ports:
      - "80:80"`}
              />
            </div>
          )}

          {/* Variables (for stack deploy) */}
          {!store.isCustomDeploy && store.stack && store.stack.variables.length > 0 && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Environment Variables
              </h2>
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-6">
                Configure the environment variables for this deployment. Required fields are marked with *.
              </p>

              {/* Render variables grouped */}
              {(() => {
                const groups = groupVariables(store.stack.variables);
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
                          value={store.variableValues[v.name] || ''}
                          onChange={(newValue) => store.setVariableValue(v.name, newValue)}
                        />
                      ))}
                    </div>
                  </div>
                ));
              })()}
            </div>
          )}

          </div>

        {/* Sidebar */}
        <div className="space-y-6">
          {/* Import & Deploy Actions - First for better UX */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            {/* Import .env Button (only for stack deploy with variables) */}
            {!store.isCustomDeploy && store.stack && store.stack.variables.length > 0 && (
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

                {/* Reset to Defaults Button */}
                <button
                  onClick={store.handleResetToDefaults}
                  className="w-full inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600 mb-3"
                >
                  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                  </svg>
                  Reset to Defaults
                </button>
              </>
            )}

            {/* Run Precheck Button */}
            {!store.isCustomDeploy && store.selectedStackId && (
              <button
                onClick={() => navigate(`/deploy/${stackId}/precheck`, {
                  state: {
                    environmentId: activeEnvironment?.id,
                    stackId: store.selectedStackId,
                    stackName: store.stackName,
                    variableValues: store.variableValues,
                  }
                })}
                disabled={!activeEnvironment || !store.stackName.trim()}
                className="w-full inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600 mb-3"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                </svg>
                Run Precheck
              </button>
            )}

            {/* Deploy Button */}
            <button
              onClick={store.handleDeploy}
              disabled={!activeEnvironment || !store.stackName.trim() || (!store.isCustomDeploy && !store.selectedStackId) || (store.isCustomDeploy && !store.yamlContent.trim())}
              className="w-full inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
              </svg>
              Deploy to {activeEnvironment?.name || 'Environment'}
            </button>
            <p className="mt-2 text-xs text-center text-gray-500 dark:text-gray-400">
              This will start the deployment process
            </p>
          </div>

          {/* Stack Info (only for stack deploy) */}
          {!store.isCustomDeploy && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Stack Info
              </h2>

              {store.stack?.description && (
                <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
                  {store.stack.description}
                </p>
              )}

              <div className="space-y-3">
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 dark:text-gray-400">Services</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {store.stack?.services.length || 0}
                  </span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 dark:text-gray-400">Variables</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {store.stack?.variables.length || 0}
                  </span>
                </div>
                {store.stack?.version && (
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-500 dark:text-gray-400">Version</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {store.stack.version}
                    </span>
                  </div>
                )}
              </div>

              {store.stack && store.stack.services.length > 0 && (
                <div className="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
                  <p className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
                    Services
                  </p>
                  <div className="space-y-1">
                    {store.stack.services.map((service) => (
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
          )}

          {/* Custom Deploy Info */}
          {store.isCustomDeploy && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Custom Deployment
              </h2>
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
                Deploy any docker-compose configuration by pasting the YAML content.
              </p>
              <div className="text-sm text-gray-500 dark:text-gray-400 space-y-2">
                <p>Requirements:</p>
                <ul className="list-disc list-inside space-y-1 ml-2">
                  <li>Valid docker-compose YAML</li>
                  <li>Unique stack name</li>
                  <li>Images must be accessible</li>
                </ul>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

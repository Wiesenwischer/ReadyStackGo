import { useEffect, useRef } from 'react';
import { useParams, useLocation, Link } from 'react-router';
import { useProductPrecheck } from '@rsgo/core';
import type { ProductPrecheckStackConfig } from '@rsgo/core';
import { useAuth } from '../../context/AuthContext';
import { ProductPrecheckPanel } from '../../components/deployments/ProductPrecheckPanel';

interface PrecheckLocationState {
  environmentId: string;
  productId: string;
  deploymentName: string;
  stackConfigs: ProductPrecheckStackConfig[];
  sharedVariables: Record<string, string>;
}

export default function ProductPrecheckPage() {
  const { productId } = useParams<{ productId: string }>();
  const location = useLocation();
  const { token } = useAuth();
  const precheck = useProductPrecheck();
  const hasRun = useRef(false);

  const state = location.state as PrecheckLocationState | null;

  // Auto-run precheck on mount (user explicitly navigated here)
  useEffect(() => {
    if (state && !hasRun.current && token) {
      hasRun.current = true;
      precheck.runProductPrecheckCheck(
        state.environmentId,
        state.productId,
        state.deploymentName,
        state.stackConfigs,
        state.sharedVariables
      );
    }
  }, [state, token]);

  const handleRecheck = () => {
    if (state) {
      precheck.runProductPrecheckCheck(
        state.environmentId,
        state.productId,
        state.deploymentName,
        state.stackConfigs,
        state.sharedVariables
      );
    }
  };

  // No state — user navigated directly or refreshed
  if (!state) {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8 text-center">
            <svg className="w-12 h-12 text-gray-400 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
            </svg>
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">
              Precheck Configuration Expired
            </h2>
            <p className="text-sm text-gray-600 dark:text-gray-400 mb-6">
              Please go back to the deploy page and run the precheck from there.
            </p>
            <Link
              to={`/deploy-product/${productId}`}
              className="inline-flex items-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
            >
              Back to Configure
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6">
        <Link
          to={`/deploy-product/${productId}`}
          state={{
            restoreDeploymentName: state.deploymentName,
            restoreSharedVariables: state.sharedVariables,
            restoreStackConfigs: state.stackConfigs,
          }}
          className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back to Configure
        </Link>
      </div>

      {/* Loading State */}
      {precheck.precheckState === 'checking' && (
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex items-center gap-3">
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-brand-600"></div>
            <div>
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white">
                Running Deployment Precheck
              </h2>
              <p className="text-sm text-gray-600 dark:text-gray-400">
                Checking infrastructure for <span className="font-medium">{state.deploymentName}</span>...
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Error State */}
      {precheck.precheckState === 'error' && (
        <div className="rounded-md bg-yellow-50 p-4 dark:bg-yellow-900/20 mb-6">
          <p className="text-sm text-yellow-800 dark:text-yellow-200">
            Precheck failed: {precheck.precheckError}
          </p>
        </div>
      )}

      {/* Results */}
      {precheck.precheckResult && (
        <ProductPrecheckPanel
          result={precheck.precheckResult}
          isLoading={precheck.precheckState === 'checking'}
          onRecheck={handleRecheck}
        />
      )}
    </div>
  );
}

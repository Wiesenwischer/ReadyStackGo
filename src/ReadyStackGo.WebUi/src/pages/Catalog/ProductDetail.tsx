import { useEffect, useState, useCallback } from "react";
import { useParams, Link, useNavigate } from "react-router";
import { getProduct, type Product, type ProductStack } from "../../api/stacks";
import {
  getProductDeploymentByProduct,
  checkProductUpgrade,
  type GetProductDeploymentResponse,
} from "../../api/deployments";
import { useEnvironment } from "../../context/EnvironmentContext";

export default function ProductDetail() {
  const { productId } = useParams<{ productId: string }>();
  const { activeEnvironment } = useEnvironment();

  const navigate = useNavigate();
  const [product, setProduct] = useState<Product | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Product deployment status
  const [productDeployment, setProductDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [upgradeAvailable, setUpgradeAvailable] = useState(false);

  const loadProduct = useCallback(async () => {
    if (!productId) {
      setError("No product ID provided");
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const data = await getProduct(productId);
      setProduct(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load product");
    } finally {
      setLoading(false);
    }
  }, [productId]);

  useEffect(() => {
    loadProduct();
  }, [loadProduct]);

  // Check for active product deployment and upgrade availability
  useEffect(() => {
    if (!product || !activeEnvironment) {
      setProductDeployment(null);
      setUpgradeAvailable(false);
      return;
    }

    const checkDeploymentStatus = async () => {
      try {
        const deployment = await getProductDeploymentByProduct(
          activeEnvironment.id,
          product.groupId
        );
        setProductDeployment(deployment);

        // Check if upgrade is available
        if (deployment.canUpgrade) {
          try {
            const upgradeCheck = await checkProductUpgrade(
              activeEnvironment.id,
              deployment.productDeploymentId
            );
            setUpgradeAvailable(upgradeCheck.upgradeAvailable && upgradeCheck.canUpgrade);
          } catch {
            setUpgradeAvailable(false);
          }
        }
      } catch {
        // No active deployment for this product — that's fine
        setProductDeployment(null);
        setUpgradeAvailable(false);
      }
    };

    checkDeploymentStatus();
  }, [product, activeEnvironment]);

  const handleDeployAll = () => {
    if (product) {
      navigate(`/deploy-product/${encodeURIComponent(product.id)}`);
    }
  };

  const handleUpgradeAll = () => {
    if (productDeployment) {
      navigate(`/upgrade-product/${productDeployment.productDeploymentId}`);
    }
  };

  const handleRemoveAll = () => {
    if (productDeployment) {
      navigate(`/remove-product/${productDeployment.productDeploymentId}`);
    }
  };

  if (loading) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="flex items-center justify-center py-12">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-600 mx-auto mb-4"></div>
            <p className="text-gray-600 dark:text-gray-400">Loading product...</p>
          </div>
        </div>
      </div>
    );
  }

  if (error || !product) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
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
          <p className="text-sm text-red-800 dark:text-red-200">{error || "Product not found"}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
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

      {/* Product Header */}
      <div className="mb-8 rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex-1">
            <div className="flex items-center gap-3 mb-2">
              <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
                {product.name}
              </h1>
              {product.version && (
                <span className="inline-flex items-center rounded-full bg-gray-100 px-3 py-1 text-sm font-medium text-gray-700 dark:bg-gray-700 dark:text-gray-300">
                  v{product.version}
                  {product.availableVersions && product.availableVersions.length > 1 && (
                    <span className="ml-1 text-xs text-gray-500 dark:text-gray-400">
                      ({product.availableVersions.length} versions)
                    </span>
                  )}
                </span>
              )}
            </div>

            {product.description && (
              <p className="mb-4 text-gray-600 dark:text-gray-400">
                {product.description}
              </p>
            )}

            <div className="flex flex-wrap gap-2">
              {product.category && (
                <span className="inline-flex items-center rounded-full bg-indigo-100 px-3 py-1 text-xs font-medium text-indigo-800 dark:bg-indigo-900/30 dark:text-indigo-300">
                  {product.category}
                </span>
              )}
              <span className="inline-flex items-center rounded-full bg-blue-100 px-3 py-1 text-xs font-medium text-blue-800 dark:bg-blue-900/30 dark:text-blue-300">
                {product.stacks.length} stack{product.stacks.length !== 1 ? 's' : ''}
              </span>
              <span className="inline-flex items-center rounded-full bg-green-100 px-3 py-1 text-xs font-medium text-green-800 dark:bg-green-900/30 dark:text-green-300">
                {product.totalServices} service{product.totalServices !== 1 ? 's' : ''}
              </span>
              {product.totalVariables > 0 && (
                <span className="inline-flex items-center rounded-full bg-purple-100 px-3 py-1 text-xs font-medium text-purple-800 dark:bg-purple-900/30 dark:text-purple-300">
                  {product.totalVariables} variable{product.totalVariables !== 1 ? 's' : ''}
                </span>
              )}
            </div>

            {product.tags && product.tags.length > 0 && (
              <div className="mt-3 flex flex-wrap gap-1">
                {product.tags.map((tag) => (
                  <span
                    key={tag}
                    className="inline-flex items-center rounded bg-gray-100 px-2 py-0.5 text-xs text-gray-600 dark:bg-gray-700 dark:text-gray-400"
                  >
                    #{tag}
                  </span>
                ))}
              </div>
            )}
          </div>

          <div className="flex flex-col gap-2 sm:flex-row">
            {/* Deployed status badge */}
            {productDeployment && (
              <span className={`inline-flex items-center rounded-full px-3 py-1 text-sm font-medium whitespace-nowrap ${
                productDeployment.status === 'Running'
                  ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300'
                  : productDeployment.status === 'PartiallyRunning'
                    ? 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-300'
                    : productDeployment.status === 'Failed'
                      ? 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300'
                      : 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300'
              }`}>
                {productDeployment.status === 'Running' ? 'Deployed' :
                 productDeployment.status === 'PartiallyRunning' ? 'Partially Running' :
                 productDeployment.status}
                {productDeployment.productVersion && ` v${productDeployment.productVersion}`}
              </span>
            )}

            {/* Upgrade button (when deployed + upgrade available) */}
            {upgradeAvailable && productDeployment && (
              <button
                onClick={handleUpgradeAll}
                disabled={!activeEnvironment}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-blue-600 px-6 py-3 text-center font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16V4m0 0L3 8m4-4l4 4m6 0v12m0 0l4-4m-4 4l-4-4" />
                </svg>
                Upgrade All Stacks
              </button>
            )}

            {/* Remove button (when deployed + can remove) */}
            {productDeployment?.canRemove && (
              <button
                onClick={handleRemoveAll}
                disabled={!activeEnvironment}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-red-600 px-6 py-3 text-center font-medium text-white hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                </svg>
                Remove All Stacks
              </button>
            )}

            {/* Deploy button (when not deployed or multi-stack) */}
            {product.stacks.length > 1 && !productDeployment && (
              <button
                onClick={handleDeployAll}
                disabled={!activeEnvironment}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
                </svg>
                Deploy All Stacks
              </button>
            )}
          </div>
        </div>
      </div>

      {!activeEnvironment && (
        <div className="mb-6 rounded-md bg-yellow-50 p-4 dark:bg-yellow-900/20">
          <p className="text-sm text-yellow-800 dark:text-yellow-200">
            No environment selected. Please select an environment to deploy stacks.
          </p>
        </div>
      )}

      {/* Stacks Grid */}
      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-4 py-6 md:px-6 xl:px-7.5">
          <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
            Stacks
          </h2>
          <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">
            {product.stacks.length === 1
              ? "This product contains one deployable stack."
              : `This product contains ${product.stacks.length} deployable stacks.`}
          </p>
        </div>

        <div className="border-t border-gray-200 dark:border-gray-700 p-4">
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {product.stacks.map((stack) => (
              <StackCard
                key={stack.id}
                stack={stack}
                disabled={!activeEnvironment}
              />
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

// Stack Card Component
interface StackCardProps {
  stack: ProductStack;
  disabled: boolean;
}

function StackCard({ stack, disabled }: StackCardProps) {
  const [showServices, setShowServices] = useState(false);
  const [copied, setCopied] = useState(false);

  const handleCopyStackId = async () => {
    try {
      await navigator.clipboard.writeText(stack.id);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      const textArea = document.createElement("textarea");
      textArea.value = stack.id;
      document.body.appendChild(textArea);
      textArea.select();
      document.execCommand("copy");
      document.body.removeChild(textArea);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  // Group variables by group
  const variableGroups = stack.variables.reduce((acc, variable) => {
    const group = variable.group || 'General';
    if (!acc[group]) {
      acc[group] = [];
    }
    acc[group].push(variable);
    return acc;
  }, {} as Record<string, typeof stack.variables>);

  const groupNames = Object.keys(variableGroups);

  return (
    <div className="rounded-lg border border-gray-200 bg-gray-50 dark:border-gray-700 dark:bg-gray-800/50 overflow-hidden">
      {/* Stack Header */}
      <div className="p-4">
        <div className="flex items-start justify-between mb-3">
          <div className="flex-1">
            <h3 className="font-semibold text-gray-900 dark:text-white">
              {stack.name}
            </h3>
            {stack.description && (
              <p className="mt-1 text-sm text-gray-600 dark:text-gray-400 line-clamp-2">
                {stack.description}
              </p>
            )}
          </div>
          {disabled ? (
            <span className="ml-3 rounded bg-gray-300 px-4 py-2 text-sm font-medium text-gray-500 cursor-not-allowed">
              Deploy
            </span>
          ) : (
            <Link
              to={`/deploy/${encodeURIComponent(stack.id)}`}
              className="ml-3 rounded bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
            >
              Deploy
            </Link>
          )}
        </div>

        {/* Stats */}
        <div className="flex flex-wrap gap-2 mb-3">
          <button
            onClick={() => setShowServices(!showServices)}
            className="inline-flex items-center gap-1 rounded bg-blue-100 px-2 py-1 text-xs font-medium text-blue-800 hover:bg-blue-200 dark:bg-blue-900/30 dark:text-blue-300 dark:hover:bg-blue-900/50"
          >
            <svg className={`w-3 h-3 transition-transform ${showServices ? 'rotate-90' : ''}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
            </svg>
            {stack.services.length} service{stack.services.length !== 1 ? 's' : ''}
          </button>
          {stack.variables.length > 0 && (
            <span className="inline-flex items-center rounded bg-purple-100 px-2 py-1 text-xs font-medium text-purple-800 dark:bg-purple-900/30 dark:text-purple-300">
              {stack.variables.length} variable{stack.variables.length !== 1 ? 's' : ''}
            </span>
          )}
        </div>

        {/* Stack ID for CI/CD */}
        <div className="flex items-center gap-1.5">
          <code className="text-xs font-mono text-gray-500 dark:text-gray-400 truncate">
            {stack.id}
          </code>
          <button
            onClick={handleCopyStackId}
            className="flex-shrink-0 p-0.5 rounded text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
            title="Copy Stack ID for CI/CD"
          >
            {copied ? (
              <svg className="w-3.5 h-3.5 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
            ) : (
              <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
              </svg>
            )}
          </button>
        </div>

        {/* Variable Groups */}
        {groupNames.length > 0 && (
          <div className="flex flex-wrap gap-1">
            {groupNames.map((group) => (
              <span
                key={group}
                className="inline-flex items-center rounded bg-gray-200 px-2 py-0.5 text-xs text-gray-600 dark:bg-gray-700 dark:text-gray-400"
              >
                {group} ({variableGroups[group].length})
              </span>
            ))}
          </div>
        )}
      </div>

      {/* Expandable Services List */}
      {showServices && (
        <div className="border-t border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 p-3">
          <p className="text-xs font-medium text-gray-500 dark:text-gray-400 mb-2 uppercase tracking-wider">
            Services
          </p>
          <div className="space-y-1">
            {stack.services.map((service) => (
              <div
                key={service}
                className="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300"
              >
                <svg className="w-3 h-3 text-green-500" fill="currentColor" viewBox="0 0 8 8">
                  <circle cx="4" cy="4" r="3" />
                </svg>
                {service}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}


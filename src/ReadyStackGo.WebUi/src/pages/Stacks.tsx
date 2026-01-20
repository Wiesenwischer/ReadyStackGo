import { useEffect, useState, useCallback } from "react";
import { Link } from "react-router";
import { listDeployments, type DeploymentSummary } from "../api/deployments";
import { getProducts, syncSources, type Product } from "../api/stacks";
import { useEnvironment } from "../context/EnvironmentContext";

export default function Stacks() {
  const { activeEnvironment } = useEnvironment();
  const [deployments, setDeployments] = useState<DeploymentSummary[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [productsLoading, setProductsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [syncing, setSyncing] = useState(false);

  const loadDeployments = useCallback(async () => {
    if (!activeEnvironment) {
      setDeployments([]);
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const response = await listDeployments(activeEnvironment.id);
      if (response.success) {
        setDeployments(response.deployments);
      } else {
        setError("Failed to load deployments");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load deployments");
    } finally {
      setLoading(false);
    }
  }, [activeEnvironment]);

  const loadProducts = useCallback(async () => {
    try {
      setProductsLoading(true);
      const data = await getProducts();
      setProducts(data);
    } catch (err) {
      console.error("Failed to load products:", err);
    } finally {
      setProductsLoading(false);
    }
  }, []);

  const handleSync = async () => {
    try {
      setSyncing(true);
      await syncSources();
      await loadProducts();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to sync sources");
    } finally {
      setSyncing(false);
    }
  };

  useEffect(() => {
    loadDeployments();
    loadProducts();
  }, [loadDeployments, loadProducts]);

  const getStatusBadge = (status: string | null | undefined) => {
    const statusLower = (status || "unknown").toLowerCase();
    let colorClasses = "bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300";

    if (statusLower === "running") {
      colorClasses = "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200";
    } else if (statusLower === "deploying") {
      colorClasses = "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200";
    } else if (statusLower === "failed" || statusLower === "error") {
      colorClasses = "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200";
    } else if (statusLower === "stopped") {
      colorClasses = "bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200";
    }

    return (
      <span className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${colorClasses}`}>
        {status || "unknown"}
      </span>
    );
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString() + " " + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-title-md2 font-semibold text-black dark:text-white">
          Stack Management
        </h1>
        <div className="flex gap-3">
          {activeEnvironment ? (
            <Link
              to="/deploy/custom"
              className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
              </svg>
              Deploy Custom
            </Link>
          ) : (
            <span className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white opacity-50 cursor-not-allowed">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
              </svg>
              Deploy Custom
            </span>
          )}
          <button
            onClick={handleSync}
            disabled={syncing}
            className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600 disabled:opacity-50"
          >
            <svg className={`w-5 h-5 ${syncing ? 'animate-spin' : ''}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {syncing ? "Syncing..." : "Sync Sources"}
          </button>
        </div>
      </div>

      {!activeEnvironment && (
        <div className="mb-6 rounded-md bg-yellow-50 p-4 dark:bg-yellow-900/20">
          <p className="text-sm text-yellow-800 dark:text-yellow-200">
            No environment selected. Please select an environment to view and manage deployments.
          </p>
        </div>
      )}

      {error && (
        <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
        </div>
      )}

      <div className="flex flex-col gap-10">
        {/* Available Products Section */}
        <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="px-4 py-6 md:px-6 xl:px-7.5">
            <h4 className="text-xl font-semibold text-black dark:text-white">
              Available Products
            </h4>
            <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">
              Products from configured sources. Each product contains one or more deployable stacks.
            </p>
          </div>

          {productsLoading ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                Loading available products...
              </p>
            </div>
          ) : products.length === 0 ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                No products available. Click "Sync Sources" to load products from configured sources.
              </p>
            </div>
          ) : (
            <div className="border-t border-stroke p-4 dark:border-strokedark">
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                {products.map((product) => (
                  <ProductCard
                    key={product.id}
                    product={product}
                  />
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Deployed Stacks Section */}
        <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="px-4 py-6 md:px-6 xl:px-7.5">
            <h4 className="text-xl font-semibold text-black dark:text-white">
              Deployed Stacks {activeEnvironment && `(${activeEnvironment.name})`}
            </h4>
          </div>

          {loading ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                Loading deployments...
              </p>
            </div>
          ) : !activeEnvironment ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                Select an environment to view deployments.
              </p>
            </div>
          ) : deployments.length === 0 ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                No deployments in this environment. Deploy a stack from the available products above.
              </p>
            </div>
          ) : (
            <>
              <div className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5">
                <div className="col-span-2 flex items-center">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Stack Name</p>
                </div>
                <div className="col-span-1 hidden items-center sm:flex">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Version</p>
                </div>
                <div className="col-span-1 hidden items-center sm:flex">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Services</p>
                </div>
                <div className="col-span-2 flex items-center">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Deployed At</p>
                </div>
                <div className="col-span-1 flex items-center">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Status</p>
                </div>
                <div className="col-span-1 flex items-center">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Actions</p>
                </div>
              </div>

              {deployments.map((deployment) => (
                <div
                  key={deployment.deploymentId || deployment.stackName}
                  className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5"
                >
                  <div className="col-span-2 flex items-center">
                    <p className="text-sm text-gray-900 dark:text-white font-medium">
                      {deployment.stackName}
                    </p>
                  </div>

                  <div className="col-span-1 hidden items-center sm:flex">
                    <p className="text-sm text-gray-600 dark:text-gray-400">
                      {deployment.stackVersion || "-"}
                    </p>
                  </div>

                  <div className="col-span-1 hidden items-center sm:flex">
                    <p className="text-sm text-gray-900 dark:text-gray-300">
                      {deployment.serviceCount} service{deployment.serviceCount !== 1 ? 's' : ''}
                    </p>
                  </div>

                  <div className="col-span-2 flex items-center">
                    <p className="text-sm text-gray-600 dark:text-gray-400">
                      {formatDate(deployment.deployedAt)}
                    </p>
                  </div>

                  <div className="col-span-1 flex items-center">
                    {getStatusBadge(deployment.status)}
                  </div>

                  <div className="col-span-1 flex items-center">
                    <Link
                      to={`/deployments/${encodeURIComponent(deployment.stackName)}/remove`}
                      className="inline-flex items-center justify-center rounded bg-red-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-700"
                    >
                      Remove
                    </Link>
                  </div>
                </div>
              ))}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

// Product Card Component
interface ProductCardProps {
  product: Product;
}

function ProductCard({ product }: ProductCardProps) {
  return (
    <div className="rounded-lg border border-gray-200 bg-gray-50 dark:border-gray-700 dark:bg-gray-800/50 overflow-hidden">
      {/* Product Header */}
      <div className="p-4">
        <div className="mb-3 flex items-start justify-between">
          <div className="flex-1">
            <div className="flex items-center gap-2">
              <h5 className="font-semibold text-gray-900 dark:text-white">
                {product.name}
              </h5>
              {product.isMultiStack && (
                <span className="inline-flex items-center rounded bg-indigo-100 px-2 py-0.5 text-xs font-medium text-indigo-800 dark:bg-indigo-900/30 dark:text-indigo-300">
                  {product.stacks.length} stacks
                </span>
              )}
            </div>
            <p className="text-xs text-gray-500 dark:text-gray-400">
              {product.sourceName}
            </p>
          </div>

          <Link
            to={`/stacks/${encodeURIComponent(product.id)}`}
            className="inline-flex items-center gap-1 rounded bg-gray-200 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-300 dark:bg-gray-600 dark:text-gray-200 dark:hover:bg-gray-500"
          >
            View Details
            <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
            </svg>
          </Link>
        </div>

        {product.description && (
          <p className="mb-3 text-sm text-gray-600 dark:text-gray-400 line-clamp-2">
            {product.description}
          </p>
        )}

        <div className="flex flex-wrap gap-2 text-xs">
          <span className="inline-flex items-center rounded bg-blue-100 px-2 py-1 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300">
            {product.totalServices} service{product.totalServices !== 1 ? 's' : ''}
          </span>
          {product.totalVariables > 0 && (
            <span className="inline-flex items-center rounded bg-purple-100 px-2 py-1 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300">
              {product.totalVariables} config{product.totalVariables !== 1 ? 's' : ''}
            </span>
          )}
          {product.version && (
            <span className="inline-flex items-center rounded bg-gray-200 px-2 py-1 text-gray-700 dark:bg-gray-700 dark:text-gray-300">
              v{product.version.substring(0, 8)}
            </span>
          )}
          {product.category && (
            <span className="inline-flex items-center rounded bg-indigo-100 px-2 py-1 text-indigo-800 dark:bg-indigo-900/30 dark:text-indigo-300">
              {product.category}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

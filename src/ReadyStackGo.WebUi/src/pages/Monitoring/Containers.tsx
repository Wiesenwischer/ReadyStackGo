import { useEffect, useState, useCallback, useMemo } from "react";
import {
  containerApi,
  type Container,
  type StackContextInfo,
} from "../../api/containers";
import { useEnvironment } from "../../context/EnvironmentContext";

type ViewMode = "list" | "stacks" | "products";

export default function Containers() {
  const [containers, setContainers] = useState<Container[]>([]);
  const [context, setContext] = useState<Record<string, StackContextInfo>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [removeConfirm, setRemoveConfirm] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<ViewMode>("list");
  const { activeEnvironment } = useEnvironment();

  const loadContainers = useCallback(async () => {
    if (!activeEnvironment) {
      setContainers([]);
      setContext({});
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const [data, ctx] = await Promise.all([
        containerApi.list(activeEnvironment.id),
        containerApi.getContext(activeEnvironment.id),
      ]);
      setContainers(data);
      setContext(ctx.success ? ctx.stacks : {});
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load containers"
      );
    } finally {
      setLoading(false);
    }
  }, [activeEnvironment]);

  useEffect(() => {
    loadContainers();
  }, [loadContainers]);

  const handleStart = async (id: string) => {
    if (!activeEnvironment) return;
    try {
      setActionLoading(id);
      await containerApi.start(activeEnvironment.id, id);
      await loadContainers();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to start container"
      );
    } finally {
      setActionLoading(null);
    }
  };

  const handleStop = async (id: string) => {
    if (!activeEnvironment) return;
    try {
      setActionLoading(id);
      await containerApi.stop(activeEnvironment.id, id);
      await loadContainers();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to stop container"
      );
    } finally {
      setActionLoading(null);
    }
  };

  const handleRemove = async (id: string, force: boolean) => {
    if (!activeEnvironment) return;
    try {
      setActionLoading(id);
      setRemoveConfirm(null);
      await containerApi.remove(activeEnvironment.id, id, force);
      await loadContainers();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to remove container"
      );
    } finally {
      setActionLoading(null);
    }
  };

  const getStackName = (c: Container) => c.labels?.["rsgo.stack"];

  const getContextInfo = useCallback(
    (stackName: string | undefined) => {
      if (!stackName) return undefined;
      const key = Object.keys(context).find(
        (k) => k.toLowerCase() === stackName.toLowerCase()
      );
      return key ? context[key] : undefined;
    },
    [context]
  );

  // Grouped data for Stack/Product views
  const groupedByStack = useMemo(() => {
    const managed: Record<
      string,
      { containers: Container[]; context?: StackContextInfo }
    > = {};
    const unmanaged: Container[] = [];

    for (const c of containers) {
      const stackName = getStackName(c);
      if (stackName) {
        if (!managed[stackName]) {
          managed[stackName] = {
            containers: [],
            context: getContextInfo(stackName),
          };
        }
        managed[stackName].containers.push(c);
      } else {
        unmanaged.push(c);
      }
    }
    return { managed, unmanaged };
  }, [containers, getContextInfo]);

  const groupedByProduct = useMemo(() => {
    const products: Record<
      string,
      {
        displayName: string;
        stacks: Record<
          string,
          { containers: Container[]; context?: StackContextInfo }
        >;
      }
    > = {};
    const unknownProduct: Record<
      string,
      { containers: Container[]; context?: StackContextInfo }
    > = {};
    const unmanaged: Container[] = [];

    for (const c of containers) {
      const stackName = getStackName(c);
      if (!stackName) {
        unmanaged.push(c);
        continue;
      }
      const ctx = getContextInfo(stackName);
      const productKey = ctx?.productName ?? null;
      const productDisplay = ctx?.productDisplayName ?? null;

      if (productKey && productDisplay) {
        if (!products[productKey]) {
          products[productKey] = { displayName: productDisplay, stacks: {} };
        }
        if (!products[productKey].stacks[stackName]) {
          products[productKey].stacks[stackName] = {
            containers: [],
            context: ctx,
          };
        }
        products[productKey].stacks[stackName].containers.push(c);
      } else {
        if (!unknownProduct[stackName]) {
          unknownProduct[stackName] = { containers: [], context: ctx };
        }
        unknownProduct[stackName].containers.push(c);
      }
    }
    return { products, unknownProduct, unmanaged };
  }, [containers, getContextInfo]);

  // --- Reusable sub-components ---

  const StatusBadge = ({
    state,
    healthStatus,
  }: {
    state: string;
    healthStatus?: string;
  }) => {
    if (healthStatus && healthStatus !== "none") {
      const h = healthStatus.toLowerCase();
      if (h === "healthy")
        return (
          <span className="inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200">
            healthy
          </span>
        );
      if (h === "unhealthy")
        return (
          <span className="inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200">
            unhealthy
          </span>
        );
      if (h === "starting")
        return (
          <span className="inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200">
            starting
          </span>
        );
    }
    const s = state.toLowerCase();
    const colors =
      s === "running"
        ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200"
        : s === "exited" || s === "dead"
          ? "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200"
          : s === "paused" || s === "restarting"
            ? "bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200"
            : "bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300";
    return (
      <span
        className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${colors}`}
      >
        {s}
      </span>
    );
  };

  const OrphanedBadge = () => (
    <span className="inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200">
      orphaned
    </span>
  );

  const PortDisplay = ({ c }: { c: Container }) => (
    <span className="text-sm text-gray-700 dark:text-gray-300">
      {c.ports[0] ? `${c.ports[0].publicPort}:${c.ports[0].privatePort}` : "-"}
    </span>
  );

  const ActionButtons = ({ c }: { c: Container }) => {
    const isRunning = c.state.toLowerCase() === "running";
    const isLoading = actionLoading === c.id;
    const isConfirming = removeConfirm === c.id;

    if (isConfirming) {
      return (
        <div className="flex items-center gap-1.5">
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {isRunning ? "Force remove?" : "Remove?"}
          </span>
          <button
            onClick={() => handleRemove(c.id, isRunning)}
            disabled={isLoading}
            className="p-1 rounded text-green-600 hover:bg-green-100 dark:text-green-400 dark:hover:bg-green-900 disabled:opacity-50"
            title="Confirm"
          >
            <svg
              className="w-4 h-4"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M5 13l4 4L19 7"
              />
            </svg>
          </button>
          <button
            onClick={() => setRemoveConfirm(null)}
            className="p-1 rounded text-red-600 hover:bg-red-100 dark:text-red-400 dark:hover:bg-red-900"
            title="Cancel"
          >
            <svg
              className="w-4 h-4"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </button>
        </div>
      );
    }

    return (
      <div className="flex items-center gap-1">
        {isRunning ? (
          <button
            onClick={() => handleStop(c.id)}
            disabled={isLoading}
            className="p-1.5 rounded text-gray-500 hover:text-gray-900 hover:bg-gray-100 dark:text-gray-400 dark:hover:text-white dark:hover:bg-gray-700 disabled:opacity-50"
            title="Stop"
          >
            {isLoading ? (
              <Spinner />
            ) : (
              <svg
                className="w-4 h-4"
                fill="currentColor"
                viewBox="0 0 24 24"
              >
                <rect x="6" y="6" width="12" height="12" rx="1" />
              </svg>
            )}
          </button>
        ) : (
          <button
            onClick={() => handleStart(c.id)}
            disabled={isLoading}
            className="p-1.5 rounded text-gray-500 hover:text-gray-900 hover:bg-gray-100 dark:text-gray-400 dark:hover:text-white dark:hover:bg-gray-700 disabled:opacity-50"
            title="Start"
          >
            {isLoading ? (
              <Spinner />
            ) : (
              <svg
                className="w-4 h-4"
                fill="currentColor"
                viewBox="0 0 24 24"
              >
                <path d="M8 5v14l11-7z" />
              </svg>
            )}
          </button>
        )}
        <button
          onClick={() => setRemoveConfirm(c.id)}
          disabled={isLoading}
          className="p-1.5 rounded text-gray-500 hover:text-red-600 hover:bg-red-50 dark:text-gray-400 dark:hover:text-red-400 dark:hover:bg-red-900/30 disabled:opacity-50"
          title="Remove"
        >
          <svg
            className="w-4 h-4"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
            />
          </svg>
        </button>
      </div>
    );
  };

  const Spinner = () => (
    <svg className="w-4 h-4 animate-spin" viewBox="0 0 24 24" fill="none">
      <circle
        className="opacity-25"
        cx="12"
        cy="12"
        r="10"
        stroke="currentColor"
        strokeWidth="4"
      />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
      />
    </svg>
  );

  // --- View Toggle ---
  const listIcon = (
    <svg
      className="w-4 h-4"
      fill="none"
      stroke="currentColor"
      viewBox="0 0 24 24"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth={2}
        d="M4 6h16M4 12h16M4 18h16"
      />
    </svg>
  );
  const stackIcon = (
    <svg
      className="w-4 h-4"
      fill="none"
      stroke="currentColor"
      viewBox="0 0 24 24"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth={2}
        d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"
      />
    </svg>
  );
  const productIcon = (
    <svg
      className="w-4 h-4"
      fill="none"
      stroke="currentColor"
      viewBox="0 0 24 24"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth={2}
        d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4"
      />
    </svg>
  );

  const ViewToggle = () => (
    <div className="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5">
      {(
        [
          { key: "list", label: "List", icon: listIcon },
          { key: "stacks", label: "Stacks", icon: stackIcon },
          { key: "products", label: "Products", icon: productIcon },
        ] as const
      ).map(({ key, label, icon }) => (
        <button
          key={key}
          onClick={() => setViewMode(key)}
          className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-md transition-colors ${
            viewMode === key
              ? "bg-gray-100 text-gray-900 dark:bg-gray-700 dark:text-white"
              : "text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
          }`}
        >
          {icon}
          {label}
        </button>
      ))}
    </div>
  );

  // --- Container Row (for list view with stack+product columns) ---
  const ContainerRow = ({
    c,
    showStack,
    showProduct,
  }: {
    c: Container;
    showStack?: boolean;
    showProduct?: boolean;
  }) => {
    const stackName = getStackName(c);
    const ctx = getContextInfo(stackName);
    const isOrphaned = stackName && ctx && !ctx.deploymentExists;

    return (
      <div className="grid grid-cols-6 border-t border-gray-200 dark:border-gray-700 px-4 py-3 sm:grid-cols-12 md:px-6 items-center">
        <div
          className={`${showStack || showProduct ? "col-span-2" : "col-span-3"} flex items-center min-w-0`}
        >
          <p className="text-sm text-gray-900 dark:text-white truncate">
            {c.name}
          </p>
        </div>
        {showStack && (
          <div className="col-span-2 hidden sm:flex items-center gap-1.5 min-w-0">
            <span className="text-sm text-gray-700 dark:text-gray-300 truncate">
              {stackName || "-"}
            </span>
            {isOrphaned && <OrphanedBadge />}
          </div>
        )}
        {showProduct && (
          <div className="col-span-2 hidden sm:flex items-center min-w-0">
            <span className="text-sm text-gray-700 dark:text-gray-300 truncate">
              {ctx?.productDisplayName || "-"}
            </span>
          </div>
        )}
        <div className="col-span-2 hidden sm:flex items-center min-w-0">
          <p className="text-sm text-gray-700 dark:text-gray-300 truncate">
            {c.image}
          </p>
        </div>
        <div className="col-span-1 flex items-center">
          <StatusBadge state={c.state} healthStatus={c.healthStatus} />
        </div>
        <div className="col-span-1 flex items-center">
          <PortDisplay c={c} />
        </div>
        <div className="col-span-2 flex items-center justify-end">
          <ActionButtons c={c} />
        </div>
      </div>
    );
  };

  // --- List Header ---
  const ListHeader = ({
    showStack,
    showProduct,
  }: {
    showStack?: boolean;
    showProduct?: boolean;
  }) => (
    <div className="grid grid-cols-6 px-4 py-3 sm:grid-cols-12 md:px-6 bg-gray-50 dark:bg-gray-800/50">
      <div
        className={`${showStack || showProduct ? "col-span-2" : "col-span-3"} flex items-center`}
      >
        <p className="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
          Name
        </p>
      </div>
      {showStack && (
        <div className="col-span-2 hidden sm:flex items-center">
          <p className="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
            Stack
          </p>
        </div>
      )}
      {showProduct && (
        <div className="col-span-2 hidden sm:flex items-center">
          <p className="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
            Product
          </p>
        </div>
      )}
      <div className="col-span-2 hidden sm:flex items-center">
        <p className="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
          Image
        </p>
      </div>
      <div className="col-span-1 flex items-center">
        <p className="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
          Status
        </p>
      </div>
      <div className="col-span-1 flex items-center">
        <p className="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
          Port
        </p>
      </div>
      <div className="col-span-2 flex items-center justify-end">
        <p className="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
          Actions
        </p>
      </div>
    </div>
  );

  // --- Stack Group Header ---
  const StackGroupHeader = ({
    title,
    containers: groupContainers,
    ctx,
    isUnmanaged,
  }: {
    title: string;
    containers: Container[];
    ctx?: StackContextInfo;
    isUnmanaged?: boolean;
  }) => {
    const runningCount = groupContainers.filter(
      (c) => c.state.toLowerCase() === "running"
    ).length;
    const total = groupContainers.length;
    const isOrphaned = ctx && !ctx.deploymentExists;

    return (
      <div className="flex items-center justify-between px-4 py-3 md:px-6 bg-gray-50 dark:bg-gray-800/50">
        <div className="flex items-center gap-2">
          <h3 className="text-sm font-semibold text-gray-900 dark:text-white">
            {isUnmanaged ? "Unmanaged" : title}
          </h3>
          {isOrphaned && <OrphanedBadge />}
        </div>
        <span className="text-xs text-gray-500 dark:text-gray-400">
          {runningCount === total
            ? `${total} running`
            : `${runningCount}/${total} running`}
        </span>
      </div>
    );
  };

  // --- Compact header for containers within stack/product groups ---
  const CompactHeader = () => (
    <div className="grid grid-cols-6 px-4 py-2 sm:grid-cols-12 md:px-6 border-t border-gray-100 dark:border-gray-800">
      <div className="col-span-3 flex items-center">
        <p className="text-xs font-medium uppercase text-gray-400 dark:text-gray-500">
          Name
        </p>
      </div>
      <div className="col-span-2 hidden sm:flex items-center">
        <p className="text-xs font-medium uppercase text-gray-400 dark:text-gray-500">
          Image
        </p>
      </div>
      <div className="col-span-1 flex items-center">
        <p className="text-xs font-medium uppercase text-gray-400 dark:text-gray-500">
          Status
        </p>
      </div>
      <div className="col-span-1 flex items-center">
        <p className="text-xs font-medium uppercase text-gray-400 dark:text-gray-500">
          Port
        </p>
      </div>
      <div className="col-span-2 hidden sm:flex" />
      <div className="col-span-2 flex items-center justify-end">
        <p className="text-xs font-medium uppercase text-gray-400 dark:text-gray-500">
          Actions
        </p>
      </div>
    </div>
  );

  // --- Compact Container Row (no stack/product columns) ---
  const CompactContainerRow = ({ c }: { c: Container }) => (
    <div className="grid grid-cols-6 border-t border-gray-100 dark:border-gray-800 px-4 py-2.5 sm:grid-cols-12 md:px-6 items-center">
      <div className="col-span-3 flex items-center min-w-0">
        <p className="text-sm text-gray-900 dark:text-white truncate">
          {c.name}
        </p>
      </div>
      <div className="col-span-2 hidden sm:flex items-center min-w-0">
        <p className="text-sm text-gray-700 dark:text-gray-300 truncate">
          {c.image}
        </p>
      </div>
      <div className="col-span-1 flex items-center">
        <StatusBadge state={c.state} healthStatus={c.healthStatus} />
      </div>
      <div className="col-span-1 flex items-center">
        <PortDisplay c={c} />
      </div>
      <div className="col-span-2 hidden sm:flex" />
      <div className="col-span-2 flex items-center justify-end">
        <ActionButtons c={c} />
      </div>
    </div>
  );

  // --- Empty / Loading states ---
  const EmptyState = () => (
    <div className="px-4 py-8 md:px-6">
      <p className="text-center text-sm text-gray-500 dark:text-gray-400">
        No containers found. Make sure Docker is running.
      </p>
    </div>
  );

  const LoadingState = () => (
    <div className="px-4 py-8 md:px-6">
      <p className="text-center text-sm text-gray-500 dark:text-gray-400">
        Loading containers...
      </p>
    </div>
  );

  // =========== VIEWS ===========

  const ListView = () => (
    <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden">
      <ListHeader showStack showProduct />
      {loading ? (
        <LoadingState />
      ) : containers.length === 0 ? (
        <EmptyState />
      ) : (
        containers.map((c) => (
          <ContainerRow key={c.id} c={c} showStack showProduct />
        ))
      )}
    </div>
  );

  const StackView = () => {
    const { managed, unmanaged } = groupedByStack;
    const stackNames = Object.keys(managed).sort();

    return (
      <div className="space-y-4">
        {loading ? (
          <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden">
            <LoadingState />
          </div>
        ) : containers.length === 0 ? (
          <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden">
            <EmptyState />
          </div>
        ) : (
          <>
            {stackNames.map((name) => (
              <div
                key={name}
                className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden"
              >
                <StackGroupHeader
                  title={name}
                  containers={managed[name].containers}
                  ctx={managed[name].context}
                />
                <CompactHeader />
                {managed[name].containers.map((c) => (
                  <CompactContainerRow key={c.id} c={c} />
                ))}
              </div>
            ))}
            {unmanaged.length > 0 && (
              <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden">
                <StackGroupHeader
                  title="Unmanaged"
                  containers={unmanaged}
                  isUnmanaged
                />
                <CompactHeader />
                {unmanaged.map((c) => (
                  <CompactContainerRow key={c.id} c={c} />
                ))}
              </div>
            )}
          </>
        )}
      </div>
    );
  };

  const ProductView = () => {
    const { products, unknownProduct, unmanaged } = groupedByProduct;
    const productKeys = Object.keys(products).sort();
    const unknownStacks = Object.keys(unknownProduct).sort();

    return (
      <div className="space-y-6">
        {loading ? (
          <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden">
            <LoadingState />
          </div>
        ) : containers.length === 0 ? (
          <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden">
            <EmptyState />
          </div>
        ) : (
          <>
            {productKeys.map((productKey) => {
              const product = products[productKey];
              const stackNames = Object.keys(product.stacks).sort();
              return (
                <div
                  key={productKey}
                  className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden"
                >
                  <div className="px-4 py-3 md:px-6 bg-gray-100 dark:bg-gray-800">
                    <h3 className="text-sm font-bold text-gray-900 dark:text-white">
                      {product.displayName}
                    </h3>
                  </div>
                  {stackNames.map((stackName) => {
                    const stack = product.stacks[stackName];
                    return (
                      <div key={stackName}>
                        <StackGroupHeader
                          title={stackName}
                          containers={stack.containers}
                          ctx={stack.context}
                        />
                        <CompactHeader />
                        {stack.containers.map((c) => (
                          <CompactContainerRow key={c.id} c={c} />
                        ))}
                      </div>
                    );
                  })}
                </div>
              );
            })}
            {unknownStacks.length > 0 && (
              <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden">
                <div className="px-4 py-3 md:px-6 bg-gray-100 dark:bg-gray-800">
                  <h3 className="text-sm font-bold text-gray-900 dark:text-white">
                    Unknown Product
                  </h3>
                </div>
                {unknownStacks.map((stackName) => {
                  const stack = unknownProduct[stackName];
                  return (
                    <div key={stackName}>
                      <StackGroupHeader
                        title={stackName}
                        containers={stack.containers}
                        ctx={stack.context}
                      />
                      <CompactHeader />
                      {stack.containers.map((c) => (
                        <CompactContainerRow key={c.id} c={c} />
                      ))}
                    </div>
                  );
                })}
              </div>
            )}
            {unmanaged.length > 0 && (
              <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden">
                <div className="px-4 py-3 md:px-6 bg-gray-100 dark:bg-gray-800">
                  <h3 className="text-sm font-bold text-gray-900 dark:text-white">
                    Unmanaged
                  </h3>
                </div>
                <CompactHeader />
                {unmanaged.map((c) => (
                  <CompactContainerRow key={c.id} c={c} />
                ))}
              </div>
            )}
          </>
        )}
      </div>
    );
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
          Container Management
        </h2>
        <div className="flex items-center gap-3">
          <ViewToggle />
          <button
            onClick={loadContainers}
            className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
          >
            <svg
              className="w-5 h-5"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
              />
            </svg>
            Refresh
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-4 rounded-sm border border-red-300 bg-red-50 p-4 text-red-800 dark:border-red-700 dark:bg-red-900 dark:text-red-200">
          {error}
        </div>
      )}

      {viewMode === "list" && <ListView />}
      {viewMode === "stacks" && <StackView />}
      {viewMode === "products" && <ProductView />}
    </div>
  );
}

import { useState, useEffect, useCallback, useMemo } from 'react';
import { containerApi, type Container, type StackContextInfo } from '../api/containers';

export type ViewMode = 'list' | 'stacks' | 'products';

export interface OrphanConfirm {
  stackName: string;
  action: 'repair' | 'remove';
}

export interface StackGroup {
  containers: Container[];
  context?: StackContextInfo;
}

export interface ProductGrouping {
  products: Record<string, { displayName: string; stacks: Record<string, StackGroup> }>;
  unknownProduct: Record<string, StackGroup>;
  unmanaged: Container[];
}

export interface UseContainerStoreReturn {
  // State
  containers: Container[];
  loading: boolean;
  error: string | null;
  actionLoading: string | null;
  removeConfirm: string | null;
  orphanConfirm: OrphanConfirm | null;
  repairAllConfirm: boolean;
  orphanActionLoading: string | null;
  viewMode: ViewMode;

  // Derived data
  groupedByStack: { managed: Record<string, StackGroup>; unmanaged: Container[] };
  groupedByProduct: ProductGrouping;
  hasOrphanedStacks: boolean;

  // Actions
  refresh: () => void;
  handleStart: (id: string) => Promise<void>;
  handleStop: (id: string) => Promise<void>;
  handleRemove: (id: string, force: boolean) => Promise<void>;
  handleRepairOrphan: (stackName: string) => Promise<void>;
  handleRemoveOrphan: (stackName: string) => Promise<void>;
  handleRepairAllOrphaned: () => Promise<void>;
  setViewMode: (mode: ViewMode) => void;
  setRemoveConfirm: (id: string | null) => void;
  setOrphanConfirm: (confirm: OrphanConfirm | null) => void;
  setRepairAllConfirm: (confirm: boolean) => void;

  // Helpers
  getStackName: (c: Container) => string | undefined;
  getContextInfo: (stackName: string | undefined) => StackContextInfo | undefined;
}

export function useContainerStore(
  environmentId: string | undefined,
): UseContainerStoreReturn {
  const [containers, setContainers] = useState<Container[]>([]);
  const [context, setContext] = useState<Record<string, StackContextInfo>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [removeConfirm, setRemoveConfirm] = useState<string | null>(null);
  const [orphanConfirm, setOrphanConfirm] = useState<OrphanConfirm | null>(null);
  const [repairAllConfirm, setRepairAllConfirm] = useState(false);
  const [orphanActionLoading, setOrphanActionLoading] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<ViewMode>('list');

  const loadContainers = useCallback(async () => {
    if (!environmentId) {
      setContainers([]);
      setContext({});
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const [data, ctx] = await Promise.all([
        containerApi.list(environmentId),
        containerApi.getContext(environmentId),
      ]);
      setContainers(data);
      setContext(ctx.success ? ctx.stacks : {});
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load containers');
    } finally {
      setLoading(false);
    }
  }, [environmentId]);

  useEffect(() => {
    loadContainers();
  }, [loadContainers]);

  const handleStart = useCallback(async (id: string) => {
    if (!environmentId) return;
    try {
      setActionLoading(id);
      await containerApi.start(environmentId, id);
      await loadContainers();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start container');
    } finally {
      setActionLoading(null);
    }
  }, [environmentId, loadContainers]);

  const handleStop = useCallback(async (id: string) => {
    if (!environmentId) return;
    try {
      setActionLoading(id);
      await containerApi.stop(environmentId, id);
      await loadContainers();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to stop container');
    } finally {
      setActionLoading(null);
    }
  }, [environmentId, loadContainers]);

  const handleRemove = useCallback(async (id: string, force: boolean) => {
    if (!environmentId) return;
    try {
      setActionLoading(id);
      setRemoveConfirm(null);
      await containerApi.remove(environmentId, id, force);
      await loadContainers();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to remove container');
    } finally {
      setActionLoading(null);
    }
  }, [environmentId, loadContainers]);

  const handleRepairOrphan = useCallback(async (stackName: string) => {
    if (!environmentId) return;
    try {
      setOrphanActionLoading(`repair:${stackName}`);
      setOrphanConfirm(null);
      await containerApi.repairOrphanedStack(environmentId, stackName);
      await loadContainers();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to repair orphaned stack');
    } finally {
      setOrphanActionLoading(null);
    }
  }, [environmentId, loadContainers]);

  const handleRemoveOrphan = useCallback(async (stackName: string) => {
    if (!environmentId) return;
    try {
      setOrphanActionLoading(`remove:${stackName}`);
      setOrphanConfirm(null);
      await containerApi.removeOrphanedStack(environmentId, stackName);
      await loadContainers();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to remove orphaned stack');
    } finally {
      setOrphanActionLoading(null);
    }
  }, [environmentId, loadContainers]);

  const handleRepairAllOrphaned = useCallback(async () => {
    if (!environmentId) return;
    try {
      setOrphanActionLoading('repair-all');
      setRepairAllConfirm(false);
      await containerApi.repairAllOrphanedStacks(environmentId);
      await loadContainers();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to repair all orphaned stacks');
    } finally {
      setOrphanActionLoading(null);
    }
  }, [environmentId, loadContainers]);

  const getStackName = useCallback((c: Container) => c.labels?.['rsgo.stack'], []);

  const getContextInfo = useCallback(
    (stackName: string | undefined) => {
      if (!stackName) return undefined;
      const key = Object.keys(context).find(
        (k) => k.toLowerCase() === stackName.toLowerCase(),
      );
      return key ? context[key] : undefined;
    },
    [context],
  );

  const groupedByStack = useMemo(() => {
    const managed: Record<string, StackGroup> = {};
    const unmanaged: Container[] = [];

    for (const c of containers) {
      const stackName = getStackName(c);
      if (stackName) {
        if (!managed[stackName]) {
          managed[stackName] = { containers: [], context: getContextInfo(stackName) };
        }
        managed[stackName].containers.push(c);
      } else {
        unmanaged.push(c);
      }
    }
    return { managed, unmanaged };
  }, [containers, getStackName, getContextInfo]);

  const groupedByProduct = useMemo((): ProductGrouping => {
    const products: Record<string, { displayName: string; stacks: Record<string, StackGroup> }> = {};
    const unknownProduct: Record<string, StackGroup> = {};
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
          products[productKey].stacks[stackName] = { containers: [], context: ctx };
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
  }, [containers, getStackName, getContextInfo]);

  const hasOrphanedStacks = useMemo(
    () => Object.values(groupedByStack.managed).some((g) => g.context && !g.context.deploymentExists),
    [groupedByStack],
  );

  return {
    containers,
    loading,
    error,
    actionLoading,
    removeConfirm,
    orphanConfirm,
    repairAllConfirm,
    orphanActionLoading,
    viewMode,
    groupedByStack,
    groupedByProduct,
    hasOrphanedStacks,
    refresh: loadContainers,
    handleStart,
    handleStop,
    handleRemove,
    handleRepairOrphan,
    handleRemoveOrphan,
    handleRepairAllOrphaned,
    setViewMode,
    setRemoveConfirm,
    setOrphanConfirm,
    setRepairAllConfirm,
    getStackName,
    getContextInfo,
  };
}

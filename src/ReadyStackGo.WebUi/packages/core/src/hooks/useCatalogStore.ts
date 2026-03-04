import { useState, useEffect, useCallback } from 'react';
import { getProducts, type Product } from '../api/stacks';
import { syncAllSources } from '../api/stackSources';

export interface UseCatalogStoreReturn {
  products: Product[];
  loading: boolean;
  error: string | null;
  syncing: boolean;
  refresh: () => void;
  handleSync: () => Promise<void>;
}

export function useCatalogStore(): UseCatalogStoreReturn {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [syncing, setSyncing] = useState(false);

  const loadProducts = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await getProducts();
      setProducts(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load products');
    } finally {
      setLoading(false);
    }
  }, []);

  const handleSync = useCallback(async () => {
    try {
      setSyncing(true);
      setError(null);
      await syncAllSources();
      await loadProducts();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to sync sources');
    } finally {
      setSyncing(false);
    }
  }, [loadProducts]);

  useEffect(() => {
    loadProducts();
  }, [loadProducts]);

  return {
    products,
    loading,
    error,
    syncing,
    refresh: loadProducts,
    handleSync,
  };
}

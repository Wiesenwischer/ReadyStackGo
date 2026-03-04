import { useState, useEffect, useCallback } from 'react';
import { getProduct, type Product } from '../api/stacks';
import {
  getProductDeploymentByProduct,
  checkProductUpgrade,
  type GetProductDeploymentResponse,
} from '../api/deployments';

export interface UseProductDetailStoreReturn {
  product: Product | null;
  loading: boolean;
  error: string | null;
  productDeployment: GetProductDeploymentResponse | null;
  upgradeAvailable: boolean;
  refresh: () => void;
}

export function useProductDetailStore(
  productId: string | undefined,
  environmentId: string | undefined,
): UseProductDetailStoreReturn {
  const [product, setProduct] = useState<Product | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [productDeployment, setProductDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [upgradeAvailable, setUpgradeAvailable] = useState(false);

  const loadProduct = useCallback(async () => {
    if (!productId) {
      setError('No product ID provided');
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const data = await getProduct(productId);
      setProduct(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load product');
    } finally {
      setLoading(false);
    }
  }, [productId]);

  useEffect(() => {
    loadProduct();
  }, [loadProduct]);

  // Check for active product deployment and upgrade availability
  useEffect(() => {
    if (!product || !environmentId) {
      setProductDeployment(null);
      setUpgradeAvailable(false);
      return;
    }

    const checkDeploymentStatus = async () => {
      try {
        const deployment = await getProductDeploymentByProduct(
          environmentId,
          product.groupId,
        );
        setProductDeployment(deployment);

        if (deployment.canUpgrade) {
          try {
            const upgradeCheck = await checkProductUpgrade(
              environmentId,
              deployment.productDeploymentId,
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
  }, [product, environmentId]);

  return {
    product,
    loading,
    error,
    productDeployment,
    upgradeAvailable,
    refresh: loadProduct,
  };
}

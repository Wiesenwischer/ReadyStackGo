import { useState, useEffect, useCallback, useRef } from 'react';
import { notificationsApi, type Notification } from '../api/notifications';

const POLL_INTERVAL_MS = 60_000;

export interface UseNotificationsReturn {
  notifications: Notification[];
  count: number;
  isLoading: boolean;
  fetchNotifications: () => Promise<void>;
  dismiss: (id: string) => Promise<void>;
  dismissAll: () => Promise<void>;
}

export function useNotifications(): UseNotificationsReturn {
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [count, setCount] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const mountedRef = useRef(true);

  const fetchCount = useCallback(async () => {
    try {
      const response = await notificationsApi.getCount();
      if (mountedRef.current) {
        setCount(response.count);
      }
    } catch {
      // Silently ignore polling errors
    }
  }, []);

  const fetchNotifications = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await notificationsApi.list();
      if (mountedRef.current) {
        setNotifications(response.notifications);
        setCount(response.notifications.length);
      }
    } catch {
      // Silently ignore
    } finally {
      if (mountedRef.current) {
        setIsLoading(false);
      }
    }
  }, []);

  const dismiss = useCallback(async (id: string) => {
    try {
      await notificationsApi.dismiss(id);
      if (mountedRef.current) {
        setNotifications(prev => prev.filter(n => n.id !== id));
        setCount(c => Math.max(0, c - 1));
      }
    } catch {
      // Silently ignore
    }
  }, []);

  const dismissAll = useCallback(async () => {
    try {
      await notificationsApi.dismissAll();
      if (mountedRef.current) {
        setNotifications([]);
        setCount(0);
      }
    } catch {
      // Silently ignore
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    fetchCount();

    const interval = setInterval(fetchCount, POLL_INTERVAL_MS);

    return () => {
      mountedRef.current = false;
      clearInterval(interval);
    };
  }, [fetchCount]);

  return {
    notifications,
    count,
    isLoading,
    fetchNotifications,
    dismiss,
    dismissAll,
  };
}

import { useState, useEffect, useCallback, useRef } from 'react';
import { notificationsApi, type Notification } from '../api/notifications';

const POLL_INTERVAL_MS = 60_000;

export interface UseNotificationsReturn {
  notifications: Notification[];
  unreadCount: number;
  isLoading: boolean;
  fetchNotifications: () => Promise<void>;
  markAsRead: (id: string) => Promise<void>;
  markAllAsRead: () => Promise<void>;
  dismiss: (id: string) => Promise<void>;
}

export function useNotifications(): UseNotificationsReturn {
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const mountedRef = useRef(true);

  const fetchUnreadCount = useCallback(async () => {
    try {
      const response = await notificationsApi.getUnreadCount();
      if (mountedRef.current) {
        setUnreadCount(response.count);
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
        setUnreadCount(response.notifications.filter(n => !n.read).length);
      }
    } catch {
      // Silently ignore
    } finally {
      if (mountedRef.current) {
        setIsLoading(false);
      }
    }
  }, []);

  const markAsRead = useCallback(async (id: string) => {
    try {
      await notificationsApi.markAsRead(id);
      if (mountedRef.current) {
        setNotifications(prev =>
          prev.map(n => n.id === id ? { ...n, read: true } : n)
        );
        setUnreadCount(prev => Math.max(0, prev - 1));
      }
    } catch {
      // Silently ignore
    }
  }, []);

  const markAllAsRead = useCallback(async () => {
    try {
      await notificationsApi.markAllAsRead();
      if (mountedRef.current) {
        setNotifications(prev => prev.map(n => ({ ...n, read: true })));
        setUnreadCount(0);
      }
    } catch {
      // Silently ignore
    }
  }, []);

  const dismiss = useCallback(async (id: string) => {
    try {
      await notificationsApi.dismiss(id);
      if (mountedRef.current) {
        setNotifications(prev => {
          const removed = prev.find(n => n.id === id);
          const next = prev.filter(n => n.id !== id);
          if (removed && !removed.read) {
            setUnreadCount(c => Math.max(0, c - 1));
          }
          return next;
        });
      }
    } catch {
      // Silently ignore
    }
  }, []);

  // Poll unread count every 60s
  useEffect(() => {
    mountedRef.current = true;
    fetchUnreadCount();

    const interval = setInterval(fetchUnreadCount, POLL_INTERVAL_MS);

    return () => {
      mountedRef.current = false;
      clearInterval(interval);
    };
  }, [fetchUnreadCount]);

  return {
    notifications,
    unreadCount,
    isLoading,
    fetchNotifications,
    markAsRead,
    markAllAsRead,
    dismiss,
  };
}

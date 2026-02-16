import { apiGet, apiPost, apiDelete } from './client';

export type NotificationType = 'UpdateAvailable' | 'SourceSyncResult' | 'DeploymentResult';
export type NotificationSeverity = 'Info' | 'Success' | 'Warning' | 'Error';

export interface Notification {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  severity: NotificationSeverity;
  createdAt: string;
  read: boolean;
  actionUrl?: string;
  actionLabel?: string;
  metadata: Record<string, string>;
}

export interface ListNotificationsResponse {
  notifications: Notification[];
}

export interface UnreadCountResponse {
  count: number;
}

export const notificationsApi = {
  list: () =>
    apiGet<ListNotificationsResponse>('/api/notifications'),
  getUnreadCount: () =>
    apiGet<UnreadCountResponse>('/api/notifications/unread-count'),
  markAsRead: (id: string) =>
    apiPost('/api/notifications/' + id + '/read'),
  markAllAsRead: () =>
    apiPost('/api/notifications/read-all'),
  dismiss: (id: string) =>
    apiDelete('/api/notifications/' + id),
};

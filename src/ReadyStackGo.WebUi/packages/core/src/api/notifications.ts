import { apiGet, apiDelete } from './client';

export type NotificationType = 'UpdateAvailable' | 'SourceSyncResult' | 'DeploymentResult';
export type NotificationSeverity = 'Info' | 'Success' | 'Warning' | 'Error';

export interface Notification {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  severity: NotificationSeverity;
  createdAt: string;
  actionUrl?: string;
  actionLabel?: string;
  metadata: Record<string, string>;
}

export interface ListNotificationsResponse {
  notifications: Notification[];
}

export interface NotificationCountResponse {
  count: number;
}

export const notificationsApi = {
  list: () =>
    apiGet<ListNotificationsResponse>('/api/notifications'),
  getCount: () =>
    apiGet<NotificationCountResponse>('/api/notifications/count'),
  dismiss: (id: string) =>
    apiDelete('/api/notifications/' + id),
  dismissAll: () =>
    apiDelete('/api/notifications'),
};

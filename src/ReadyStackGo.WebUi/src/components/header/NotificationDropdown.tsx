import { useState, useRef, useEffect, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { useNotifications } from "../../hooks/useNotifications";
import { timeAgo } from "../../utils/timeAgo";
import type { NotificationSeverity } from "../../api/notifications";

const severityConfig: Record<
  NotificationSeverity,
  { icon: ReactNode; color: string; bg: string }
> = {
  Info: {
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" d="m11.25 11.25.041-.02a.75.75 0 0 1 1.063.852l-.708 2.836a.75.75 0 0 0 1.063.853l.041-.021M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9-3.75h.008v.008H12V8.25Z" />
      </svg>
    ),
    color: "text-blue-500",
    bg: "bg-blue-50 dark:bg-blue-500/10",
  },
  Success: {
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
      </svg>
    ),
    color: "text-green-500",
    bg: "bg-green-50 dark:bg-green-500/10",
  },
  Warning: {
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
      </svg>
    ),
    color: "text-amber-500",
    bg: "bg-amber-50 dark:bg-amber-500/10",
  },
  Error: {
    icon: (
      <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z" />
      </svg>
    ),
    color: "text-red-500",
    bg: "bg-red-50 dark:bg-red-500/10",
  },
};

const NotificationDropdown = () => {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();
  const {
    notifications,
    unreadCount,
    fetchNotifications,
    markAsRead,
    markAllAsRead,
    dismiss,
  } = useNotifications();

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (
        dropdownRef.current &&
        !dropdownRef.current.contains(event.target as Node)
      ) {
        setIsOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleToggle = () => {
    const opening = !isOpen;
    setIsOpen(opening);
    if (opening) {
      fetchNotifications();
    }
  };

  const handleNotificationClick = async (id: string, actionUrl?: string) => {
    await markAsRead(id);
    if (actionUrl) {
      setIsOpen(false);
      navigate(actionUrl);
    }
  };

  const handleDismiss = async (
    e: React.MouseEvent,
    id: string
  ) => {
    e.stopPropagation();
    await dismiss(id);
  };

  return (
    <div className="relative" ref={dropdownRef}>
      <button
        onClick={handleToggle}
        className="relative flex items-center justify-center w-10 h-10 text-gray-700 rounded-lg hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800"
        aria-label="Notifications"
      >
        <svg
          width="20"
          height="20"
          viewBox="0 0 20 20"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
        >
          <path
            fillRule="evenodd"
            clipRule="evenodd"
            d="M8.75 3.5C8.75 2.53 9.53 1.75 10.5 1.75C11.47 1.75 12.25 2.53 12.25 3.5V3.76C13.58 4.28 14.55 5.52 14.7 7.01L15.07 10.76C15.15 11.67 15.54 12.53 16.17 13.2C16.52 13.57 16.48 14.16 16.09 14.49C15.87 14.67 15.59 14.77 15.31 14.77H5.69C5.41 14.77 5.13 14.67 4.91 14.49C4.52 14.16 4.48 13.57 4.83 13.2C5.46 12.53 5.85 11.67 5.93 10.76L6.3 7.01C6.45 5.52 7.42 4.28 8.75 3.76V3.5ZM10.5 3.25C10.36 3.25 10.25 3.36 10.25 3.5V3.77H10.75V3.5C10.75 3.36 10.64 3.25 10.5 3.25ZM9.25 5.25C8.42 5.25 7.72 5.83 7.54 6.63L7.17 10.38C7.06 11.53 6.56 12.62 5.75 13.48C5.68 13.56 5.69 13.68 5.76 13.75C5.78 13.77 5.81 13.77 5.84 13.77H15.16C15.19 13.77 15.22 13.77 15.24 13.75C15.31 13.68 15.32 13.56 15.25 13.48C14.44 12.62 13.94 11.53 13.83 10.38L13.46 6.63C13.28 5.83 12.58 5.25 11.75 5.25H9.25Z"
            fill="currentColor"
          />
          <path
            d="M8.25 16C8.25 15.59 8.59 15.25 9 15.25H12C12.41 15.25 12.75 15.59 12.75 16C12.75 17.52 11.52 18.75 10 18.75C8.48 18.75 7.25 17.52 7.25 16C7.25 15.59 7.59 15.25 8 15.25H8.25ZM10 17.25C10.69 17.25 11.25 16.69 11.25 16H8.75C8.75 16.69 9.31 17.25 10 17.25Z"
            fill="currentColor"
          />
        </svg>
        {unreadCount > 0 && (
          <span className="absolute top-0 right-0 z-1 flex h-5 w-5 items-center justify-center">
            <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-red-400 opacity-75"></span>
            <span className="relative inline-flex h-4 w-4 items-center justify-center rounded-full bg-red-500 text-[10px] font-bold text-white">
              {unreadCount > 9 ? "9+" : unreadCount}
            </span>
          </span>
        )}
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-96 rounded-lg border border-gray-200 bg-white shadow-lg dark:border-gray-800 dark:bg-gray-900 z-50">
          <div className="flex items-center justify-between border-b border-gray-200 px-4 py-3 dark:border-gray-800">
            <h5 className="text-sm font-medium text-gray-900 dark:text-white">
              Notifications
            </h5>
            {unreadCount > 0 && (
              <button
                onClick={markAllAsRead}
                className="text-xs text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300"
              >
                Mark all as read
              </button>
            )}
          </div>

          <div className="max-h-96 overflow-y-auto">
            {notifications.length === 0 ? (
              <div className="px-4 py-8 text-center">
                <svg
                  className="mx-auto h-8 w-8 text-gray-300 dark:text-gray-600"
                  fill="none"
                  viewBox="0 0 24 24"
                  strokeWidth={1.5}
                  stroke="currentColor"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M14.857 17.082a23.848 23.848 0 0 0 5.454-1.31A8.967 8.967 0 0 1 18 9.75V9A6 6 0 0 0 6 9v.75a8.967 8.967 0 0 1-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 0 1-5.714 0m5.714 0a3 3 0 1 1-5.714 0"
                  />
                </svg>
                <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
                  No notifications
                </p>
              </div>
            ) : (
              notifications.map((notification) => {
                const config = severityConfig[notification.severity] ?? severityConfig.Info;
                return (
                  <div
                    key={notification.id}
                    onClick={() =>
                      handleNotificationClick(
                        notification.id,
                        notification.actionUrl
                      )
                    }
                    className={`group flex items-start gap-3 px-4 py-3 border-b border-gray-100 dark:border-gray-800 last:border-b-0 cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800/50 ${
                      !notification.read
                        ? "bg-blue-50/50 dark:bg-blue-500/5"
                        : ""
                    }`}
                  >
                    <div
                      className={`mt-0.5 flex-shrink-0 rounded-full p-1 ${config.bg} ${config.color}`}
                    >
                      {config.icon}
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="flex items-start justify-between gap-2">
                        <p
                          className={`text-sm ${
                            !notification.read
                              ? "font-semibold text-gray-900 dark:text-white"
                              : "font-medium text-gray-700 dark:text-gray-300"
                          }`}
                        >
                          {notification.title}
                        </p>
                        <button
                          onClick={(e) =>
                            handleDismiss(e, notification.id)
                          }
                          className="flex-shrink-0 rounded p-0.5 text-gray-400 opacity-0 transition-opacity hover:text-gray-600 group-hover:opacity-100 dark:text-gray-500 dark:hover:text-gray-300"
                          aria-label="Dismiss notification"
                        >
                          <svg
                            className="h-4 w-4"
                            fill="none"
                            viewBox="0 0 24 24"
                            strokeWidth={1.5}
                            stroke="currentColor"
                          >
                            <path
                              strokeLinecap="round"
                              strokeLinejoin="round"
                              d="M6 18 18 6M6 6l12 12"
                            />
                          </svg>
                        </button>
                      </div>
                      <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400 line-clamp-2">
                        {notification.message}
                      </p>
                      <p className="mt-1 text-xs text-gray-400 dark:text-gray-500">
                        {timeAgo(notification.createdAt)}
                      </p>
                    </div>
                    {!notification.read && (
                      <span className="mt-2 h-2 w-2 flex-shrink-0 rounded-full bg-blue-500" />
                    )}
                  </div>
                );
              })
            )}
          </div>
        </div>
      )}
    </div>
  );
};

export default NotificationDropdown;

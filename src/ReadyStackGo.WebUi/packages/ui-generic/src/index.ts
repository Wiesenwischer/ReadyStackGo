// @rsgo/ui-generic - Public API

// Contexts
export { AuthProvider, useAuth } from './context/AuthContext';
export { ThemeProvider } from './context/ThemeContext';
export { EnvironmentProvider, useEnvironment } from './context/EnvironmentContext';
export { SidebarProvider, useSidebar } from './context/SidebarContext';

// SignalR wrapper hooks (bridge useAuth → @rsgo/core hubs)
export { useDeploymentHub } from './hooks/useDeploymentHub';
export { useHealthHub } from './hooks/useHealthHub';
export { useContainerLogsHub } from './hooks/useContainerLogsHub';

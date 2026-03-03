# @rsgo/core

Framework-agnostic business logic layer for ReadyStackGo WebUI. This package contains all API clients, SignalR hub connections, state management hooks (ViewModel layer), and utility functions.

Downstream forks can replace the UI layer (`@rsgo/ui-generic`) while reusing `@rsgo/core` as-is.

## Architecture

```
@rsgo/core/src/
├── api/            HTTP client + 16 domain API modules
├── hooks/          ViewModel store hooks (page-level state management)
├── realtime/       SignalR hub connections (deployment, health, container logs, updates)
├── services/       Auth + Environment services (token management, environment selection)
└── utils/          Utility functions (timeAgo)
```

## API Layer (`api/`)

Pure TypeScript HTTP client with typed request/response objects. No React dependency.

| Module | Description |
|--------|-------------|
| `client` | Base HTTP client (`apiGet`, `apiPost`, `apiPut`, `apiDelete`) with auth header injection |
| `apiKeys` | API key management (CRUD, regenerate) |
| `containers` | Docker container operations (list, inspect, stop, restart, remove, repair) |
| `dashboard` | Dashboard statistics |
| `deployments` | Stack + product deployments (deploy, upgrade, rollback, remove, stop, restart, redeploy) |
| `environments` | Environment management (CRUD, Docker host config) |
| `health` | Health monitoring (environment summary, service health, history) |
| `notifications` | Notification management (list, mark read, dismiss) |
| `onboarding` | Onboarding wizard flow |
| `organizations` | Organization management |
| `registries` | Container registry management (CRUD, test connection) |
| `stackSources` | Stack source management (Git, local, catalog) |
| `stacks` | Stack definitions and catalog |
| `system` | System info, updates, maintenance mode |
| `user` | User profile and preferences |
| `volumes` | Docker volume management |
| `wizard` | Setup wizard state |

## SignalR Hubs (`realtime/`)

Real-time communication via SignalR. Each hub accepts a `token: string | null` parameter for authentication (dependency inversion — no direct context access).

| Hub | Purpose | Key Events |
|-----|---------|------------|
| `useDeploymentHub` | Deployment progress tracking | `onProgress`, `onInitLog`, `onCompleted` |
| `useHealthHub` | Health monitoring updates | `onEnvironmentHealthChanged`, `onDeploymentHealthChanged` |
| `useContainerLogsHub` | Live container log streaming | `onLogReceived`, `onStreamEnded` |
| `useUpdateHub` | Application update notifications | `onUpdateAvailable` |

## Store Hooks (`hooks/`)

ViewModel-layer hooks that encapsulate all business logic for pages. Each hook manages state, API calls, SignalR subscriptions, and derived data. Pages become purely presentational.

**Pattern:**
```typescript
import { useAuth } from '../context/AuthContext';   // your UI layer provides auth
import { useDeployStackStore } from '@rsgo/core';

function DeployStackPage() {
  const { token } = useAuth();
  const store = useDeployStackStore(token, environmentId, deploymentId);
  // store.state, store.deployment, store.handleDeploy(), etc.
}
```

### Settings Stores
| Hook | Page | Key Features |
|------|------|--------------|
| `useRegistryStore` | Registry settings | CRUD, test connection, modal state |
| `useStackSourceStore` | Stack source settings | CRUD, sync operations |
| `useTlsStore` | TLS/certificate settings | Let's Encrypt, upload, reset |
| `useApiKeyStore` | API key settings | CRUD, regenerate, copy |

### Stack Deployment Stores
| Hook | Page | Key Features |
|------|------|--------------|
| `useDeployStackStore` | Deploy stack | Variables, env import, SignalR progress |
| `useUpgradeStackStore` | Upgrade stack | Version check, variables, SignalR progress |
| `useRollbackStore` | Rollback stack | Version selection, SignalR progress |
| `useRemoveStackStore` | Remove stack | Confirmation, removal |

### Product Deployment Stores
| Hook | Page | Key Features |
|------|------|--------------|
| `useDeployProductStore` | Deploy product | Multi-stack variables, env import, per-stack SignalR progress |
| `useUpgradeProductStore` | Upgrade product | Upgrade check, variables, per-stack SignalR progress |
| `useRemoveProductStore` | Remove product | Multi-stack removal with progress |
| `useRedeployProductStore` | Redeploy product | Redeploy with SignalR progress |
| `useRetryProductStore` | Retry product | Retry failed deployment with progress |
| `useRestartProductStore` | Restart product | Container restart confirmation |
| `useStopProductStore` | Stop product | Container stop confirmation |

### List & Detail Stores
| Hook | Page | Key Features |
|------|------|--------------|
| `useDeploymentsStore` | Deployments list | Stack + product deployments, health hub |
| `useDeploymentDetailStore` | Deployment detail | Health hub, maintenance mode, rollback/upgrade info |
| `useProductDeploymentDetailStore` | Product deployment detail | Formatters, variable toggle |
| `useHealthDashboardStore` | Health dashboard | SignalR updates, filtering, product grouping |

## Services (`services/`)

| Service | Description |
|---------|-------------|
| `AuthService` | Login/logout, token persistence (localStorage), user state management |
| `EnvironmentService` | Environment loading, selection, persistence |

## Usage in Downstream Forks

1. Install `@rsgo/core` as a workspace dependency
2. Provide `token: string | null` from your auth system to store hooks and SignalR hubs
3. Provide `environmentId: string | undefined` from your environment selection to store hooks
4. Build your own pages using the store hook return values (state, data, actions)

The store hooks return typed interfaces (e.g., `UseDeployStackStoreReturn`) — use these types to ensure your pages handle all states correctly.

# Roadmap

Rough outlook on planned versions and features.

## Released

- **v0.1** – Container View + Start/Stop
- **v0.2** – Local Admin + Hardcoded Stack
- **v0.3** – Wizard + TLS Bootstrap + Manifest Deploy (2025-01-19)
- **v0.4** – Multi-Environment + Docker Compose Deployment (2025-01-28)
  - Polymorphic Environment Types (Docker Socket)
  - Docker Compose Stack Deployment (Portainer-style)
  - Automatic Environment Variable Detection
  - Stack Sources System with Recursive Discovery
  - 404 Page, 4-Step Wizard, Environment Management UI
  - Registry Authentication for Image Pulls
  - Deployment Warnings for Local Image Fallback
- **v0.5** – Environment Setup Flow + PublicWeb (2025-11-30)
  - Environment Creation as Optional Wizard Step
  - EnvironmentGuard for Environment-Dependent Pages
  - SetupEnvironment Page When No Environment Exists
  - PublicWeb Landing Page (DE/EN) with Astro/Starlight
  - ams.project Showcase, Legal Notice, Privacy Policy
  - Install Script with Docker Hub Image
- **v0.6** – DDD Architecture + Security Hardening (2025-12-01)
  - SQLite Database for Persistent Storage
  - Domain-Driven Design with Bounded Contexts
  - Clean Architecture Implementation
  - MediatR for Command/Query Separation
  - RBAC (Role-Based Access Control)
  - Wizard Timeout (5-Minute Time Window for Admin Creation After Server Start)
  - API Lockdown After Timeout Expiration (like Portainer)
- **v0.7** – Automated Release Workflow (2025-12-03)
  - Fully Automated Release Workflow with GitHub Actions
  - PublicWeb Deployment on Every Release
- **v0.8** – Simplified Release Management (2025-12-03)
  - Simplified Release Workflow
  - GitHub-based Release Notes Integration
- **v0.9** – Dev Builds + Example Stacks (2025-12-03)
  - Draft-only Releases with Manual Publish
  - Docker Dev Builds on ghcr.io
  - Example Stacks Included in Docker Image
- **v0.10** – RSGo Manifest Format (2025-12-07)
  - Native Stack Definition Format for ReadyStackGo
  - 17 Variable Types (String, Number, Boolean, Select, Password, Port, Url, Email, etc.)
  - Docker Compose Import with Automatic Conversion
  - Multi-Stack Products with Shared Variables and Fragments
  - Regex Validation for Input Fields
  - Complete Manifest Specification and Schema Reference
- **v0.11** – Domain Refactoring & REST API (2025-12-11)
  - DeployStack REST Endpoint for Stack Deployment via API
  - SignalR DeploymentHub for Real-time Progress Updates
  - StackValidationInfo for Pre-deployment Validation
  - Maintenance Observer Integration
  - Bounded Contexts Documentation
  - Domain Events for Deployment context (DeploymentStarted, DeploymentCompleted, etc.)
  - RuntimeStackConfig Entity in Deployment Context
  - Structured ServiceTemplate, VolumeDefinition, NetworkDefinition
  - ProductId navigation from Stack detail to Catalog
  - Deployment API uses stackId instead of raw YAML
- **v0.12** – Domain Refactoring (DDD) - Phase 2 (2025-12-13)
  - Removed `MaintenanceObserver` from `StackDefinition` (belongs to ProductDefinition)
  - Infrastructure services don't publish domain events (Application layer responsibility)
  - `IStackSourceProvider.LoadStacksAsync` returns `StackDefinition` directly
  - Infrastructure reorganization by technical concerns (DataAccess, Docker, Security assemblies)
  - Removed obsolete `StackVariable` class - use `Variable` instead
  - Removed unused `StackDefinitionLoaded` and `StackSourceSynced` events
  - `Variable` record with comprehensive UI metadata (Type, Pattern, Options, Min/Max, Group, Order)
  - Built-in `Validate()` method for type-specific validation
  - Clean build with 0 warnings (fixed CS0105, CS8602, CS8604)
- **v0.13** – Health Monitoring (2025-12-15)
  - Load Health Check Configuration from RSGO Manifest
  - `ServiceHealthCheck` extended with HTTP/TCP fields (type, path, port, expectedStatusCodes, https)
  - `HealthCheckConfigs` collection in Deployment aggregate
  - Health configs extracted during deployment and persisted to database
  - `HealthCollectorService` uses health configs for HTTP/TCP checks
  - 41 unit tests + 15 integration tests with TestContainers
- **v0.14** – Health Dashboard, Upgrade & Rollback (2025-12-19)
  - Stack Health Dashboard (UI for health status visualization)
  - Health History with 24h retention and configurable intervals
  - Stack Upgrade (Update deployed stacks to newer catalog versions)
  - SemVer-based version comparison
  - Environment-level operation locking (one operation per environment)
  - Pre-upgrade snapshot for recovery
  - Deployment Rollback (Manual recovery after failed upgrades)
  - Only available after failed upgrade (not for downgrades)
  - Single snapshot per deployment (previous version only)
  - Product authors can disable via `metadata.rollback.enabled: false`
- **v0.15** – Multi-Version Catalog, Registry Management & Git Stack Sources (2025-01-12)
  - Settings Page with Registry Management UI
  - CRUD for Docker Registries (Name, URL, Username, Password)
  - Image Patterns for automatic credential matching (glob-style: `library/*`, `ghcr.io/**`)
  - Default registry support
  - Database-first credential resolution during deployment
  - 64 integration tests (Endpoints + Repository)
  - Multi-Version Catalog Support
  - Multiple versions per product in catalog
  - New `metadata.productId` field for grouping versions across sources
  - `ProductDefinition.GroupId` for version grouping (uses productId or falls back to sourceId:name)
  - `IProductCache` extended with version-aware methods (GetProductVersions, GetAvailableUpgrades, etc.)
  - Cross-source upgrade support (e.g., local → git repository)
  - Version dropdown in deployment UI for selecting specific version
  - Upgrade detection shows all available higher versions
  - Git Repository Stack Source Provider (Load Stacks from Git Repos)
  - Stack Sources Management UI (Add/Edit/Delete sources, credentials for private repos)
- **v0.16** – Remove Deployment + Progress Notifications (2026-01-20)
  - Remove Deployment page with SignalR progress notifications
  - DDD Redesign and Rollback Progress Notifications
- **v0.17** – TLS & Certificate Management + Release Info (2026-01-22)
  - Settings UI refactoring with tab navigation (General, TLS, Registries, Stack Sources)
  - TLS Certificate Management
    - Self-signed certificate (auto-generated on first start)
    - Custom certificate upload (PFX and PEM formats)
    - Certificate info display (subject, issuer, expiry, thumbprint)
  - Let's Encrypt Integration
    - HTTP-01 Challenge (port 80 validation)
    - DNS-01 Challenge with Manual and Cloudflare provider
    - Automatic certificate renewal (30 days before expiry)
    - Staging mode for testing
  - Reverse Proxy Support
    - SSL Termination mode (proxy handles HTTPS, backend receives HTTP)
    - SSL Passthrough mode (proxy forwards encrypted traffic)
    - Re-Encryption mode (proxy terminates and re-encrypts)
    - ForwardedHeaders middleware configuration
    - X-Forwarded-For/Proto/Host header processing
  - Release Info in Sidebar
    - Version display (current server version)
    - Update notification banner (when new version available)
    - Link to GitHub release notes
    - Dismissable notification with localStorage persistence
  - Public Website TLS Documentation (DE/EN)
  - Stack Sources Git Credentials Support
  - Registry Management improvements

## Planned

### v0.18 – Init Container UI/UX Improvements
- Deployment removal status feedback (show progress when removing containers)
- Real-time init container logs during deployment (visible in UI)
- Separate init container counting (not counted as regular services)
- Init containers excluded from health monitoring (only shown during deployment)
- Optional: Automatic cleanup of exited init containers after successful deployment

### v0.19 – Docker Volumes Management
- Docker Volumes View (List All Volumes per Environment)
- Volume Details (Size, Mount Points, Labels)
- Create/Delete Volumes
- Detect Orphaned Volumes

### v0.20 – Metrics & Audit
- Metrics & Alerting
- Audit Logs

### v0.21 – CI/CD Integration
- Webhooks for External CI/CD Systems
- API for Automated Deployments

### v0.22 – Multi-User Support
- User Management UI
- Create/Edit Users
- Password Reset Flow

### v0.23 – Feature Flags
- Feature Flags UI in Admin
- Feature Toggle at Organization Level
- Environment Variables for Feature Flags

### v1.0 – Production Ready
- Review and Complete Documentation
- Code Cleanup and Refactoring
- Performance Optimizations

## Post v1.0

### Future Features
- Advanced Connection Mode (Per-Context Connections)
- Plugin System
- Multi-Node Support (Multiple Docker Hosts per Environment)
- Kubernetes Environment Support

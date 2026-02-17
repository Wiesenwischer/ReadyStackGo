# Roadmap

Feature backlog and release history for ReadyStackGo.
Planned items are organized as **Epics** (thematic feature groups) — ordered by priority (top = next).
Release version numbers are assigned when an Epic ships, not during planning.

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
- **v0.18** – Init Container UI/UX Improvements (2026-02-10)
  - Separate Init Container Counting in Deployment Progress
  - Real-time Init Container Log Streaming via SignalR
  - Init Containers Excluded from Health Monitoring
  - Automatic Init Container Cleanup After Successful Init Phase
- **v0.19** – CI/CD Integration (2026-02-11)
  - API Key Domain Model with SHA-256 Hashed Key Storage
  - API Key Authentication Handler with Multi-Scheme Auth (PolicyScheme)
  - API Key Management CRUD Endpoints and Settings UI
  - Redeploy Webhook (`POST /api/hooks/redeploy`) for Fresh Image Deployments
  - Upgrade Webhook (`POST /api/hooks/upgrade`) with Catalog Version Resolution
  - Catalog Sync Webhook (`POST /api/hooks/sync-sources`)
  - Fine-grained Permission System per API Key (Redeploy, Upgrade, SyncSources)
  - Optional Environment-Scope for API Keys
  - Pipeline Examples for curl, GitHub Actions, and Azure DevOps
- **v0.20** – Deploy Hook Improvements & Bugfixes (2026-02-12)
  - Idempotent Deploy Hook with Variable Default Value Fixes
  - Deploy Permission Option in API Key Creation UI
  - WebUI Headless Refactoring Specification
- **v0.21** – UX Improvements (2026-02-12)
  - Copy-to-Clipboard Stack ID Button on Catalog StackCard
- **v0.22** – Self-Update (2026-02-13)
  - UI-Triggered Self-Update via Helper Container Pattern
  - Version Detection via GitHub Releases API
  - Assembly Version Baking in Dockerfile (GIT_SEMVER Build Arg)
  - Helper Container (`rsgo-updater`) for Zero-Downtime Container Swap
  - Maintenance Page Served by Helper During Container Restart
  - Self-Update Documentation (DE/EN) and Landing Page Highlight
- **v0.23** – Self-Update UX (2026-02-13)
  - Dedicated Full-Page Update Status Screen with Progress Display
  - Version Transition Badge (Current → Target Version)
  - Error State with Retry and Back-to-Dashboard Options
  - Helper Container Maintenance Page Restyled with RSGO Branding
- **v0.24** – Preconfigured Sources, Registry Wizard & Setup Flow Redesign (2026-02-15)
  - Curated Source Registry (Embedded JSON Catalog of Known Git/Local Sources)
  - Wizard Stack Source Selection (Opt-in Instead of Auto-Creation)
  - Add From Catalog in Stack Source Settings (One-Click Add from Registry)
  - Image Reference Extraction from Synced Stack Manifests (Auto-Detect Host + Namespace)
  - Container Registry Detection Endpoint (Grouped by Host + Pattern)
  - Wizard Step for Container Registries (Two-Column Verified/Action-Required Layout)
  - Credential Verification via Docker Registry v2 API Token Flow
  - Two-Phase Setup: Secure Admin Creation (Phase 1) + Guided Onboarding (Phase 2)
  - Auto-Login After Admin Creation (JWT in CreateAdmin Response)
  - Wizard Simplified to Single Step (Admin Only, 5-Min Timeout)
  - Mandatory Onboarding Flow: Organization → Environment → Sources → Registries
  - Onboarding Status API (Authenticated, No Timeout Dependency)
- **v0.25** – Docker Volumes Management (2026-02-16)
  - Docker Volumes View (List All Volumes per Environment)
  - Volume Details (Size, Mount Points, Labels, Container References)
  - Create/Delete Volumes with Driver Selection
  - Orphaned Volume Detection and Bulk Cleanup
- **v0.26** – Container Management Improvements (2026-02-17)
  - Container Remove Action with Force Flag and Safety Checks
  - Container Context Endpoint (Stack/Product/Deployment Resolution)
  - Three Container Views (List, Stack-Grouped, Product-Grouped)
  - Orphaned Container Detection (RSGO-Labeled Without Active Deployment)
  - Unmanaged Container Group (External Containers Without RSGO Labels)
  - Dedicated Delete Confirmation Pages (Stack Sources, Environments)
  - ams.project Stack Source in Curated Registry
  - External Network Auto-Creation for Multi-Stack Products
- **v0.27** – Product Deployment (2026-02-17)
  - ProductDeployment Aggregate Root with ProductStackDeployment Child Entities
  - Product-Level State Machine (Deploying → Running / PartiallyRunning / Failed → Removing → Removed)
  - Deploy Product Flow (Orchestrated N-Stack Deployment with Shared Variable Wizard)
  - Upgrade Product Flow (Cross-Stack Version Upgrade with Variable Merging)
  - Check Upgrade Availability (Catalog Version Comparison with New/Removed Stack Detection)
  - Remove Product Flow (Reverse-Order Multi-Stack Removal)
  - Product Deployment Status in Catalog (Per-Stack Deploy State on ProductDetail)
  - SignalR Multi-Stack Progress (Per-Stack + Overall Progress Events)
  - Health-Sync Background Service (Eventual Consistency Between Product and Stack Status)
  - ContinueOnError Flag for Partial Failure Tolerance
  - Three-Tier Variable System (Stack Defaults → Shared Variables → Per-Stack Overrides)

---

## Planned

Epics are listed in priority order. Top = next.

### Epic: Notifications & System Info

**Phase 1** (next)
- In-Memory Notification Store (max 50, transient, no DB)
- Notification API Endpoints (list, unread-count, mark-read, dismiss)
- NotificationDropdown with Real Data (Bell Icon, Badge, Polling 60s)
- Update Available Notification (deduplicated per version)
- Source Sync Result Notification (success/warning/error)
- Deployment Result Notification (deploy, upgrade, rollback, remove)
- Settings > System Tab (version info, build info, "Check for updates" button)
- User-Dropdown Version Badge (version display + update indicator)
- Shared `useVersionInfo` Hook (SidebarWidget, UserDropdown, Settings)
- `forceCheck` Query Parameter on Version Endpoint (bypass 24h cache)

**Phase 2** (later)
- Container Health Change Notification (unhealthy, stopped — with throttling)
- API Key First-Use Notification
- TLS Certificate Expiry Notification (30d, 14d, 7d, 3d, 1d staged warnings)

### Epic: Distribution Architecture

**WebUI Monorepo Refactoring**
- pnpm Workspaces Monorepo (packages/core, packages/ui-generic, apps/rsgo-generic)
- @rsgo/core Package (TypeScript types, API clients, ViewModel hooks, SignalR services)
- @rsgo/ui-generic Package (React components, pages, layouts, thin contexts)
- ViewModel Hooks for All Pages (Settings, Deployments, Catalog, Health, Wizard)
- SignalR Service Classes with Thin React Hook Wrappers
- Auth/Environment Services Extracted from React Contexts
- Build Pipeline Adjustment (pnpm in Dockerfile)

**Backend Extension Points**
- ISetupWizardDefinitionProvider (data-driven wizard steps)
- IBootstrapper Pattern (distribution-specific initialization on first start)
- GenericSetupWizardDefinitionProvider + GenericBootstrapper (default implementations)
- NuGet Meta-Package ReadyStackGo.Core (Domain + Application + Infrastructure)
- Multi-Assembly FastEndpoints Endpoint Discovery
- Distribution Architecture Documentation

### Epic: OCI Stack Bundles

**Phase 1 — Format + Source**
- OCI Stack Bundle Format Specification (stack.yaml + lock.json + meta.json)
- OciRegistry StackSourceType Extension with Factory Method
- OCI Registry Client (list tags, read manifests, pull layers)
- OciStackSourceProvider (sync tags → parse stackId + version → populate cache)
- OCI Source UI in Add Stack Source Flow

**Phase 2 — Import + CI/CD**
- ImportStackSource for Local Stack Snapshots (from OCI, Git, Upload)
- OCI Import Flow (pull bundle, extract, store via ImportStackSource)
- Lock-file Based Deployment (prefer image@digest from lock.json)
- CI/CD Tooling and Documentation (Dockerfile template, ORAS examples)

### Epic: Stack Marketplace

**Browse**
- StackCatalogSource Domain Model (embedded, git-json, http-json sources)
- StackCatalogEntry Domain Model (name, slug, description, category, tags, logo, registry hints)
- Catalog Source Providers (embedded JSON, Git repo with stack-catalog.json)
- Marketplace API Endpoints (list, detail, search, filter, sync)
- Marketplace Browse UI (tile grid, search, category filter)
- Marketplace Detail Page (markdown description, registry requirements check)

**Install Flow**
- StackInstallation Domain Model (org + environment scoped)
- Install from Marketplace Endpoint (registry prerequisite validation)
- Install Flow UI (org/env selection, registry check, variable resolution, deploy)
- Installation Status in Marketplace Tiles and Detail Pages

### Epic: Metrics & Audit
- Metrics & Alerting
- Audit Logs

### Epic: Multi-User Support
- User Management UI
- Create/Edit Users
- Password Reset Flow

### Epic: Feature Flags
- Feature Flags UI in Admin
- Feature Toggle at Organization Level
- Environment Variables for Feature Flags

### v1.0 – Production Ready
- Review and Complete Documentation
- Code Cleanup and Refactoring
- Performance Optimizations

---

## Post v1.0

### Future Epics
- Advanced Connection Mode (Per-Context Connections)
- Plugin System
- Multi-Node Support (Multiple Docker Hosts per Environment)
- Kubernetes Environment Support

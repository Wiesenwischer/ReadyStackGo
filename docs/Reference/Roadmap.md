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
  - Catalog Bounded Context with Sources, Stacks, Manifests, Events
  - Domain Events: `StackDefinitionImported`, `RuntimeConfigImported`, `StackSourceSynced`
  - RuntimeStackConfig Entity in Deployment Context
  - StackManagement namespace removed (merged into Catalog)
  - Structured ServiceTemplate, VolumeDefinition, NetworkDefinition
  - ProductId navigation from Stack detail to Catalog
  - Deployment API uses stackId instead of raw YAML

## Planned

### v0.12 – Domain Refactoring (DDD) - Phase 2
Complete event-driven architecture as specified in [Bounded-Contexts.md](../Architecture/Bounded-Contexts.md).

**Event-Driven Data Flow**
- Publish `StackDefinitionLoaded` event when YAML is parsed in Catalog
- Publish `RuntimeConfigLoaded` event for maintenance/health config
- Event handlers in Deployment context receive runtime-relevant data
- Event bus infrastructure (MediatR notifications or similar)

**Context Isolation**
- Remove `MaintenanceObserver` from `StackDefinition` (belongs to RuntimeConfig)
- Deployment context receives config via events, not direct Catalog references
- Clean separation: Catalog owns YAML/parsing, Deployment owns runtime behavior

### v0.13 – Health Monitoring + Rollback
- Container Health Monitoring (Status, Health, RestartCount)
- Stack Health Dashboard
- Deployment Rollback (Restore Previous Stack Version)

### v0.14 – Registry Management + Git Stack Sources
- Settings Page for Registry Management
- Multiple Docker Registries Configurable (Name, URL, Username, Password)
- GitRepository StackSourceProvider (Load Stacks from Git Repos)

### v0.15 – Release & TLS Management
- Release Management UI
- Custom TLS Certificates

### v0.16 – Metrics & Audit
- Metrics & Alerting
- Audit Logs

### v0.17 – Docker Volumes Management
- Docker Volumes View (List All Volumes per Environment)
- Volume Details (Size, Mount Points, Labels)
- Create/Delete Volumes
- Detect Orphaned Volumes

### v0.18 – CI/CD Integration
- Webhooks for External CI/CD Systems
- API for Automated Deployments

### v0.19 – Multi-User Support
- User Management UI
- Create/Edit Users
- Password Reset Flow

### v0.20 – Feature Flags
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

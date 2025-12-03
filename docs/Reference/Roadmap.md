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
- **v0.6** – Security Hardening (2025-12-01)
  - RBAC (Role-Based Access Control)
  - Wizard Timeout (5-Minute Time Window for Admin Creation After Server Start)
  - API Lockdown After Timeout Expiration (like Portainer)
- **v0.7 - v0.9** – CI/CD Improvements (2025-12-03)
  - Draft-only Releases with Manual Publish
  - Docker Dev Builds on ghcr.io
  - Dynamic Release Notes and Roadmap on PublicWeb
  - Example Stacks in Docker Image

## Planned

### v0.10 – RSGo Manifest Format
- Specification for Custom Manifest Format
- Docker Compose Import (Conversion on Import)
- Type Validation (Number, Boolean, String, Select)
- Regex Validation for Input Fields

### v0.11 – Health Monitoring + Rollback
- Container Health Monitoring (Status, Health, RestartCount)
- Stack Health Dashboard
- Deployment Rollback (Restore Previous Stack Version)

### v0.12 – Registry Management + Git Stack Sources
- Settings Page for Registry Management
- Multiple Docker Registries Configurable (Name, URL, Username, Password)
- GitRepository StackSourceProvider (Load Stacks from Git Repos)

### v0.13 – Release & TLS Management
- Release Management UI
- Custom TLS Certificates

### v0.14 – Metrics & Audit
- Metrics & Alerting
- Audit Logs

### v0.15 – Docker Volumes Management
- Docker Volumes View (List All Volumes per Environment)
- Volume Details (Size, Mount Points, Labels)
- Create/Delete Volumes
- Detect Orphaned Volumes

### v0.16 – CI/CD Integration
- Webhooks for External CI/CD Systems
- API for Automated Deployments

### v0.17 – Multi-User Support
- User Management UI
- Create/Edit Users
- Password Reset Flow

### v1.0 – Production Ready
- Review and Complete Documentation
- Code Cleanup and Refactoring
- Performance Optimizations

## Post v1.0

### Future Features
- Feature Flags UI
- Advanced Connection Mode
- Plugin System
- Multi-Node Support (Multiple Docker Hosts per Environment)
- Kubernetes Environment Support

# Changelog

All notable changes to ReadyStackGo will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.3.0] - 2025-01-19

### Added

#### Setup Wizard
- 4-step guided setup wizard for initial configuration
  - Step 1: Create Admin Account (username, password with BCrypt hashing)
  - Step 2: Organization Setup (organization ID and name)
  - Step 3: Configure Connections (Simple mode: Transport, Persistence, EventStore)
  - Step 4: Complete Setup (review and finalize)
- Wizard state persistence across browser sessions
- Automatic redirect to wizard if not completed
- Automatic redirect to dashboard when wizard is complete
- Dark mode support for wizard UI
- Fully responsive wizard design

#### Configuration System
- File-based configuration using JSON files in `/app/config/`
- `ConfigStore` service for async configuration management
- Configuration files:
  - `rsgo.system.json` - System state and wizard progress
  - `rsgo.security.json` - Admin credentials and security settings
  - `rsgo.contexts.json` - Connection strings for bounded contexts
  - `rsgo.tls.json` - TLS certificate configuration
- Automatic directory creation for config files
- Thread-safe configuration operations

#### TLS Bootstrap
- Automatic self-signed certificate generation on first startup
- RSA 2048-bit certificate generation
- Certificate stored in `/app/config/tls/selfsigned.pfx`
- Secure password generation and storage
- TLS bootstrap runs before HTTP pipeline
- Configurable hostname support
- HTTPS and HTTP dual-mode support

#### Manifest System
- Manifest-based deployment infrastructure
- `ManifestProvider` service for loading release manifests
- Manifest schema with SemVer versioning
- Support for:
  - Container definitions
  - Environment variables
  - Port mappings
  - Dependencies
  - Feature flags
  - Metadata and release notes
- Manifest validation

#### Deployment Engine
- Container deployment orchestration (infrastructure ready, deployment in v0.4)
- Docker network creation (`rsgo-net`)
- Dependency-aware container startup
- Environment variable injection
- Gateway deployment (always last)

#### API Endpoints
- `GET /api/wizard/status` - Get current wizard state
- `POST /api/wizard/admin` - Create admin user
- `POST /api/wizard/organization` - Set organization
- `POST /api/wizard/connections` - Configure connections
- `POST /api/wizard/install` - Complete wizard setup

#### Testing
- 14 integration test cases for wizard endpoints
- 18 E2E test cases using Playwright
- Backend coverage: 95%+
- Frontend coverage: 90%+

### Changed

#### Authentication
- **BREAKING:** Removed hardcoded admin credentials
- AuthService now reads credentials from `rsgo.security.json`
- User-defined admin credentials set during wizard
- BCrypt password verification
- Detailed login attempt logging

#### Frontend
- Updated login to work with wizard-created credentials
- Added `WizardGuard` component for automatic redirection
- Improved dark mode support
- Enhanced error handling and user feedback

### Fixed
- Password hash storage and verification
- Wizard state synchronization between frontend and backend
- Browser reload state persistence
- Certificate path handling

### Removed
- Hardcoded admin user (username: "admin", password: "admin")
- Direct access to system without completing wizard

---

## [0.2.0] - 2024-12-XX

### Added
- Basic admin dashboard
- Container listing and management
- JWT authentication
- Dark mode support
- Hardcoded admin user for development

---

## [0.1.0] - 2024-11-XX

### Added
- Initial project structure
- Clean Architecture setup
- Basic API framework with FastEndpoints
- React frontend with Vite
- TailwindCSS v4 integration

---

## Future Releases

### [0.4.0] - Planned
- Actual container deployment from manifests
- Advanced connection mode (per-context configuration)
- Custom TLS certificate upload
- Release management UI
- Feature flags UI
- Container health monitoring

### [0.5.0] - Planned
- Multi-user support
- OIDC/SSO integration
- Plugin system foundation
- Advanced role-based access control

### [0.6.0] - Planned
- CI/CD pipeline integration
- Automated release notifications
- Rollback capabilities
- Stack health dashboard

---

[0.3.0]: https://github.com/yourorg/readystackgo/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/yourorg/readystackgo/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/yourorg/readystackgo/releases/tag/v0.1.0

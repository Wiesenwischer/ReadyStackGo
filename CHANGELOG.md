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

#### Multi-Environment Support
- **Environments:** Organizations can have multiple environments (Development, Test, Production)
- **Environment Selector:** UI dropdown to switch between environments
- **Per-Environment Configuration:**
  - Independent connection strings (Transport, Persistence, EventStore)
  - Separate Docker host per environment
  - Environment-specific deployed stacks and containers
- **Wizard Update:** Combined Organization + Default Environment setup step
- **Configuration Files:** Per-environment config files (`rsgo.contexts.{env}.json`)
- **Migration:** Automatic upgrade from v0.3 single-environment to v0.4 multi-environment

#### Environment Management
- Create, update, delete environments via Settings UI
- Test Docker host connectivity before saving
- Default environment designation
- Environment-scoped container and stack views

#### API Enhancements
- `GET /api/environments` - List all environments
- `POST /api/environments` - Create new environment
- `PUT /api/environments/{id}` - Update environment
- `DELETE /api/environments/{id}` - Delete environment
- Environment query parameter for container/stack endpoints

#### Limitations (v0.4)
- Single Docker host per environment (multi-node deferred to v0.5)
- Simple connection mode only (Advanced mode in future release)
- Basic Docker host auth (TLS support in v0.5)

### [0.5.0] - Planned

#### Container Deployment
- Actual stack deployment from manifests
- Docker network creation and management
- Container health monitoring
- Deployment rollback capabilities

#### Database Migration & Multi-User Support
- **SQLite Database:** Migrate from JSON file storage to SQLite for better concurrency and ACID transactions
- **Multi-user support:** Individual user accounts with invitation system
- User session management and activity tracking
- Audit logging for all operations
- Migration tool: `rsgo.system.json` → `readystackgo.db`

#### Multi-Node Support
- Multiple Docker hosts per environment
- Load balancing across nodes
- Node health monitoring
- Distributed container orchestration

#### Authentication & Authorization
- OIDC/SSO integration (optional)
- Role-based access control (RBAC)
- Per-environment access permissions
- API key management

### [0.6.0] - Planned

#### Release Management
- Release management UI
- Feature flags UI
- Stack version comparison
- Automated release notifications

#### Advanced Configuration
- Advanced connection mode (per-context configuration)
- Custom TLS certificate upload
- Let's Encrypt integration
- Environment templates and cloning

### [0.7.0] - Planned

#### CI/CD Integration
- Webhook support for automated deployments
- CI/CD pipeline integration
- Git repository monitoring
- Automated testing integration

#### Operations & Monitoring
- Stack health dashboard
- Container logs aggregation
- Metrics and alerting
- Audit logs per environment

---

[0.3.0]: https://github.com/yourorg/readystackgo/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/yourorg/readystackgo/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/yourorg/readystackgo/releases/tag/v0.1.0

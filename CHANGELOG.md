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
- **Polymorphic Environment Types:** Docker Socket, Docker API, Docker Agent (Portainer-like)
- **Organizations without Environments:** Optional environment creation during setup
- **Environment Management UI:** Create, update, delete environments via Settings
- **Environment Selector:** UI dropdown to switch between environments
- **Per-Environment Docker Hosts:** Each environment connects to separate Docker daemon
- **Environment-Scoped Resources:** Containers and stacks isolated per environment

#### Simplified Wizard (4 Steps → 3 Steps)
- **Removed Step 3:** "Configure Connections" removed from wizard
- **No Mandatory Environments:** Organization can exist without environments
- New Flow: Admin Account → Organization → Complete
- Faster onboarding, less complexity

#### Stack-Specific Configuration System (Docker Compose)
- **Docker Compose Support:** Deploy stacks using standard `docker-compose.yml` files
- **Automatic Variable Detection:** Parse `${VARIABLE}` syntax from compose files
- **Deployment-Time Configuration:** Users provide values when deploying stack
- **Dynamic UI Generation:** Configuration form generated from detected variables
- **Default Value Support:** Extract defaults from `${VAR:-default}` syntax
- **Per-Deployment Storage:** `/app/config/deployments/{env}/{stack}.deployment.json`
- **No Global Connection Strings:** Removed `rsgo.contexts.json`
- **Portainer-Compatible:** Familiar workflow for existing Portainer users

#### Domain Model Improvements
- **Organization Aggregate:** Can exist without environments
- **Environment Entity:** Polymorphic type hierarchy (Strategy Pattern)
  - `DockerSocketEnvironment` (v0.4 - Unix socket / named pipe - ONLY THIS TYPE in v0.4)
  - `DockerApiEnvironment` (Post-v1.0 - TCP with TLS)
  - `DockerAgentEnvironment` (Post-v1.0 - Portainer Edge Agent)
  - `KubernetesEnvironment` (Post-v1.0 - Future)
- **JSON Type Discriminator:** `$type` field for polymorphic serialization
- **Connection Uniqueness:** Enforced via `GetConnectionString()` method

#### API Enhancements
- `GET /api/environments` - List all environments
- `POST /api/environments` - Create new environment
- `PUT /api/environments/{id}` - Update environment
- `DELETE /api/environments/{id}` - Delete environment
- `POST /api/deployments` - Deploy stack with configuration
- `GET /api/deployments/{id}/configuration` - Get deployment config

#### Breaking Changes
- `WizardState.ConnectionsSet` removed (auto-migrated to `Installed`)
- `rsgo.contexts.json` removed (archived as `.v0.3.backup`)
- Organization factory method signature changed: `Create(id, name)` (no environment)

#### Migration from v0.3
- Automatic migration on startup
- `rsgo.contexts.json` archived and values pre-filled in first deployment
- No manual intervention required

### [0.5.0] - Planned

#### Enhanced Stack Format (Custom Manifests)
- **Custom Manifest Format:** Optional enhanced format with validation
- **Type Validation:** Numbers, booleans, strings, select fields
- **Regex Validation:** Enforce URL formats and patterns
- **Required Fields:** Explicit required field marking
- **Sensitive Fields:** Password/secret field marking
- **Documentation:** Display names, descriptions, placeholders
- **Dual Format Support:** Both Docker Compose and Custom Manifest formats supported

#### Database Migration & Multi-User Support
- **SQLite Database:** Migrate from JSON file storage to SQLite for better concurrency and ACID transactions
- **Multi-user support:** Individual user accounts with invitation system
- User session management and activity tracking
- Audit logging for all operations
- Migration tool: `rsgo.system.json` → `readystackgo.db`

#### Container Management Enhancements
- Container health monitoring
- Deployment rollback capabilities
- Multi-container orchestration improvements

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

### [1.0.0] - Planned

#### Production Readiness
- Plugin-System
- Performance Optimierungen
- Comprehensive Documentation
- Security hardening
- Production-grade deployment options

### Post-v2.0 - Future

#### Multi-Node Support
- Multi-Node-Support (mehrere Docker Hosts pro Environment)
- Load balancing across nodes
- Node health monitoring
- Distributed container orchestration

#### Additional Environment Types
- **DockerApiEnvironment:** TCP/HTTP connection with optional TLS
- **DockerAgentEnvironment:** Portainer Edge Agent support
- TLS certificate management for Docker API connections

#### Advanced Orchestration
- Kubernetes Environment Support
- Advanced networking features
- Service mesh integration

---

[0.3.0]: https://github.com/yourorg/readystackgo/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/yourorg/readystackgo/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/yourorg/readystackgo/releases/tag/v0.1.0

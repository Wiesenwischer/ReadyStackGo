
# ReadyStackGo – Complete Specification

## Table of Contents
1. Introduction
2. Goals
3. System Overview
4. Architecture
5. Components
6. Configuration Model
7. Setup Wizard
8. Deployment Pipeline
9. Release Management
10. Feature Flags
11. Security
12. Roadmap

---

# 1. Introduction

ReadyStackGo is a self-hosted deployment and administration system for containerized software stacks. The goal is to enable on-premise customers, partners, and internal teams to easily install, maintain, and update complex microservice systems without having to interact directly with Docker Compose, Kubernetes, or manual deployment scripts.

The core of ReadyStackGo is a **single Admin container** that:
- provides a web UI,
- contains a setup wizard,
- automatically bootstraps TLS,
- processes release manifests,
- installs and updates container stacks,
- manages configuration in a versioned manner,
- and can later support multi-node deployments.

ReadyStackGo is fully offline-capable and only requires:
- Docker on the host,
- access to the Docker Socket.

This makes the system ideal for:
- On-premise operation
- Isolated customer networks
- Edge installations
- Internal development stacks


# 2. Goals

ReadyStackGo is intended to be a fully integrated, easy-to-use, and robust management platform.
The main goals are:

## 2.1 Primary Goals
1. **A single Admin container** manages the entire stack.
2. **Setup Wizard** that makes installations extremely simple for customers.
3. **TLS Automation** (Self-Signed + later Custom Certificates).
4. **Manifest-based Installation & Updates** for entire stacks.
5. **Central Configuration** via rsgo-config (Volume).
6. **Feature Flags** for customer- or organization-specific activations.
7. **Offline Operation**: No cloud connection required.
8. **Extensibility**: BFFs, APIs, Business Contexts, Gateways.

## 2.2 Non-Goals (for initial releases)
- No Kubernetes.
- No focus on Multi-Tenancy SaaS (Organization instead).
- No setup of external databases.
- No automatic clustering.
- No dynamic container orchestration like Swarm/K8s.

---

# 3. System Overview

ReadyStackGo consists of three layers:

1. **Admin Container (ReadyStackGo itself)**
   - UI
   - Wizard
   - TLS Management
   - Manifest Loader
   - Deployment Engine
   - Config Store

2. **Gateway / BFF Layer**
   - BFF Desktop
   - BFF Web
   - Public API Gateway

3. **Business Contexts (AMS Microservices)**
   - Project
   - Memo
   - Discussion
   - Identity
   - etc.

All systems are defined as Docker containers and specified through manifests.

---

# 4. Architecture

## 4.1 Layer Model (Clean Architecture)

- **API Layer**
  - Endpoints (FastEndpoints)
  - Authentication / Roles
  - Input/Output DTOs

- **Application**
  - Dispatcher
  - Commands & Queries
  - Orchestration / Policies

- **Domain**
  - Pure Business Objects
  - Value Objects
  - Policies

- **Infrastructure**
  - Docker Service
  - TLS Service
  - ConfigStore
  - ManifestProvider

- **Frontend**
  - React + Tailwind + TailAdmin
  - Wizard
  - Admin UI



# 5. Component Overview

ReadyStackGo consists of several clearly separated components that work together to manage the entire stack.
Each component is replaceable, testable, and extensible.

## 5.1 ReadyStackGo Admin (Central Container)

The Admin container provides:

- Web UI (React + TailAdmin)
- API (FastEndpoints)
- Setup Wizard
- TLS Handling (Bootstrap & Management)
- Manifest Management
- Deployment Engine
- Config Store
- Logs & Status Queries

The Admin container is the **only container** that a customer needs to start manually.

---

## 5.2 Gateway Layer

The manifest stack can contain any number of gateways, typically:

- **Edge Gateway** (TLS Termination, Reverse Proxy)
- **Public API Gateway**
- **BFF Desktop**
- **BFF Web**

These gateways are always deployed **last** to ensure that the underlying services are already running.

---

## 5.3 Business Contexts (Microservices)

Examples:

- Project
- Memo
- Discussion
- Identity
- Notification
- Search
- Files
- etc.

Each context:

- is a container,
- uses the same stack lifecycle,
- has its own versions,
- can have connections (DB/Eventstore/Transport),
- is defined in the manifests.

---

## 5.4 Configuration Components

### 5.4.1 Config Store (rsgo-config Volume)

Contains all persistent data:

```
rsgo.system.json
rsgo.security.json
rsgo.tls.json
rsgo.contexts.json
rsgo.features.json
rsgo.release.json
rsgo.nodes.json
tls/<certs>
```

### 5.4.2 Manifest Provider

- Loads release manifests from the filesystem or later from a registry.
- Validates versions and schema.

### 5.4.3 Deployment Engine

- Creates the deployment plan per manifest
- Start/Stop/Remove/Update containers
- Health checks
- Maintains rsgo.release.json

### 5.4.4 TLS Engine

- Generates Self-Signed certificate during bootstrap
- Allows later uploading of custom certificates

---

# 6. Configuration Model (Detail)

The entire configuration is clearly structured.

## 6.1 rsgo.system.json

Stores:

- Organization
- Ports
- Base URL
- Docker network name
- Wizard status
- Deployment mode
- Node configuration (Single Node for now)

### Example

```json
{
  "organization": {
    "id": "customer-a",
    "name": "Customer A"
  },
  "baseUrl": "https://ams.customer-a.com",
  "httpPort": 8080,
  "httpsPort": 8443,
  "dockerNetwork": "rsgo-net",
  "mode": "SingleNode",
  "wizardState": "Installed"
}
```

---

## 6.2 rsgo.security.json

Stores:

- Local Admin (Password hashed, salted)
- Role model (Admin/Operator)
- Optional external Identity Provider configuration (OIDC)
- Local Admin fallback toggle

---

## 6.3 rsgo.tls.json

Defines:

- tlsMode: SelfSigned or Custom
- Certificate path
- Port
- httpEnabled
- terminatingContext

---

## 6.4 rsgo.contexts.json

For Simple and Advanced Mode:

### Simple Mode

```json
{
  "mode": "Simple",
  "globalConnections": {
    "transport": "TransportCS",
    "persistence": "Server=.;Database=ams",
    "eventStore": "esdb://..."
  },
  "contexts": {
    "project": {},
    "memo": {},
    "discussion": {},
    "identity": {},
    "bffDesktop": {},
    "bffWeb": {},
    "publicApi": {}
  }
}
```

### Advanced Mode

```json
{
  "mode": "Advanced",
  "contexts": {
    "project": {
      "connections": {
        "transport": "...",
        "persistence": "...",
        "eventStore": "..."
      }
    }
  }
}
```

---

## 6.5 rsgo.features.json

Global Feature Flags:

- Cross-context
- True/False Values
- Passed to containers as `RSGO_FEATURE_*`

---

## 6.6 rsgo.release.json

Stores the state after deployment:

```json
{
  "installedStackVersion": "4.3.0",
  "installedContexts": {
    "project": "6.4.0",
    "memo": "4.1.3"
  },
  "installDate": "2025-03-01T10:12:00Z"
}
```

---



# 7. Setup Wizard (Detail)

The Setup Wizard is intentionally kept compact and guides a new user through the minimally necessary setup steps.
All advanced features can be adjusted later in the Admin UI.

The Wizard is only active when:

```
rsgo.system.json.wizardState != "Installed"
```

---

## 7.1 Flow Overview

1. **Create Admin**
2. **Create Organization**
3. **Set Connections (Simple Mode)**
4. **Summary**
5. **Install Stack**
6. Wizard locks itself → Login becomes active

---

## 7.2 Step 1 – Admin

User enters:

- Username
- Password

The API stores:

- Password hashed
- Salt generated
- Role: admin
- wizardState = "AdminCreated"

Storage in `rsgo.security.json`.

---

## 7.3 Step 2 – Organization

Data:

- ID (technical)
- Name (display)

Stores in `rsgo.system.json`:

```json
{
  "organization": { "id": "customer-a", "name": "Customer A GmbH" }
}
```

wizardState = "OrganizationSet".

---

## 7.4 Step 3 – Connections (Simple Mode)

User enters:

- Transport Connection String
- Persistence Connection String
- EventStore Connection String (optional)

rsgo creates:

```json
"mode": "Simple",
"globalConnections": { ... }
```

wizardState = "ConnectionsSet".

---

## 7.5 Step 4 – Summary

The Wizard shows:

- Organization
- Connections
- Suggested Manifest (e.g., latest version)
- All Contexts

---

## 7.6 Step 5 – Installation

The API:

1. reads the manifest
2. creates the deployment plan
3. stops old containers (if present)
4. creates/starts containers
5. writes `rsgo.release.json`
6. sets wizardState = "Installed"

After this step, the Wizard is deactivated.

---

# 8. Deployment Pipeline (Detail)

The deployment process is the heart of ReadyStackGo.

## 8.1 Steps Overview

1. Load Manifest
2. Load Configs (`rsgo.system.json`, `rsgo.contexts.json`, `rsgo.features.json`)
3. Generate EnvVars
4. Ensure Docker Network (`rsgo-net`)
5. Execute Context-wise Deployment
6. Deploy Gateway last
7. Save Release Status

---

## 8.2 EnvVar Generation

The following EnvVars are generated:

### 1. System
- `RSGO_ORG_ID`
- `RSGO_ORG_NAME`
- `RSGO_STACK_VERSION`

### 2. Feature Flags
- `RSGO_FEATURE_<name>=true/false`

### 3. Connections
Depending on Simple/Advanced Mode:

- `RSGO_CONNECTION_transport`
- `RSGO_CONNECTION_persistence`
- `RSGO_CONNECTION_eventStore`

### 4. Manifest-specific Variables
e.g.:

- `ROUTE_DESKTOP=http://ams-bff-desktop`
- `ROUTE_PUBLIC_API=http://ams-public-api`

---

## 8.3 Container Deployment Order

For each manifest definition:

1. **Stop & Remove** (if container exists)
2. **Create & Start** with:
   - Image
   - EnvVars
   - Ports
   - Network
   - Name
3. **Health check** (optional)
4. Deploy Gateway **last**

---

## 8.4 Error and Rollback Strategy

### On Error During Deployment:

- Error is logged
- Further deployment process stops
- User receives:
  - Error code
  - Error description
- rsgo.release.json is NOT updated

Rollback V1 (simple):

- Previous containers remain untouched
- User can via the UI:
  - Retry deployment
  - Install older release version

---

## 8.5 rsgo.release.json Update

Example after deployment:

```json
{
  "installedStackVersion": "4.3.0",
  "installedContexts": {
    "project": "6.4.0",
    "memo": "4.1.3",
    ...
  },
  "installDate": "2025-04-12T10:22:00Z"
}
```

---

# 9. Release Management

## 9.1 Manifest Files

A manifest defines:

- Stack version
- Context versions
- Context-specific EnvVars
- Gateway configuration
- Feature defaults
- Dependencies

---

## 9.2 Example Manifest (detailed)

```json
{
  "manifestVersion": "1.0.0",
  "stackVersion": "4.3.0",
  "schemaVersion": 12,
  "gateway": {
    "context": "edge-gateway",
    "protocol": "https",
    "publicPort": 8443,
    "internalHttpPort": 8080
  },
  "contexts": {
    "project": {
      "image": "registry/ams.project-api",
      "version": "6.4.0",
      "containerName": "ams-project",
      "internal": true
    },
    "bffDesktop": {
      "image": "registry/ams.bff-desktop",
      "version": "1.3.0",
      "containerName": "ams-bff-desktop",
      "internal": false,
      "env": {
        "ROUTE_PROJECT": "http://ams-project"
      }
    }
  },
  "features": {
    "newColorTheme": { "default": true },
    "discussionV2": { "default": false }
  }
}
```

---

## 9.3 Release Lifecycle

1. **CI creates new Manifest**
2. **ReadyStackGo loads Manifest**
3. **Admin selects Manifest**
4. **Deployment Engine executes installation**
5. **Release saved**



# 10. Feature Flags

Feature Flags allow dynamically enabling or disabling business functionality – cross-context and centrally controlled.

## 10.1 Properties
- Globally valid (not limited to one context).
- Passed as Environment Variables to every container.
- Can be extended per organization later.
- Stored in `rsgo.features.json`.
- Can later have default values in the manifest.

## 10.2 Example `rsgo.features.json`

```json
{
  "newColorTheme": true,
  "discussionV2": false,
  "memoRichEditor": true
}
```

-> Containers receive:
```
RSGO_FEATURE_newColorTheme=true
RSGO_FEATURE_discussionV2=false
RSGO_FEATURE_memoRichEditor=true
```

## 10.3 Feature Flags in Manifest

Contexts can define default values in the manifest:

```json
"features": {
  "newColorTheme": { "default": true },
  "discussionV2": { "default": false }
}
```

These values can be overridden by the admin later.

## 10.4 UI (Admin Area)

The administration sees a list of all features:

| Feature Name       | Active | Description |
|--------------------|--------|-------------|
| newColorTheme      | ✔️    | New UI Theme |
| discussionV2       | ❌    | New Discussion API |
| memoRichEditor     | ✔️    | Rich Text Editor |

Each change is saved in `rsgo.features.json`.

---

# 11. Security

ReadyStackGo must be robust and secure for both on-premise and production environments.

## 11.1 Authentication Modes

1. **Local Authentication**
   - Default
   - Username + Password
   - Stores password as Hash + Salt

2. **External Identity Provider (OIDC)**
   - Keycloak
   - ams.identity
   - Azure AD (later)
   - Roles via Claims

3. **Local Admin Fallback**
   - Active or deactivatable
   - Guarantees login even if IdP fails

---

## 11.2 Authorization / Roles

### Roles

- **admin**
  - Can perform deployments
  - Can change configuration
  - Can manage TLS
  - Can adjust Feature Flags

- **operator**
  - Can only start/stop containers
  - Can view logs

### Role Source

- With Local Auth:
  - Roles in `rsgo.security.json`

- With OIDC:
  - From Claim (e.g., `"role" : "rsgo-admin"`)

---

## 11.3 Password Security

- Password hashing via PBKDF2 or Argon2
- Password salt generated per user
- No plaintext password storage

---

## 11.4 TLS / HTTPS

### Bootstrap
- First start generates Self-Signed Certificate
- Certificate stored under `/app/config/tls/`

### Custom Certificate
- Admin UI allows PFX upload
- Storage in `rsgo.tls.json`

### TLS Termination
- Occurs in the **Gateway**, not in the Admin container itself
- Advantage: HTTP can be used internally between containers

---

## 11.5 API Security

- JWT or Cookie Token
- Anti-CSRF (with Cookies)
- Rate Limiting (optional later)
- Secure Headers, HSTS

---

# 12. Roadmap (detailed)

This roadmap covers only the main lines; modules can be developed in parallel.

## 12.1 Version v0.1 – Container Management MVP

- API: List, Start, Stop
- DockerService Base
- UI: Container Overview
- No Login
- No Wizard

## 12.2 Version v0.2 – Local Admin & Hardcoded Stack

- Login/Logout
- Local Authentication
- Rights Management
- Dashboard
- Hardcoded Stack Deployment

## 12.3 Version v0.3 – Bootstrap, Wizard, TLS

- Self-Signed TLS
- Wizard with 4 steps
- First Manifest Loading
- Basic Deployment Engine

## 12.4 Version v0.4 – Release Management

- Manifest Management
- Versioning
- Update/Upgrade Flow
- Release Status Display

## 12.5 Version v0.5 – Admin Comfort

- TLS Upload
- Feature Flags UI
- Advanced Connections
- Node Configuration (Single Node)

## 12.6 Version v1.0 – Multi Node (Cluster-capable)

- Actively use rsgo.nodes.json
- Per Node: Roles (Gateway Node, Compute Node)
- Node Discovery
- Remote Docker Hosts

---

# → End of Complete Specification

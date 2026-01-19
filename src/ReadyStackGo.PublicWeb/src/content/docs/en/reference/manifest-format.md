---
title: RSGo Manifest Schema
description: Complete reference for the RSGo Manifest Format - the native stack definition format for ReadyStackGo
---

The RSGo Manifest is the native stack definition format for ReadyStackGo. It provides type-validated variables, rich metadata, multi-stack support, and modular composition through includes.

## Document Structure

```yaml
version: "1.0"                    # Format version (optional)

metadata:                         # Product/Stack metadata
  name: My Product
  productVersion: "1.0.0"         # Makes this a Product (deployable)
  ...

sharedVariables:                  # Variables shared across all stacks (Multi-Stack only)
  REGISTRY: ...

variables:                        # Variables for this stack (Single-Stack or Fragment)
  PORT: ...

stacks:                           # Stack definitions (Multi-Stack only)
  api:
    include: api.yaml
  db:
    services: ...

services:                         # Service definitions (Single-Stack or Fragment)
  app: ...

volumes:                          # Volume definitions
  data: {}

networks:                         # Network definitions
  frontend: {}
```

---

## Manifest Types

### 1. Product (Single-Stack)

A single-stack product contains services directly at the root level:

```yaml
metadata:
  name: Whoami
  productVersion: "1.0.0"         # ← Makes it a Product

variables:
  PORT:
    type: Port
    default: "8080"

services:
  whoami:
    image: traefik/whoami:latest
    ports:
      - "${PORT}:80"
```

### 2. Product (Multi-Stack)

A multi-stack product contains multiple stacks with shared variables:

```yaml
metadata:
  name: Enterprise Platform
  productVersion: "3.1.0"         # ← Makes it a Product

sharedVariables:                  # ← Available to all stacks
  REGISTRY:
    type: String
    default: myregistry.io

stacks:
  api:
    include: api.yaml             # ← External fragment
  monitoring:
    services:                     # ← Inline stack
      prometheus: ...
```

### 3. Fragment

A fragment has no `productVersion` and can only be included from a product:

```yaml
# identity.yaml - Fragment (no productVersion)
metadata:
  name: Identity Access
  description: Identity Provider

variables:
  CERT_PATH:
    type: String
    default: /etc/ssl/certs/identity.pfx

services:
  identity-api:
    image: ${REGISTRY}/identity:latest   # ← Uses shared variable
```

---

## Metadata

### Product Metadata

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `name` | string | **Yes** | Display name of the product |
| `productId` | string | No | Unique product identifier for grouping versions across sources. Uses reverse domain notation (e.g., `com.example.myproduct`). If not set, defaults to `sourceId:name`. |
| `description` | string | No | Description of what the product does |
| `productVersion` | string | **Yes*** | Version string (e.g., "3.1.0"). *Required for Products |
| `author` | string | No | Author or maintainer name |
| `documentation` | string | No | URL to documentation |
| `icon` | string | No | URL to icon image for UI display |
| `category` | string | No | Category for filtering (e.g., "Database", "CMS") |
| `tags` | string[] | No | Tags for search and filtering |

#### Product Identification

The `productId` field is used to group different versions of the same product together, even when they come from different sources (local directory, Git repository, registry).

**Use cases:**
- Migrate a product from local development to a Git repository while maintaining version history
- Allow upgrades between versions from different sources
- Prevent accidental grouping of unrelated products with the same name

**Recommendations:**
- Use reverse domain notation: `com.yourcompany.productname`
- Keep it stable across versions - changing `productId` creates a new product group
- If not specified, RSGO generates an ID as `sourceId:name`

**Example:**

```yaml
metadata:
  name: WordPress
  productId: org.wordpress.stack        # Unique identifier across all sources
  description: Production-ready WordPress stack with MySQL backend
  productVersion: "6.0.0"
  author: ReadyStackGo Team
  documentation: https://docs.example.com/wordpress
  icon: https://example.com/icons/wordpress.png
  category: CMS
  tags:
    - wordpress
    - cms
    - blog
    - mysql
```

### Recommended Categories

| Category | Description |
|----------|-------------|
| `CMS` | Content Management Systems |
| `Database` | Databases and data stores |
| `Monitoring` | Monitoring, logging, and observability |
| `Identity` | Authentication and authorization |
| `Messaging` | Message brokers and queues |
| `Cache` | Caching systems |
| `Storage` | File storage and object storage |
| `Testing` | Test and debug tools |
| `Enterprise` | Enterprise applications |
| `Examples` | Example and demo stacks |

---

## Variables

Variables allow users to configure a product before deployment. They are displayed as form fields in the ReadyStackGo UI.

### Variable Definition

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `label` | string | No | Human-readable label |
| `description` | string | No | Help text shown in UI |
| `type` | string | No | Variable type (default: `String`) |
| `default` | string | No | Default value |
| `required` | boolean | No | Whether the variable must be provided |
| `placeholder` | string | No | Placeholder text for input field |
| `pattern` | string | No | Regex pattern for validation |
| `patternError` | string | No | Error message when pattern fails |
| `options` | array | No | Options for `Select` type |
| `min` | number | No | Minimum value for `Number` type |
| `max` | number | No | Maximum value for `Number` type |
| `group` | string | No | Group name for UI organization |
| `order` | integer | No | Display order within group |

For complete variable type reference, see [Variable Types](/en/reference/variable-types/).

### Variable Grouping

Variables can be organized into groups for better UX:

```yaml
variables:
  # Network Group
  HTTP_PORT:
    label: HTTP Port
    type: Port
    default: "80"
    group: Network
    order: 1

  HTTPS_PORT:
    label: HTTPS Port
    type: Port
    default: "443"
    group: Network
    order: 2

  # Database Group
  DB_HOST:
    label: Database Host
    type: String
    default: localhost
    group: Database
    order: 1
```

---

## Services

Services define the Docker containers to deploy.

### Service Definition

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `image` | string | **Yes** | Docker image (e.g., `nginx:latest`) |
| `containerName` | string | No | Container name (default: `stack_servicename`) |
| `ports` | string[] | No | Port mappings (`host:container`) |
| `environment` | object | No | Environment variables |
| `volumes` | string[] | No | Volume mappings |
| `networks` | string[] | No | Networks to connect |
| `dependsOn` | string[] | No | Service dependencies |
| `restart` | string | No | Restart policy |
| `command` | string | No | Command override |
| `entrypoint` | string | No | Entrypoint override |
| `workingDir` | string | No | Working directory |
| `user` | string | No | User to run as |
| `labels` | object | No | Container labels |
| `healthCheck` | object | No | Health check configuration |

### Service Example

```yaml
services:
  api:
    image: ${REGISTRY}/api:${VERSION}
    containerName: my-api
    ports:
      - "${API_PORT}:8080"
      - "8443:8443"
    environment:
      ASPNETCORE_ENVIRONMENT: ${ENVIRONMENT}
      ConnectionStrings__Database: ${DB_CONNECTION}
      LOG_LEVEL: ${LOG_LEVEL}
    volumes:
      - api_data:/app/data
      - ./config:/app/config:ro
    networks:
      - frontend
      - backend
    dependsOn:
      - database
      - cache
    restart: unless-stopped
    healthCheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      startPeriod: 40s
```

### Port Mappings

```yaml
ports:
  - "8080:80"                    # host:container
  - "${PORT}:80"                 # variable substitution
  - "127.0.0.1:8080:80"          # bind to specific IP
  - "8080-8090:80-90"            # port range
```

### Restart Policies

| Policy | Description |
|--------|-------------|
| `no` | Never restart (default) |
| `on-failure` | Restart on non-zero exit |
| `unless-stopped` | Always restart unless explicitly stopped |
| `always` | Always restart |

### Health Check

```yaml
healthCheck:
  test: ["CMD", "curl", "-f", "http://localhost/health"]
  interval: 30s
  timeout: 10s
  retries: 3
  startPeriod: 40s
```

---

## Volumes

```yaml
volumes:
  # Named volume (managed by Docker)
  app_data: {}

  # Volume with driver options
  db_data:
    driver: local
    driverOpts:
      type: none
      o: bind
      device: /mnt/data

  # External volume (already exists)
  shared_data:
    external: true
```

---

## Networks

```yaml
networks:
  # Default bridge network
  frontend:
    driver: bridge

  # External network (already exists)
  proxy:
    external: true
```

---

## Multi-Stack Products

### sharedVariables

Variables defined in `sharedVariables` are available to all stacks:

```yaml
sharedVariables:
  REGISTRY:
    label: Docker Registry
    type: String
    default: docker.io

  LOG_LEVEL:
    label: Log Level
    type: Select
    options:
      - value: debug
      - value: info
      - value: error
    default: info

stacks:
  api:
    services:
      api:
        image: ${REGISTRY}/api:latest      # Uses REGISTRY
        environment:
          LOG_LEVEL: ${LOG_LEVEL}          # Uses LOG_LEVEL
```

### Stack Entries

Each stack can be:
- **Include**: Reference to an external fragment file
- **Inline**: Full stack definition within the product

```yaml
stacks:
  # Include external file
  identity:
    include: identity/identity-access.yaml

  # Include with variable override
  api:
    include: api/api.yaml
    variables:
      LOG_LEVEL:
        default: debug             # Override default for this stack

  # Inline definition
  monitoring:
    metadata:
      name: Monitoring
    services:
      prometheus:
        image: prom/prometheus:latest
```

### Variable Override

Stacks can override shared variable defaults:

```yaml
sharedVariables:
  LOG_LEVEL:
    type: Select
    options:
      - value: debug
      - value: info
      - value: error
    default: info                  # Default for most stacks

stacks:
  identity:
    include: identity.yaml
    variables:
      LOG_LEVEL:
        default: debug             # Identity needs more logging
```

**Value Resolution:**

| Priority | Source |
|----------|--------|
| 1 (highest) | User input |
| 2 | Stack variable override |
| 3 | Shared variable default |
| 4 (lowest) | Empty |

---

## Include Mechanism

Include paths are relative to the product manifest:

```
stacks/
└── myproduct/
    ├── myproduct.yaml           # include: identity/stack.yaml
    └── identity/
        └── stack.yaml           # ← Resolved here
```

---

## Variable Substitution

Variables are substituted using `${VARIABLE_NAME}` syntax:

```yaml
variables:
  REGISTRY:
    default: docker.io
  VERSION:
    default: "1.0.0"
  PORT:
    type: Port
    default: "8080"

services:
  app:
    image: ${REGISTRY}/myapp:${VERSION}    # docker.io/myapp:1.0.0
    ports:
      - "${PORT}:80"                        # 8080:80
    environment:
      API_URL: http://${HOST}:${PORT}       # http://host:8080
```

---

## File Structure

### Single Products

```
stacks/
├── whoami.yaml                  # Simple single-stack product
└── wordpress.yaml               # WordPress product
```

### Multi-Stack Products

```
stacks/
└── enterprise-platform/
    ├── enterprise-platform.yaml # Product manifest
    ├── IdentityAccess/
    │   └── identity-access.yaml # Fragment
    └── Infrastructure/
        └── monitoring.yaml      # Fragment
```

---

## Complete Examples

### Simple Product

```yaml
metadata:
  name: Whoami
  description: Simple HTTP service for testing
  productVersion: "1.0.0"
  category: Testing
  tags:
    - whoami
    - testing

variables:
  PORT:
    label: Port
    description: Port to access the service
    type: Port
    default: "8081"
    group: Network

services:
  whoami:
    image: traefik/whoami:latest
    ports:
      - "${PORT}:80"
    restart: unless-stopped
```

### Database Product

```yaml
metadata:
  name: PostgreSQL
  description: PostgreSQL database server
  productVersion: "15.0.0"
  category: Database

variables:
  POSTGRES_PORT:
    label: Port
    type: Port
    default: "5432"
    group: Network

  POSTGRES_USER:
    label: Username
    type: String
    default: postgres
    group: Authentication

  POSTGRES_PASSWORD:
    label: Password
    type: Password
    required: true
    group: Authentication

  POSTGRES_DB:
    label: Database Name
    type: String
    default: postgres
    group: Database

services:
  postgres:
    image: postgres:15
    ports:
      - "${POSTGRES_PORT}:5432"
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    restart: unless-stopped
    healthCheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER}"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres_data: {}
```

### Multi-Stack Enterprise Product

```yaml
metadata:
  name: Enterprise Platform
  description: Complete enterprise platform with modular components
  productVersion: "3.1.0"
  category: Enterprise

sharedVariables:
  REGISTRY:
    label: Docker Registry
    type: String
    default: myregistry.io
    group: Registry

  ENVIRONMENT:
    label: Environment
    type: Select
    options:
      - value: development
        label: Development
      - value: staging
        label: Staging
      - value: production
        label: Production
    default: development
    group: General

  LOG_LEVEL:
    label: Log Level
    type: Select
    options:
      - value: Debug
      - value: Information
      - value: Warning
      - value: Error
    default: Warning
    group: Logging

  DB_CONNECTION:
    label: Database Connection
    type: SqlServerConnectionString
    group: Database

stacks:
  identity:
    include: IdentityAccess/identity-access.yaml
    variables:
      LOG_LEVEL:
        default: Debug            # Identity needs verbose logging

  api:
    include: API/api.yaml

  monitoring:
    metadata:
      name: Monitoring

    variables:
      GRAFANA_PORT:
        label: Grafana Port
        type: Port
        default: "3000"

    services:
      prometheus:
        image: prom/prometheus:latest
        ports:
          - "9090:9090"
        restart: unless-stopped

      grafana:
        image: grafana/grafana:latest
        ports:
          - "${GRAFANA_PORT}:3000"
        dependsOn:
          - prometheus
        restart: unless-stopped
```

---

## Loader Behavior

1. **Scan**: Recursively scan `stacks/` for `*.yaml` and `*.yml` files
2. **Parse**: Parse each manifest file
3. **Classify**:
   - Has `metadata.productVersion` → **Product** (load)
   - No `productVersion` → **Fragment** (skip, load via include)
4. **Resolve Includes**: Resolve `include` paths relative to product manifest
5. **Merge Variables**: Merge `sharedVariables` with stack variables

---

## See Also

- [Variable Types](/en/reference/variable-types/)

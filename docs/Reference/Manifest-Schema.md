# RSGo Manifest Schema

The RSGo Manifest is the native stack definition format for ReadyStackGo. It provides type-validated variables, rich metadata, multi-stack support, and modular composition through includes.

---

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
| `description` | string | No | Description of what the product does |
| `productVersion` | string | **Yes*** | Version string (e.g., "3.1.0"). *Required for Products |
| `author` | string | No | Author or maintainer name |
| `documentation` | string | No | URL to documentation |
| `icon` | string | No | URL to icon image for UI display |
| `category` | string | No | Category for filtering (e.g., "Database", "CMS") |
| `tags` | string[] | No | Tags for search and filtering |

**Example:**

```yaml
metadata:
  name: WordPress
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

### Variable Types

#### Basic Types

| Type | Description | UI Element | Validation |
|------|-------------|------------|------------|
| `String` | Free-form text input | Text input | Optional regex pattern |
| `Number` | Numeric value | Number input | Optional min/max |
| `Boolean` | True/false toggle | Checkbox/Toggle | "true" or "false" |
| `Password` | Sensitive data (masked) | Password input | Minimum 8 characters |
| `Port` | Network port number | Number input | Integer 1-65535 |

#### Extended Types

| Type | Description | UI Element | Validation |
|------|-------------|------------|------------|
| `Url` | URL input | Text input | Valid URL format |
| `Email` | Email address | Text input | Valid email format |
| `Path` | File system path | Text input | Path format |
| `MultiLine` | Multi-line text | Textarea | None |
| `Select` | Dropdown selection | Dropdown | Must be one of options |

#### Connection String Types

| Type | Description | UI Element |
|------|-------------|------------|
| `ConnectionString` | Generic connection string | Text input |
| `SqlServerConnectionString` | SQL Server connection | Builder dialog |
| `PostgresConnectionString` | PostgreSQL connection | Builder dialog |
| `MySqlConnectionString` | MySQL connection | Builder dialog |
| `MongoConnectionString` | MongoDB connection | Builder dialog |
| `RedisConnectionString` | Redis connection | Builder dialog |
| `EventStoreConnectionString` | EventStoreDB connection | Builder dialog |

Connection string types provide a **builder dialog** in the UI that helps construct valid connection strings with proper escaping and formatting.

### Variable Examples

#### String with Pattern Validation

```yaml
variables:
  VERSION_TAG:
    label: Version Tag
    description: Semantic version tag (e.g., v1.0.0)
    type: String
    default: v1.0.0
    pattern: "^v\\d+\\.\\d+\\.\\d+$"
    patternError: Version must match format v#.#.# (e.g., v1.0.0)
```

#### Number with Range

```yaml
variables:
  MAX_CONNECTIONS:
    label: Max Connections
    description: Maximum number of database connections
    type: Number
    default: "100"
    min: 1
    max: 1000
```

#### Select with Options

```yaml
variables:
  ENVIRONMENT:
    label: Environment
    description: Deployment environment
    type: Select
    default: development
    options:
      - value: development
        label: Development
        description: Local development environment
      - value: staging
        label: Staging
        description: Pre-production testing
      - value: production
        label: Production
        description: Live production environment
```

#### Password (Required)

```yaml
variables:
  DB_PASSWORD:
    label: Database Password
    description: Password for the database user
    type: Password
    required: true
    placeholder: Enter a strong password
```

#### Port

```yaml
variables:
  HTTP_PORT:
    label: HTTP Port
    description: Port for HTTP traffic
    type: Port
    default: "8080"
```

#### Connection String

```yaml
variables:
  DATABASE:
    label: SQL Server Connection
    description: Connection string for the primary database
    type: SqlServerConnectionString
    default: "Server=localhost;Database=mydb;User Id=sa;Password=Password123!;TrustServerCertificate=true;"
```

#### Email

```yaml
variables:
  ADMIN_EMAIL:
    label: Admin Email
    description: Administrator email for notifications
    type: Email
    default: admin@example.com
    placeholder: admin@yourdomain.com
```

#### MultiLine

```yaml
variables:
  SSL_CERTIFICATE:
    label: SSL Certificate
    description: PEM-encoded SSL certificate
    type: MultiLine
    placeholder: |
      -----BEGIN CERTIFICATE-----
      ...
      -----END CERTIFICATE-----
```

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

  DB_PASSWORD:
    label: Database Password
    type: Password
    required: true
    group: Database
    order: 2
```

**Recommended Groups:**

| Group | Description |
|-------|-------------|
| `General` | General settings |
| `Network` | Ports, DNS, URLs |
| `Database` | Database connections |
| `Security` | Certificates, passwords |
| `Logging` | Log levels, outputs |
| `Performance` | Timeouts, pools, threads |
| `Advanced` | Advanced configuration |

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
    labels:
      app: my-api
      environment: ${ENVIRONMENT}
```

### Port Mappings

```yaml
ports:
  - "8080:80"                    # host:container
  - "${PORT}:80"                 # variable substitution
  - "127.0.0.1:8080:80"          # bind to specific IP
  - "8080-8090:80-90"            # port range
```

### Environment Variables

```yaml
environment:
  SIMPLE_VALUE: myvalue
  FROM_VARIABLE: ${MY_VAR}
  CONNECTION: ${DB_CONNECTION}
  COMBINED: http://${HOST}:${PORT}
```

### Volume Mappings

```yaml
volumes:
  - data:/app/data               # named volume
  - ./config:/app/config:ro      # bind mount (read-only)
  - ${DATA_PATH}:/data           # variable path
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

| Property | Type | Description |
|----------|------|-------------|
| `test` | string[] | Health check command |
| `interval` | string | Time between checks (e.g., "30s") |
| `timeout` | string | Timeout for each check (e.g., "10s") |
| `retries` | integer | Retries before marking unhealthy |
| `startPeriod` | string | Grace period before checks start |

### RSGO Labels

ReadyStackGo uses special container labels for stack identification and operation mode management.

#### Stack Identification

The `rsgo.stack` label identifies which stack a container belongs to:

```yaml
services:
  api:
    image: myapp/api:latest
    labels:
      rsgo.stack: my-application
```

> **Note:** This label is automatically added by ReadyStackGo during deployment. You typically don't need to set it manually in your manifest.

#### Maintenance Mode Behavior

The `rsgo.maintenance` label controls how containers behave when the stack enters Maintenance Mode:

```yaml
services:
  postgres:
    image: postgres:16
    labels:
      rsgo.stack: my-app
      rsgo.maintenance: ignore    # Won't be stopped during maintenance

  api:
    image: myapp/api:latest
    labels:
      rsgo.stack: my-app
      # No rsgo.maintenance = will be stopped during maintenance
```

| Value | Behavior |
|-------|----------|
| `ignore` | Container keeps running during maintenance mode |
| *(not set)* | Container is stopped when entering maintenance mode |

**Use Cases for `rsgo.maintenance: ignore`:**

- **Databases**: Keep PostgreSQL, MySQL, or other databases running for migrations
- **Message Brokers**: Keep RabbitMQ, Kafka running to preserve messages
- **Shared Services**: Services used by multiple stacks

#### Complete Example with Health and Maintenance

```yaml
metadata:
  name: Production App
  productVersion: "2.0.0"

services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthCheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5
    labels:
      rsgo.maintenance: ignore    # Database stays up during maintenance

  api:
    image: myapp/api:${VERSION}
    ports:
      - "${API_PORT}:8080"
    environment:
      DATABASE_URL: postgres://postgres:${DB_PASSWORD}@postgres:5432/app
    dependsOn:
      - postgres
    healthCheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      startPeriod: 40s
    # No rsgo.maintenance label = stopped during maintenance

  worker:
    image: myapp/worker:${VERSION}
    environment:
      DATABASE_URL: postgres://postgres:${DB_PASSWORD}@postgres:5432/app
    dependsOn:
      - postgres
    # No health check = relies on container state
    # No rsgo.maintenance = stopped during maintenance

volumes:
  postgres_data: {}
```

**Maintenance Mode Workflow:**

1. User sets stack to "Maintenance" mode
2. ReadyStackGo stops containers without `rsgo.maintenance: ignore`
3. Database continues running for maintenance tasks
4. User performs maintenance (migrations, backups, etc.)
5. User sets stack back to "Normal" mode
6. ReadyStackGo starts all stopped containers

See also: [Health Monitoring](../Operations/Health-Monitoring.md) | [Operation Mode](../Operations/Operation-Mode.md)

---

## Volumes

### Volume Definition

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

| Property | Type | Description |
|----------|------|-------------|
| `driver` | string | Volume driver (default: `local`) |
| `external` | boolean | Whether volume already exists |
| `driverOpts` | object | Driver-specific options |

---

## Networks

### Network Definition

```yaml
networks:
  # Default bridge network
  frontend:
    driver: bridge

  # Internal network (no external access)
  backend:
    driver: bridge
    internal: true

  # External network (already exists)
  proxy:
    external: true
```

| Property | Type | Description |
|----------|------|-------------|
| `driver` | string | Network driver (default: `bridge`) |
| `external` | boolean | Whether network already exists |
| `driverOpts` | object | Driver-specific options |

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
      description: Prometheus and Grafana
    variables:
      GRAFANA_PORT:
        type: Port
        default: "3000"
    services:
      prometheus:
        image: prom/prometheus:latest
      grafana:
        image: grafana/grafana:latest
        ports:
          - "${GRAFANA_PORT}:3000"
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

**Override Rules:**

1. Stack variables inherit all properties from shared variables
2. Only `default` value is overridden
3. User input always takes precedence

**Value Resolution:**

| Priority | Source |
|----------|--------|
| 1 (highest) | User input |
| 2 | Stack variable override |
| 3 | Shared variable default |
| 4 (lowest) | Empty |

---

## Include Mechanism

### Basic Include

```yaml
# product.yaml
stacks:
  identity:
    include: identity-access.yaml
```

```yaml
# identity-access.yaml (Fragment - no productVersion!)
metadata:
  name: Identity Access

variables:
  CERT_PATH:
    type: String
    default: /etc/ssl/certs/identity.pfx

services:
  identity-api:
    image: ${REGISTRY}/identity:latest
```

### Path Resolution

Include paths are relative to the product manifest:

```
stacks/
└── myproduct/
    ├── myproduct.yaml           # include: identity/stack.yaml
    └── identity/
        └── stack.yaml           # ← Resolved here
```

### Nested Directories

```yaml
# stacks/enterprise/enterprise.yaml
stacks:
  identity:
    include: IdentityAccess/identity.yaml
    # → stacks/enterprise/IdentityAccess/identity.yaml

  monitoring:
    include: Infrastructure/monitoring.yaml
    # → stacks/enterprise/Infrastructure/monitoring.yaml
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

**Substitution Contexts:**

- `image`: Docker image reference
- `ports`: Port mappings
- `environment`: Environment variable values
- `volumes`: Volume paths
- `command`: Command arguments
- `labels`: Label values

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
    ├── ProjectManagement/
    │   └── project.yaml         # Fragment
    └── Infrastructure/
        ├── database.yaml        # Fragment
        └── monitoring.yaml      # Fragment
```

### Shared Fragments

```
stacks/
├── shared/
│   └── monitoring.yaml          # Reusable fragment
├── product-a/
│   └── product-a.yaml           # include: ../shared/monitoring.yaml
└── product-b/
    └── product-b.yaml           # include: ../shared/monitoring.yaml
```

---

## Complete Examples

### Simple Product

```yaml
# whoami.yaml
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
# postgres.yaml
metadata:
  name: PostgreSQL
  description: PostgreSQL database server
  productVersion: "15.0.0"
  category: Database
  tags:
    - postgresql
    - database

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
# enterprise.yaml
metadata:
  name: Enterprise Platform
  description: Complete enterprise platform with modular components
  productVersion: "3.1.0"
  category: Enterprise
  tags:
    - enterprise
    - microservices

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
    default: "Server=db;Database=app;User Id=sa;Password=Password123!;TrustServerCertificate=true;"
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
      description: Prometheus and Grafana monitoring

    variables:
      GRAFANA_PORT:
        label: Grafana Port
        type: Port
        default: "3000"
        group: Monitoring

      GRAFANA_PASSWORD:
        label: Grafana Admin Password
        type: Password
        default: admin
        group: Monitoring

    services:
      prometheus:
        image: prom/prometheus:latest
        ports:
          - "9090:9090"
        volumes:
          - prometheus_data:/prometheus
        restart: unless-stopped

      grafana:
        image: grafana/grafana:latest
        ports:
          - "${GRAFANA_PORT}:3000"
        environment:
          GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_PASSWORD}
        volumes:
          - grafana_data:/var/lib/grafana
        dependsOn:
          - prometheus
        restart: unless-stopped

    volumes:
      prometheus_data: {}
      grafana_data: {}
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

- [Products](../Concepts/Products.md) - Product concepts
- [Multi-Stack](../Concepts/Multi-Stack.md) - Multi-stack products
- [Stack Fragments](../Concepts/Stack-Fragments.md) - Fragments
- [Best Practices](../Concepts/Best-Practices.md) - Guidelines
- [Stack Sources](Stack-Sources.md) - Stack source configuration
- [Health Monitoring](../Operations/Health-Monitoring.md) - Real-time container health monitoring
- [Operation Mode](../Operations/Operation-Mode.md) - Maintenance mode and container lifecycle

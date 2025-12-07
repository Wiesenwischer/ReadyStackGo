# RSGo Manifest Schema

The RSGo Manifest is the native stack definition format for ReadyStackGo. It extends Docker Compose concepts with type validation, rich metadata, and multi-stack support.

## Manifest Types

### 1. Product Manifest

A **Product** is the primary deployment unit. It has a `productVersion` in the metadata section.

```yaml
metadata:
  name: WordPress
  description: WordPress with MySQL backend
  productVersion: "6.0.0"      # This makes it a Product
  category: CMS
  tags:
    - wordpress
    - cms
    - blog

variables:
  WORDPRESS_PORT:
    label: Port
    type: Port
    default: "8080"

services:
  wordpress:
    image: wordpress:latest
    ports:
      - "${WORDPRESS_PORT}:80"
```

### 2. Stack Fragment

A **Fragment** has no `productVersion` and is only loadable via `include` from a Product.

```yaml
# identity-access.yaml (Fragment)
metadata:
  name: Identity Access
  description: Identity Provider

variables:
  PORT:
    type: Port
    default: "7614"

services:
  identity-api:
    image: myregistry/identity:latest
```

### 3. Multi-Stack Product

A Product can contain multiple stacks, either inline or via include:

```yaml
metadata:
  name: ams.project
  description: Enterprise Platform
  productVersion: "3.1.0"
  category: Enterprise

# Variables shared across all stacks
sharedVariables:
  REGISTRY:
    label: Docker Registry
    type: String
    default: amssolution
  ENVIRONMENT:
    label: Environment
    type: Select
    options:
      - value: dev
        label: Development
      - value: prod
        label: Production
    default: dev

stacks:
  # Include external file
  identity:
    include: identity-access.yaml

  # Inline definition
  monitoring:
    metadata:
      name: Monitoring
    services:
      prometheus:
        image: prom/prometheus:latest
```

---

## Schema Reference

### Root Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `version` | string | Yes | Manifest format version (currently "1.0") |
| `metadata` | object | Yes | Product/Stack metadata |
| `sharedVariables` | object | No | Variables shared across all stacks (multi-stack only) |
| `variables` | object | No | Variable definitions (single-stack) |
| `stacks` | object | No | Stack definitions (multi-stack) |
| `services` | object | No | Service definitions (single-stack) |
| `volumes` | object | No | Volume definitions |
| `networks` | object | No | Network definitions |

### Metadata

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `name` | string | Yes | Human-readable name |
| `description` | string | No | Description of the product/stack |
| `productVersion` | string | No* | Version (e.g., "3.1.0"). *Required for Products |
| `author` | string | No | Author or maintainer |
| `documentation` | string | No | URL to documentation |
| `icon` | string | No | Icon URL for UI |
| `category` | string | No | Category (e.g., "Database", "CMS") |
| `tags` | array | No | Tags for filtering |

### Variable Definition

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `label` | string | No | Human-readable label |
| `description` | string | No | Help text |
| `type` | string | No | Variable type (see below) |
| `default` | string | No | Default value |
| `required` | boolean | No | Whether the variable is required |
| `pattern` | string | No | Regex pattern for validation |
| `patternError` | string | No | Error message for pattern validation |
| `options` | array | No | Options for Select type |
| `min` | number | No | Minimum value (Number type) |
| `max` | number | No | Maximum value (Number type) |
| `placeholder` | string | No | Placeholder text |
| `group` | string | No | Group name for UI organization |
| `order` | integer | No | Display order within group |

### Variable Types

| Type | Description | Validation |
|------|-------------|------------|
| `String` | Text input | Optional regex pattern |
| `Number` | Numeric input | Integer or decimal, optional min/max |
| `Boolean` | True/false toggle | "true" or "false" |
| `Select` | Dropdown selection | Must be one of defined options |
| `Password` | Password input | Masked in UI, min 8 characters |
| `Port` | Network port | Integer 1-65535 |
| `SqlServerConnectionString` | SQL Server connection string | Builder dialog with server, database, auth options |
| `PostgresConnectionString` | PostgreSQL connection string | Builder dialog with SSL mode, pooling options |
| `MySqlConnectionString` | MySQL connection string | Builder dialog with charset, SSL options |
| `EventStoreConnectionString` | EventStoreDB connection string | Builder dialog for gRPC endpoint |
| `MongoConnectionString` | MongoDB connection string | Builder dialog with replica set, auth options |
| `RedisConnectionString` | Redis connection string | Builder dialog with sentinel, cluster options |
| `ConnectionString` | Generic connection string | Text input only (no builder) |

### Select Option

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `value` | string | Yes | Value when selected |
| `label` | string | No | Display label |
| `description` | string | No | Help text for option |

### Service Definition

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `image` | string | Yes | Docker image |
| `containerName` | string | No | Container name |
| `ports` | array | No | Port mappings |
| `environment` | object | No | Environment variables |
| `volumes` | array | No | Volume mappings |
| `networks` | array | No | Networks to connect |
| `dependsOn` | array | No | Service dependencies |
| `restart` | string | No | Restart policy |
| `command` | string | No | Command override |
| `entrypoint` | string | No | Entrypoint override |
| `workingDir` | string | No | Working directory |
| `user` | string | No | User to run as |
| `labels` | object | No | Container labels |
| `healthCheck` | object | No | Health check configuration |

### Stack Entry (Multi-Stack)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `include` | string | No* | Path to external file |
| `metadata` | object | No* | Stack metadata |
| `variables` | object | No | Stack-specific variables |
| `services` | object | No* | Service definitions |
| `volumes` | object | No | Volume definitions |
| `networks` | object | No | Network definitions |

*Either `include` OR (`services` with optional `metadata`) must be provided.

---

## Examples

### Simple Single-Stack Product

```yaml
version: "1.0"

metadata:
  name: Whoami
  productVersion: "1.0.0"
  category: Testing

variables:
  PORT:
    label: Port
    type: Port
    default: "8081"

services:
  whoami:
    image: traefik/whoami:latest
    ports:
      - "${PORT}:80"
```

### Multi-Stack with Shared Variables

```yaml
version: "1.0"

metadata:
  name: Enterprise Platform
  productVersion: "2.0.0"

sharedVariables:
  REGISTRY:
    type: String
    default: myregistry.io
  LOG_LEVEL:
    type: Select
    options:
      - value: debug
      - value: info
      - value: warn
      - value: error
    default: info

stacks:
  api:
    variables:
      API_PORT:
        type: Port
        default: "3000"
    services:
      api:
        image: ${REGISTRY}/api:latest
        ports:
          - "${API_PORT}:3000"
        environment:
          LOG_LEVEL: ${LOG_LEVEL}

  worker:
    include: worker.yaml
```

### Complex Stack with All Variable Types

```yaml
version: "1.0"

metadata:
  name: Complete Example
  productVersion: "1.0.0"

variables:
  # String with pattern validation
  EMAIL:
    label: Admin Email
    type: String
    pattern: "^[^@]+@[^@]+$"
    patternError: Must be a valid email
    required: true
    group: Admin
    order: 1

  # Password
  DB_PASSWORD:
    label: Database Password
    type: Password
    required: true
    group: Database
    order: 1

  # Number with range
  WORKERS:
    label: Worker Count
    type: Number
    default: "4"
    min: 1
    max: 32
    group: Performance

  # Port
  WEB_PORT:
    label: Web Port
    type: Port
    default: "8080"
    group: Network

  # Select
  ENVIRONMENT:
    label: Environment
    type: Select
    options:
      - value: development
        label: Development
        description: Local development
      - value: staging
        label: Staging
      - value: production
        label: Production
        description: Production environment
    default: development
    group: General

  # Boolean
  DEBUG:
    label: Enable Debug
    type: Boolean
    default: "false"
    group: General

services:
  app:
    image: myapp:latest
    ports:
      - "${WEB_PORT}:8080"
    environment:
      ADMIN_EMAIL: ${EMAIL}
      DB_PASSWORD: ${DB_PASSWORD}
      WORKERS: ${WORKERS}
      ENVIRONMENT: ${ENVIRONMENT}
      DEBUG: ${DEBUG}
```

---

## File Structure

### Single Products

```
stacks/
  whoami.yaml              # Single-stack product
  wordpress.yaml           # Single-stack product
```

### Multi-Stack Products

```
stacks/
  ams-project/
    ams-project.yaml       # Product manifest with includes
    identity-access.yaml   # Fragment
    infrastructure.yaml    # Fragment
    monitoring.yaml        # Fragment
```

### Mixed

```
stacks/
  whoami.yaml              # Single-stack product
  wordpress.yaml           # Single-stack product
  ams-project/
    ams-project.yaml       # Multi-stack product
    identity-access.yaml   # Fragment
```

---

## Loader Behavior

1. Scan all `*.yaml` / `*.yml` files recursively
2. Parse each file
3. If `metadata.productVersion` exists → Load as Product
4. If no `productVersion` → Skip (Fragment, loaded via include)
5. Resolve `include` references relative to manifest file
6. Merge `sharedVariables` with stack-specific variables

## Variable Precedence

1. Stack-specific variables override shared variables
2. User-provided values override defaults
3. `.env` file values (if applicable) override YAML defaults

---

## Multi-Stack Variable Override Behavior

When a variable is defined in both `sharedVariables` and a stack's `variables`, the following rules apply:

### Definition Merge

Stack-specific variable definitions **extend** shared variable definitions:

```yaml
sharedVariables:
  LOG_LEVEL:
    label: Log Level
    type: Select
    options:
      - value: debug
      - value: info
      - value: error
    default: info
    group: Logging

stacks:
  identity:
    variables:
      LOG_LEVEL:
        default: debug    # Override only the default
```

In this example, the `LOG_LEVEL` variable in the `identity` stack inherits all properties (label, type, options, group) from the shared definition but uses `debug` as its default value.

### UI Behavior

The deployment UI provides a clean standard view with optional per-stack overrides:

1. **Standard View**: Shows shared variables with their values. Changes apply to all stacks.

2. **Override Dialog**: Each variable has an optional override button (⚙) that opens a dialog for stack-specific customization.

3. **Pre-fill Behavior**: When a stack defines a variable that also exists in `sharedVariables`:
   - The override dialog checkbox is pre-activated
   - The field is pre-filled with the stack's default value
   - User can clear the override to revert to the shared value

### Value Resolution at Deployment

| Scenario | Resolved Value |
|----------|----------------|
| Only shared variable defined | Shared default or user input |
| Stack variable with override | Stack-specific value |
| Stack variable cleared by user | Falls back to shared value |
| Stack variable not in shared | Stack-specific value only |

### Example: Identity Stack with Override

```yaml
# stack.yaml (Product)
sharedVariables:
  MIN_LOG_LEVEL:
    label: Minimum Log Level
    type: Select
    default: Warning
    options:
      - value: Debug
      - value: Information
      - value: Warning
      - value: Error

stacks:
  identity:
    include: identity-access.yaml
```

```yaml
# identity-access.yaml (Fragment)
variables:
  MIN_LOG_LEVEL:
    default: Debug    # Identity needs verbose logging
```

**Deployment UI**:
- User sees `MIN_LOG_LEVEL = Warning` in standard view
- For identity stack: Override checkbox is pre-activated, field shows `Debug`
- User can accept the override, modify it, or clear it to use `Warning`

---

## See Also

- [Stack Sources](Stack-Sources.md)
- [Configuration Reference](Configuration-Reference.md)

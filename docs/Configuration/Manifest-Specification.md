# Manifest Specification

The RSGo Manifest is the native stack definition format for ReadyStackGo. It describes the complete target state of a deployable stack using YAML.

---

## Overview

A manifest defines:

- **Metadata**: Product name, version, description, and categorization
- **Variables**: User-configurable parameters with types and validation
- **Services**: Docker containers to deploy
- **Volumes**: Persistent storage definitions
- **Networks**: Network configurations

---

## Quick Example

```yaml
metadata:
  name: Whoami
  productVersion: "1.0.0"
  category: Testing

variables:
  PORT:
    label: Port
    type: Port
    default: "8080"

services:
  whoami:
    image: traefik/whoami:latest
    ports:
      - "${PORT}:80"
    restart: unless-stopped
```

---

## Manifest Types

### 1. Product (Single-Stack)

Contains services directly at the root level. Has `productVersion` in metadata.

### 2. Product (Multi-Stack)

Contains multiple stacks via `stacks` section with shared variables.

### 3. Fragment

No `productVersion` - only loadable via `include` from a product.

---

## Key Features

### Variable Types

| Type | Description |
|------|-------------|
| `String` | Free-form text |
| `Number` | Numeric value with optional min/max |
| `Boolean` | True/false toggle |
| `Password` | Masked sensitive data |
| `Port` | Network port (1-65535) |
| `Select` | Dropdown selection |
| `Url`, `Email`, `Path` | Validated formats |
| `SqlServerConnectionString` | Builder dialog for SQL Server |
| `PostgresConnectionString` | Builder dialog for PostgreSQL |
| `MySqlConnectionString` | Builder dialog for MySQL |
| `MongoConnectionString` | Builder dialog for MongoDB |
| `RedisConnectionString` | Builder dialog for Redis |

### Variable Substitution

Use `${VARIABLE_NAME}` syntax in services:

```yaml
services:
  app:
    image: ${REGISTRY}/myapp:${VERSION}
    ports:
      - "${PORT}:80"
    environment:
      DATABASE: ${DB_CONNECTION}
```

### Import Variables from .env File

When deploying a stack, you can import variable values from a `.env` file instead of entering them manually:

1. Click the **Import .env** button in the Deploy page sidebar
2. Select your `.env` file
3. Matching variables are automatically populated

**Supported .env Format:**

```bash
# Comments are ignored
DB_HOST=localhost
DB_PORT=5432
DB_PASSWORD="secret with spaces"
DB_NAME='single-quoted'
SIMPLE_VALUE=no-quotes-needed
```

**Features:**

- Lines starting with `#` are treated as comments
- Empty lines are ignored
- Values can be quoted (double `"` or single `'`) or unquoted
- Only variables defined in the manifest are imported (others are ignored)
- Existing values are overwritten by imported values

### Multi-Stack with Shared Variables

```yaml
sharedVariables:
  REGISTRY:
    type: String
    default: docker.io

stacks:
  api:
    include: api.yaml
  monitoring:
    services:
      prometheus:
        image: prom/prometheus:latest
```

---

## Full Reference

For complete documentation including:

- All variable types and properties
- Service configuration options
- Volume and network definitions
- Multi-stack and fragment patterns
- Complete examples

See: **[RSGo Manifest Schema](../Reference/Manifest-Schema.md)**

---

## Related Documentation

- [Products](../Concepts/Products.md) - Product concepts
- [Multi-Stack](../Concepts/Multi-Stack.md) - Multi-stack products
- [Stack Fragments](../Concepts/Stack-Fragments.md) - Reusable fragments
- [Best Practices](../Concepts/Best-Practices.md) - Guidelines

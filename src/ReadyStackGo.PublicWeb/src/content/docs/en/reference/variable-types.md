---
title: Variable Types
description: Reference for all supported variable types in the RSGo Manifest Format
---

The RSGo Manifest Format supports typed variables with automatic UI generation and validation. Each type has specific properties and a matching editor in the web UI.

## Basic Types

### String

Simple text input with optional pattern validation.

```yaml
EMAIL:
  label: Email Address
  type: String
  pattern: "^[^@]+@[^@]+\\.[^@]+$"
  patternError: Please enter a valid email address
  required: true
  placeholder: user@example.com
```

| Property | Description |
|----------|-------------|
| `pattern` | Regular expression for validation |
| `patternError` | Error message when pattern doesn't match |
| `placeholder` | Placeholder text in input field |

---

### Number

Numeric input with optional min/max constraints.

```yaml
WORKERS:
  label: Worker Threads
  type: Number
  default: "4"
  min: 1
  max: 32
  description: Number of parallel workers (1-32)
```

| Property | Description |
|----------|-------------|
| `min` | Minimum value |
| `max` | Maximum value |

**UI**: Number field with validation

---

### Boolean

Toggle switch for yes/no values.

```yaml
DEBUG:
  label: Debug Mode
  type: Boolean
  default: "false"
  description: Enables extended logging output
```

**Valid values**: `"true"` or `"false"` (as strings)

**UI**: Toggle switch

---

### Password

Password input with hidden display.

```yaml
DB_PASSWORD:
  label: Database Password
  type: Password
  required: true
  description: At least 8 characters
```

**UI**: Password field with eye icon to show/hide

---

### Port

Network port with automatic validation (1-65535).

```yaml
WEB_PORT:
  label: Web Port
  type: Port
  default: "8080"
  description: HTTP port for the application
```

| Property | Description |
|----------|-------------|
| `min` | Minimum port (default: 1) |
| `max` | Maximum port (default: 65535) |

**UI**: Number field with port validation

---

### Select

Dropdown selection from predefined options.

```yaml
ENVIRONMENT:
  label: Environment
  type: Select
  default: development
  options:
    - value: development
      label: Development
      description: Local development environment
    - value: staging
      label: Staging
      description: Test environment
    - value: production
      label: Production
      description: Live system
```

| Option Property | Description |
|-----------------|-------------|
| `value` | Technical value (required) |
| `label` | Display text |
| `description` | Additional description |

**UI**: Dropdown menu

---

## Connection String Types

These types provide specialized builder dialogs for database connections.

### SqlServerConnectionString

Microsoft SQL Server connection string.

```yaml
DB_CONNECTION:
  label: SQL Server Connection
  type: SqlServerConnectionString
  required: true
  group: Database
```

**Builder Dialog Features**:
- Configure server and port separately
- Windows Authentication or SQL Login
- Options: Encrypt, TrustServerCertificate, MARS
- Live preview of connection string
- **Test Connection** button

**Generated Format**:
```
Server=myserver,1433;Database=mydb;User Id=sa;Password=***;TrustServerCertificate=true
```

---

### PostgresConnectionString

PostgreSQL connection string.

```yaml
PG_CONNECTION:
  label: PostgreSQL Connection
  type: PostgresConnectionString
  required: true
```

**Builder Dialog Features**:
- Host, Port, Database
- Username and Password
- SSL Mode (Disable, Require, Prefer)
- Connection Pooling options
- Test Connection

**Generated Format**:
```
Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=***;SSL Mode=Prefer
```

---

### MySqlConnectionString

MySQL/MariaDB connection string.

```yaml
MYSQL_CONNECTION:
  label: MySQL Connection
  type: MySqlConnectionString
  required: true
```

**Builder Dialog Features**:
- Server and Port
- Database and User
- SSL options
- Charset configuration
- Test Connection

**Generated Format**:
```
Server=localhost;Port=3306;Database=mydb;User=root;Password=***;SslMode=Required
```

---

### MongoConnectionString

MongoDB connection string.

```yaml
MONGO_CONNECTION:
  label: MongoDB Connection
  type: MongoConnectionString
```

**Builder Dialog Features**:
- Single Host or Replica Set
- Authentication and AuthSource
- SSL/TLS options
- Read Preference
- Test Connection

**Generated Format**:
```
mongodb://user:pass@host1:27017,host2:27017/mydb?replicaSet=rs0&authSource=admin
```

---

### RedisConnectionString

Redis connection string.

```yaml
REDIS_URL:
  label: Redis Server
  type: RedisConnectionString
  default: redis://localhost:6379
```

**Builder Dialog Features**:
- Host and Port
- Password (optional)
- Database number
- SSL options
- Sentinel configuration

**Generated Format**:
```
redis://user:password@host:6379/0?ssl=true
```

---

### EventStoreConnectionString

EventStoreDB gRPC connection string.

```yaml
EVENTSTORE_CONNECTION:
  label: EventStore Connection
  type: EventStoreConnectionString
```

**Builder Dialog Features**:
- gRPC Endpoint
- TLS configuration
- Cluster mode

**Generated Format**:
```
esdb://admin:changeit@localhost:2113?tls=true
```

---

### ConnectionString (Generic)

Generic connection string without specialized builder.

```yaml
CUSTOM_CONNECTION:
  label: Custom Connection
  type: ConnectionString
```

**UI**: Simple text field (no builder)

---

## Variable Grouping

Variables can be organized into logical groups:

```yaml
variables:
  # Database group
  DB_HOST:
    type: String
    group: database
    order: 1
  DB_PORT:
    type: Port
    group: database
    order: 2
  DB_PASSWORD:
    type: Password
    group: database
    order: 3

  # Network group
  WEB_PORT:
    type: Port
    group: network
    order: 1
  API_PORT:
    type: Port
    group: network
    order: 2
```

| Property | Description |
|----------|-------------|
| `group` | Name of the group |
| `order` | Order within the group |

**UI**: Variables are displayed ordered by groups with group headers.

---

## Validation

All types support:

| Property | Description |
|----------|-------------|
| `required` | Field must be filled |
| `default` | Default value |
| `description` | Help text below the field |

### Validation Order

1. Required check (if `required: true`)
2. Type-specific validation (e.g., port range)
3. Pattern validation (if `pattern` defined)

### Error Display

Validation errors are displayed directly below the input field in red.

---

## See Also

- [RSGo Manifest Format](/en/reference/manifest-format/)

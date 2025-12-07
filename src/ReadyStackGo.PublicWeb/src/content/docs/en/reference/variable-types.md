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

**UI**: Single-line text input

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

## Extended Types

### Url

URL input with format validation.

```yaml
API_ENDPOINT:
  label: API Endpoint
  description: External API endpoint URL
  type: Url
  default: "https://api.example.com"
  placeholder: "https://..."
```

**UI**: Text input with URL validation

---

### Email

Email address input with format validation.

```yaml
ADMIN_EMAIL:
  label: Admin Email
  description: Administrator email for notifications
  type: Email
  default: admin@example.com
  placeholder: admin@yourdomain.com
```

**UI**: Text input with email validation

---

### Path

File system path input.

```yaml
DATA_PATH:
  label: Data Path
  description: Path for data storage
  type: Path
  default: /data
```

**UI**: Text input for paths

---

### MultiLine

Multi-line text input for larger content.

```yaml
SSL_CERTIFICATE:
  label: SSL Certificate
  description: PEM-encoded SSL certificate
  type: MultiLine
  placeholder: |
    -----BEGIN CERTIFICATE-----
    ...
    -----END CERTIFICATE-----
```

**UI**: Textarea with multiple lines

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
    group: Database
    order: 1
  DB_PORT:
    type: Port
    group: Database
    order: 2
  DB_PASSWORD:
    type: Password
    group: Database
    order: 3

  # Network group
  WEB_PORT:
    type: Port
    group: Network
    order: 1
  API_PORT:
    type: Port
    group: Network
    order: 2
```

| Property | Description |
|----------|-------------|
| `group` | Name of the group |
| `order` | Order within the group |

**UI**: Variables are displayed ordered by groups with group headers.

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

## Complete Example

```yaml
variables:
  # String with pattern
  VERSION_TAG:
    label: Version Tag
    type: String
    default: v1.0.0
    pattern: "^v\\d+\\.\\d+\\.\\d+$"
    patternError: Version must match format v#.#.# (e.g., v1.0.0)
    group: Versions
    order: 1

  # Number with range
  MAX_CONNECTIONS:
    label: Max Connections
    type: Number
    default: "100"
    min: 1
    max: 1000
    group: Performance

  # Boolean
  ENABLE_DEBUG:
    label: Enable Debug Mode
    type: Boolean
    default: "false"
    group: General

  # Password
  ADMIN_PASSWORD:
    label: Admin Password
    type: Password
    required: true
    group: Security

  # Port
  HTTP_PORT:
    label: HTTP Port
    type: Port
    default: "8080"
    group: Network

  # Select
  ENVIRONMENT:
    label: Environment
    type: Select
    default: development
    options:
      - value: development
        label: Development
      - value: staging
        label: Staging
      - value: production
        label: Production
    group: General

  # URL
  API_ENDPOINT:
    label: API Endpoint
    type: Url
    default: "https://api.example.com"
    group: External Services

  # Email
  ADMIN_EMAIL:
    label: Admin Email
    type: Email
    default: admin@example.com
    group: Notifications

  # MultiLine
  SSL_CERTIFICATE:
    label: SSL Certificate
    type: MultiLine
    placeholder: |
      -----BEGIN CERTIFICATE-----
      ...
      -----END CERTIFICATE-----
    group: Security

  # SQL Server Connection
  DATABASE:
    label: SQL Server Connection
    type: SqlServerConnectionString
    group: Database
```

---

## See Also

- [RSGo Manifest Format](/en/reference/manifest-format/)

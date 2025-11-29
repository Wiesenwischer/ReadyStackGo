# Stack Sources

Stack Sources allow you to define reusable Docker Compose stack templates that can be deployed to any environment. ReadyStackGo supports loading stacks from local directories with full support for Docker Compose conventions.

## Recursive Directory Search

ReadyStackGo **recursively searches** all subdirectories within a stack source. This allows you to organize your stacks hierarchically:

```
stacks/
├── ams.project/                    # Project folder
│   ├── IdentityAccess/             # Folder-based stack
│   │   └── docker-compose.yml
│   └── Messaging/                  # Folder-based stack
│       └── docker-compose.yml
├── examples/                       # Category folder
│   ├── WordPress/                  # Folder-based stack
│   │   └── docker-compose.yml
│   └── whoami.yml                  # Single-file stack
└── redis.yml                       # Single-file stack at root
```

The **relative path** from the source root is displayed in the UI as `Source / RelativePath` (e.g., `Local / ams.project`).

## Supported Formats

ReadyStackGo supports two formats for defining stacks:

### 1. Single-File Stacks

Simple stacks can be defined as standalone YAML files:

```
examples/
├── simple-nginx.yml
├── redis.yml
└── whoami.yml
```

The filename (without extension) becomes the stack name.

### 2. Folder-Based Stacks

Complex stacks with multiple files should use the folder-based format:

```
examples/
└── WordPress/
    ├── docker-compose.yml           # Required: Main compose file
    ├── docker-compose.override.yml  # Optional: Override file (auto-merged)
    └── .env                         # Optional: Default variable values
```

The folder name becomes the stack name.

## Stack Description Extraction

ReadyStackGo automatically extracts descriptions from the first comment block in your compose files:

```yaml
# IdentityServer Identity Provider
# Standalone identity provider deployment
# Usage: docker-compose up -d
version: '3.8'
services:
  identityserver:
    image: duendesoftware/identityserver:latest
```

**Extraction Rules:**
- Comments **at the beginning** of the file are extracted as the description
- Lines starting with "Usage:" are excluded
- Maximum **2 lines** are displayed in the UI
- Lines are joined with newlines (multi-line display)
- Empty comments and technical markers (like `vim:` or `yaml`) are ignored

**Result in UI:**
```
IdentityServer Identity Provider
Standalone identity provider deployment
```

**Important:** Comments must appear **before** the `version:` line. Comments placed after `version:` will not be extracted.

## File Merging

When using folder-based stacks, ReadyStackGo automatically merges `docker-compose.override.yml` into the main compose file using **Docker Compose merge semantics**.

### Merge Rules

| Element Type | Merge Behavior |
|--------------|----------------|
| **Scalars** (image, restart, etc.) | Override replaces base value |
| **ports, expose, dns, dns_search, tmpfs** | Concatenated (both sets combined) |
| **environment, labels, volumes, devices** | Merged by key (override values win for same key) |
| **Nested Maps** (services.web.*) | Recursive deep merge |

> **Note:** This follows [Docker Compose merge semantics](https://docs.docker.com/compose/how-tos/multiple-compose-files/merge/). Environment variables and volumes are merged by key, while ports are concatenated.

### Merge Order

Files are merged in the following order:
1. `docker-compose.yml` (base)
2. `docker-compose.override.yml` (merged on top)

**Later files override earlier files.**

### Example

**docker-compose.yml:**
```yaml
version: '3.8'

services:
  web:
    image: nginx:1.20
    environment:
      - DEBUG=false
      - LOG_LEVEL=info
    ports:
      - "80:80"
    restart: always
```

**docker-compose.override.yml:**
```yaml
version: '3.8'

services:
  web:
    image: nginx:latest           # Replaces nginx:1.20
    environment:
      - DEBUG=true                # Replaces DEBUG=false
      - DEV_MODE=enabled          # Added to environment
    ports:
      - "8443:443"                # Added to ports (concatenated)
    # restart not specified → keeps "always" from base
```

**Merged Result:**
```yaml
version: '3.8'

services:
  web:
    image: nginx:latest
    environment:
      - DEBUG=true
      - LOG_LEVEL=info
      - DEV_MODE=enabled
    ports:
      - "80:80"
      - "8443:443"
    restart: always
```

## Environment Variables

### Variable Detection

ReadyStackGo automatically detects environment variables in your compose files using the `${VAR}` and `${VAR:-default}` syntax:

```yaml
services:
  db:
    image: mysql:${MYSQL_VERSION:-8.0}
    environment:
      - MYSQL_ROOT_PASSWORD=${DB_ROOT_PASSWORD}
      - MYSQL_DATABASE=${DB_NAME:-myapp}
```

Detected variables:
- `MYSQL_VERSION` - Optional (has default: `8.0`)
- `DB_ROOT_PASSWORD` - Required (no default)
- `DB_NAME` - Optional (has default: `myapp`)

### .env File Support

For folder-based stacks, you can include a `.env` file with default values:

**.env:**
```bash
# Database Configuration
DB_NAME=wordpress
DB_USER=wordpress
DB_PASSWORD=changeme
DB_ROOT_PASSWORD=rootchangeme

# Application Settings
WORDPRESS_PORT=8080
MYSQL_VERSION=8.0
```

**How .env files are processed:**

1. Variables from `.env` are loaded as defaults
2. If a variable in the YAML has no default (`${VAR}`), the `.env` value becomes its default
3. Variables only in `.env` (not in YAML) are also shown in the UI
4. User-provided values during deployment override all defaults

### Variable Priority (highest to lowest)

1. **User input** during deployment
2. **.env file value**
3. **YAML inline default** (`${VAR:-default}`) - fallback only

## Configuring Stack Sources

Stack sources are configured in `appsettings.json`:

```json
{
  "StackSources": {
    "Sources": [
      {
        "Id": "builtin",
        "Name": "Built-in Examples",
        "Type": "local-directory",
        "Enabled": true,
        "Path": "examples",
        "FilePattern": "*.yml;*.yaml"
      },
      {
        "Id": "custom",
        "Name": "Custom Stacks",
        "Type": "local-directory",
        "Enabled": true,
        "Path": "C:/my-stacks",
        "FilePattern": "*.yml;*.yaml"
      }
    ]
  }
}
```

### Configuration Options

| Option | Description |
|--------|-------------|
| `Id` | Unique identifier for the source |
| `Name` | Display name in the UI |
| `Type` | Source type (`local-directory`) |
| `Enabled` | Whether this source is active |
| `Path` | Directory path (absolute or relative to app) |
| `FilePattern` | Semicolon-separated glob patterns for single-file stacks |

## API Endpoints

### List All Stacks

```http
GET /api/stack-sources/stacks
```

Returns all available stack definitions from all enabled sources.

### Get Stack Details

```http
GET /api/stack-sources/stacks/{stackId}
```

Returns the full stack definition including:
- Merged YAML content (if override files exist)
- All detected variables with defaults
- List of additional files

### Sync Sources

```http
POST /api/stack-sources/sync
```

Reloads all stacks from all enabled sources. Use this after adding new stack files.

## Best Practices

### When to Use Single-File Format

- Simple stacks with few services
- No environment-specific overrides needed
- Self-contained configuration

### When to Use Folder-Based Format

- Multi-service stacks with complex configuration
- Different settings for dev/prod (use override file)
- Many environment variables (use .env file)
- Stacks cloned from Git repositories

### Naming Conventions

- Use lowercase names with hyphens: `my-stack`, `wordpress-mysql`
- Be descriptive: `grafana-prometheus` not just `monitoring`
- Version in name if needed: `postgres-15`, `redis-7`

### Security Considerations

- Never commit secrets in `.env` files
- Use `.env` only for non-sensitive defaults
- Sensitive values should be entered during deployment
- Mark sensitive variables as required (no default)

## Troubleshooting

### Stacks Not Appearing

1. Click "Sync Sources" to reload
2. Check that the source is enabled
3. Verify file extension matches `FilePattern`
4. Check API logs for parsing errors

### Merge Not Working as Expected

1. Ensure `docker-compose.override.yml` is in the same folder
2. Check YAML syntax in both files
3. Remember: `ports` are concatenated, `environment` and `volumes` are merged by key
4. Use the API to inspect the merged result

### Variables Not Detected

1. Use `${VAR}` or `${VAR:-default}` syntax
2. Variable names must be uppercase with underscores
3. Check both main and override files are being parsed

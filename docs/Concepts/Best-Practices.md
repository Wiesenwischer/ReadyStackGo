# Best Practices für Stack-Definitionen

## Übersicht

Diese Dokumentation fasst bewährte Praktiken für die Erstellung von Produkten, Multi-Stack-Konfigurationen und Fragments in ReadyStackGo zusammen.

---

## Allgemeine Prinzipien

### 1. Keep It Simple

Beginne einfach und erweitere bei Bedarf:

```yaml
# BEGINNE SO
metadata:
  name: My App
  productVersion: "1.0.0"

services:
  app:
    image: myapp:latest
    ports:
      - "8080:80"
```

```yaml
# ERWEITERE BEI BEDARF
metadata:
  name: My App
  productVersion: "1.0.0"

variables:
  PORT:
    label: Port
    type: Port
    default: "8080"

services:
  app:
    image: myapp:latest
    ports:
      - "${PORT}:80"
```

### 2. Explizit vor Implizit

Mache Konfigurationen explizit, auch wenn sie dem Default entsprechen:

```yaml
# GUT: Explizit
services:
  app:
    restart: unless-stopped
    networks:
      - default

# WENIGER GUT: Implizit (funktioniert, aber weniger klar)
services:
  app:
    image: myapp:latest
```

### 3. Dokumentiere Entscheidungen

Nutze `description` Felder um Entscheidungen zu erklären:

```yaml
variables:
  WORKER_COUNT:
    label: Worker Count
    description: |
      Number of worker threads.
      Recommended: 2x CPU cores for I/O bound,
      1x CPU cores for CPU bound workloads.
    type: Number
    default: "4"
    min: 1
    max: 32
```

---

## Versionierung

### Semantic Versioning

Verwende [Semantic Versioning](https://semver.org/):

| Version | Bedeutung |
|---------|-----------|
| `1.0.0` → `1.0.1` | Patch: Bugfix, keine Breaking Changes |
| `1.0.0` → `1.1.0` | Minor: Neue Features, rückwärtskompatibel |
| `1.0.0` → `2.0.0` | Major: Breaking Changes |

### Breaking Changes dokumentieren

Bei Major-Updates, dokumentiere Breaking Changes:

```yaml
metadata:
  name: My App
  productVersion: "2.0.0"
  description: |
    v2.0.0 Breaking Changes:
    - DATABASE_URL is now required
    - Default port changed from 8080 to 3000
    - Removed deprecated LEGACY_MODE variable
```

### Version in Images

Vermeide `latest` in Produktion:

```yaml
# ENTWICKLUNG: latest ist OK
services:
  app:
    image: myapp:latest

# PRODUKTION: Spezifische Version
services:
  app:
    image: myapp:2.1.3
```

---

## Variablen

### Sinnvolle Defaults

Setze Defaults, die für die meisten Anwendungsfälle funktionieren:

```yaml
variables:
  # GUT: Sinnvolle Defaults
  LOG_LEVEL:
    default: info       # Nicht zu verbose, nicht zu still

  HTTP_PORT:
    default: "8080"     # Standard HTTP-Alternative Port

  DB_POOL_SIZE:
    default: "10"       # Vernünftiger Mittelwert
```

### Required nur wenn nötig

Markiere nur wirklich erforderliche Felder als `required`:

```yaml
variables:
  # MUSS vom Benutzer gesetzt werden (keine sinnvoller Default möglich)
  DATABASE_PASSWORD:
    type: Password
    required: true      # Kein Default für Passwörter!

  # Kann einen Default haben
  DATABASE_HOST:
    type: String
    default: localhost  # Sinnvoller Default für Entwicklung
    # required: false   # Nicht nötig anzugeben
```

### Gruppierung und Reihenfolge

Organisiere Variablen logisch:

```yaml
variables:
  # Gruppe 1: Allgemein
  ENVIRONMENT:
    group: General
    order: 1

  APP_NAME:
    group: General
    order: 2

  # Gruppe 2: Netzwerk
  HTTP_PORT:
    group: Network
    order: 1

  HTTPS_PORT:
    group: Network
    order: 2

  # Gruppe 3: Datenbank
  DB_HOST:
    group: Database
    order: 1

  DB_PORT:
    group: Database
    order: 2

  DB_PASSWORD:
    group: Database
    order: 3
```

**Empfohlene Gruppenreihenfolge:**

1. General
2. Network
3. Database
4. Security
5. Logging
6. Performance
7. Advanced

### Validation nutzen

Nutze Pattern-Validierung für strukturierte Eingaben:

```yaml
variables:
  EMAIL:
    type: String
    pattern: "^[^@]+@[^@]+\\.[^@]+$"
    patternError: Please enter a valid email address

  HOSTNAME:
    type: String
    pattern: "^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$"
    patternError: Invalid hostname format
```

---

## Services

### Restart-Policies

Wähle die richtige Restart-Policy:

| Policy | Verwendung |
|--------|------------|
| `no` | Einmalige Tasks, Init-Container |
| `on-failure` | Services die nicht dauerhaft laufen sollen |
| `unless-stopped` | Standard für die meisten Services |
| `always` | Kritische Services die immer laufen müssen |

```yaml
services:
  # Dauerhaft laufender Service
  api:
    restart: unless-stopped

  # Kritischer Service
  database:
    restart: always

  # Einmaliger Init-Job
  db-migration:
    restart: no
```

### Health Checks

Definiere Health Checks für kritische Services:

```yaml
services:
  api:
    image: myapi:latest
    healthCheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      startPeriod: 40s
```

### Abhängigkeiten

Nutze `dependsOn` um Startreihenfolge zu definieren:

```yaml
services:
  database:
    image: postgres:15

  api:
    image: myapi:latest
    dependsOn:
      - database       # API startet nach Database

  worker:
    image: myworker:latest
    dependsOn:
      - api           # Worker startet nach API
      - database      # und nach Database
```

**Hinweis:** `dependsOn` garantiert nur die Startreihenfolge, nicht dass der abhängige Service bereit ist. Nutze Health Checks für robuste Abhängigkeiten.

### Resource Limits (Geplant)

Für Produktionsumgebungen, definiere Ressourcenlimits:

```yaml
services:
  api:
    image: myapi:latest
    resources:
      limits:
        cpus: "2"
        memory: 2G
      reservations:
        cpus: "0.5"
        memory: 512M
```

---

## Multi-Stack

### Wann Single-Stack vs. Multi-Stack?

| Szenario | Empfehlung |
|----------|------------|
| < 5 Services | Single-Stack |
| 5-10 Services, eine Domain | Single-Stack oder Multi-Stack |
| > 10 Services | Multi-Stack |
| Mehrere Teams | Multi-Stack |
| Wiederverwendbare Komponenten | Multi-Stack mit Fragments |

### Shared Variables richtig nutzen

Definiere als Shared Variables:

- **Registry-Konfiguration**: Einmal definieren, überall nutzen
- **Environment-Variablen**: dev/staging/prod
- **Logging-Konfiguration**: Konsistentes Logging
- **Gemeinsame Datenbank-Connections**: Wenn Services dieselbe DB nutzen
- **Netzwerk-Konfiguration**: External DNS, Ports

```yaml
sharedVariables:
  REGISTRY:
    label: Docker Registry
    default: docker.io
    group: Registry

  ENVIRONMENT:
    label: Environment
    type: Select
    options:
      - value: development
      - value: staging
      - value: production
    group: General

  LOG_LEVEL:
    label: Log Level
    type: Select
    options:
      - value: debug
      - value: info
      - value: warning
      - value: error
    default: info
    group: Logging
```

### Fragment-Granularität

Wähle die richtige Granularität:

```yaml
# ZU FEIN: Jeder Service ein Fragment
stacks:
  service-a:
    include: service-a.yaml   # Nur 1 Service
  service-b:
    include: service-b.yaml   # Nur 1 Service

# ZU GROB: Alles in einem Fragment
stacks:
  everything:
    include: all-20-services.yaml

# RICHTIG: Nach Domain/Verantwortlichkeit
stacks:
  identity:                   # 3-5 Services
    include: identity.yaml
  business:                   # 4-6 Services
    include: business.yaml
  infrastructure:             # 3-4 Services
    include: infrastructure.yaml
```

---

## Sicherheit

### Keine Secrets in Defaults

```yaml
# FALSCH: Echtes Passwort als Default
variables:
  DB_PASSWORD:
    default: "MyProductionPassword123!"

# RICHTIG: Placeholder oder leer
variables:
  DB_PASSWORD:
    type: Password
    required: true
    placeholder: "Enter a strong password"
```

### Sensible Daten als Password-Typ

```yaml
variables:
  # Alle sensiblen Daten als Password
  DATABASE_PASSWORD:
    type: Password

  API_KEY:
    type: Password

  JWT_SECRET:
    type: Password

  SMTP_PASSWORD:
    type: Password
```

### Minimale Berechtigungen

```yaml
services:
  app:
    # Nicht als root laufen
    user: "1000:1000"

    # Read-only Filesystem wenn möglich
    volumes:
      - config:/app/config:ro    # Read-only Mount

    # Nur benötigte Ports
    ports:
      - "8080:8080"              # Nur der notwendige Port
```

---

## Netzwerk

### Interne Kommunikation

Nutze Service-Namen für interne Kommunikation:

```yaml
services:
  api:
    environment:
      # Interne URLs: Service-Name
      DATABASE_URL: postgres://db:5432/mydb
      CACHE_URL: redis://cache:6379

  db:
    image: postgres:15
    # Kein Port-Mapping für interne Services
    # ports:
    #   - "5432:5432"   # Nicht nötig!

  cache:
    image: redis:7
```

### Externe Zugänge

Exponiere nur notwendige Ports:

```yaml
services:
  # Externe Zugang: Port-Mapping
  nginx:
    ports:
      - "${HTTP_PORT}:80"
      - "${HTTPS_PORT}:443"

  # Interne Services: Keine Port-Mappings
  api:
    # Kein ports: Mapping!
    networks:
      - internal

  db:
    # Kein ports: Mapping!
    networks:
      - internal
```

### Netzwerk-Isolation

Trenne Netzwerke nach Funktion:

```yaml
services:
  nginx:
    networks:
      - frontend
      - backend

  api:
    networks:
      - backend
      - database

  db:
    networks:
      - database

networks:
  frontend:
    driver: bridge
  backend:
    driver: bridge
  database:
    driver: bridge
    internal: true    # Kein externer Zugang
```

---

## Volumes

### Benannte Volumes

Verwende benannte Volumes statt Bind Mounts:

```yaml
# GUT: Benannte Volumes (portabel)
volumes:
  postgres_data: {}
  redis_data: {}

services:
  db:
    volumes:
      - postgres_data:/var/lib/postgresql/data

# VERMEIDEN: Bind Mounts (host-spezifisch)
services:
  db:
    volumes:
      - /var/data/postgres:/var/lib/postgresql/data
```

### Backup-freundliche Struktur

Organisiere Volumes nach Backup-Wichtigkeit:

```yaml
volumes:
  # Kritisch: Regelmäßige Backups
  database_data: {}
  user_uploads: {}

  # Weniger kritisch: Seltene Backups
  logs: {}
  cache: {}

services:
  db:
    volumes:
      - database_data:/var/lib/postgresql/data

  app:
    volumes:
      - user_uploads:/app/uploads    # Kritisch
      - cache:/app/cache            # Unwichtig
```

---

## Logging

### Konsistente Log-Level

Definiere Log-Level als Shared Variable:

```yaml
sharedVariables:
  LOG_LEVEL:
    label: Log Level
    type: Select
    options:
      - value: debug
        description: Verbose output for debugging
      - value: info
        description: Standard operational logs
      - value: warning
        description: Only warnings and errors
      - value: error
        description: Only errors
    default: info
```

### Structured Logging

Konfiguriere strukturiertes Logging wenn möglich:

```yaml
services:
  api:
    environment:
      LOG_FORMAT: json
      LOG_LEVEL: ${LOG_LEVEL}
      LOG_OUTPUT: stdout
```

---

## Performance

### Resource-Aware Defaults

Setze Defaults basierend auf typischen Deployments:

```yaml
variables:
  # Für kleine/mittlere Installationen
  DB_POOL_SIZE:
    label: Database Connection Pool Size
    type: Number
    default: "10"
    min: 5
    max: 100
    description: |
      Connection pool size.
      Small installations: 5-10
      Medium: 10-25
      Large: 25-50
```

### Startup-Reihenfolge optimieren

Starte kritische Services zuerst:

```yaml
stacks:
  # 1. Infrastructure zuerst
  infrastructure:
    include: infrastructure.yaml

  # 2. Dann abhängige Services
  backend:
    include: backend.yaml

  # 3. Zuletzt Gateways
  gateway:
    include: gateway.yaml
```

---

## Dokumentation

### Metadata vollständig ausfüllen

```yaml
metadata:
  name: Enterprise Platform
  description: |
    Complete enterprise platform with:
    - User management and SSO
    - Document management
    - Collaboration tools
    - Reporting dashboard
  productVersion: "3.1.0"
  author: My Company
  documentation: https://docs.mycompany.com/platform
  category: Enterprise
  tags:
    - enterprise
    - collaboration
    - documents
```

### Variablen dokumentieren

```yaml
variables:
  CACHE_TTL:
    label: Cache TTL
    description: |
      Time-to-live for cached items in seconds.

      Recommended values:
      - Development: 60 (1 minute)
      - Production: 3600 (1 hour)

      Lower values = more database load but fresher data
      Higher values = less load but potentially stale data
    type: Number
    default: "300"
    min: 60
    max: 86400
```

---

## Checkliste

### Vor dem Release

- [ ] `productVersion` ist korrekt und folgt SemVer
- [ ] Alle Variablen haben `label` und `description`
- [ ] Sensible Daten nutzen `type: Password`
- [ ] Keine echten Passwörter in Defaults
- [ ] Health Checks für kritische Services definiert
- [ ] Restart-Policies gesetzt
- [ ] Volumes für persistente Daten definiert
- [ ] Dokumentation aktuell

### Code Review

- [ ] Variablen-Namen sind konsistent
- [ ] Gruppen und Reihenfolge sinnvoll
- [ ] Keine hardcodierten Werte die variabel sein sollten
- [ ] Netzwerk-Konfiguration korrekt
- [ ] Abhängigkeiten stimmen

---

## Weiterführende Dokumentation

- [Produkte](Products.md) - Grundlagen zu Produkten
- [Multi-Stack](Multi-Stack.md) - Multi-Stack Produkte
- [Stack Fragments](Stack-Fragments.md) - Details zu Fragments
- [Manifest Schema](../Reference/Manifest-Schema.md) - Vollständige Schema-Referenz

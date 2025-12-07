---
title: RSGo Manifest Schema
description: Vollständige Referenz des RSGo Manifest Formats - das native Stack-Definitionsformat für ReadyStackGo
---

Das RSGo Manifest ist das native Stack-Definitionsformat für ReadyStackGo. Es bietet typisierte Variablen, reichhaltige Metadaten, Multi-Stack Unterstützung und modulare Zusammensetzung durch Includes.

## Dokumentstruktur

```yaml
version: "1.0"                    # Format-Version (optional)

metadata:                         # Product/Stack Metadaten
  name: Mein Product
  productVersion: "1.0.0"         # Macht es zu einem Product (deploybar)
  ...

sharedVariables:                  # Variablen für alle Stacks (nur Multi-Stack)
  REGISTRY: ...

variables:                        # Variablen für diesen Stack (Single-Stack oder Fragment)
  PORT: ...

stacks:                           # Stack-Definitionen (nur Multi-Stack)
  api:
    include: api.yaml
  db:
    services: ...

services:                         # Service-Definitionen (Single-Stack oder Fragment)
  app: ...

volumes:                          # Volume-Definitionen
  data: {}

networks:                         # Netzwerk-Definitionen
  frontend: {}
```

---

## Manifest-Typen

### 1. Product (Single-Stack)

Ein Single-Stack Product enthält Services direkt auf Root-Ebene:

```yaml
metadata:
  name: Whoami
  productVersion: "1.0.0"         # ← Macht es zu einem Product

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

Ein Multi-Stack Product enthält mehrere Stacks mit geteilten Variablen:

```yaml
metadata:
  name: Enterprise Platform
  productVersion: "3.1.0"         # ← Macht es zu einem Product

sharedVariables:                  # ← Verfügbar für alle Stacks
  REGISTRY:
    type: String
    default: myregistry.io

stacks:
  api:
    include: api.yaml             # ← Externes Fragment
  monitoring:
    services:                     # ← Inline Stack
      prometheus: ...
```

### 3. Fragment

Ein Fragment hat keine `productVersion` und kann nur aus einem Product inkludiert werden:

```yaml
# identity.yaml - Fragment (keine productVersion!)
metadata:
  name: Identity Access
  description: Identity Provider

variables:
  CERT_PATH:
    type: String
    default: /etc/ssl/certs/identity.pfx

services:
  identity-api:
    image: ${REGISTRY}/identity:latest   # ← Nutzt geteilte Variable
```

---

## Metadata

### Product Metadata

| Eigenschaft | Typ | Pflicht | Beschreibung |
|-------------|-----|---------|--------------|
| `name` | string | **Ja** | Anzeigename des Products |
| `description` | string | Nein | Beschreibung des Products |
| `productVersion` | string | **Ja*** | Version (z.B. "3.1.0"). *Pflicht für Products |
| `author` | string | Nein | Autor oder Maintainer |
| `documentation` | string | Nein | URL zur Dokumentation |
| `icon` | string | Nein | URL zum Icon für die UI |
| `category` | string | Nein | Kategorie (z.B. "Database", "CMS") |
| `tags` | string[] | Nein | Tags für Suche und Filter |

**Beispiel:**

```yaml
metadata:
  name: WordPress
  description: Produktionsreifer WordPress Stack mit MySQL Backend
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

### Empfohlene Kategorien

| Kategorie | Beschreibung |
|-----------|--------------|
| `CMS` | Content Management Systeme |
| `Database` | Datenbanken und Datenspeicher |
| `Monitoring` | Monitoring, Logging, Observability |
| `Identity` | Authentifizierung und Autorisierung |
| `Messaging` | Message Broker und Queues |
| `Cache` | Caching-Systeme |
| `Storage` | Dateispeicher und Object Storage |
| `Testing` | Test- und Debug-Tools |
| `Enterprise` | Enterprise-Anwendungen |
| `Examples` | Beispiel-Stacks |

---

## Variablen

Variablen ermöglichen Benutzern die Konfiguration eines Products vor dem Deployment. Sie werden als Formularfelder in der ReadyStackGo UI angezeigt.

### Variablen-Definition

| Eigenschaft | Typ | Pflicht | Beschreibung |
|-------------|-----|---------|--------------|
| `label` | string | Nein | Lesbare Bezeichnung |
| `description` | string | Nein | Hilfetext in der UI |
| `type` | string | Nein | Variablentyp (Standard: `String`) |
| `default` | string | Nein | Standardwert |
| `required` | boolean | Nein | Ob die Variable ausgefüllt werden muss |
| `placeholder` | string | Nein | Platzhaltertext im Eingabefeld |
| `pattern` | string | Nein | Regex-Pattern zur Validierung |
| `patternError` | string | Nein | Fehlermeldung bei Validierungsfehler |
| `options` | array | Nein | Optionen für `Select`-Typ |
| `min` | number | Nein | Minimalwert für `Number`-Typ |
| `max` | number | Nein | Maximalwert für `Number`-Typ |
| `group` | string | Nein | Gruppenname für UI-Organisation |
| `order` | integer | Nein | Anzeigereihenfolge in der Gruppe |

Für die vollständige Variablentyp-Referenz siehe [Variablentypen](/de/reference/variable-types/).

### Variablen-Gruppierung

Variablen können für bessere UX in Gruppen organisiert werden:

```yaml
variables:
  # Netzwerk-Gruppe
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

  # Datenbank-Gruppe
  DB_HOST:
    label: Datenbank-Host
    type: String
    default: localhost
    group: Database
    order: 1
```

---

## Services

Services definieren die zu deployenden Docker Container.

### Service-Definition

| Eigenschaft | Typ | Pflicht | Beschreibung |
|-------------|-----|---------|--------------|
| `image` | string | **Ja** | Docker Image (z.B. `nginx:latest`) |
| `containerName` | string | Nein | Container-Name (Standard: `stack_servicename`) |
| `ports` | string[] | Nein | Port-Mappings (`host:container`) |
| `environment` | object | Nein | Umgebungsvariablen |
| `volumes` | string[] | Nein | Volume-Mappings |
| `networks` | string[] | Nein | Zu verbindende Netzwerke |
| `dependsOn` | string[] | Nein | Service-Abhängigkeiten |
| `restart` | string | Nein | Restart-Policy |
| `command` | string | Nein | Command-Override |
| `entrypoint` | string | Nein | Entrypoint-Override |
| `workingDir` | string | Nein | Arbeitsverzeichnis |
| `user` | string | Nein | Benutzer für Ausführung |
| `labels` | object | Nein | Container-Labels |
| `healthCheck` | object | Nein | Health-Check Konfiguration |

### Service-Beispiel

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

### Port-Mappings

```yaml
ports:
  - "8080:80"                    # host:container
  - "${PORT}:80"                 # Variablen-Substitution
  - "127.0.0.1:8080:80"          # Bindung an spezifische IP
  - "8080-8090:80-90"            # Port-Bereich
```

### Restart-Policies

| Policy | Beschreibung |
|--------|--------------|
| `no` | Nie neu starten (Standard) |
| `on-failure` | Bei Fehler neu starten |
| `unless-stopped` | Immer neu starten, außer explizit gestoppt |
| `always` | Immer neu starten |

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
  # Named Volume (von Docker verwaltet)
  app_data: {}

  # Volume mit Driver-Optionen
  db_data:
    driver: local
    driverOpts:
      type: none
      o: bind
      device: /mnt/data

  # Externes Volume (existiert bereits)
  shared_data:
    external: true
```

---

## Networks

```yaml
networks:
  # Standard Bridge-Netzwerk
  frontend:
    driver: bridge

  # Externes Netzwerk (existiert bereits)
  proxy:
    external: true
```

---

## Multi-Stack Products

### sharedVariables

In `sharedVariables` definierte Variablen sind für alle Stacks verfügbar:

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
        image: ${REGISTRY}/api:latest      # Nutzt REGISTRY
        environment:
          LOG_LEVEL: ${LOG_LEVEL}          # Nutzt LOG_LEVEL
```

### Stack-Einträge

Jeder Stack kann sein:
- **Include**: Referenz auf eine externe Fragment-Datei
- **Inline**: Vollständige Stack-Definition innerhalb des Products

```yaml
stacks:
  # Externe Datei einbinden
  identity:
    include: identity/identity-access.yaml

  # Include mit Variable Override
  api:
    include: api/api.yaml
    variables:
      LOG_LEVEL:
        default: debug             # Override für diesen Stack

  # Inline-Definition
  monitoring:
    metadata:
      name: Monitoring
    services:
      prometheus:
        image: prom/prometheus:latest
```

### Variable Override

Stacks können geteilte Variablen-Defaults überschreiben:

```yaml
sharedVariables:
  LOG_LEVEL:
    type: Select
    options:
      - value: debug
      - value: info
      - value: error
    default: info                  # Standard für die meisten Stacks

stacks:
  identity:
    include: identity.yaml
    variables:
      LOG_LEVEL:
        default: debug             # Identity benötigt mehr Logging
```

**Wert-Auflösung:**

| Priorität | Quelle |
|-----------|--------|
| 1 (höchste) | Benutzereingabe |
| 2 | Stack-Variable Override |
| 3 | Shared Variable Default |
| 4 (niedrigste) | Leer |

---

## Include-Mechanismus

Include-Pfade sind relativ zum Product-Manifest:

```
stacks/
└── myproduct/
    ├── myproduct.yaml           # include: identity/stack.yaml
    └── identity/
        └── stack.yaml           # ← Wird hier aufgelöst
```

---

## Variablen-Substitution

Variablen werden mit `${VARIABLE_NAME}` Syntax ersetzt:

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

## Dateistruktur

### Einzelne Products

```
stacks/
├── whoami.yaml                  # Einfaches Single-Stack Product
└── wordpress.yaml               # WordPress Product
```

### Multi-Stack Products

```
stacks/
└── enterprise-platform/
    ├── enterprise-platform.yaml # Product-Manifest
    ├── IdentityAccess/
    │   └── identity-access.yaml # Fragment
    └── Infrastructure/
        └── monitoring.yaml      # Fragment
```

---

## Vollständige Beispiele

### Einfaches Product

```yaml
metadata:
  name: Whoami
  description: Einfacher HTTP-Service für Tests
  productVersion: "1.0.0"
  category: Testing
  tags:
    - whoami
    - testing

variables:
  PORT:
    label: Port
    description: Port für den Service
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

### Datenbank-Product

```yaml
metadata:
  name: PostgreSQL
  description: PostgreSQL Datenbankserver
  productVersion: "15.0.0"
  category: Database

variables:
  POSTGRES_PORT:
    label: Port
    type: Port
    default: "5432"
    group: Network

  POSTGRES_USER:
    label: Benutzername
    type: String
    default: postgres
    group: Authentication

  POSTGRES_PASSWORD:
    label: Passwort
    type: Password
    required: true
    group: Authentication

  POSTGRES_DB:
    label: Datenbankname
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
  description: Vollständige Enterprise-Plattform mit modularen Komponenten
  productVersion: "3.1.0"
  category: Enterprise

sharedVariables:
  REGISTRY:
    label: Docker Registry
    type: String
    default: myregistry.io
    group: Registry

  ENVIRONMENT:
    label: Umgebung
    type: Select
    options:
      - value: development
        label: Entwicklung
      - value: staging
        label: Staging
      - value: production
        label: Produktion
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
    label: Datenbankverbindung
    type: SqlServerConnectionString
    group: Database

stacks:
  identity:
    include: IdentityAccess/identity-access.yaml
    variables:
      LOG_LEVEL:
        default: Debug            # Identity benötigt ausführliches Logging

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

## Loader-Verhalten

1. **Scannen**: Rekursiv `stacks/` nach `*.yaml` und `*.yml` Dateien durchsuchen
2. **Parsen**: Jede Manifest-Datei parsen
3. **Klassifizieren**:
   - Hat `metadata.productVersion` → **Product** (laden)
   - Keine `productVersion` → **Fragment** (überspringen, via include laden)
4. **Includes auflösen**: Include-Pfade relativ zum Product-Manifest auflösen
5. **Variablen mergen**: `sharedVariables` mit Stack-Variablen zusammenführen

---

## Siehe auch

- [Variablentypen](/de/reference/variable-types/)

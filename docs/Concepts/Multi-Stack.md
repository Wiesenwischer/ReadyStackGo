# Multi-Stack Produkte

## Übersicht

Ein **Multi-Stack Produkt** ist ein Produkt, das aus mehreren zusammengehörigen Stacks besteht. Dies ermöglicht die Modularisierung komplexer Anwendungen in logisch getrennte, aber gemeinsam verwaltete Einheiten.

---

## Warum Multi-Stack?

### Problemstellung

Bei komplexen Enterprise-Anwendungen entstehen häufig folgende Herausforderungen:

- **Große Manifest-Dateien**: Ein monolithisches Manifest mit 20+ Services wird unübersichtlich
- **Unterschiedliche Teams**: Verschiedene Teams verantworten verschiedene Komponenten
- **Separate Konfiguration**: Manche Services benötigen eigene Konfigurationsgruppen
- **Modulare Architektur**: Microservice-Architekturen erfordern modulare Deployment-Definitionen

### Lösung: Multi-Stack

Multi-Stack adressiert diese Probleme durch:

- **Modulare Organisation**: Jeder Stack in einer eigenen Datei
- **Gemeinsame Variablen**: `sharedVariables` gelten für alle Stacks
- **Stack-spezifische Variablen**: Jeder Stack kann eigene Variablen definieren
- **Include-Mechanismus**: Externe Dateien als Fragmente einbinden
- **Einheitliche Versionierung**: Das gesamte Produkt hat eine Version

---

## Architektur

### Single-Stack vs. Multi-Stack

```
┌─────────────────────────────────────────────────────────────────┐
│                      Single-Stack Produkt                        │
├─────────────────────────────────────────────────────────────────┤
│  metadata:                                                       │
│    productVersion: "1.0.0"                                       │
│                                                                  │
│  variables: { ... }                                              │
│                                                                  │
│  services:                                                       │
│    service-a: { ... }                                            │
│    service-b: { ... }                                            │
│    service-c: { ... }                                            │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      Multi-Stack Produkt                         │
├─────────────────────────────────────────────────────────────────┤
│  metadata:                                                       │
│    productVersion: "1.0.0"                                       │
│                                                                  │
│  sharedVariables: { ... }     ◄── Gelten für alle Stacks        │
│                                                                  │
│  stacks:                                                         │
│    ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐│
│    │   Stack: api    │  │  Stack: worker  │  │ Stack: monitor  ││
│    ├─────────────────┤  ├─────────────────┤  ├─────────────────┤│
│    │ variables: {}   │  │ variables: {}   │  │ variables: {}   ││
│    │ services:       │  │ services:       │  │ services:       ││
│    │   api-gateway   │  │   worker-1      │  │   prometheus    ││
│    │   api-backend   │  │   worker-2      │  │   grafana       ││
│    └─────────────────┘  └─────────────────┘  └─────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

### Dateistruktur

```
stacks/
└── enterprise-platform/
    ├── enterprise-platform.yaml    # Produkt-Manifest (mit productVersion)
    ├── identity.yaml               # Fragment: Identity Stack
    ├── api.yaml                    # Fragment: API Stack
    ├── worker.yaml                 # Fragment: Worker Stack
    └── monitoring.yaml             # Fragment: Monitoring Stack
```

---

## Manifest-Struktur

### Produkt-Manifest mit Stacks

```yaml
version: "1.0"

metadata:
  name: Enterprise Platform
  description: Complete enterprise platform with modular components
  productVersion: "3.1.0"
  category: Enterprise
  tags:
    - enterprise
    - microservices
    - platform

# Gemeinsame Variablen für alle Stacks
sharedVariables:
  REGISTRY:
    label: Docker Registry
    description: Docker registry for all images
    type: String
    default: myregistry.io
    group: Registry
    order: 1

  ENVIRONMENT:
    label: Environment
    description: Deployment environment
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
    order: 1

  LOG_LEVEL:
    label: Log Level
    description: Minimum log level for all services
    type: Select
    options:
      - value: debug
      - value: info
      - value: warning
      - value: error
    default: info
    group: Logging
    order: 1

  DB_CONNECTION:
    label: Database Connection
    description: Primary database connection string
    type: SqlServerConnectionString
    default: "Server=db;Database=app;User Id=sa;Password=Password123!"
    group: Database
    order: 1

# Stack-Definitionen
stacks:
  # Stack via Include (externe Datei)
  identity:
    include: identity.yaml

  # Stack via Include
  api:
    include: api.yaml

  # Inline-Stack-Definition
  monitoring:
    metadata:
      name: Monitoring
      description: Prometheus and Grafana monitoring stack

    variables:
      GRAFANA_PORT:
        label: Grafana Port
        type: Port
        default: "3000"
        group: Monitoring

      GRAFANA_ADMIN_PASSWORD:
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
          GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_ADMIN_PASSWORD}
          GF_LOG_LEVEL: ${LOG_LEVEL}
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

## Shared Variables

### Konzept

`sharedVariables` sind Variablen, die für alle Stacks im Produkt gelten. Sie werden einmal definiert und können in jedem Stack referenziert werden.

```yaml
sharedVariables:
  REGISTRY:
    label: Docker Registry
    type: String
    default: docker.io

stacks:
  api:
    services:
      api:
        image: ${REGISTRY}/api:latest    # Verwendet sharedVariable

  worker:
    services:
      worker:
        image: ${REGISTRY}/worker:latest  # Verwendet dieselbe Variable
```

### UI-Darstellung

In der Deployment-UI werden Shared Variables prominent angezeigt:

```
┌─ Gemeinsame Einstellungen ────────────────────────────────────────┐
│                                                                    │
│  Docker Registry:    [myregistry.io        ]                       │
│  Environment:        [Development     ▼    ]                       │
│  Log Level:          [info            ▼    ]                       │
│  Database Connection: [Server=db;...       ] [Builder ⚙]          │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘

┌─ Stack: Identity ─────────────────────────────────────────────────┐
│  Certificate Path:   [/etc/ssl/certs/...   ]                       │
│  Certificate Password: [********          ]                        │
└────────────────────────────────────────────────────────────────────┘

┌─ Stack: Monitoring ───────────────────────────────────────────────┐
│  Grafana Port:       [3000    ]                                    │
│  Grafana Admin Password: [********]                                │
└────────────────────────────────────────────────────────────────────┘
```

---

## Variable Override

### Stack-spezifische Überschreibung

Stacks können Shared Variables überschreiben. Dies ist nützlich, wenn ein Stack einen anderen Default-Wert benötigt:

```yaml
sharedVariables:
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

stacks:
  api:
    # Verwendet den Shared Default: info
    services:
      api:
        environment:
          LOG_LEVEL: ${LOG_LEVEL}

  identity:
    variables:
      LOG_LEVEL:
        default: debug    # Override: Identity braucht mehr Logging

    services:
      identity:
        environment:
          LOG_LEVEL: ${LOG_LEVEL}
```

### Override-Regeln

1. **Definition Merge**: Stack-Variablen erben alle Eigenschaften der Shared Variable
2. **Default Override**: Nur der `default`-Wert wird überschrieben
3. **UI Verhalten**: Override wird in der UI als "angepasst" markiert

### Priorität der Werte

```
┌─────────────────────────────────────────────────────────────┐
│                    Wert-Auflösung                            │
├─────────────────────────────────────────────────────────────┤
│  1. Benutzer-Eingabe (höchste Priorität)                     │
│     ↓                                                        │
│  2. Stack-spezifischer Override                              │
│     ↓                                                        │
│  3. Shared Variable Default                                  │
│     ↓                                                        │
│  4. Leerer Wert (wenn kein Default)                          │
└─────────────────────────────────────────────────────────────┘
```

---

## Include-Mechanismus

### Externe Fragmente einbinden

Mit `include` werden externe YAML-Dateien als Stack eingebunden:

```yaml
# enterprise-platform.yaml (Produkt)
stacks:
  identity:
    include: identity.yaml

  api:
    include: subdirectory/api.yaml
```

```yaml
# identity.yaml (Fragment - kein productVersion!)
metadata:
  name: Identity
  description: Identity Provider Stack

variables:
  IDENTITY_PORT:
    label: Identity Port
    type: Port
    default: "7614"

services:
  identity-api:
    image: ${REGISTRY}/identity:latest
    ports:
      - "${IDENTITY_PORT}:8080"
    environment:
      DB_CONNECTION: ${DB_CONNECTION}
      LOG_LEVEL: ${LOG_LEVEL}
```

### Include mit Variables-Override

Auch bei Include können Variablen überschrieben werden:

```yaml
stacks:
  identity:
    include: identity.yaml
    variables:
      LOG_LEVEL:
        default: debug    # Override für diesen Stack
```

### Pfad-Auflösung

Include-Pfade werden relativ zum Produkt-Manifest aufgelöst:

```
stacks/
└── enterprise/
    ├── enterprise.yaml          # include: identity/stack.yaml
    └── identity/
        └── stack.yaml           # Wird hier gesucht
```

---

## Praxisbeispiel: Enterprise Microservices

### Verzeichnisstruktur

```
stacks/
└── ams.project/
    ├── ams-project.yaml              # Hauptprodukt
    ├── IdentityAccess/
    │   └── identity-access.yaml      # Identity Fragment
    ├── ProjectManagement/
    │   └── project.yaml              # Project Fragment
    ├── Collaboration/
    │   └── collaboration.yaml        # Collaboration Fragment
    └── Infrastructure/
        ├── database.yaml             # Database Fragment
        └── messaging.yaml            # Messaging Fragment
```

### Hauptprodukt

```yaml
# ams-project.yaml
version: "1.0"

metadata:
  name: ams.project
  description: Enterprise Project Management Platform
  productVersion: "3.1.0"
  category: Enterprise
  tags:
    - ams
    - project-management
    - enterprise

sharedVariables:
  # Registry
  REGISTRY:
    label: Docker Registry
    type: String
    default: amssolution
    group: Registry

  # Environment
  ENVIRONMENT_NAME:
    label: Environment Name
    type: String
    default: local
    group: Environment

  # External Access
  EXTERNAL_DNS:
    label: External DNS/IP
    description: External DNS name or IP for service access
    type: String
    default: host.docker.internal
    group: Network

  # Primary Database
  AMS_DB:
    label: AMS Database
    type: SqlServerConnectionString
    default: "Server=ams-mssql;Database=AMS01;User Id=sa;Password=Password123!;TrustServerCertificate=true;"
    group: Database

  # Logging
  MIN_LOG_LEVEL:
    label: Minimum Log Level
    type: Select
    options:
      - value: Verbose
      - value: Debug
      - value: Information
      - value: Warning
      - value: Error
      - value: Fatal
    default: Warning
    group: Logging

  # Service Bus
  SERVICE_BUS_CATALOG:
    label: Service Bus Catalog
    type: String
    default: AMS01
    group: ServiceBus

stacks:
  # Infrastructure zuerst
  infrastructure:
    include: Infrastructure/database.yaml

  # Identity als Basis für andere Services
  identity:
    include: IdentityAccess/identity-access.yaml

  # Business Domains
  project:
    include: ProjectManagement/project.yaml

  collaboration:
    include: Collaboration/collaboration.yaml
```

### Identity Fragment

```yaml
# IdentityAccess/identity-access.yaml
metadata:
  name: Identity Access
  description: Identity Provider based on IdentityServer

variables:
  # Stack-spezifische Variablen
  IDENTITY_ACCESS_DB:
    label: Identity Database
    type: SqlServerConnectionString
    default: "Server=ams-mssql;Database=AMS01-Identity;User Id=sa;Password=Password123!;TrustServerCertificate=true;"
    group: Database

  CERT_PATH:
    label: Certificate Path
    type: String
    default: /etc/ssl/certs/identity-api.pfx
    group: Security

  CERT_PASSWORD:
    label: Certificate Password
    type: Password
    default: "P@ssw0rd!"
    group: Security

  # Override: Identity braucht mehr Logging
  MIN_LOG_LEVEL:
    default: Debug

services:
  identity-api:
    image: ${REGISTRY}/identityaccess.identityserver:linux-latest
    ports:
      - "7614:8080"
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__IdentityAccess: ${IDENTITY_ACCESS_DB}
      ConnectionStrings__ams: ${AMS_DB}
      IdentityServerUrl: http://${EXTERNAL_DNS}:7614
      Serilog__MinimumLevel: ${MIN_LOG_LEVEL}
      CertificatePath: ${CERT_PATH}
      CertificatePassword: ${CERT_PASSWORD}
      SERVICE_BUS_CATALOG: ${SERVICE_BUS_CATALOG}
    networks:
      - ams-network

  identity-adminportal:
    image: ${REGISTRY}/identityaccess.adminportal:linux-latest
    ports:
      - "7615:8080"
    restart: unless-stopped
    environment:
      ConnectionStrings__IdentityAccess: ${IDENTITY_ACCESS_DB}
      IdentityServerUrl: http://identity-api:8080
      Serilog__MinimumLevel: ${MIN_LOG_LEVEL}
    networks:
      - ams-network

networks:
  ams-network:
    driver: bridge
```

---

## Deployment-Reihenfolge

### Automatische Abhängigkeitsauflösung

ReadyStackGo deployed Stacks in der definierten Reihenfolge. Innerhalb eines Stacks werden `dependsOn`-Abhängigkeiten berücksichtigt:

```yaml
stacks:
  # 1. Wird zuerst deployed
  infrastructure:
    include: infrastructure.yaml

  # 2. Wird nach infrastructure deployed
  identity:
    include: identity.yaml

  # 3. Wird nach identity deployed
  api:
    include: api.yaml
```

### Explizite Stack-Abhängigkeiten

Für komplexe Szenarien können Stack-Abhängigkeiten explizit definiert werden (geplantes Feature):

```yaml
stacks:
  api:
    include: api.yaml
    dependsOn:
      - identity
      - database
```

---

## Best Practices

### 1. Logische Gruppierung

Gruppiere Services nach Domain oder Funktion:

```yaml
stacks:
  identity:      # Authentifizierung & Autorisierung
  business:      # Geschäftslogik
  integration:   # API-Gateways, BFFs
  infrastructure: # Datenbanken, Caches
  monitoring:    # Prometheus, Grafana
```

### 2. Shared Variables sinnvoll nutzen

Definiere als Shared Variables:
- Registry-Einstellungen
- Umgebungskonfiguration
- Gemeinsame Datenbank-Connections
- Logging-Einstellungen
- Netzwerk-Konfiguration

### 3. Fragment-Granularität

- **Zu fein**: Jeder Service ein Fragment → Overhead
- **Zu grob**: Alles in einem Fragment → Kein Vorteil
- **Optimal**: Ein Fragment pro Domain/Bounded Context

### 4. Konsistente Namenskonvention

```
stacks/
└── product-name/
    ├── product-name.yaml           # Produkt (mit Bindestrich)
    ├── domain-a/
    │   └── domain-a.yaml           # Fragment
    └── domain-b/
        └── domain-b.yaml           # Fragment
```

### 5. Variable Overrides dokumentieren

Wenn ein Stack einen Override definiert, dokumentiere warum:

```yaml
variables:
  # Identity benötigt detailliertes Logging für
  # Debugging von Authentication-Problemen
  MIN_LOG_LEVEL:
    default: Debug
```

---

## Troubleshooting

### Fragment wird nicht geladen

**Problem**: Stack wird in der UI nicht angezeigt

**Lösung**: Prüfe, dass das Fragment kein `productVersion` hat

```yaml
# FALSCH - wird als eigenes Produkt geladen
metadata:
  name: Identity
  productVersion: "1.0.0"  # ← Entfernen!

# RICHTIG - wird als Fragment behandelt
metadata:
  name: Identity
  # Kein productVersion!
```

### Variable nicht aufgelöst

**Problem**: `${VARIABLE}` wird nicht ersetzt

**Lösung**: Prüfe, dass die Variable in `sharedVariables` oder `variables` definiert ist

```yaml
sharedVariables:
  MY_VAR:
    default: value

stacks:
  my-stack:
    services:
      app:
        environment:
          VAR: ${MY_VAR}  # ← Muss in sharedVariables oder stack.variables definiert sein
```

### Include-Pfad nicht gefunden

**Problem**: `include: path/to/file.yaml` schlägt fehl

**Lösung**: Pfade sind relativ zum Produkt-Manifest

```yaml
# Wenn Produkt in: stacks/myproduct/myproduct.yaml
# Dann ist include relativ zu: stacks/myproduct/

include: subfolder/fragment.yaml
# → Sucht: stacks/myproduct/subfolder/fragment.yaml
```

---

## Weiterführende Dokumentation

- [Produkte](Products.md) - Grundlagen zu Produkten
- [Stack Fragments](Stack-Fragments.md) - Details zu Fragments
- [Manifest Schema](../Reference/Manifest-Schema.md) - Vollständige Schema-Referenz
- [Best Practices](Best-Practices.md) - Empfehlungen für die Praxis

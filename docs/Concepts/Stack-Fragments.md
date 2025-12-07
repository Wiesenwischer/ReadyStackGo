# Stack Fragments

## Übersicht

Ein **Stack Fragment** (oder kurz **Fragment**) ist ein wiederverwendbarer, modularer Baustein für Multi-Stack Produkte. Fragments enthalten Service-Definitionen, Variablen und Konfigurationen, sind aber nicht eigenständig deploybar.

---

## Was ist ein Fragment?

Ein Fragment ist:

- **Kein Produkt**: Hat kein `productVersion` in der Metadata
- **Nicht direkt deploybar**: Erscheint nicht in der Stack-Auswahl
- **Inkludierbar**: Wird über `include` in ein Produkt eingebunden
- **Wiederverwendbar**: Kann in mehreren Produkten verwendet werden
- **Eigenständig konfigurierbar**: Kann eigene Variablen definieren

### Warum Fragments?

| Szenario | Ohne Fragments | Mit Fragments |
|----------|----------------|---------------|
| Große Manifeste | 1000+ Zeilen, unübersichtlich | Modular, pro Datei ~100-200 Zeilen |
| Team-Struktur | Alle editieren eine Datei | Jedes Team hat eigene Dateien |
| Wiederverwendung | Copy & Paste | Include & Anpassen |
| Code Reviews | Schwer zu reviewen | Fokussierte Änderungen |
| Versionskontrolle | Merge-Konflikte | Weniger Konflikte |

---

## Fragment vs. Produkt

### Erkennungsmerkmal

Der entscheidende Unterschied ist das Fehlen von `productVersion`:

```yaml
# PRODUKT - hat productVersion
metadata:
  name: WordPress
  productVersion: "6.0.0"    # ← Macht es zum Produkt
```

```yaml
# FRAGMENT - hat KEIN productVersion
metadata:
  name: Identity Access
  description: Identity Provider
  # Kein productVersion!       # ← Macht es zum Fragment
```

### Loader-Verhalten

ReadyStackGo scannt das `stacks/` Verzeichnis und unterscheidet:

```
Scan stacks/**/*.yaml
        │
        ▼
    Hat productVersion?
        │
    ┌───┴───┐
    │       │
   Ja      Nein
    │       │
    ▼       ▼
 Produkt  Fragment
 (laden)  (ignorieren,
          nur via include)
```

---

## Anatomie eines Fragments

### Minimales Fragment

```yaml
metadata:
  name: My Fragment

services:
  my-service:
    image: myimage:latest
```

### Vollständiges Fragment

```yaml
# identity-access.yaml
metadata:
  name: Identity Access
  description: Identity Provider based on IdentityServer
  category: Identity
  tags:
    - identity
    - authentication
    - oauth

variables:
  # Stack-spezifische Variablen
  IDENTITY_PORT:
    label: Identity API Port
    description: Port for the Identity API
    type: Port
    default: "7614"
    group: Network
    order: 1

  CERT_PATH:
    label: Certificate Path
    description: Path to the TLS certificate
    type: String
    default: /etc/ssl/certs/identity.pfx
    group: Security
    order: 1

  CERT_PASSWORD:
    label: Certificate Password
    description: Password for the certificate
    type: Password
    required: true
    group: Security
    order: 2

  # Override einer Shared Variable (vom Produkt)
  LOG_LEVEL:
    default: Debug    # Identity braucht mehr Logging

services:
  identity-api:
    image: ${REGISTRY}/identity-api:latest
    ports:
      - "${IDENTITY_PORT}:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: ${ENVIRONMENT}
      ConnectionStrings__Identity: ${IDENTITY_DB}
      ConnectionStrings__Main: ${MAIN_DB}
      CertificatePath: ${CERT_PATH}
      CertificatePassword: ${CERT_PASSWORD}
      Serilog__MinimumLevel: ${LOG_LEVEL}
    volumes:
      - identity_certs:/etc/ssl/certs:ro
    networks:
      - app-network
    restart: unless-stopped
    healthCheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  identity-admin:
    image: ${REGISTRY}/identity-admin:latest
    ports:
      - "7615:8080"
    environment:
      IdentityServerUrl: http://identity-api:8080
      Serilog__MinimumLevel: ${LOG_LEVEL}
    dependsOn:
      - identity-api
    networks:
      - app-network
    restart: unless-stopped

volumes:
  identity_certs: {}

networks:
  app-network:
    driver: bridge
```

---

## Variablen in Fragments

### Lokale Fragment-Variablen

Fragments können eigene Variablen definieren, die nur für diesen Stack gelten:

```yaml
# Fragment
variables:
  MY_LOCAL_VAR:
    label: Lokale Variable
    type: String
    default: local-value

services:
  my-service:
    environment:
      MY_VAR: ${MY_LOCAL_VAR}    # Nur in diesem Fragment verfügbar
```

### Referenzieren von Shared Variables

Fragments können Variablen referenzieren, die im Produkt als `sharedVariables` definiert sind:

```yaml
# Produkt
sharedVariables:
  REGISTRY:
    default: docker.io
  LOG_LEVEL:
    default: info
```

```yaml
# Fragment - kann Shared Variables nutzen
services:
  my-service:
    image: ${REGISTRY}/my-image:latest    # ← Aus sharedVariables
    environment:
      LOG_LEVEL: ${LOG_LEVEL}             # ← Aus sharedVariables
```

### Override von Shared Variables

Fragments können den Default-Wert einer Shared Variable überschreiben:

```yaml
# Produkt
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
```

```yaml
# Fragment mit Override
variables:
  LOG_LEVEL:
    default: debug    # Nur der Default wird überschrieben
                      # Alle anderen Eigenschaften (label, type, options)
                      # werden vom Produkt geerbt
```

### Variablen-Auflösung

Die Reihenfolge der Auflösung:

1. **Benutzer-Eingabe** (höchste Priorität)
2. **Fragment-Variable Default** (wenn definiert)
3. **Shared Variable Default**
4. **Leer** (wenn kein Default)

```
User Input:     "error"    →  Verwendet: "error"
Fragment:       "debug"    →  Verwendet: "debug" (wenn kein User Input)
Shared:         "info"     →  Verwendet: "info" (wenn weder User noch Fragment)
```

---

## Einbindung in Produkte

### Einfaches Include

```yaml
# Produkt
stacks:
  identity:
    include: identity-access.yaml
```

### Include mit Variablen-Override

```yaml
# Produkt
stacks:
  identity:
    include: identity-access.yaml
    variables:
      LOG_LEVEL:
        default: warning    # Override auf Produkt-Ebene
```

### Include aus Unterverzeichnissen

```yaml
# Produkt in: stacks/enterprise/enterprise.yaml
stacks:
  identity:
    include: IdentityAccess/identity.yaml
    # → Sucht: stacks/enterprise/IdentityAccess/identity.yaml

  monitoring:
    include: ../shared/monitoring.yaml
    # → Sucht: stacks/shared/monitoring.yaml
```

---

## Wiederverwendung von Fragments

### Szenario: Gemeinsames Monitoring

Mehrere Produkte nutzen dasselbe Monitoring-Fragment:

```
stacks/
├── shared/
│   └── monitoring.yaml           # Gemeinsames Fragment
├── product-a/
│   └── product-a.yaml            # include: ../shared/monitoring.yaml
└── product-b/
    └── product-b.yaml            # include: ../shared/monitoring.yaml
```

### Monitoring-Fragment

```yaml
# shared/monitoring.yaml
metadata:
  name: Monitoring Stack
  description: Prometheus and Grafana monitoring

variables:
  PROMETHEUS_RETENTION:
    label: Data Retention
    description: How long to keep metrics data
    type: String
    default: 15d
    group: Storage

  GRAFANA_PORT:
    label: Grafana Port
    type: Port
    default: "3000"
    group: Network

  GRAFANA_ADMIN_PASSWORD:
    label: Grafana Admin Password
    type: Password
    default: admin
    group: Security

services:
  prometheus:
    image: prom/prometheus:latest
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.retention.time=${PROMETHEUS_RETENTION}'
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
      GF_USERS_ALLOW_SIGN_UP: "false"
    volumes:
      - grafana_data:/var/lib/grafana
    dependsOn:
      - prometheus
    restart: unless-stopped

volumes:
  prometheus_data: {}
  grafana_data: {}
```

### Verwendung in Produkt A

```yaml
# product-a/product-a.yaml
metadata:
  name: Product A
  productVersion: "1.0.0"

stacks:
  app:
    services:
      api:
        image: product-a/api:latest

  monitoring:
    include: ../shared/monitoring.yaml
    variables:
      GRAFANA_PORT:
        default: "3001"    # Anderer Port für Product A
```

### Verwendung in Produkt B

```yaml
# product-b/product-b.yaml
metadata:
  name: Product B
  productVersion: "2.0.0"

stacks:
  app:
    services:
      api:
        image: product-b/api:latest

  monitoring:
    include: ../shared/monitoring.yaml
    variables:
      PROMETHEUS_RETENTION:
        default: 30d       # Längere Retention für Product B
```

---

## Organisationsstrategien

### Nach Domain (Bounded Context)

Ideal für DDD-basierte Architekturen:

```
stacks/
└── enterprise/
    ├── enterprise.yaml
    ├── IdentityAccess/
    │   └── identity.yaml
    ├── ProjectManagement/
    │   └── project.yaml
    ├── Collaboration/
    │   └── collab.yaml
    └── Reporting/
        └── reporting.yaml
```

### Nach Layer

Für klassische Layer-Architekturen:

```
stacks/
└── enterprise/
    ├── enterprise.yaml
    ├── infrastructure/
    │   ├── database.yaml
    │   ├── cache.yaml
    │   └── messaging.yaml
    ├── backend/
    │   ├── api.yaml
    │   └── workers.yaml
    └── frontend/
        ├── web.yaml
        └── admin.yaml
```

### Nach Team

Wenn Teams für bestimmte Bereiche verantwortlich sind:

```
stacks/
└── enterprise/
    ├── enterprise.yaml
    ├── team-platform/
    │   ├── infrastructure.yaml
    │   └── monitoring.yaml
    ├── team-identity/
    │   └── identity.yaml
    └── team-business/
        ├── orders.yaml
        └── inventory.yaml
```

---

## Best Practices

### 1. Klare Verantwortlichkeiten

Jedes Fragment sollte eine klare, abgegrenzte Verantwortlichkeit haben:

```yaml
# GUT: Klare Verantwortlichkeit
metadata:
  name: Identity Access
  description: Authentication and Authorization services

# SCHLECHT: Zu viel in einem Fragment
metadata:
  name: Identity and Logging and Monitoring
```

### 2. Sinnvolle Variablen-Defaults

Setze Defaults, die für die meisten Szenarien funktionieren:

```yaml
variables:
  # GUT: Sinnvoller Default
  LOG_LEVEL:
    default: info

  # SCHLECHT: Kein Default, User muss immer eingeben
  LOG_LEVEL:
    required: true
```

### 3. Dokumentation im Fragment

Nutze `description` für alle Variablen und die Metadata:

```yaml
metadata:
  name: Identity Access
  description: |
    Identity Provider based on IdentityServer.
    Provides OAuth2/OpenID Connect authentication.

variables:
  CERT_PATH:
    label: Certificate Path
    description: |
      Path to the PFX certificate file.
      Must be mounted as volume.
```

### 4. Konsistente Gruppen

Verwende konsistente Gruppennamen über alle Fragments:

```yaml
# Standard-Gruppen:
# - Network      (Ports, DNS, URLs)
# - Database     (Connection Strings)
# - Security     (Zertifikate, Passwörter)
# - Logging      (Log Level, Sinks)
# - Performance  (Timeouts, Pools)
```

### 5. Minimale Abhängigkeiten

Fragments sollten möglichst wenige Shared Variables benötigen:

```yaml
# GUT: Wenige externe Abhängigkeiten
services:
  app:
    image: ${REGISTRY}/app:latest    # Nur REGISTRY von außen

# VERMEIDEN: Zu viele externe Abhängigkeiten
services:
  app:
    environment:
      VAR1: ${SHARED_VAR_1}
      VAR2: ${SHARED_VAR_2}
      VAR3: ${SHARED_VAR_3}
      # ... 10 weitere
```

---

## Troubleshooting

### Fragment erscheint als eigenes Produkt

**Problem**: Fragment wird in der Stack-Liste angezeigt

**Ursache**: `productVersion` ist definiert

**Lösung**: `productVersion` entfernen

```yaml
metadata:
  name: My Fragment
  # productVersion: "1.0.0"  ← Entfernen!
```

### Include-Datei nicht gefunden

**Problem**: Fehler beim Laden des Produkts

**Ursache**: Falscher Pfad

**Lösung**: Pfade sind relativ zum Produkt-Manifest

```yaml
# Produkt in: stacks/myproduct/main.yaml

# RICHTIG
include: fragments/identity.yaml
# → Sucht: stacks/myproduct/fragments/identity.yaml

# FALSCH
include: /absolute/path/identity.yaml
include: stacks/myproduct/fragments/identity.yaml
```

### Variable nicht aufgelöst

**Problem**: `${VAR}` wird nicht ersetzt

**Mögliche Ursachen**:

1. Variable nicht in `sharedVariables` oder `variables` definiert
2. Tippfehler im Variablennamen
3. Variable in anderem Stack definiert (nicht geteilt)

**Lösung**: Prüfe Variablen-Definition

```yaml
# Produkt
sharedVariables:
  MY_VAR:      # ← Muss hier sein
    default: value

# Oder im Fragment
variables:
  MY_VAR:      # ← Oder hier
    default: value

# Service
services:
  app:
    environment:
      VAR: ${MY_VAR}    # ← Muss mit Definition übereinstimmen
```

---

## Weiterführende Dokumentation

- [Produkte](Products.md) - Grundlagen zu Produkten
- [Multi-Stack](Multi-Stack.md) - Multi-Stack Produkte
- [Manifest Schema](../Reference/Manifest-Schema.md) - Vollständige Schema-Referenz
- [Best Practices](Best-Practices.md) - Empfehlungen für die Praxis

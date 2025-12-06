---
title: RSGo Manifest Format
description: Vollständige Referenz des RSGo Manifest Formats - das native Stack-Format von ReadyStackGo
---

Das RSGo Manifest ist das native Stack-Definitionsformat für ReadyStackGo. Es erweitert Docker Compose Konzepte um Typisierung, Validierung, reichhaltige Metadaten und Multi-Stack Unterstützung.

## Manifest-Typen

### 1. Product Manifest

Ein **Product** ist die primäre Deployment-Einheit. Es hat eine `productVersion` im Metadata-Bereich.

```yaml
metadata:
  name: WordPress
  description: WordPress mit MySQL Backend
  productVersion: "6.0.0"      # Dies macht es zu einem Product
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

Ein **Fragment** hat keine `productVersion` und kann nur via `include` aus einem Product geladen werden.

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

Ein Product kann mehrere Stacks enthalten, entweder inline oder via include:

```yaml
metadata:
  name: ams.project
  description: Enterprise Platform
  productVersion: "3.1.0"
  category: Enterprise

# Variablen für alle Stacks
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
  # Externe Datei einbinden
  identity:
    include: identity-access.yaml

  # Inline-Definition
  monitoring:
    metadata:
      name: Monitoring
    services:
      prometheus:
        image: prom/prometheus:latest
```

---

## Schema-Referenz

### Root-Eigenschaften

| Eigenschaft | Typ | Pflicht | Beschreibung |
|-------------|-----|---------|--------------|
| `version` | string | Ja | Manifest-Format Version (aktuell "1.0") |
| `metadata` | object | Ja | Product/Stack Metadaten |
| `sharedVariables` | object | Nein | Variablen für alle Stacks (nur Multi-Stack) |
| `variables` | object | Nein | Variablen-Definitionen (Single-Stack) |
| `stacks` | object | Nein | Stack-Definitionen (Multi-Stack) |
| `services` | object | Nein | Service-Definitionen (Single-Stack) |
| `volumes` | object | Nein | Volume-Definitionen |
| `networks` | object | Nein | Network-Definitionen |

### Metadata

| Eigenschaft | Typ | Pflicht | Beschreibung |
|-------------|-----|---------|--------------|
| `name` | string | Ja | Lesbarer Name |
| `description` | string | Nein | Beschreibung |
| `productVersion` | string | Nein* | Version (z.B. "3.1.0"). *Pflicht für Products |
| `author` | string | Nein | Autor oder Maintainer |
| `documentation` | string | Nein | URL zur Dokumentation |
| `icon` | string | Nein | Icon-URL für die UI |
| `category` | string | Nein | Kategorie (z.B. "Database", "CMS") |
| `tags` | array | Nein | Tags für Filter |

### Variable Definition

| Eigenschaft | Typ | Pflicht | Beschreibung |
|-------------|-----|---------|--------------|
| `label` | string | Nein | Lesbares Label |
| `description` | string | Nein | Hilfetext |
| `type` | string | Nein | Variablentyp (siehe unten) |
| `default` | string | Nein | Standardwert |
| `required` | boolean | Nein | Ob die Variable erforderlich ist |
| `pattern` | string | Nein | Regex-Pattern für Validierung |
| `patternError` | string | Nein | Fehlermeldung bei Pattern-Verletzung |
| `options` | array | Nein | Optionen für Select-Typ |
| `min` | number | Nein | Minimalwert (Number-Typ) |
| `max` | number | Nein | Maximalwert (Number-Typ) |
| `placeholder` | string | Nein | Platzhalter-Text |
| `group` | string | Nein | Gruppenname für UI-Organisation |
| `order` | integer | Nein | Anzeigereihenfolge in der Gruppe |

---

## Beispiele

### Einfaches Single-Stack Product

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

### Multi-Stack mit geteilten Variablen

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

### Kompletter Stack mit allen Variablentypen

```yaml
version: "1.0"

metadata:
  name: Complete Example
  productVersion: "1.0.0"

variables:
  # String mit Pattern-Validierung
  EMAIL:
    label: Admin Email
    type: String
    pattern: "^[^@]+@[^@]+$"
    patternError: Muss eine gültige E-Mail sein
    required: true
    group: Admin
    order: 1

  # Passwort
  DB_PASSWORD:
    label: Datenbank Passwort
    type: Password
    required: true
    group: Database
    order: 1

  # Number mit Bereich
  WORKERS:
    label: Worker Anzahl
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
    label: Umgebung
    type: Select
    options:
      - value: development
        label: Development
        description: Lokale Entwicklung
      - value: staging
        label: Staging
      - value: production
        label: Production
        description: Produktionsumgebung
    default: development
    group: General

  # Boolean
  DEBUG:
    label: Debug aktivieren
    type: Boolean
    default: "false"
    group: General

  # SQL Server Connection String
  DB_CONNECTION:
    label: Datenbankverbindung
    type: SqlServerConnectionString
    required: true
    group: Database
    order: 2

services:
  app:
    image: myapp:latest
    ports:
      - "${WEB_PORT}:8080"
    environment:
      ADMIN_EMAIL: ${EMAIL}
      DB_PASSWORD: ${DB_PASSWORD}
      DB_CONNECTION: ${DB_CONNECTION}
      WORKERS: ${WORKERS}
      ENVIRONMENT: ${ENVIRONMENT}
      DEBUG: ${DEBUG}
```

---

## Dateistruktur

### Einzelne Products

```
stacks/
  whoami.yaml              # Single-Stack Product
  wordpress.yaml           # Single-Stack Product
```

### Multi-Stack Products

```
stacks/
  ams-project/
    ams-project.yaml       # Product Manifest mit includes
    identity-access.yaml   # Fragment
    infrastructure.yaml    # Fragment
    monitoring.yaml        # Fragment
```

### Gemischt

```
stacks/
  whoami.yaml              # Single-Stack Product
  wordpress.yaml           # Single-Stack Product
  ams-project/
    ams-project.yaml       # Multi-Stack Product
    identity-access.yaml   # Fragment
```

---

## Loader-Verhalten

1. Alle `*.yaml` / `*.yml` Dateien rekursiv scannen
2. Jede Datei parsen
3. Wenn `metadata.productVersion` existiert → Als Product laden
4. Keine `productVersion` → Überspringen (Fragment, wird via include geladen)
5. `include` Referenzen relativ zur Manifest-Datei auflösen
6. `sharedVariables` mit stack-spezifischen Variablen zusammenführen

## Variablen-Priorität

1. Stack-spezifische Variablen überschreiben geteilte Variablen
2. Vom Benutzer eingegebene Werte überschreiben Standardwerte
3. `.env` Datei-Werte (falls vorhanden) überschreiben YAML-Standardwerte

---

## Multi-Stack Variable Override Behavior

Wenn eine Variable sowohl in `sharedVariables` als auch in den `variables` eines Stacks definiert ist, gelten folgende Regeln:

### Definition Merge

Stack-spezifische Variablen-Definitionen **erweitern** geteilte Variablen-Definitionen:

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
        default: debug    # Nur den Standardwert überschreiben
```

In diesem Beispiel erbt die `LOG_LEVEL` Variable im `identity` Stack alle Eigenschaften (label, type, options, group) von der geteilten Definition, verwendet aber `debug` als Standardwert.

### UI-Verhalten

Die Deployment-UI bietet eine saubere Standardansicht mit optionalen Stack-spezifischen Overrides:

1. **Standardansicht**: Zeigt geteilte Variablen mit ihren Werten. Änderungen gelten für alle Stacks.

2. **Override-Dialog**: Jede Variable hat einen optionalen Override-Button (⚙) der einen Dialog für Stack-spezifische Anpassungen öffnet.

3. **Pre-fill Verhalten**: Wenn ein Stack eine Variable definiert, die auch in `sharedVariables` existiert:
   - Die Override-Checkbox ist vorausgewählt
   - Das Feld ist mit dem Stack-Standardwert vorausgefüllt
   - Benutzer kann den Override löschen um zum geteilten Wert zurückzukehren

---

## Siehe auch

- [Variablentypen](/de/reference/variable-types/)

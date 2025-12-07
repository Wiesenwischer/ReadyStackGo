# Produkte in ReadyStackGo

## Übersicht

Ein **Produkt** ist die primäre Deployment-Einheit in ReadyStackGo. Es repräsentiert eine vollständige, deploybare Anwendung oder ein zusammengehöriges Set von Services, die gemeinsam verwaltet und versioniert werden.

---

## Was ist ein Produkt?

Ein Produkt in ReadyStackGo ist:

- **Eine versionierte Einheit**: Jedes Produkt hat eine `productVersion` (z.B. "3.1.0")
- **Selbstständig deploybar**: Kann direkt über die ReadyStackGo-UI installiert werden
- **Konfigurierbar**: Enthält Variablen, die vor dem Deployment angepasst werden können
- **Wiederverwendbar**: Kann auf verschiedenen Umgebungen mit unterschiedlichen Konfigurationen deployed werden

### Produkt vs. Fragment

| Eigenschaft | Produkt | Fragment |
|-------------|---------|----------|
| Hat `productVersion` | Ja | Nein |
| Direkt deploybar | Ja | Nein |
| Sichtbar in der Stack-Auswahl | Ja | Nein |
| Kann inkludiert werden | Nein | Ja |
| Eigenständig gültig | Ja | Nur mit Produkt |

---

## Anatomie eines Produkts

### Minimales Produkt

Ein minimales Produkt benötigt nur die `metadata` mit `productVersion` und mindestens einen Service:

```yaml
metadata:
  name: Whoami
  productVersion: "1.0.0"

services:
  whoami:
    image: traefik/whoami:latest
    ports:
      - "8080:80"
```

### Vollständiges Produkt

Ein vollständiges Produkt nutzt alle verfügbaren Features:

```yaml
version: "1.0"

metadata:
  name: WordPress
  description: Production-ready WordPress stack with database backend
  productVersion: "6.0.0"
  author: ReadyStackGo Team
  documentation: https://docs.example.com/wordpress
  icon: https://example.com/wordpress-icon.png
  category: CMS
  tags:
    - wordpress
    - cms
    - blog

variables:
  WORDPRESS_PORT:
    label: WordPress Port
    description: Port to access WordPress
    type: Port
    default: "8080"
    group: Network
    order: 1

  DB_PASSWORD:
    label: Database Password
    type: Password
    required: true
    group: Database
    order: 1

services:
  wordpress:
    image: wordpress:latest
    ports:
      - "${WORDPRESS_PORT}:80"
    environment:
      WORDPRESS_DB_PASSWORD: ${DB_PASSWORD}
    volumes:
      - wordpress_data:/var/www/html
    dependsOn:
      - db
    restart: unless-stopped

  db:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_PASSWORD}
    volumes:
      - db_data:/var/lib/mysql
    restart: unless-stopped

volumes:
  wordpress_data: {}
  db_data: {}

networks:
  default:
    driver: bridge
```

---

## Metadata im Detail

Die Metadata-Sektion beschreibt das Produkt und wird in der ReadyStackGo-UI angezeigt:

### Pflichtfelder

| Feld | Beschreibung | Beispiel |
|------|--------------|----------|
| `name` | Anzeigename des Produkts | `WordPress` |
| `productVersion` | Semantische Version | `6.0.0` |

### Optionale Felder

| Feld | Beschreibung | Beispiel |
|------|--------------|----------|
| `description` | Kurzbeschreibung | `Production-ready WordPress stack` |
| `author` | Autor oder Maintainer | `ReadyStackGo Team` |
| `documentation` | Link zur Dokumentation | `https://docs.example.com` |
| `icon` | Icon-URL für die UI | `https://example.com/icon.png` |
| `category` | Kategorie für Filterung | `CMS`, `Database`, `Monitoring` |
| `tags` | Tags für Suche und Filterung | `["wordpress", "cms", "blog"]` |

### Kategorien

Empfohlene Standard-Kategorien:

- **CMS** - Content Management Systeme
- **Database** - Datenbanken und Datenspeicher
- **Monitoring** - Überwachung und Logging
- **Identity** - Authentifizierung und Autorisierung
- **Messaging** - Message Broker und Queues
- **Cache** - Caching-Systeme
- **Storage** - Dateispeicher und Object Storage
- **Testing** - Test- und Debug-Tools
- **Enterprise** - Unternehmensanwendungen

---

## Variablen

Variablen ermöglichen die Konfiguration eines Produkts vor dem Deployment. Sie werden in der UI als Formularfelder dargestellt.

### Variablen-Definition

```yaml
variables:
  MY_VARIABLE:
    label: Meine Variable           # Anzeigename
    description: Beschreibung       # Hilfetext
    type: String                    # Variablentyp
    default: "Standardwert"         # Standardwert
    required: true                  # Pflichtfeld
    pattern: "^[a-z]+$"             # Regex-Validierung
    patternError: Nur Kleinbuchstaben erlaubt
    group: Allgemein                # Gruppierung in der UI
    order: 1                        # Reihenfolge in der Gruppe
```

### Verfügbare Variablentypen

| Typ | Beschreibung | UI-Element |
|-----|--------------|------------|
| `String` | Textfeld | Input-Feld |
| `Number` | Numerisch (mit min/max) | Number-Input |
| `Boolean` | Wahr/Falsch | Toggle/Checkbox |
| `Select` | Auswahl aus Optionen | Dropdown |
| `Password` | Passwort (maskiert) | Password-Input |
| `Port` | Netzwerk-Port (1-65535) | Number-Input mit Validierung |
| `SqlServerConnectionString` | SQL Server Connection | Builder-Dialog |
| `PostgresConnectionString` | PostgreSQL Connection | Builder-Dialog |
| `MySqlConnectionString` | MySQL Connection | Builder-Dialog |
| `EventStoreConnectionString` | EventStoreDB Connection | Builder-Dialog |
| `MongoConnectionString` | MongoDB Connection | Builder-Dialog |
| `RedisConnectionString` | Redis Connection | Builder-Dialog |
| `ConnectionString` | Generischer Connection String | Text-Input |

### Variablen-Gruppierung

Variablen können in Gruppen organisiert werden:

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
    label: Database Host
    type: String
    group: Database
    order: 1

  DB_PASSWORD:
    label: Database Password
    type: Password
    group: Database
    order: 2
```

In der UI werden diese Variablen dann gruppiert angezeigt:

```
┌─ Network ─────────────────────────┐
│ HTTP Port:  [80     ]             │
│ HTTPS Port: [443    ]             │
└───────────────────────────────────┘

┌─ Database ────────────────────────┐
│ Database Host:     [localhost   ] │
│ Database Password: [********    ] │
└───────────────────────────────────┘
```

---

## Variablen-Referenzierung

Variablen werden in Services mit `${VARIABLE_NAME}` referenziert:

```yaml
variables:
  APP_PORT:
    type: Port
    default: "3000"

  LOG_LEVEL:
    type: Select
    options:
      - value: debug
      - value: info
      - value: error
    default: info

services:
  app:
    image: myapp:latest
    ports:
      - "${APP_PORT}:3000"
    environment:
      LOG_LEVEL: ${LOG_LEVEL}
```

### Variablen in verschiedenen Kontexten

Variablen können verwendet werden in:

- **ports**: `"${PORT}:80"`
- **environment**: `VAR_NAME: ${VARIABLE}`
- **image**: `${REGISTRY}/myapp:${VERSION}`
- **volumes**: `${DATA_PATH}:/data`
- **command**: `--config ${CONFIG_FILE}`

---

## Services

Services definieren die Container, die deployed werden:

```yaml
services:
  webserver:
    image: nginx:latest
    containerName: my-nginx          # Optionaler Container-Name
    ports:
      - "80:80"
      - "443:443"
    environment:
      NGINX_HOST: ${HOSTNAME}
    volumes:
      - ./html:/usr/share/nginx/html:ro
      - nginx_logs:/var/log/nginx
    networks:
      - frontend
    dependsOn:
      - backend
    restart: unless-stopped
    healthCheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      startPeriod: 40s
    labels:
      app: webserver
      environment: production
```

### Service-Eigenschaften

| Eigenschaft | Beschreibung | Beispiel |
|-------------|--------------|----------|
| `image` | Docker Image | `nginx:latest` |
| `containerName` | Container-Name | `my-nginx` |
| `ports` | Port-Mappings | `["80:80", "443:443"]` |
| `environment` | Umgebungsvariablen | `{KEY: value}` |
| `volumes` | Volume-Mappings | `["data:/app/data"]` |
| `networks` | Netzwerke | `["frontend", "backend"]` |
| `dependsOn` | Abhängigkeiten | `["database", "cache"]` |
| `restart` | Restart-Policy | `unless-stopped` |
| `command` | Command Override | `npm start` |
| `entrypoint` | Entrypoint Override | `/docker-entrypoint.sh` |
| `workingDir` | Arbeitsverzeichnis | `/app` |
| `user` | Benutzer | `1000:1000` |
| `labels` | Container-Labels | `{app: myapp}` |
| `healthCheck` | Health-Check-Konfiguration | siehe oben |

---

## Volumes und Networks

### Volumes

```yaml
volumes:
  # Benanntes Volume
  app_data: {}

  # Volume mit Driver-Optionen
  db_data:
    driver: local
    driverOpts:
      type: none
      o: bind
      device: /mnt/data
```

### Networks

```yaml
networks:
  # Standard-Netzwerk
  frontend:
    driver: bridge

  # Externes Netzwerk (bereits vorhanden)
  existing_network:
    external: true
```

---

## Produkt-Lebenszyklus

### 1. Erstellung

```
┌─────────────────┐
│ Manifest YAML   │
│ erstellen       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ In stacks/      │
│ Verzeichnis     │
│ ablegen         │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ ReadyStackGo    │
│ lädt Manifest   │
└─────────────────┘
```

### 2. Deployment

```
┌─────────────────┐
│ Produkt in UI   │
│ auswählen       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Variablen       │
│ konfigurieren   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Deploy starten  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Container       │
│ werden erstellt │
└─────────────────┘
```

### 3. Updates

```
┌─────────────────┐
│ Neue Version    │
│ des Manifests   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Update in UI    │
│ verfügbar       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Rolling Update  │
│ oder Redeploy   │
└─────────────────┘
```

---

## Best Practices für Produkte

### Versionierung

- Verwende [Semantic Versioning](https://semver.org/)
- **Major**: Breaking Changes
- **Minor**: Neue Features, rückwärtskompatibel
- **Patch**: Bugfixes

```yaml
metadata:
  productVersion: "2.1.3"
  # 2 = Major (Breaking Change seit v1)
  # 1 = Minor (Neues Feature)
  # 3 = Patch (Dritter Bugfix)
```

### Variablen-Defaults

- Setze sinnvolle Defaults für alle Variablen
- Markiere nur echte Pflichtfelder als `required`
- Verwende `group` und `order` für bessere UX

### Sicherheit

- Verwende `type: Password` für sensible Daten
- Setze keine echten Passwörter als Defaults
- Dokumentiere Sicherheitsanforderungen

### Dokumentation

- Nutze `description` für alle Variablen
- Füge `documentation` URL hinzu
- Verwende aussagekräftige Tags

---

## Beispiele

### Einfaches Test-Produkt

```yaml
metadata:
  name: Whoami
  description: Simple HTTP service for testing
  productVersion: "1.0.0"
  category: Testing
  tags:
    - whoami
    - testing

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
```

### Produktions-Datenbank

```yaml
metadata:
  name: PostgreSQL
  description: PostgreSQL database server
  productVersion: "15.0.0"
  category: Database
  tags:
    - postgresql
    - database
    - sql

variables:
  POSTGRES_PORT:
    label: PostgreSQL Port
    type: Port
    default: "5432"
    group: Network

  POSTGRES_USER:
    label: Database User
    type: String
    default: postgres
    group: Authentication

  POSTGRES_PASSWORD:
    label: Database Password
    type: Password
    required: true
    group: Authentication

  POSTGRES_DB:
    label: Default Database
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

---

## Weiterführende Dokumentation

- [Multi-Stack Produkte](Multi-Stack.md) - Produkte mit mehreren Stacks
- [Stack Fragments](Stack-Fragments.md) - Wiederverwendbare Stack-Teile
- [Manifest Schema](../Reference/Manifest-Schema.md) - Vollständige Schema-Referenz
- [Best Practices](Best-Practices.md) - Empfehlungen für die Praxis

---
title: Variablentypen
description: Referenz aller unterstützten Variablentypen im RSGo Manifest Format
---

Das RSGo Manifest Format unterstützt typisierte Variablen mit automatischer UI-Generierung und Validierung. Jeder Typ hat spezifische Eigenschaften und einen passenden Editor in der Web-UI.

## Basis-Typen

### String

Einfache Texteingabe mit optionaler Pattern-Validierung.

```yaml
EMAIL:
  label: E-Mail Adresse
  type: String
  pattern: "^[^@]+@[^@]+\\.[^@]+$"
  patternError: Bitte geben Sie eine gültige E-Mail ein
  required: true
  placeholder: user@example.com
```

| Eigenschaft | Beschreibung |
|-------------|--------------|
| `pattern` | Regulärer Ausdruck für Validierung |
| `patternError` | Fehlermeldung bei Pattern-Verletzung |
| `placeholder` | Platzhalter-Text im Eingabefeld |

**UI**: Einzeiliges Textfeld

---

### Number

Numerische Eingabe mit optionalen Min/Max-Grenzen.

```yaml
WORKERS:
  label: Worker-Threads
  type: Number
  default: "4"
  min: 1
  max: 32
  description: Anzahl paralleler Worker (1-32)
```

| Eigenschaft | Beschreibung |
|-------------|--------------|
| `min` | Minimalwert |
| `max` | Maximalwert |

**UI**: Zahlenfeld mit Validierung

---

### Boolean

Schalter für Ja/Nein-Werte.

```yaml
DEBUG:
  label: Debug-Modus
  type: Boolean
  default: "false"
  description: Aktiviert erweiterte Logging-Ausgabe
```

**Gültige Werte**: `"true"` oder `"false"` (als Strings)

**UI**: Toggle-Schalter

---

### Password

Passwort-Eingabe mit verdeckter Anzeige.

```yaml
DB_PASSWORD:
  label: Datenbank-Passwort
  type: Password
  required: true
  description: Mindestens 8 Zeichen
```

**UI**: Passwortfeld mit Auge-Symbol zum Ein-/Ausblenden

---

### Port

Netzwerk-Port mit automatischer Validierung (1-65535).

```yaml
WEB_PORT:
  label: Web Port
  type: Port
  default: "8080"
  description: HTTP-Port der Anwendung
```

| Eigenschaft | Beschreibung |
|-------------|--------------|
| `min` | Minimaler Port (Standard: 1) |
| `max` | Maximaler Port (Standard: 65535) |

**UI**: Zahlenfeld mit Port-Validierung

---

### Select

Dropdown-Auswahl aus vordefinierten Optionen.

```yaml
ENVIRONMENT:
  label: Umgebung
  type: Select
  default: development
  options:
    - value: development
      label: Entwicklung
      description: Lokale Entwicklungsumgebung
    - value: staging
      label: Staging
      description: Testumgebung
    - value: production
      label: Produktion
      description: Live-System
```

| Options-Eigenschaft | Beschreibung |
|---------------------|--------------|
| `value` | Technischer Wert (erforderlich) |
| `label` | Anzeigetext |
| `description` | Zusätzliche Beschreibung |

**UI**: Dropdown-Menü

---

## Erweiterte Typen

### Url

URL-Eingabe mit Format-Validierung.

```yaml
API_ENDPOINT:
  label: API Endpoint
  description: Externe API-URL
  type: Url
  default: "https://api.example.com"
  placeholder: "https://..."
```

**UI**: Textfeld mit URL-Validierung

---

### Email

E-Mail-Adresse mit Format-Validierung.

```yaml
ADMIN_EMAIL:
  label: Admin E-Mail
  description: E-Mail-Adresse für Benachrichtigungen
  type: Email
  default: admin@example.com
  placeholder: admin@ihredomain.de
```

**UI**: Textfeld mit E-Mail-Validierung

---

### Path

Dateisystem-Pfad Eingabe.

```yaml
DATA_PATH:
  label: Daten-Pfad
  description: Pfad für Datenspeicherung
  type: Path
  default: /data
```

**UI**: Textfeld für Pfade

---

### MultiLine

Mehrzeilige Texteingabe für größeren Inhalt.

```yaml
SSL_CERTIFICATE:
  label: SSL-Zertifikat
  description: PEM-kodiertes SSL-Zertifikat
  type: MultiLine
  placeholder: |
    -----BEGIN CERTIFICATE-----
    ...
    -----END CERTIFICATE-----
```

**UI**: Textarea mit mehreren Zeilen

---

## Connection String Typen

Diese Typen bieten spezialisierte Builder-Dialoge für Datenbankverbindungen.

### SqlServerConnectionString

Microsoft SQL Server Connection String.

```yaml
DB_CONNECTION:
  label: SQL Server Verbindung
  type: SqlServerConnectionString
  required: true
  group: Database
```

**Builder-Dialog Features**:
- Server und Port separat konfigurieren
- Windows-Authentifizierung oder SQL-Login
- Optionen: Encrypt, TrustServerCertificate, MARS
- Live-Vorschau des Connection Strings
- **Verbindung testen** Button

**Generiertes Format**:
```
Server=myserver,1433;Database=mydb;User Id=sa;Password=***;TrustServerCertificate=true
```

---

### PostgresConnectionString

PostgreSQL Connection String.

```yaml
PG_CONNECTION:
  label: PostgreSQL Verbindung
  type: PostgresConnectionString
  required: true
```

**Builder-Dialog Features**:
- Host, Port, Datenbank
- Benutzername und Passwort
- SSL-Modus (Disable, Require, Prefer)
- Connection Pooling Optionen
- Verbindung testen

**Generiertes Format**:
```
Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=***;SSL Mode=Prefer
```

---

### MySqlConnectionString

MySQL/MariaDB Connection String.

```yaml
MYSQL_CONNECTION:
  label: MySQL Verbindung
  type: MySqlConnectionString
  required: true
```

**Builder-Dialog Features**:
- Server und Port
- Datenbank und Benutzer
- SSL-Optionen
- Charset-Konfiguration
- Verbindung testen

**Generiertes Format**:
```
Server=localhost;Port=3306;Database=mydb;User=root;Password=***;SslMode=Required
```

---

### MongoConnectionString

MongoDB Connection String.

```yaml
MONGO_CONNECTION:
  label: MongoDB Verbindung
  type: MongoConnectionString
```

**Builder-Dialog Features**:
- Single Host oder Replica Set
- Authentifizierung und AuthSource
- SSL/TLS-Optionen
- Read Preference
- Verbindung testen

**Generiertes Format**:
```
mongodb://user:pass@host1:27017,host2:27017/mydb?replicaSet=rs0&authSource=admin
```

---

### RedisConnectionString

Redis Connection String.

```yaml
REDIS_URL:
  label: Redis Server
  type: RedisConnectionString
  default: redis://localhost:6379
```

**Builder-Dialog Features**:
- Host und Port
- Passwort (optional)
- Datenbank-Nummer
- SSL-Optionen
- Sentinel-Konfiguration

**Generiertes Format**:
```
redis://user:password@host:6379/0?ssl=true
```

---

### EventStoreConnectionString

EventStoreDB gRPC Connection String.

```yaml
EVENTSTORE_CONNECTION:
  label: EventStore Verbindung
  type: EventStoreConnectionString
```

**Builder-Dialog Features**:
- gRPC Endpoint
- TLS-Konfiguration
- Cluster-Modus

**Generiertes Format**:
```
esdb://admin:changeit@localhost:2113?tls=true
```

---

### ConnectionString (Generisch)

Generischer Connection String ohne spezialisierten Builder.

```yaml
CUSTOM_CONNECTION:
  label: Custom Verbindung
  type: ConnectionString
```

**UI**: Einfaches Textfeld (kein Builder)

---

## Variablen-Gruppierung

Variablen können in logische Gruppen organisiert werden:

```yaml
variables:
  # Datenbank-Gruppe
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

  # Netzwerk-Gruppe
  WEB_PORT:
    type: Port
    group: Network
    order: 1
  API_PORT:
    type: Port
    group: Network
    order: 2
```

| Eigenschaft | Beschreibung |
|-------------|--------------|
| `group` | Name der Gruppe |
| `order` | Reihenfolge innerhalb der Gruppe |

**UI**: Variablen werden gruppiert mit Überschriften angezeigt.

**Empfohlene Gruppen:**

| Gruppe | Beschreibung |
|--------|--------------|
| `General` | Allgemeine Einstellungen |
| `Network` | Ports, DNS, URLs |
| `Database` | Datenbankverbindungen |
| `Security` | Zertifikate, Passwörter |
| `Logging` | Log-Level, Ausgaben |
| `Performance` | Timeouts, Pools, Threads |
| `Advanced` | Erweiterte Konfiguration |

---

## Validierung

Alle Typen unterstützen:

| Eigenschaft | Beschreibung |
|-------------|--------------|
| `required` | Feld muss ausgefüllt werden |
| `default` | Standardwert |
| `description` | Hilfetext unter dem Feld |

### Validierungs-Reihenfolge

1. Required-Prüfung (wenn `required: true`)
2. Typ-spezifische Validierung (z.B. Port-Bereich)
3. Pattern-Validierung (wenn `pattern` definiert)

### Fehleranzeige

Validierungsfehler werden direkt unter dem Eingabefeld in Rot angezeigt.

---

## Vollständiges Beispiel

```yaml
variables:
  # String mit Pattern
  VERSION_TAG:
    label: Version Tag
    type: String
    default: v1.0.0
    pattern: "^v\\d+\\.\\d+\\.\\d+$"
    patternError: Version muss Format v#.#.# haben (z.B. v1.0.0)
    group: Versions
    order: 1

  # Number mit Bereich
  MAX_CONNECTIONS:
    label: Max Verbindungen
    type: Number
    default: "100"
    min: 1
    max: 1000
    group: Performance

  # Boolean
  ENABLE_DEBUG:
    label: Debug aktivieren
    type: Boolean
    default: "false"
    group: General

  # Password
  ADMIN_PASSWORD:
    label: Admin Passwort
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
    label: Umgebung
    type: Select
    default: development
    options:
      - value: development
        label: Entwicklung
      - value: staging
        label: Staging
      - value: production
        label: Produktion
    group: General

  # URL
  API_ENDPOINT:
    label: API Endpoint
    type: Url
    default: "https://api.example.com"
    group: External Services

  # Email
  ADMIN_EMAIL:
    label: Admin E-Mail
    type: Email
    default: admin@example.com
    group: Notifications

  # MultiLine
  SSL_CERTIFICATE:
    label: SSL Zertifikat
    type: MultiLine
    placeholder: |
      -----BEGIN CERTIFICATE-----
      ...
      -----END CERTIFICATE-----
    group: Security

  # SQL Server Connection
  DATABASE:
    label: SQL Server Verbindung
    type: SqlServerConnectionString
    group: Database
```

---

## Siehe auch

- [RSGo Manifest Format](/de/reference/manifest-format/)

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

---

### Number

Numerische Eingabe mit optionalen Min/Max-Grenzen.

```yaml
WORKERS:
  label: Worker Threads
  type: Number
  default: "4"
  min: 1
  max: 32
  description: Anzahl paralleler Worker (1-32)
```

| Eigenschaft | Beschreibung |
|-------------|--------------|
| `min` | Minimaler Wert |
| `max` | Maximaler Wert |

**UI**: Zahlenfeld mit Validierung

---

### Boolean

Toggle-Schalter für Ja/Nein-Werte.

```yaml
DEBUG:
  label: Debug-Modus
  type: Boolean
  default: "false"
  description: Aktiviert erweiterte Logging-Ausgaben
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

**UI**: Passwortfeld mit Eye-Icon zum Anzeigen/Verstecken

---

### Port

Netzwerk-Port mit automatischer Validierung (1-65535).

```yaml
WEB_PORT:
  label: Web-Port
  type: Port
  default: "8080"
  description: HTTP Port für die Anwendung
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
      description: Test-Umgebung
    - value: production
      label: Produktion
      description: Live-System
```

| Option-Eigenschaft | Beschreibung |
|--------------------|--------------|
| `value` | Technischer Wert (Pflicht) |
| `label` | Anzeigetext |
| `description` | Zusätzliche Beschreibung |

**UI**: Dropdown-Menü

---

## Connection String Typen

Diese Typen bieten spezialisierte Builder-Dialoge für Datenbankverbindungen.

### SqlServerConnectionString

Microsoft SQL Server Verbindungsstring.

```yaml
DB_CONNECTION:
  label: SQL Server Verbindung
  type: SqlServerConnectionString
  required: true
  group: Database
```

**Builder-Dialog Features**:
- Server und Port getrennt konfigurieren
- Windows-Authentifizierung oder SQL-Login
- Optionen: Encrypt, TrustServerCertificate, MARS
- Live-Vorschau des Connection Strings
- **Test Connection** Button

**Generiertes Format**:
```
Server=myserver,1433;Database=mydb;User Id=sa;Password=***;TrustServerCertificate=true
```

---

### PostgresConnectionString

PostgreSQL Verbindungsstring.

```yaml
PG_CONNECTION:
  label: PostgreSQL Verbindung
  type: PostgresConnectionString
  required: true
```

**Builder-Dialog Features**:
- Host, Port, Datenbank
- Benutzer und Passwort
- SSL Mode (Disable, Require, Prefer)
- Connection Pooling Optionen
- Test Connection

**Generiertes Format**:
```
Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=***;SSL Mode=Prefer
```

---

### MySqlConnectionString

MySQL/MariaDB Verbindungsstring.

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
- Test Connection

**Generiertes Format**:
```
Server=localhost;Port=3306;Database=mydb;User=root;Password=***;SslMode=Required
```

---

### MongoConnectionString

MongoDB Verbindungsstring.

```yaml
MONGO_CONNECTION:
  label: MongoDB Verbindung
  type: MongoConnectionString
```

**Builder-Dialog Features**:
- Single Host oder Replica Set
- Authentifizierung und AuthSource
- SSL/TLS Optionen
- Read Preference
- Test Connection

**Generiertes Format**:
```
mongodb://user:pass@host1:27017,host2:27017/mydb?replicaSet=rs0&authSource=admin
```

---

### RedisConnectionString

Redis Verbindungsstring.

```yaml
REDIS_URL:
  label: Redis Server
  type: RedisConnectionString
  default: redis://localhost:6379
```

**Builder-Dialog Features**:
- Host und Port
- Passwort (optional)
- Database-Nummer
- SSL-Optionen
- Sentinel-Konfiguration

**Generiertes Format**:
```
redis://user:password@host:6379/0?ssl=true
```

---

### EventStoreConnectionString

EventStoreDB gRPC Verbindungsstring.

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
  label: Benutzerdefinierte Verbindung
  type: ConnectionString
```

**UI**: Einfaches Textfeld (kein Builder)

---

## Variablen-Gruppierung

Variablen können in logische Gruppen organisiert werden:

```yaml
variables:
  # Datenbankgruppe
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

  # Netzwerkgruppe
  WEB_PORT:
    type: Port
    group: network
    order: 1
  API_PORT:
    type: Port
    group: network
    order: 2
```

| Eigenschaft | Beschreibung |
|-------------|--------------|
| `group` | Name der Gruppe |
| `order` | Reihenfolge innerhalb der Gruppe |

**UI**: Variablen werden nach Gruppen geordnet mit Gruppenüberschriften angezeigt.

---

## Validierung

Alle Typen unterstützen:

| Eigenschaft | Beschreibung |
|-------------|--------------|
| `required` | Feld muss ausgefüllt werden |
| `default` | Standardwert |
| `description` | Hilfetext unter dem Feld |

### Validierungsreihenfolge

1. Required-Check (falls `required: true`)
2. Typ-spezifische Validierung (z.B. Port-Bereich)
3. Pattern-Validierung (falls `pattern` definiert)

### Fehlerdarstellung

Validierungsfehler werden direkt unter dem Eingabefeld in Rot angezeigt.

---

## Siehe auch

- [RSGo Manifest Format](/de/reference/manifest-format/)

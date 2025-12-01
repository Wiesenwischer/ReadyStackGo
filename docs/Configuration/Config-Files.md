# Config Files (`rsgo.*.json`)

Dieses Dokument beschreibt alle zentralen Config-Dateien, die durch den Admin-Container verwaltet werden.

## Übersicht v0.6

Mit v0.6 wurde die Konfiguration grundlegend überarbeitet:

| Speicher | Datentyp | Beschreibung |
|----------|----------|--------------|
| **SQLite** | Dynamische Daten | Organizations, Users, Environments, Deployments |
| **JSON** | Statische Konfiguration | System-Einstellungen, TLS, Features, Release-Info |

### Aktuelle JSON-Dateien

| Datei | Beschreibung |
|-------|--------------|
| `rsgo.system.json` | Wizard-Status, BaseUrl, Ports, Netzwerk |
| `rsgo.tls.json` | TLS-Zertifikate und -Modus |
| `rsgo.features.json` | Feature Flags |
| `rsgo.release.json` | Installierte Stack-Version |

### Entfernte Dateien (seit v0.6)

| Datei | Ersetzt durch |
|-------|---------------|
| ~~`rsgo.security.json`~~ | SQLite: Users-Tabelle |
| ~~`rsgo.organization.json`~~ | SQLite: Organizations-Tabelle |
| ~~`rsgo.contexts.json`~~ | Entfernt (obsolet seit v0.4) |
| ~~`rsgo.connections.json`~~ | Entfernt (obsolet seit v0.4) |

---

## rsgo.system.json

Speichert System-Einstellungen und Wizard-Status.

```json
{
  "baseUrl": "https://localhost:8443",
  "httpPort": 8080,
  "httpsPort": 8443,
  "networkName": "rsgo-net",
  "wizardState": "Completed",
  "deploymentMode": "SingleNode"
}
```

### Felder

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `baseUrl` | string | Basis-URL für die Admin-UI |
| `httpPort` | int | HTTP-Port (Standard: 8080) |
| `httpsPort` | int | HTTPS-Port (Standard: 8443) |
| `networkName` | string | Docker-Netzwerk für Container |
| `wizardState` | enum | NotStarted, AdminCreated, OrganizationSet, EnvironmentCreated, Completed |
| `deploymentMode` | enum | SingleNode, MultiNode |

---

## rsgo.tls.json

Definiert TLS-Modus und Zertifikatspfade.

```json
{
  "mode": "SelfSigned",
  "certificatePath": "/app/certs/rsgo.pfx",
  "certificatePassword": "***",
  "customCertificateThumbprint": null
}
```

### Felder

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `mode` | enum | SelfSigned, Custom, None |
| `certificatePath` | string | Pfad zum PFX-Zertifikat |
| `certificatePassword` | string | Passwort für PFX (verschlüsselt) |
| `customCertificateThumbprint` | string? | Thumbprint eines Custom-Zertifikats |

---

## rsgo.features.json

Globale Feature Flags, die als Umgebungsvariablen an Container übergeben werden.

```json
{
  "features": {
    "AUDIT_LOGGING": true,
    "ADVANCED_SEARCH": false,
    "BETA_FEATURES": false
  }
}
```

### Verwendung

Feature Flags werden als `RSGO_FEATURE_<NAME>` Environment-Variablen an deployte Container übergeben:

```bash
RSGO_FEATURE_AUDIT_LOGGING=true
RSGO_FEATURE_ADVANCED_SEARCH=false
```

---

## rsgo.release.json

Enthält Informationen über die installierte Stack-Version.

```json
{
  "installedStackVersion": "1.0.0",
  "installDate": "2024-01-15T10:30:00Z",
  "installedContexts": {
    "api": "1.0.0",
    "web": "1.0.0",
    "db": "15.0"
  }
}
```

### Felder

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `installedStackVersion` | string? | Installierte Stack-Version |
| `installDate` | DateTime? | Zeitpunkt der Installation |
| `installedContexts` | Dictionary | Service-Name → Version |

---

## Speicherort

Alle JSON-Dateien befinden sich im Config-Volume:

```
/app/config/
├── rsgo.system.json
├── rsgo.tls.json
├── rsgo.features.json
└── rsgo.release.json
```

Im Docker-Container wird dieses Verzeichnis typischerweise als Volume gemountet:

```yaml
volumes:
  - rsgo-config:/app/config
```

---

## SQLite-Datenbank (v0.6)

Die dynamischen Daten werden in einer SQLite-Datenbank gespeichert:

```
/app/data/readystackgo.db
```

### Tabellen

| Tabelle | Beschreibung |
|---------|--------------|
| `Organizations` | Organizations mit Name, Beschreibung, Status |
| `Users` | Benutzer mit Username, Email, Password-Hash, Enablement |
| `UserRoleAssignments` | Rollen-Zuweisungen mit Scope |
| `Environments` | Docker-Environments (Socket/API) |
| `Deployments` | Deployment-History |
| `DeployedServices` | Services pro Deployment |

---

## ConfigStore Interface

Der Zugriff auf JSON-Konfigurationen erfolgt über `IConfigStore`:

```csharp
public interface IConfigStore
{
    // System Config
    Task<SystemConfig> GetSystemConfigAsync();
    Task SaveSystemConfigAsync(SystemConfig config);

    // TLS Config
    Task<TlsConfig> GetTlsConfigAsync();
    Task SaveTlsConfigAsync(TlsConfig config);

    // Features Config
    Task<FeaturesConfig> GetFeaturesConfigAsync();
    Task SaveFeaturesConfigAsync(FeaturesConfig config);

    // Release Config
    Task<ReleaseConfig> GetReleaseConfigAsync();
    Task SaveReleaseConfigAsync(ReleaseConfig config);
}
```

---

## Migration von älteren Versionen

### Von v0.5 zu v0.6

1. `rsgo.security.json` → SQLite Users-Tabelle
2. `rsgo.organization.json` → SQLite Organizations-Tabelle
3. `rsgo.contexts.json` → Entfernt (war bereits obsolet)

Die Migration erfolgt automatisch beim ersten Start von v0.6.

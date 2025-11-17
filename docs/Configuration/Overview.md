# Konfigurationsübersicht

ReadyStackGo verwendet ein strukturiertes Konfigurationssystem, das auf JSON-Dateien basiert und im persistenten Volume `rsgo-config` gespeichert wird.

## Konfigurationsprinzipien

### 1. Deklarativ
Alle Konfiguration ist deklarativ in JSON-Dateien beschrieben.

### 2. Versioniert
Konfigurationsdateien werden zusammen mit dem Stack versioniert.

### 3. Validiert
Jede Konfiguration wird gegen ein Schema validiert.

### 4. Persistent
Alle Konfigurationen werden im Docker Volume `rsgo-config` persistiert.

## Konfigurationsdateien

ReadyStackGo verwendet mehrere spezialisierte Konfigurationsdateien, alle mit dem Präfix `rsgo.`:

### [`rsgo.system.json`](Config-Files.md#rsgo-system-json)
System- und Organisationskonfiguration
- Organisation (ID, Name)
- Netzwerkkonfiguration
- Ports (HTTP, HTTPS)
- Base URL
- Wizard-Status
- Deployment-Modus

### [`rsgo.security.json`](Config-Files.md#rsgo-security-json)
Sicherheits- und Authentifizierungskonfiguration
- Lokale Admin-Benutzer
- Passwort-Hashes
- JWT-Konfiguration
- OIDC-Provider (zukünftig)
- Rollen und Berechtigungen

### [`rsgo.tls.json`](Config-Files.md#rsgo-tls-json)
TLS-Konfiguration
- TLS-Modus (SelfSigned/Custom)
- Zertifikatspfad
- Ports
- HTTP-Redirect-Einstellungen

### [`rsgo.contexts.json`](Config-Files.md#rsgo-contexts-json)
Kontext- und Verbindungskonfiguration
- Verbindungsmodus (Simple/Advanced)
- Globale Connection Strings
- Kontextspezifische Verbindungen
- Kontext-Metadaten

### [`rsgo.features.json`](Config-Files.md#rsgo-features-json)
Feature-Flags
- Globale Feature-Schalter
- True/False-Werte
- Werden als `RSGO_FEATURE_*` EnvVars übergeben

### [`rsgo.release.json`](Config-Files.md#rsgo-release-json)
Release-Status
- Installierte Stack-Version
- Kontext-Versionen
- Installationsdatum
- Deployment-Historie

### [`rsgo.nodes.json`](Config-Files.md#rsgo-nodes-json) (Zukünftig)
Multi-Node-Konfiguration
- Node-Definitionen
- Node-Rollen
- Remote-Docker-Hosts

## Konfigurationsmodi

### Simple Mode (Standard)
Alle Kontexte nutzen die gleichen globalen Verbindungen:
```json
{
  "mode": "Simple",
  "globalConnections": {
    "transport": "amqp://rabbitmq:5672",
    "persistence": "Server=sql;Database=ams",
    "eventStore": "esdb://eventstore:2113"
  }
}
```

### Advanced Mode
Jeder Kontext kann individuelle Verbindungen haben:
```json
{
  "mode": "Advanced",
  "contexts": {
    "project": {
      "connections": {
        "transport": "amqp://rabbitmq-project:5672",
        "persistence": "Server=sql-project;Database=project"
      }
    }
  }
}
```

## Konfigurationsspeicherort

Alle Konfigurationsdateien befinden sich im Docker Volume:
```
/app/config/
├── rsgo.system.json
├── rsgo.security.json
├── rsgo.tls.json
├── rsgo.contexts.json
├── rsgo.features.json
├── rsgo.release.json
└── tls/
    ├── certificate.pfx
    └── self-signed.pfx
```

## Konfiguration zur Laufzeit

### Lesen
```csharp
var systemConfig = await configStore.LoadSystemConfigAsync();
```

### Schreiben
```csharp
await configStore.SaveSystemConfigAsync(systemConfig);
```

### Validierung
Alle Konfigurationen werden automatisch validiert:
- JSON-Schema-Validierung
- Business-Rule-Validierung
- Abhängigkeits-Validierung

## Environment Variables

Aus den Konfigurationsdateien werden automatisch Environment Variables für die Container generiert:

### System-Variables
```bash
RSGO_ORG_ID=kunde-a
RSGO_ORG_NAME=Kunde A GmbH
RSGO_STACK_VERSION=4.3.0
```

### Feature-Variables
```bash
RSGO_FEATURE_newColorTheme=true
RSGO_FEATURE_discussionV2=false
```

### Connection-Variables
```bash
RSGO_CONNECTION_transport=amqp://rabbitmq:5672
RSGO_CONNECTION_persistence=Server=sql;Database=ams
RSGO_CONNECTION_eventStore=esdb://eventstore:2113
```

## Best Practices

1. **Nicht manuell editieren**: Verwenden Sie die Admin-UI oder API
2. **Backups erstellen**: Volume `rsgo-config` regelmäßig sichern
3. **Simple Mode bevorzugen**: Nur bei Bedarf zu Advanced wechseln
4. **Secrets extern**: Passwörter in Secrets-Management-Systemen
5. **Versionierung**: Konfigurationsänderungen dokumentieren

## Nächste Schritte

- [Config-Dateien im Detail](Config-Files.md)
- [Manifest-Spezifikation](Manifest-Specification.md)
- [Feature Flags](Feature-Flags.md)

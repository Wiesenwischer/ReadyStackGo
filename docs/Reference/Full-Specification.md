
# ReadyStackGo – Gesamtspezifikation

## Inhaltsverzeichnis
1. Einleitung  
2. Ziele  
3. Systemübersicht  
4. Architektur  
5. Komponenten  
6. Konfigurationsmodell  
7. Setup-Wizard  
8. Deployment-Pipeline  
9. Release-Management  
10. Feature Flags  
11. Sicherheit  
12. Roadmap

---

# 1. Einleitung

ReadyStackGo ist ein selbst gehostetes Deployment- und Administrationssystem für containerisierte Software-Stacks. Ziel ist es, On-Premise-Kunden, Partnern und internen Teams eine einfache Installation, Wartung und Aktualisierung komplexer Microservice-Systeme zu ermöglichen, ohne dass diese direkt mit Docker Compose, Kubernetes oder manuellen Deployment-Skripten interagieren müssen.

Der Kern von ReadyStackGo ist ein **einziger Admin-Container**, der:
- eine Web-UI bereitstellt,
- einen Setup-Wizard enthält,
- TLS automatisch bootstrapped,
- Release-Manifeste verarbeitet,
- Container-Stacks installiert und aktualisiert,
- Konfiguration versioniert verwaltet,
- und später Multi-Node-Deployments unterstützen kann.

ReadyStackGo ist vollständig offline-fähig und benötigt nur:
- Docker auf dem Host,
- den Zugriff auf den Docker Socket.

Dies macht das System ideal für:
- On-Premise-Betrieb
- isolierte Kundennetzwerke
- Edge-Installationen
- interne Entwicklungs-Stacks


# 2. Ziele

ReadyStackGo soll eine vollständig integrierte, leicht zu bedienende und robuste Managementplattform darstellen.  
Die wichtigsten Ziele sind:

## 2.1 Primäre Ziele
1. **Ein einzelner Admin-Container** verwaltet den gesamten Stack.  
2. **Setup-Wizard**, der Installationen für Kunden extrem vereinfacht.  
3. **TLS-Automatisierung** (Self-Signed + später Custom-Zertifikate).  
4. **Manifest-basierte Installation & Updates** für ganze Stacks.  
5. **Zentrale Konfiguration** über rsgo-config (Volume).  
6. **Feature Flags** für kunden- oder organisationsspezifische Aktivierungen.  
7. **Offline-Betrieb**: Keine Cloud-Anbindung notwendig.  
8. **Erweiterbarkeit**: BFFs, APIs, Business-Kontexte, Gateways.  

## 2.2 Nicht-Ziele (für die ersten Releases)
- Kein Kubernetes.  
- Kein Fokus auf Multi-Tenancy SaaS (stattdessen Organisation).  
- Kein Setup von externen Datenbanken.  
- Kein automatisches Clustering.  
- Keine dynamische Container-Orchestrierung wie Swarm/K8s.

---

# 3. Systemübersicht

ReadyStackGo besteht aus drei Schichten:

1. **Admin-Container (ReadyStackGo selbst)**
   - UI
   - Wizard
   - TLS Management
   - Manifest Loader
   - Deployment Engine
   - Config Store

2. **Gateway / BFF-Schicht**
   - BFF Desktop
   - BFF Web
   - Public API Gateway

3. **Fachliche Kontexte (AMS Microservices)**
   - Project
   - Memo
   - Discussion
   - Identity
   - etc.

Alle Systeme sind als Docker-Container definiert und werden durch Manifeste spezifiziert.

---

# 4. Architektur

## 4.1 Schichtenmodell (Clean Architecture)

- **API-Schicht**
  - Endpoints (FastEndpoints)
  - Authentifizierung / Rollen
  - Input/Output DTOs

- **Application**
  - Dispatcher
  - Commands & Queries
  - Orchestrierung / Policies

- **Domain**
  - Reine Geschäftsobjekte
  - Value Objects
  - Policies

- **Infrastructure**
  - Docker-Service
  - TLS-Service
  - ConfigStore
  - ManifestProvider

- **Frontend**
  - React + Tailwind + TailAdmin
  - Wizard
  - Admin UI



# 5. Komponentenübersicht

ReadyStackGo besteht aus mehreren klar getrennten Komponenten, die im Zusammenspiel den gesamten Stack verwalten.  
Jede Komponente ist austauschbar, testbar und erweiterbar.

## 5.1 ReadyStackGo Admin (zentraler Container)

Der Admin-Container stellt bereit:

- Web UI (React + TailAdmin)
- API (FastEndpoints)
- Setup Wizard
- TLS Handling (Bootstrap & Verwaltung)
- Manifest-Verwaltung
- Deployment Engine
- Config Store
- Logs & Statusabfragen

Der Admin-Container ist der **einzige Container**, den ein Kunde manuell starten muss.

---

## 5.2 Gateway-Schicht

Der Manifest-Stack kann beliebig viele Gateways enthalten, typischerweise:

- **Edge Gateway** (TLS-Termination, Reverse Proxy)
- **Public API Gateway**
- **BFF Desktop**
- **BFF Web**

Diese Gateways werden immer **zuletzt** deployed, um sicherzustellen, dass die darunterliegenden Dienste bereits laufen.

---

## 5.3 Fachliche Kontexte (Microservices)

Beispiele:

- Project
- Memo
- Discussion
- Identity
- Notification
- Search
- Files
- u.a.

Jeder Kontext:

- ist ein Container,
- nutzt den gleichen Stack-Lebenszyklus,
- besitzt eigene Versionen,
- kann Verbindungen (DB/Eventstore/Transport) haben,
- wird in den Manifesten definiert.

---

## 5.4 Konfigurationskomponenten

### 5.4.1 Config Store (rsgo-config Volume)

Darin liegen alle persistenten Daten:

```
rsgo.system.json
rsgo.security.json
rsgo.tls.json
rsgo.contexts.json
rsgo.features.json
rsgo.release.json
rsgo.nodes.json
tls/<certs>
```

### 5.4.2 Manifest Provider

- Lädt Release-Manifeste vom Dateisystem oder später einer Registry.
- Validiert Versionen und Schema.

### 5.4.3 Deployment Engine

- Erzeugt den Deploymentplan pro Manifest
- Start/Stop/Remove/Update Container
- Healthchecks
- Maintains rsgo.release.json

### 5.4.4 TLS Engine

- Generiert Self-Signed Zertifikat beim Bootstrap
- Erlaubt späteres Hochladen eigener Zertifikate

---

# 6. Konfigurationsmodell (Detail)

Die gesamte Konfiguration wird klar strukturiert.

## 6.1 rsgo.system.json

Speichert:

- Organisation
- Ports
- Base URL
- Docker Netzwerkname
- Wizard-Status
- Deployment-Modus
- Node-Konfiguration (Single Node vorerst)

### Beispiel

```json
{
  "organization": {
    "id": "kunde-a",
    "name": "Kunde A"
  },
  "baseUrl": "https://ams.kunde-a.de",
  "httpPort": 8080,
  "httpsPort": 8443,
  "dockerNetwork": "rsgo-net",
  "mode": "SingleNode",
  "wizardState": "Installed"
}
```

---

## 6.2 rsgo.security.json

Speichert:

- Lokalen Admin (Passwort gehasht, salted)
- Rollenmodell (Admin/Operator)
- Optional externe Identity Provider Konfiguration (OIDC)
- Local Admin fallback toggle

---

## 6.3 rsgo.tls.json

Definiert:

- tlsMode: SelfSigned oder Custom
- Zertifikatspfad
- Port
- httpEnabled
- terminatingContext

---

## 6.4 rsgo.contexts.json

Für Simple und Advanced Mode:

### Simple Mode

```json
{
  "mode": "Simple",
  "globalConnections": {
    "transport": "TransportCS",
    "persistence": "Server=.;Database=ams",
    "eventStore": "esdb://..."
  },
  "contexts": {
    "project": {},
    "memo": {},
    "discussion": {},
    "identity": {},
    "bffDesktop": {},
    "bffWeb": {},
    "publicApi": {}
  }
}
```

### Advanced Mode

```json
{
  "mode": "Advanced",
  "contexts": {
    "project": {
      "connections": {
        "transport": "...",
        "persistence": "...",
        "eventStore": "..."
      }
    }
  }
}
```

---

## 6.5 rsgo.features.json

Globale Feature-Flags:

- Kontextübergreifend
- True/False Values
- Werden als `RSGO_FEATURE_*` an Container übergeben

---

## 6.6 rsgo.release.json

Speichert den Zustand nach Deployment:

```json
{
  "installedStackVersion": "4.3.0",
  "installedContexts": {
    "project": "6.4.0",
    "memo": "4.1.3"
  },
  "installDate": "2025-03-01T10:12:00Z"
}
```

---



# 7. Setup-Wizard (Detail)

Der Setup-Wizard ist bewusst kompakt gehalten und führt einen neuen Benutzer durch die minimal notwendigen Einrichtungsschritte.  
Alle fortgeschrittenen Features können später in der Admin-UI angepasst werden.

Der Wizard ist nur aktiv, wenn:

```
rsgo.system.json.wizardState != "Installed"
```

---

## 7.1 Ablaufübersicht

1. **Admin anlegen**
2. **Organisation anlegen**
3. **Verbindungen setzen (Simple Mode)**
4. **Zusammenfassung**
5. **Stack installieren**
6. Wizard sperrt sich → Login wird aktiv

---

## 7.2 Schritt 1 – Admin

Benutzer gibt ein:

- Username  
- Passwort  

Die API speichert:

- Passwort gehasht
- Salt generiert
- Rolle: admin
- wizardState = "AdminCreated"

Speicherung in `rsgo.security.json`.

---

## 7.3 Schritt 2 – Organisation

Daten:

- ID (technisch)
- Name (Anzeige)

Speichert in `rsgo.system.json`:

```json
{
  "organization": { "id": "kunde-a", "name": "Kunde A GmbH" }
}
```

wizardState = "OrganizationSet".

---

## 7.4 Schritt 3 – Verbindungen (Simple Mode)

Der Benutzer trägt ein:

- Transport Connection String  
- Persistence Connection String  
- EventStore Connection String (optional)

rsgo erzeugt:

```json
"mode": "Simple",
"globalConnections": { ... }
```

wizardState = "ConnectionsSet".

---

## 7.5 Schritt 4 – Zusammenfassung

Der Wizard zeigt:

- Organisation
- Verbindungen
- Vorgeschlagenes Manifest (z. B. aktuellste Version)
- Alle Kontexte

---

## 7.6 Schritt 5 – Installation

Die API:

1. liest das Manifest  
2. erzeugt den Deploymentplan  
3. stoppt alte Container (falls vorhanden)  
4. erzeugt/stattet Container  
5. schreibt `rsgo.release.json`  
6. setzt wizardState = "Installed"  

Nach diesem Schritt ist der Wizard deaktiviert.

---

# 8. Deployment-Pipeline (Detail)

Der Deploymentprozess ist das Herzstück von ReadyStackGo.

## 8.1 Schritte im Überblick

1. Manifest laden  
2. Configs laden (`rsgo.system.json`, `rsgo.contexts.json`, `rsgo.features.json`)  
3. EnvVars generieren  
4. Docker-Netzwerk sicherstellen (`rsgo-net`)  
5. Kontextweise Deployment ausführen  
6. Gateway zuletzt deployen  
7. Release-Status speichern  

---

## 8.2 EnvVar Generierung

Folgende EnvVars werden erzeugt:

### 1. System  
- `RSGO_ORG_ID`
- `RSGO_ORG_NAME`
- `RSGO_STACK_VERSION`

### 2. Feature-Flags  
- `RSGO_FEATURE_<name>=true/false`

### 3. Verbindungen  
Je nach Simple/Advanced Mode:

- `RSGO_CONNECTION_transport`
- `RSGO_CONNECTION_persistence`
- `RSGO_CONNECTION_eventStore`

### 4. Manifest-spezifische Variablen  
z. B.:

- `ROUTE_DESKTOP=http://ams-bff-desktop`
- `ROUTE_PUBLIC_API=http://ams-public-api`

---

## 8.3 Container Deployment Reihenfolge

Für jede Manifest-Definition:

1. **Stop & Remove** (falls Container existiert)
2. **Create & Start** mit:
   - Image
   - EnvVars
   - Ports
   - Network
   - Name
3. **Healthcheck** (optional)
4. Gateway **zuletzt** deployen

---

## 8.4 Fehler- und Rollbackstrategie

### Bei Fehler während des Deployments:

- Fehler wird protokolliert
- weiterer Deploymentprozess stoppt
- Benutzer erhält:
  - Fehlercode  
  - Fehlerbeschreibung  
- rsgo.release.json wird NICHT aktualisiert

Rollback V1 (einfach):

- vorherige Container bleiben unangetastet  
- Benutzer kann über die UI:
  - Deployment wiederholen  
  - ältere Release-Version installieren  

---

## 8.5 rsgo.release.json Aktualisierung

Beispiel nach Deployment:

```json
{
  "installedStackVersion": "4.3.0",
  "installedContexts": {
    "project": "6.4.0",
    "memo": "4.1.3",
    ...
  },
  "installDate": "2025-04-12T10:22:00Z"
}
```

---

# 9. Release Management

## 9.1 Manifest-Dateien

Ein Manifest definiert:

- Stack-Version
- Kontext-Versionen
- Kontextspezifische EnvVars
- Gateway-Konfiguration
- Feature-Defaults
- Abhängigkeiten

---

## 9.2 Beispielmanifest (ausführlich)

```json
{
  "manifestVersion": "1.0.0",
  "stackVersion": "4.3.0",
  "schemaVersion": 12,
  "gateway": {
    "context": "edge-gateway",
    "protocol": "https",
    "publicPort": 8443,
    "internalHttpPort": 8080
  },
  "contexts": {
    "project": {
      "image": "registry/ams.project-api",
      "version": "6.4.0",
      "containerName": "ams-project",
      "internal": true
    },
    "bffDesktop": {
      "image": "registry/ams.bff-desktop",
      "version": "1.3.0",
      "containerName": "ams-bff-desktop",
      "internal": false,
      "env": {
        "ROUTE_PROJECT": "http://ams-project"
      }
    }
  },
  "features": {
    "newColorTheme": { "default": true },
    "discussionV2": { "default": false }
  }
}
```

---

## 9.3 Release Lifecycle

1. **CI erzeugt neues Manifest**
2. **ReadyStackGo lädt Manifest**
3. **Admin wählt Manifest aus**
4. **Deployment Engine führt Installation**
5. **Release gespeichert**



# 10. Feature Flags

Feature Flags erlauben, fachliche Funktionalitäten dynamisch ein- oder auszuschalten – kontextübergreifend und zentral gesteuert.

## 10.1 Eigenschaften
- Global gültig (nicht auf einen Kontext beschränkt).
- Werden als Environment Variablen an jeden Container übergeben.
- Können später pro Organisation erweitert werden.
- Werden in `rsgo.features.json` gespeichert.
- Können später auch im Manifest Vorbelegungen haben.

## 10.2 Beispiel `rsgo.features.json`

```json
{
  "newColorTheme": true,
  "discussionV2": false,
  "memoRichEditor": true
}
```

-> Container erhalten:
```
RSGO_FEATURE_newColorTheme=true
RSGO_FEATURE_discussionV2=false
RSGO_FEATURE_memoRichEditor=true
```

## 10.3 Feature Flags im Manifest

Kontexte können im Manifest Standardwerte definieren:

```json
"features": {
  "newColorTheme": { "default": true },
  "discussionV2": { "default": false }
}
```

Diese Werte können später vom Admin überschrieben werden.

## 10.4 UI (Admin-Bereich)

Die Administration sieht eine Liste aller Features:

| Feature Name       | Aktiv | Beschreibung |
|--------------------|-------|--------------|
| newColorTheme      | ✔️    | Neues UI-Theme |
| discussionV2       | ❌    | Neue Diskussion-API |
| memoRichEditor     | ✔️    | Rich Text Editor |

Jede Änderung wird in `rsgo.features.json` gespeichert.

---

# 11. Sicherheit

ReadyStackGo muss sowohl für On-Premise als auch produktive Umgebungen robust und sicher sein.

## 11.1 Authentifizierungsmodi

1. **Local Authentication**
   - Default
   - Benutzername + Passwort
   - Speichert Passwort als Hash + Salt

2. **Externer Identity Provider (OIDC)**
   - Keycloak
   - ams.identity
   - Azure AD (später)
   - Rollen über Claims

3. **Lokaler Admin Fallback**
   - Aktiv oder deaktivierbar
   - Garantiert Login auch bei IdP-Ausfall

---

## 11.2 Autorisierung / Rollen

### Rollen

- **admin**
  - Kann Deployments durchführen
  - Kann Konfiguration ändern
  - Kann TLS managen
  - Kann Feature Flags anpassen

- **operator**
  - Kann nur Container starten/stoppen
  - Kann Logs einsehen

### Rollenquelle

- Bei Local Auth:
  - Rollen in `rsgo.security.json`

- Bei OIDC:
  - Aus Claim (z. B. `"role" : "rsgo-admin"`)

---

## 11.3 Passwortsicherheit

- Passwort-Hashing via PBKDF2 oder Argon2
- Passwort-Salt wird pro Benutzer generiert
- Keine Speicherung von Klartextpasswörtern

---

## 11.4 TLS / HTTPS

### Bootstrap
- Erststart generiert Self-Signed Zertifikat
- Zertifikat wird unter `/app/config/tls/` gespeichert

### Custom Zertifikat
- Admin-UI erlaubt Upload eines PFX
- Speicherung in `rsgo.tls.json`

### TLS-Termination
- Erfolgt im **Gateway**, nicht im Admin-Container selbst
- Vorteil: Container-intern kann HTTP verwendet werden

---

## 11.5 API Security

- JWT oder Cookie-Token
- Anti-CSRF (bei Cookies)
- Rate Limiting (optional später)
- Secure Headers, HSTS

---

# 12. Roadmap (detailliert)

Diese Roadmap umfasst nur die Hauptlinien, Module können parallel entwickelt werden.

## 12.1 Version v0.1 – Container Management MVP

- API: List, Start, Stop
- DockerService Basis
- UI: Container Übersicht
- Kein Login
- Kein Wizard

## 12.2 Version v0.2 – Local Admin & Hardcoded Stack

- Login/Logout
- Lokale Authentifizierung
- Rechteverwaltung
- Dashboard
- Hardcoded Stack-Deployment

## 12.3 Version v0.3 – Bootstrap, Wizard, TLS

- Self-Signed TLS
- Wizard mit 4 Schritten
- Erste Manifest-Ladung
- Basis-Deployment Engine

## 12.4 Version v0.4 – Release Management

- Manifest-Verwaltung
- Versionierung
- Update/Upgrade Flow
- Release-Statusanzeige

## 12.5 Version v0.5 – Admin Komfort

- TLS Upload
- Feature Flags UI
- Advanced Connections
- Node-Konfiguration (Single Node)

## 12.6 Version v1.0 – Multi Node (Clusterfähig)

- rsgo.nodes.json aktiv nutzen
- Pro Node: Rollen (Gateway Node, Compute Node)
- Node-Discovery
- Remote Docker Hosts

---

# → Ende der Gesamtspezifikation

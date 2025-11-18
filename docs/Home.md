# ReadyStackGo Documentation

Willkommen zur Dokumentation von **ReadyStackGo** – einer selbst gehosteten Plattform zur einfachen Installation, Aktualisierung und Verwaltung komplexer Microservice-Stacks auf Basis von Docker.

---

## Project Status

- **Build Pipeline:** [View Latest Build](https://tfsmain.ams.local/tfs/ams/Products/_build?definitionId=ReadyStackGo)
- **Docker Hub:** [amssolution/readystackgo](https://hub.docker.com/r/amssolution/readystackgo)
- **Repository:** [Azure DevOps](https://tfsmain.ams.local/tfs/ams/Products/_git/ReadyStackGo)
- **License:** MIT

---

## Was ist ReadyStackGo?

ReadyStackGo (RSGO) ist eine selbst gehostete Plattform, die die Bereitstellung komplexer Microservice-Stacks extrem vereinfacht. Mit einem **einzigen Admin-Container**, einer **modernen Web-UI** und **manifestbasierten Deployments** können On-Premise-Kunden Software-Stacks ohne direkte Interaktion mit Docker Compose oder Kubernetes installieren und verwalten.

### Hauptmerkmale

- 🧩 **Ein einzelner Admin-Container** verwaltet den gesamten Stack
- 🧙 **Geführter Setup-Wizard** für einfache Erstinstallation
- 🔐 **TLS-Handling** (Self-Signed Bootstrap & Custom-Zertifikate)
- 📦 **Manifest-basierte Deployments** für ganze Stacks (SemVer)
- ⚙️ **Zentrale Konfiguration** via `rsgo.*.json`
- 🏷️ **Feature Flags** (fachliche Schalter, kontextübergreifend)
- 🔁 **CI/CD-Integration** für automatische Stack-Releases
- 🔒 **Security** mit Admin/Operator-Rollen, JWT & später OIDC
- 🔌 **Plugin-fähig** (geplantes Plugin-System)

---

## Schnellstart

Für einen schnellen Einstieg:

1. [Getting Started/Overview](Getting-Started/Overview.md) - Projektübersicht
2. [Getting Started/Quick-Start](Getting-Started/Quick-Start.md) - Schnellstart-Anleitung
3. [Getting Started/Installation](Getting-Started/Installation.md) - Installationsanleitung

---

## Dokumentationsstruktur

### 📚 [Getting Started](Getting-Started/Overview.md)
Erste Schritte, Schnellstart und Installation
- [Übersicht](Getting-Started/Overview.md)
- [Quick Start](Getting-Started/Quick-Start.md)
- [Installation](Getting-Started/Installation.md)

### 🏗️ [Architecture](Architecture/Overview.md)
Systemarchitektur und Komponenten
- [Architektur-Übersicht](Architecture/Overview.md)
- [Komponenten](Architecture/Components.md)
- [Container-Lifecycle](Architecture/Container-Lifecycle.md)
- [Deployment Engine](Architecture/Deployment-Engine.md)

### ⚙️ [Configuration](Configuration/Overview.md)
Konfigurationsverwaltung und Manifeste
- [Konfigurationsübersicht](Configuration/Overview.md)
- [Config-Dateien](Configuration/Config-Files.md)
- [Manifest-Spezifikation](Configuration/Manifest-Specification.md)
- [Feature Flags](Configuration/Feature-Flags.md)

### 🧙 [Setup Wizard](Setup-Wizard/Wizard-Flow.md)
Geführter Installationsassistent
- [Wizard-Flow](Setup-Wizard/Wizard-Flow.md)

### 🔒 [Security](Security/Overview.md)
Authentifizierung, Autorisierung und TLS
- [Sicherheitsübersicht](Security/Overview.md)
- [Authentifizierung](Security/Authentication.md)
- [Autorisierung](Security/Authorization.md)
- [TLS-Konfiguration](Security/TLS-Configuration.md)

### 🔧 [Operations](Operations/Release-Management.md)
Betrieb, Updates und Troubleshooting
- [Release Management](Operations/Release-Management.md)
- [Updates](Operations/Updates.md)
- [Troubleshooting](Operations/Troubleshooting.md)

### 💻 [Development](Development/Setup-Environment.md)
Entwicklungsumgebung und Contribution Guidelines
- [Entwicklungsumgebung](Development/Setup-Environment.md)
- [Contributing](Development/Contributing.md)
- [Coding Guidelines](Development/Coding-Guidelines.md)
- [Testing](Development/Testing.md)

### 🔁 [CI/CD](CI-CD/Pipeline-Integration.md)
Continuous Integration & Deployment
- [Pipeline-Integration](CI-CD/Pipeline-Integration.md)

### 📖 [Reference](Reference/Full-Specification.md)
Technische Referenz und Spezifikationen
- [Gesamtspezifikation](Reference/Full-Specification.md)
- [Technische Spezifikation](Reference/Technical-Specification.md)
- [API-Referenz](Reference/API-Reference.md)
- [Konfigurationsreferenz](Reference/Configuration-Reference.md)
- [Manifest-Schema](Reference/Manifest-Schema.md)

### 🗺️ [Roadmap](Roadmap/Roadmap.md)
Zukunftspläne und Features
- [Roadmap](Roadmap/Roadmap.md)
- [Plugin-System](Roadmap/Plugin-System.md)

---

## Für verschiedene Rollen

### Ich bin ein **Administrator/Operator**
- Start: [Quick Start Guide](Getting-Started/Quick-Start.md)
- Setup: [Setup Wizard](Setup-Wizard/Wizard-Flow.md)
- Betrieb: [Operations](Operations/Release-Management.md)

### Ich bin ein **Entwickler**
- Setup: [Entwicklungsumgebung](Development/Setup-Environment.md)
- Guidelines: [Contributing](Development/Contributing.md)
- Architektur: [Architecture Overview](Architecture/Overview.md)

### Ich bin ein **Architekt**
- Architektur: [Architecture](Architecture/Overview.md)
- Spezifikation: [Technical Specification](Reference/Technical-Specification.md)
- Sicherheit: [Security](Security/Overview.md)

---

## Hilfe & Support

- **Contributing**: Siehe [Contributing Guide](Development/Contributing.md)
- **Troubleshooting**: Siehe [Troubleshooting](Operations/Troubleshooting.md)
- **Issues**: Erstelle ein Issue im Repository

---

**Version**: 0.1.0
**Letzte Aktualisierung**: 2025-11-17

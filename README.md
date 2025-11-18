# ReadyStackGo

[![Build Status](https://tfsmain.ams.local/tfs/ams/Products/_apis/build/status%2FReadyStackGo?branchName=develop)](https://tfsmain.ams.local/tfs/ams/Products/_build/latest?definitionId=&branchName=develop)
[![Tests](https://img.shields.io/azure-devops/tests/ams/Products/ReadyStackGo/develop)](https://tfsmain.ams.local/tfs/ams/Products/_build/latest?definitionId=&branchName=develop)
[![Docker Hub](https://img.shields.io/docker/v/amssolution/readystackgo?label=docker&logo=docker)](https://hub.docker.com/r/amssolution/readystackgo)
[![Docker Image Size](https://img.shields.io/docker/image-size/amssolution/readystackgo/latest)](https://hub.docker.com/r/amssolution/readystackgo)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18-61DAFB?logo=react)](https://react.dev/)

ReadyStackGo (RSGO) ist eine selbst gehostete Plattform, um komplexe Microservice-Stacks auf Basis von Docker **einfach zu installieren, zu aktualisieren und zu verwalten** – mit einem einzigen Admin-Container, einer modernen Web-UI und manifestbasierten Deployments.

---

## Features

- 🧩 **Ein einzelner Admin-Container** verwaltet den gesamten Stack
- 🧙 **Geführter Setup-Wizard** (Admin, Organisation, Verbindungen, Installation)
- 🔐 **TLS-Handling** (Self-Signed Bootstrap & später Custom-Zertifikate)
- 📦 **Manifest-basierte Deployments** für ganze Stacks (SemVer)
- ⚙️ **Zentrale Konfiguration** via `rsgo.*.json`
- 🏷️ **Feature Flags** (fachliche Schalter, kontextübergreifend)
- 🔁 **CI/CD-Integration** für automatische Stack-Releases
- 🔒 **Security** mit Admin/Operator-Rollen, JWT & später OIDC
- 🔌 **Plugin-fähig** (geplantes Plugin-System)

---

## Repository-Struktur

```text
/
├─ src/
│  ├─ ReadyStackGo.Api
│  ├─ ReadyStackGo.Application
│  ├─ ReadyStackGo.Domain
│  ├─ ReadyStackGo.Infrastructure
│  └─ ReadyStackGo.WebUi
├─ manifests/
├─ docs/
│  ├─ Getting-Started/
│  ├─ Architecture/
│  ├─ Configuration/
│  ├─ Setup-Wizard/
│  ├─ Security/
│  ├─ Operations/
│  ├─ Development/
│  ├─ CI-CD/
│  ├─ Reference/
│  └─ Roadmap/
├─ CONTRIBUTING.md
└─ README.md
```

---

## Quick Start (Konzept)

1. **Admin-Container starten**  
   ```bash
   docker run -d \
     --name readystackgo-admin \
     -p 8443:8443 \
     -v /var/run/docker.sock:/var/run/docker.sock \
     -v rsgo-config:/app/config \
     --restart unless-stopped \
     your-registry/readystackgo/admin:0.1.0
   ```

2. **Wizard im Browser öffnen**  
   `https://<host>:8443`

3. **Wizard-Schritte durchlaufen**  
   - Admin-Benutzer anlegen  
   - Organisation definieren  
   - Verbindungen setzen (Simple Mode)  
   - Manifest wählen & installieren  

4. **Admin-UI nutzen**  
   - Container-Übersicht  
   - Releases verwalten  
   - TLS konfigurieren  
   - Feature Flags schalten  

---

## Dokumentation

Die ausführliche Dokumentation findest du im Ordner [`docs/`](./docs):

### Schnellstart
- [Übersicht](./docs/Getting-Started/Overview.md) – Was ist ReadyStackGo?
- [Quick Start](./docs/Getting-Started/Quick-Start.md) – Schnellstart-Anleitung
- [Installation](./docs/Getting-Started/Installation.md) – Detaillierte Installation

### Architektur & Konzepte
- [Architektur](./docs/Architecture/Overview.md) – Systemarchitektur
- [Komponenten](./docs/Architecture/Components.md) – Komponentenübersicht
- [Deployment Engine](./docs/Architecture/Deployment-Engine.md) – Deployment-Logik

### Konfiguration
- [Konfiguration](./docs/Configuration/Overview.md) – Konfigurationskonzepte
- [Config-Dateien](./docs/Configuration/Config-Files.md) – `rsgo.*.json` Dateien
- [Manifest-Spezifikation](./docs/Configuration/Manifest-Specification.md) – Manifest-Format
- [Feature Flags](./docs/Configuration/Feature-Flags.md) – Feature-Flag-System

### Weitere Themen
- [Setup-Wizard](./docs/Setup-Wizard/Wizard-Flow.md) – Wizard im Detail
- [Security](./docs/Security/Overview.md) – Sicherheitsarchitektur
- [TLS](./docs/Security/TLS-Configuration.md) – TLS-Konfiguration
- [CI/CD](./docs/CI-CD/Pipeline-Integration.md) – CI/CD-Integration
- [Roadmap](./docs/Roadmap/Roadmap.md) – Zukunftspläne

### Vollständige Dokumentation
Siehe [Dokumentations-Übersicht](./docs/Home.md) für die vollständige Übersicht.  

---

## Contributing

Siehe [Contributing Guide](./docs/Development/Contributing.md).

---

## Lizenz

Lizenztext nach Bedarf ergänzen (z. B. MIT, Apache 2.0 etc.).
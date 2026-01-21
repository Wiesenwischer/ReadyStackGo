# ReadyStackGo

[![CI](https://github.com/Wiesenwischer/ReadyStackGo/actions/workflows/ci.yml/badge.svg)](https://github.com/Wiesenwischer/ReadyStackGo/actions/workflows/ci.yml)
[![Docker Hub](https://img.shields.io/docker/v/wiesenwischer/readystackgo?label=docker&logo=docker)](https://hub.docker.com/r/wiesenwischer/readystackgo)
[![Docker Image Size](https://img.shields.io/docker/image-size/wiesenwischer/readystackgo/latest)](https://hub.docker.com/r/wiesenwischer/readystackgo)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)
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

## Getting Started

### Option 1: Docker Run

```bash
docker run -d \
  --name readystackgo \
  -p 8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v rsgo-config:/app/config \
  wiesenwischer/readystackgo:latest
```

> **Hinweis zum Config-Volume:** Das Volume `-v rsgo-config:/app/config` speichert Admin-Credentials, Wizard-Status und Konfiguration persistent. Ohne dieses Volume startet ReadyStackGo bei jedem Container-Neustart frisch mit dem Setup-Wizard – praktisch zum Testen. Volume manuell löschen: `docker volume rm rsgo-config`

### Option 2: Docker Compose

```bash
docker compose up -d
```

> **Frisch starten:** Um den Wizard erneut zu durchlaufen, Volume löschen mit `docker compose down -v`.

### Option 3: Lokale Entwicklung

**Voraussetzungen:**
- .NET 9.0 SDK
- Node.js 20+
- Docker (für Container-Management)

```bash
# Repository klonen
git clone https://github.com/Wiesenwischer/ReadyStackGo.git
cd ReadyStackGo

# Backend starten
cd src/ReadyStackGo.Api
dotnet run

# Frontend starten (neues Terminal)
cd src/ReadyStackGo.WebUi
npm install
npm run dev
```

### Nach dem Start

1. **Browser öffnen:** `http://localhost:8080` (Docker) oder `http://localhost:5173` (Entwicklung)
2. **Setup-Wizard durchlaufen:**
   - Admin-Benutzer anlegen
   - Organisation definieren
   - Docker-Environment konfigurieren
3. **Stacks deployen** über die Web-UI

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
- [Docker Registries](./docs/Configuration/Registries.md) – Registry-Verwaltung & Image Patterns
- [Feature Flags](./docs/Configuration/Feature-Flags.md) – Feature-Flag-System

### Weitere Themen
- [Setup-Wizard](./docs/Setup-Wizard/Wizard-Flow.md) – Wizard im Detail
- [Security](./docs/Security/Overview.md) – Sicherheitsarchitektur
- [TLS](./docs/Security/TLS-Configuration.md) – TLS-Konfiguration
- [CI/CD](./docs/CI-CD/Pipeline-Integration.md) – CI/CD-Integration
- [Roadmap](./docs/Reference/Roadmap.md) – Zukunftspläne

### Vollständige Dokumentation
Siehe [Dokumentations-Übersicht](./docs/Home.md) für die vollständige Übersicht.

---

## Contributing

Siehe [Contributing Guide](./docs/Development/Contributing.md).

---

## Lizenz

[MIT License](LICENSE.md) - Copyright (c) 2025 Marcus Dammann

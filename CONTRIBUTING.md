# Contributing to ReadyStackGo

Vielen Dank für dein Interesse an ReadyStackGo!  
Dieses Dokument beschreibt, wie du am Projekt mitarbeiten kannst.

---

## 1. Grundprinzipien

- **Clean Architecture** im Backend (API/Application/Domain/Infrastructure)
- **Dispatcher-Pattern** statt MediatR
- **Klare Verantwortlichkeiten** pro Schicht
- **Konsistente Benennung** (Namespaces `ReadyStackGo.*`, Config `rsgo.*.json`)
- **Testbarkeit**: Businesslogik in Handlers, nicht in Endpoints

---

## 2. Entwicklungs-Setup

Siehe [`SETUP.md`](./SETUP.md) für Details zu:

- .NET 9 Setup
- Node/Tailwind Setup
- Docker-Anforderungen
- Debugging

---

## 3. Branching & Workflow

Empfohlenes Modell (Beispiel):

- `main` – stabile Releases
- `develop` – Integrationsbranch
- `feature/*` – Feature-Branches

PR-Workflow:

1. Issue erstellen oder Issue referenzieren
2. Branch `feature/<kurze-beschreibung>` anlegen
3. Änderungen implementieren
4. Tests ausführen
5. PR nach `develop` mit Beschreibung & Screenshots (falls UI)

---

## 4. Coding Guidelines (Backend)

- Sprache: C# (.NET 9)
- Projektstruktur:  
  - `ReadyStackGo.Api` – FastEndpoints  
  - `ReadyStackGo.Application` – Commands/Queries/Handler  
  - `ReadyStackGo.Domain` – Entities/ValueObjects  
  - `ReadyStackGo.Infrastructure` – Docker/Files/TLS  

- Keine direkte Logik in Endpoints – immer `IDispatcher` nutzen
- Services als Interfaces in `Application` oder `Domain` definieren
- Implementierungen in `Infrastructure`

---

## 5. Coding Guidelines (Frontend)

- Sprache: TypeScript + React
- Styling: Tailwind, basierend auf TailAdmin
- API-Aufrufe über klar typisierte Client-Module
- Zustand bevorzugt über React Query / SWR
- Formulare mit klaren Validierungen

---

## 6. Tests

- Unit-Tests für:
  - Commands/Queries Handler
  - DeploymentPlan-Erzeugung
  - Manifest-Validierung
- Später: Integration-Tests für API

---

## 7. Architekturentscheidungen

Wichtige Architekturentscheidungen sollten als **ADR (Architecture Decision Record)** dokumentiert werden, z. B.:

- Wahl des Dispatcher-Patterns
- Aufbau der Manifest-Schemata
- TLS-Handling im Gateway statt im Admin-Container

---

## 8. Code Style

- C#: `dotnet format` nutzen
- Prettier/ESLint für TypeScript
- Keine ungenutzten `using`s / Imports
- Konsistente Benennung von Commands: `VerbNounCommand`

---

Vielen Dank für deinen Beitrag zu ReadyStackGo ❤️

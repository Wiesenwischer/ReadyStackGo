# Contributing to ReadyStackGo

Thank you for your interest in ReadyStackGo!
This document describes how you can contribute to the project.

---

## 1. Core Principles

- **Clean Architecture** in the backend (API/Application/Domain/Infrastructure)
- **Dispatcher Pattern** instead of MediatR
- **Clear responsibilities** per layer
- **Consistent naming** (Namespaces `ReadyStackGo.*`, Config `rsgo.*.json`)
- **Testability**: Business logic in handlers, not in endpoints

---

## 2. Development Setup

See [`SETUP.md`](./SETUP.md) for details on:

- .NET 9 Setup
- Node/Tailwind Setup
- Docker requirements
- Debugging

---

## 3. Branching & Workflow

ReadyStackGo uses **trunk-based development**:

- `main` – single source of truth
- `feature/*` – feature branches
- `bugfix/*` – bugfix branches

PR Workflow:

1. Create issue or reference existing issue
2. Create branch `feature/<short-description>` from `main`
3. Implement changes
4. Run tests
5. PR to `main` with description & screenshots (if UI)

---

## 4. Coding Guidelines (Backend)

- Language: C# (.NET 9)
- Project structure:
  - `ReadyStackGo.Api` – FastEndpoints
  - `ReadyStackGo.Application` – Commands/Queries/Handler
  - `ReadyStackGo.Domain` – Entities/ValueObjects
  - `ReadyStackGo.Infrastructure` – Docker/Files/TLS

- No direct logic in endpoints – always use `IDispatcher`
- Define services as interfaces in `Application` or `Domain`
- Implementations in `Infrastructure`

---

## 5. Coding Guidelines (Frontend)

- Language: TypeScript + React
- Styling: Tailwind, based on TailAdmin
- API calls via clearly typed client modules
- State preferably via React Query / SWR
- Forms with clear validations

---

## 6. Tests

- Unit tests for:
  - Commands/Queries handlers
  - DeploymentPlan generation
  - Manifest validation
- Later: Integration tests for API

---

## 7. Architecture Decisions

Important architecture decisions should be documented as **ADR (Architecture Decision Record)**, e.g.:

- Choice of dispatcher pattern
- Structure of manifest schemas
- TLS handling in gateway instead of admin container

---

## 8. Code Style

- C#: Use `dotnet format`
- Prettier/ESLint for TypeScript
- No unused `using`s / imports
- Consistent naming of commands: `VerbNounCommand`

---

Thank you for your contribution to ReadyStackGo!

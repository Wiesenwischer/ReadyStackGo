# Contributing to ReadyStackGo

Thank you for your interest in ReadyStackGo!
This document describes how you can contribute to the project.

---

## 1. Core Principles

- **Clean Architecture** in the backend (API/Application/Domain/Infrastructure)
- **Dispatcher Pattern** instead of MediatR
- **Clear responsibilities** per layer
- **Consistent naming** (Namespaces `ReadyStackGo.*`, Config `rsgo.*.json`)
- **Testability**: Business logic in Handlers, not in Endpoints

---

## 2. Development Setup

See [`SETUP.md`](./SETUP.md) for details on:

- .NET 9 Setup
- Node/Tailwind Setup
- Docker Requirements
- Debugging

---

## 3. Branching & Workflow

Recommended model (example):

- `main` – stable releases
- `develop` – integration branch
- `feature/*` – feature branches

PR Workflow:

1. Create issue or reference existing issue
2. Create branch `feature/<short-description>`
3. Implement changes
4. Run tests
5. PR to `develop` with description & screenshots (if UI)

---

## 4. Coding Guidelines (Backend)

- Language: C# (.NET 9)
- Project Structure:
  - `ReadyStackGo.Api` – FastEndpoints
  - `ReadyStackGo.Application` – Commands/Queries/Handlers
  - `ReadyStackGo.Domain` – Entities/ValueObjects
  - `ReadyStackGo.Infrastructure` – Docker/Files/TLS

- No direct logic in Endpoints – always use `IDispatcher`
- Define Services as Interfaces in `Application` or `Domain`
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
  - Commands/Queries Handlers
  - DeploymentPlan generation
  - Manifest validation
- Later: Integration tests for API

---

## 7. Architecture Decisions

Important architecture decisions should be documented as **ADR (Architecture Decision Records)**, e.g.:

- Choice of Dispatcher Pattern
- Structure of Manifest Schemas
- TLS Handling in Gateway instead of Admin Container

---

## 8. Code Style

- C#: Use `dotnet format`
- Prettier/ESLint for TypeScript
- No unused `using`s / Imports
- Consistent naming of Commands: `VerbNounCommand`

---

Thank you for your contribution to ReadyStackGo!

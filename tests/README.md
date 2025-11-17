# ReadyStackGo Tests

Diese Dokumentation beschreibt die Teststrategie und wie Tests ausgeführt werden.

## Testarchitektur

```
┌─────────────────────────────────────────┐
│ E2E Tests (Playwright)                  │
│ - UI → API → Docker Integration         │
│ - Vollständige User Journeys            │
│ - Browser-basiert                       │
└─────────────────────────────────────────┘
            ↓
┌─────────────────────────────────────────┐
│ Integration Tests                       │
│ (xUnit + Testcontainers + WebAppFactory)│
│ - API → DockerService → Docker          │
│ - Echte Docker-Container                │
│ - HTTP API Tests                        │
└─────────────────────────────────────────┘
            ↓
┌─────────────────────────────────────────┐
│ Unit Tests (xUnit)                      │
│ - Business Logic                        │
│ - DTOs, Mappings                        │
│ - Isolierte Komponenten                 │
└─────────────────────────────────────────┘
```

## Backend Integration Tests

### Voraussetzungen
- Docker Desktop muss laufen
- .NET 9 SDK

### Ausführen

```bash
# Alle Integration Tests
dotnet test tests/ReadyStackGo.IntegrationTests

# Mit detailliertem Output
dotnet test tests/ReadyStackGo.IntegrationTests --logger "console;verbosity=detailed"

# Einzelnen Test
dotnet test tests/ReadyStackGo.IntegrationTests --filter "FullyQualifiedName~DockerServiceIntegrationTests.ListContainersAsync_ShouldReturnContainers"
```

### Was wird getestet?

#### DockerService Tests
- `ListContainersAsync` - Auflisten aller Container
- `StartContainerAsync` - Container starten
- `StopContainerAsync` - Container stoppen
- Container-Properties korrekt gemappt

#### API Endpoint Tests
- `GET /api/containers` - Container-Liste via HTTP
- `POST /api/containers/{id}/start` - Start via API
- `POST /api/containers/{id}/stop` - Stop via API
- CORS-Konfiguration

### Technologie-Stack
- **xUnit** - Test-Framework
- **Testcontainers** - Echte Docker-Container für Tests
- **FluentAssertions** - Readable Assertions
- **WebApplicationFactory** - In-Memory API Tests

## E2E Tests (Playwright)

### Voraussetzungen
- Node.js 20+
- Docker Desktop muss laufen
- Playwright Browsers installiert

### Installation

```bash
cd src/ReadyStackGo.WebUI
npm install
npx playwright install chromium
```

### Ausführen

```bash
# Alle E2E Tests (headless)
npm run test:e2e

# Mit UI Mode (interaktiv)
npm run test:e2e:ui

# Mit sichtbarem Browser
npm run test:e2e:headed
```

### Was wird getestet?

#### Container Management Tests
- Containers-Seite anzeigen
- Container-Liste laden
- Refresh-Button funktioniert
- Error-Handling bei API-Fehler

#### Container Actions Tests
- Start-Button für gestoppte Container
- Stop-Button für laufende Container
- Loading-States während Actions
- Status-Badges mit korrekten Farben
- Port-Mappings anzeigen

### Test-Konfiguration

Die Konfiguration befindet sich in `src/ReadyStackGo.WebUI/playwright.config.ts`.

**Wichtig:** Playwright startet automatisch:
1. Frontend Dev Server (Vite) auf http://localhost:5174
2. Backend API Server (.NET) auf http://localhost:5259

## CI/CD (GitHub Actions)

Tests laufen automatisch bei:
- Push auf `main`, `develop`, `feature/**`
- Pull Requests nach `main` oder `develop`

### Workflow Jobs

1. **backend-tests** - Backend Integration Tests mit Testcontainers
2. **e2e-tests** - Playwright E2E Tests
3. **code-quality** - Linting & Type Checking

### Artefakte

- Backend Test Results: `backend-test-results`
- Playwright Report: `playwright-report` (30 Tage)

## Best Practices

### Integration Tests
✅ **DO**
- Echte Docker-Container verwenden (Testcontainers)
- Tests isoliert halten (eigene Container pro Test)
- Cleanup in `DisposeAsync()`
- Assertions mit FluentAssertions

❌ **DON'T**
- Docker-API mocken (verwende echte Container)
- Tests von einander abhängig machen
- Shared State zwischen Tests

### E2E Tests
✅ **DO**
- User-Perspektive testen (wie würde ein User es nutzen?)
- Warten auf Elemente (`waitFor`, nicht `waitForTimeout`)
- Sprechende Selektoren (`getByRole`, `getByText`)
- Error-Szenarien testen

❌ **DON'T**
- Implementation-Details testen
- Fixe Timeouts ohne Grund
- CSS-Klassen direkt verwenden (wenn möglich)
- Tests überkompliziert machen

## Troubleshooting

### Docker ist nicht verfügbar
```
Error: Cannot connect to Docker daemon
```
**Lösung:** Docker Desktop starten

### Port bereits in Verwendung
```
Error: Port 5174 is already in use
```
**Lösung:** Laufende Dev-Server stoppen oder Port in `playwright.config.ts` ändern

### Tests laufen zu langsam
- Tests parallel laufen lassen: `dotnet test --parallel`
- Playwright: `workers` in config erhöhen
- Nur geänderte Tests: `dotnet test --filter`

## Weitere Informationen

- [Testcontainers Dokumentation](https://dotnet.testcontainers.org/)
- [Playwright Dokumentation](https://playwright.dev/)
- [xUnit Dokumentation](https://xunit.net/)
- [FluentAssertions Dokumentation](https://fluentassertions.com/)

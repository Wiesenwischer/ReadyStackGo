# Phase: Registry Wizard UX (v0.25)

## Ziel

Neuer Wizard-Schritt, der Container-Registries automatisch aus den gewählten Stack-Quellen erkennt und dem Benutzer ermöglicht, Zugangsdaten inline (ohne Modals) zu konfigurieren.

## Analyse

### Bestehende Infrastruktur (Wiederverwendung)

- **Registry Aggregate Root** (`Domain/Deployment/Registries/Registry.cs`): `MatchesImage()` mit Glob-Patterns, `GetRegistryHost()`, `ImagePatterns`, Credentials
- **Registry CRUD** (`Api/Endpoints/Registries/`): Vollständige REST-API mit MediatR + FastEndpoints
- **Registry Settings UI** (`WebUi/src/pages/Settings/Registries/`): AddRegistry mit Preset-Templates (Docker Hub, GHCR, GitLab, Quay.io, Custom), Patterns-Textarea, Credential-Felder
- **5-Schritt-Wizard** (`WebUi/src/pages/Wizard/`): WizardLayout mit Stepper, State Machine (NotStarted → AdminCreated → OrganizationSet → Installed), 5-Minuten-Timeout
- **ServiceTemplate.Image** (`Domain/StackManagement/Stacks/ServiceTemplate.cs`): Enthält parseable Image-Referenzen
- **ParseImageReference** (`Infrastructure.Docker/Services/DeploymentEngine.cs:1013`): Existierende name:tag-Splitlogik
- **Three-Tier Credentials** (`RegistryCredentialProvider.cs`): DB → Config → Docker Config mit Pattern-Matching
- **IProductSourceService**: `SyncSourceAsync()` für Source-Sync, `GetProductsAsync()` für geladene Produkte
- **IProductCache**: `GetAllStacks()`, `GetStack()` für Zugriff auf geladene StackDefinitions

### Architektur-Pattern (Vorbilder)

| Pattern | Vorbild | Pfad |
|---------|---------|------|
| MediatR Query/Command | ListRegistrySourcesQuery | `Application/UseCases/StackSources/ListRegistrySources/` |
| FastEndpoint (Wizard) | ListRegistryForWizardEndpoint | `Api/Endpoints/Wizard/ListRegistryForWizardEndpoint.cs` |
| Wizard UI Step | StackSourcesStep.tsx | `WebUi/src/pages/Wizard/StackSourcesStep.tsx` |
| Wizard API Client | wizard.ts | `WebUi/src/api/wizard.ts` |
| Image Parsing | ParseImageReference | `Infrastructure.Docker/Services/DeploymentEngine.cs` |

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [x] **Feature 1: Image Reference Extraction Service** – Parst `ServiceTemplate.Image`-Strings, gruppiert nach Host+Namespace (PR #99)
  - Neue Dateien:
    - `Application/Services/IImageReferenceExtractor.cs` (Interface + DTOs)
    - `Infrastructure/Services/ImageReferenceExtractor.cs` (Implementierung)
    - `Infrastructure/DependencyInjection.cs` (Registration)
  - Tests: 40 Unit Tests in `UnitTests/Services/ImageReferenceExtractorTests.cs`
  - Abhängig von: –

- [x] **Feature 2: Detect Registries Endpoint** – Lädt Stacks aus Cache, extrahiert Images, gruppiert in Registry-Areas (PR #100)
  - Neue Dateien:
    - `Application/UseCases/Wizard/DetectRegistries/DetectRegistriesQuery.cs`
    - `Application/UseCases/Wizard/DetectRegistries/DetectRegistriesHandler.cs`
    - `Api/Endpoints/Wizard/DetectRegistriesEndpoint.cs`
  - Tests: 13 Unit Tests in `UnitTests/Application/Wizard/DetectRegistriesHandlerTests.cs`
  - Abhängig von: Feature 1

- [x] **Feature 3: Bulk Create Registries Endpoint** – Erstellt Registry-Einträge aus Wizard-Input (PR #101)
  - Neue Dateien:
    - `Application/UseCases/Wizard/SetRegistries/SetRegistriesCommand.cs`
    - `Application/UseCases/Wizard/SetRegistries/SetRegistriesHandler.cs`
    - `Api/Endpoints/Wizard/SetRegistriesEndpoint.cs`
  - Tests: 16 Unit Tests in `UnitTests/Application/Wizard/SetRegistriesHandlerTests.cs`
  - Entscheidung: Kein neuer WizardState – bestehende State Machine leitet Status aus DB ab, optional wie Step 3+4
  - Abhängig von: –

- [x] **Feature 4: Wizard Step UI** – Step 5 (Container Registries), Install wird Step 6 (PR #102)
  - Geänderte Dateien:
    - `WebUi/src/pages/Wizard/index.tsx` (6 Steps statt 5)
    - `WebUi/src/pages/Wizard/WizardLayout.tsx` (6 Steps)
  - Neue Dateien:
    - `WebUi/src/pages/Wizard/RegistriesStep.tsx`
    - `WebUi/src/api/wizard.ts` (neue API-Funktionen: detectRegistries, setRegistries)
  - Abhängig von: Feature 2, Feature 3

- [x] **Feature 5: Tests (Unit + Integration)** – Tests wurden parallel zur Implementierung geschrieben
  - Gesamt: 69 neue Unit Tests (40 + 13 + 16), alle 1685 Tests grün
  - Abhängig von: Feature 1-4

- [ ] **Dokumentation & Website** – PublicWeb (DE/EN), Roadmap
  - Abhängig von: Feature 1-5

- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main
  - Abhängig von: alle

## Detailplan pro Feature

### Feature 1: Image Reference Extraction Service

**Ziel:** Service der Image-Referenzen aus StackDefinitions extrahiert und in Registry-Bereiche gruppiert.

**Interface (`Application/Services/IImageReferenceExtractor.cs`):**

```csharp
public record ParsedImageReference(
    string OriginalReference,
    string Host,          // z.B. "docker.io", "ghcr.io"
    string Namespace,     // z.B. "amssolution", "library", "wiesenwischer"
    string Repository,    // z.B. "ams-api"
    string? Tag);

public record RegistryArea(
    string Host,
    string Namespace,
    string SuggestedPattern,  // z.B. "amssolution/*"
    string SuggestedName,     // z.B. "Docker Hub – amssolution"
    bool IsLikelyPublic,      // true für "library" Namespace auf docker.io
    IReadOnlyList<string> Images);  // Alle Image-Referenzen in diesem Bereich

public interface IImageReferenceExtractor
{
    ParsedImageReference ParseImageReference(string imageReference);
    IReadOnlyList<RegistryArea> GroupByRegistryArea(IEnumerable<string> imageReferences);
}
```

**Parsing-Logik:**
- Kein Host → `docker.io` (Docker Hub Default)
- Kein Namespace (z.B. `nginx:latest`) → Namespace = `library`
- Host mit Port (z.B. `registry:5000/img`) → Host = `registry:5000`
- Variable-Referenzen (`${VERSION}`) → Tag ignorieren, Rest parsen
- Digest-Referenzen (`@sha256:...`) → Digest ignorieren

**Gruppierung:**
- Gruppiert nach `Host + Namespace`
- SuggestedPattern: `{namespace}/*` (für docker.io), `{host}/{namespace}/*` (für andere)
- SuggestedName: `{Host} – {Namespace}` oder `Docker Hub – {Namespace}`
- IsLikelyPublic: true wenn `docker.io` + `library` (offizielle Docker Hub Images)

**Edge Cases:**
- Leere Image-Strings → ignorieren
- Nur Tag ohne Image → ignorieren
- Duplikate → deduplizieren innerhalb RegistryArea.Images

### Feature 2: Detect Registries Endpoint

**Ziel:** Endpoint der basierend auf den bereits gesyncten Stacks die benötigten Registry-Bereiche erkennt und mit bestehenden Registry-Konfigurationen abgleicht.

**Query:**
```csharp
public record DetectRegistriesQuery() : IRequest<DetectRegistriesResult>;

public record DetectRegistriesResult(
    IReadOnlyList<DetectedRegistryArea> Areas);

public record DetectedRegistryArea(
    string Host,
    string Namespace,
    string SuggestedPattern,
    string SuggestedName,
    bool IsLikelyPublic,
    bool IsConfigured,       // Bereits eine passende Registry in DB vorhanden
    IReadOnlyList<string> Images);
```

**Handler-Logik:**
1. Lade alle Stacks aus `IProductCache.GetAllStacks()`
2. Sammle alle `ServiceTemplate.Image`-Werte (alle Lifecycles, dedupliziert)
3. Übergib an `IImageReferenceExtractor.GroupByRegistryArea()`
4. **Falls keine Stacks vorhanden:** Liefere Default-Registry-Set (Docker Hub, GHCR, GitLab Registry, Quay.io) als Fallback
5. Für jede RegistryArea: Prüfe ob eine bestehende Registry in der DB diese Images matcht
6. Markiere `IsConfigured = true` wenn ein Match gefunden wird

**Endpoint:**
- `GET /api/wizard/detected-registries`
- AllowAnonymous + WizardTimeoutPreProcessor
- Response: `DetectedRegistryArea[]`

### Feature 3: Bulk Create Registries Endpoint

**Ziel:** Erstellt Registry-Einträge für die im Wizard konfigurierten Registry-Bereiche.

**Command:**
```csharp
public record RegistryInput(
    string Name,
    string Host,
    string Pattern,
    bool RequiresAuth,
    string? Username,
    string? Password);

public record SetRegistriesCommand(
    IReadOnlyList<RegistryInput> Registries) : IRequest<SetRegistriesResult>;

public record SetRegistriesResult(
    bool Success,
    int RegistriesCreated);
```

**Handler-Logik:**
1. Lade die erste Organization (wie in RegistryCredentialProvider)
2. Für jede RegistryInput:
   - Überspringe wenn `RequiresAuth == false` (public, keine Konfiguration nötig)
   - Prüfe ob bereits eine Registry mit gleichem Pattern/Host existiert → Überspringe
   - Erstelle `Registry` Aggregate mit `Name`, `Url` (aus Host), `ImagePatterns`, Credentials
   - Persistiere via `IRegistryRepository`
3. Return count

**Endpoint:**
- `POST /api/wizard/registries`
- AllowAnonymous + WizardTimeoutPreProcessor
- Request Body: `SetRegistriesRequest { Registries: RegistryInput[] }`
- Response: `SetRegistriesResponse { Success, RegistriesCreated }`

### Feature 4: Wizard Step UI

**Ziel:** Neuer Wizard-Step "Container Registries" als Step 5 (Install verschiebt sich auf Step 6).

**Stepper-Änderungen:**
1. Admin → 2. Organization → 3. Environment → 4. Stack Sources → **5. Container Registries** → 6. Install

**RegistriesStep.tsx:**
- Lädt beim Mount `GET /api/wizard/detected-registries`
- Loading-State während Detection läuft
- Pro RegistryArea eine Card:
  - Titel: `{Host} – {SuggestedPattern}` oder `Docker Hub – {Namespace}`
  - Image-Liste (collapsible): Alle betroffenen Images
  - Badge: "Bereits konfiguriert" (grün) oder "Konfiguration nötig" (gelb) oder "Public" (grau)
  - Auth-Radio: ● Authentifizierung erforderlich / ○ Public (kein Login nötig)
  - Wenn Auth: Username + Password/Token Felder inline
  - Pattern-Feld (editierbar, vorausgefüllt mit SuggestedPattern)
  - Name-Feld (editierbar, vorausgefüllt mit SuggestedName)
- Public Areas (IsLikelyPublic): Radio defaultmäßig auf "Public", Hinweistext
- "Skip" Button (optional, alle überspringen)
- "Continue" Button → validiert, erstellt Registries via POST, geht weiter

**State Management:**
- Lokaler State pro Card: `{ name, pattern, requiresAuth, username, password }`
- Cards mit `IsConfigured = true` → bereits konfiguriert-Badge, kein Formular
- Cards mit `IsLikelyPublic = true` → defaultmäßig auf "Public"

**Wizard-Flow Anpassung:**
- `index.tsx`: wizardState-Mapping anpassen, Step 5 = RegistriesStep, Step 6 = Install
- `WizardLayout.tsx`: 6 Steps statt 5
- Step 5 ist optional (Skip möglich) — wie Environment und StackSources

## Offene Punkte

Alle geklärt ✓

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Image-Quelle | A) Sync triggern B) Nur Cache | **B) Nur Cache** | Sync passiert in Step 4. Doppelter Sync wäre langsam und überflüssig. |
| Leere Sources | A) Hinweis B) Skip C) Default-Registries | **C) Default-Registries** | Auch ohne Stacks: typische Registries (Docker Hub, GHCR, GitLab) als Default-Set anbieten. |
| Wizard State | A) Neuer State B) Optional wie Step 3+4 | **B) Optional** | State Machine leitet Status aus DB ab. Kein neuer State nötig – konsistent mit Step 3+4. |
| Init-Container | A) Separat B) Zusammen | **B) Zusammen** | Alle Images aus allen ServiceTemplates fließen in die Erkennung ein, unabhängig vom Lifecycle. |

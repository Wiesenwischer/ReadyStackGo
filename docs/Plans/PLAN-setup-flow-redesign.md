# Phase: Setup Flow Redesign (v0.26)

## Ziel

Den Setup-Wizard in zwei Phasen aufteilen: eine kurze, zeitlimitierte Admin-Erstellung mit Auto-Login (Phase 1) und ein zeitloses, authentifiziertes Onboarding über eine Dashboard-Checklist (Phase 2). Damit wird der Zeitdruck bei der Ersteinrichtung eliminiert und gleichzeitig die Architektur für künftige Auth-Methoden (OAuth, Portal) vorbereitet.

## Analyse

### Aktueller Flow (v0.25)

```
/wizard (unauthentifiziert, 5-Min-Timeout für ALLES)
  Step 1: Admin (Username + Password)         ~10s
  Step 2: Organization (ID + Name)            ~10s
  Step 3: Environment (Socket Path)           ~10s  [optional]
  Step 4: Stack Sources (Catalog Selection)   ~15s  [optional]
  Step 5: Container Registries (v2 API Flow)  30s–3min [optional]
  Step 6: Complete Setup                      ~5s
```

**Probleme:**
- 5-Minuten-Timeout für den gesamten Flow, obwohl nur Step 1 sicherheitsrelevant ist
- Timer startet bei Container-Start, nicht bei erstem Zugriff
- Registry-Prüfungen (Step 5) können allein mehrere Minuten dauern
- Bei Timeout: Alles verloren, kompletter Neustart (Container restart)
- Organization-Erstellung ist an Admin-Erstellung gekoppelt (verhindert OAuth/Portal)

### Neuer Flow (v0.26)

```
PHASE 1: /wizard (unauthentifiziert, 5-Min-Timeout)
  Admin erstellen → Auto-Login (JWT im Response)

PHASE 2: /dashboard (authentifiziert, KEIN Timeout)
  Onboarding-Checklist:
    ☐ Organization einrichten        ← Pflicht, blockt andere Items
    ☐ Docker Environment einrichten  ← Optional
    ☐ Stack Sources hinzufügen       ← Optional
    ☐ Container Registries prüfen    ← Optional
    [Einrichtung abschließen] oder [Später erledigen]
```

### Bestehende Infrastruktur (Wiederverwendung)

| Komponente | Pfad | Nutzen |
|------------|------|--------|
| LoginEndpoint (JWT-Generierung) | `Api/Endpoints/Auth/LoginEndpoint.cs` | Response-Shape als Vorbild für Auto-Login |
| TokenService | `Infrastructure.Security/Authentication/TokenService.cs` | JWT-Generierung für Auto-Login |
| AuthContext | `WebUi/src/context/AuthContext.tsx` | Token-Storage in localStorage |
| Dashboard Error Banner | `WebUi/src/pages/Dashboard.tsx:49-53` | UI-Pattern für Onboarding-Banner |
| WizardGuard | `WebUi/src/components/wizard/WizardGuard.tsx` | Redirect-Logik anpassen |
| Settings-Seiten | `WebUi/src/pages/Settings/` | Environment, Sources, Registries bereits vorhanden |
| OrganizationProvisioningService | `Application/Services/OrganizationProvisioningService.cs` | Org-Erstellung + Role-Assignment |
| WizardTimeoutService | `Infrastructure/Configuration/WizardTimeoutService.cs` | Timeout-Logik anpassen |

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: Auto-Login nach Admin-Erstellung** – CreateAdminEndpoint gibt JWT zurück, Frontend loggt automatisch ein
  - Betroffene Dateien:
    - `Api/Endpoints/Wizard/CreateAdminEndpoint.cs` (Response erweitern)
    - `Application/UseCases/Wizard/CreateAdmin/CreateAdminHandler.cs` (Token generieren)
    - `WebUi/src/api/wizard.ts` (Response-Typ anpassen)
    - `WebUi/src/pages/Wizard/AdminStep.tsx` (Auto-Login nach Erstellung)
    - `WebUi/src/context/AuthContext.tsx` (setAuthFromToken-Methode)
  - Abhängig von: –

- [ ] **Feature 2: Wizard auf Phase 1 reduzieren** – Nur noch Admin-Erstellung, dann Redirect zum Dashboard
  - Betroffene Dateien:
    - `WebUi/src/pages/Wizard/index.tsx` (1 Step statt 6, Redirect nach Login)
    - `WebUi/src/pages/Wizard/WizardLayout.tsx` (vereinfachtes Layout, kein Stepper)
    - `WebUi/src/components/wizard/WizardGuard.tsx` (Logik anpassen)
  - Entscheidung: Alte Steps (Org, Env, Sources, Registries) bleiben als Dateien erhalten, werden aber nicht mehr im Wizard verwendet — Settings-Seiten übernehmen
  - Abhängig von: Feature 1

- [ ] **Feature 3: Onboarding-Status API** – Neuer Endpoint für den Einrichtungsstatus
  - Neue Dateien:
    - `Application/UseCases/Onboarding/GetOnboardingStatus/GetOnboardingStatusQuery.cs`
    - `Application/UseCases/Onboarding/GetOnboardingStatus/GetOnboardingStatusHandler.cs`
    - `Api/Endpoints/Onboarding/GetOnboardingStatusEndpoint.cs`
  - Response:
    ```typescript
    interface OnboardingStatus {
      isComplete: boolean;           // Alle Pflicht-Items erledigt
      isDismissed: boolean;          // User hat Checklist geschlossen
      items: {
        organization: { done: boolean; name?: string };
        environment: { done: boolean; count: number };
        stackSources: { done: boolean; count: number };
        registries: { done: boolean; count: number };
      };
    }
    ```
  - Handler prüft: Org vorhanden? Environments vorhanden? Sources vorhanden? Registries vorhanden?
  - Abhängig von: –

- [ ] **Feature 4: Onboarding-Checklist UI** – Dashboard-Komponente mit Setup-Fortschritt
  - Neue Dateien:
    - `WebUi/src/components/onboarding/OnboardingChecklist.tsx`
    - `WebUi/src/api/onboarding.ts`
  - Geänderte Dateien:
    - `WebUi/src/pages/Dashboard.tsx` (Checklist einbinden)
  - Design:
    - Amber/Blue-Banner oben im Dashboard (nicht modal, nicht blockierend)
    - Checklist-Items mit Status-Icons (✓ erledigt, → einrichten)
    - Organization als erstes Item, muss erledigt sein bevor andere aktiv werden
    - Jedes Item verlinkt zur entsprechenden Settings-Seite
    - "Einrichtung abschließen"-Button → markiert Onboarding als complete
    - "Später erledigen"-Button → dismisst die Checklist (localStorage + API)
  - Abhängig von: Feature 3

- [ ] **Feature 5: Organization-Erstellung aus Dashboard** – Org-Setup als eigenständigen Flow
  - Neue Dateien:
    - `WebUi/src/pages/Settings/Organization/SetupOrganization.tsx` (oder Modal)
    - `Api/Endpoints/Organization/CreateOrganizationEndpoint.cs` (authentifiziert)
  - Betroffene Dateien:
    - `WebUi/src/components/onboarding/OnboardingChecklist.tsx` (Link zu Org-Setup)
  - Entscheidung: Einfaches Formular (Name + ID), nutzt bestehenden `OrganizationProvisioningService`
  - Wichtig: Endpoint ist authentifiziert (JWT), nicht AllowAnonymous
  - Abhängig von: Feature 3

- [ ] **Feature 6: Timeout-Scope einschränken** – Timeout gilt nur noch für Phase 1
  - Betroffene Dateien:
    - `Api/Endpoints/Wizard/WizardTimeoutPreProcessor.cs` (nur noch für CreateAdmin)
    - `Infrastructure/Configuration/WizardTimeoutService.cs` (ggf. vereinfachen)
    - `Application/UseCases/Wizard/GetWizardStatus/GetWizardStatusHandler.cs` (States anpassen)
  - Wizard-States werden vereinfacht:
    - `NotStarted` → Kein Admin vorhanden
    - `Installed` → Admin vorhanden (Wizard abgeschlossen)
    - ~~AdminCreated~~ und ~~OrganizationSet~~ entfallen (Onboarding übernimmt)
  - Abhängig von: Feature 2

- [ ] **Feature 7: Dismiss-Endpoint + Cleanup** – Onboarding-Dismiss persistieren, alte Wizard-Steps bereinigen
  - Neue Dateien:
    - `Api/Endpoints/Onboarding/DismissOnboardingEndpoint.cs`
  - Betroffene Dateien:
    - `Infrastructure/Configuration/SystemConfig.cs` (OnboardingDismissed Flag)
  - Alte Wizard-Endpoints die unangetastet bleiben (abwärtskompatibel für API/E2E):
    - SetOrganization, SetEnvironment, SetSources, SetRegistries, Install
    - Diese werden weiterhin funktionieren, sind aber nicht mehr Teil des UI-Wizard
  - Abhängig von: Feature 4

- [ ] **Tests** – Unit + E2E
  - Abhängig von: Feature 1-7

- [ ] **Dokumentation & Website** – Aktualisierte Wizard-Docs, Onboarding-Docs
  - Abhängig von: Tests

- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main
  - Abhängig von: alle

## Detailplan pro Feature

### Feature 1: Auto-Login nach Admin-Erstellung

**Backend:**

CreateAdminEndpoint Response erweitern:

```csharp
public class CreateAdminResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    // NEU: Auto-Login-Daten
    public string? Token { get; init; }
    public string? Username { get; init; }
    public string? Role { get; init; }
}
```

CreateAdminHandler: Nach erfolgreicher User-Erstellung den TokenService aufrufen:

```csharp
// Nach: await _userRepository.AddAsync(user, ct);
var token = _tokenService.GenerateToken(user);
return new CreateAdminResult(
    Success: true,
    Message: "Admin user created successfully",
    Token: token,
    Username: user.Username,
    Role: "admin");
```

**Frontend:**

AuthContext erweitern um `setAuthDirectly(token, user)`:

```typescript
// Neue Methode in AuthContext
const setAuthDirectly = (token: string, user: { username: string; role: string }) => {
  localStorage.setItem('auth_token', token);
  localStorage.setItem('auth_user', JSON.stringify(user));
  setToken(token);
  setUser(user);
};
```

AdminStep nach erfolgreicher Erstellung:

```typescript
const response = await createAdmin({ username, password });
if (response.success && response.token) {
  setAuthDirectly(response.token, {
    username: response.username!,
    role: response.role!,
  });
  // Redirect zum Dashboard (mit Onboarding-Checklist)
  navigate('/dashboard');
}
```

### Feature 2: Wizard auf Phase 1 reduzieren

**Wizard index.tsx:**

```typescript
// Statt 6 Steps: Nur noch Admin-Erstellung
// Wenn wizardState === 'Installed' → redirect /login
// Wenn wizardState === 'NotStarted' → AdminStep anzeigen
// Nach Admin-Erstellung → Auto-Login → redirect /dashboard
```

**WizardLayout:**
- Kein Stepper mehr (nur noch 1 Step)
- Titel: "Welcome to ReadyStackGo"
- Untertitel: "Create your admin account to get started"
- Countdown-Timer bleibt (für den einen Step)

**WizardGuard:**
- Prüft nur noch: Ist ein Admin vorhanden?
- Wenn nein → /wizard
- Wenn ja → Dashboard passieren lassen

### Feature 3: Onboarding-Status API

**Handler-Logik:**

```csharp
public async Task<OnboardingStatus> Handle(GetOnboardingStatusQuery query, CancellationToken ct)
{
    var hasOrg = await _orgRepository.GetAllAsync(ct) is { Count: > 0 };
    var org = hasOrg ? (await _orgRepository.GetAllAsync(ct)).First() : null;
    var envCount = hasOrg ? await _environmentRepository.CountAsync(ct) : 0;
    var sourceCount = await _stackSourceRepository.CountAsync(ct);
    var registryCount = await _registryRepository.CountAsync(ct);
    var isDismissed = await _systemConfigService.GetValueAsync("OnboardingDismissed", ct) == "true";

    return new OnboardingStatus
    {
        IsComplete = hasOrg, // Org ist das einzige Pflicht-Item
        IsDismissed = isDismissed,
        Items = new OnboardingItems
        {
            Organization = new(hasOrg, org?.Name),
            Environment = new(envCount > 0, envCount),
            StackSources = new(sourceCount > 0, sourceCount),
            Registries = new(registryCount > 0, registryCount)
        }
    };
}
```

### Feature 4: Onboarding-Checklist UI

**Design-Konzept:**

```
┌─────────────────────────────────────────────────────────┐
│ 🔧 Complete Your Setup                    [Dismiss ✕]  │
│                                                         │
│ ✓ Admin account created                                 │
│ → Set up your organization          [Configure →]       │
│ ○ Add a Docker environment          (requires org)      │
│ ○ Configure stack sources           (requires org)      │
│ ○ Set up container registries       (optional)          │
│                                                         │
│ [Complete Setup]                                        │
└─────────────────────────────────────────────────────────┘
```

- Amber-Border solange Org fehlt, Blue-Border wenn nur optionale Items offen
- Items ohne Org sind ausgegraut mit "(requires organization)" Hinweis
- Erledigte Items zeigen grünen Haken + Zusammenfassung (z.B. "2 environments configured")
- "Configure →" Button führt zur jeweiligen Settings-Seite
- "Complete Setup" Button ruft Dismiss-Endpoint auf und blendet Banner aus
- "Dismiss ✕" speichert in localStorage UND API (überlebt Browser-Wechsel)

### Feature 5: Organization-Erstellung aus Dashboard

Neuer authentifizierter Endpoint:

```
POST /api/organizations
Body: { id: string, name: string }
Response: { success: boolean, organizationId: string }
Auth: JWT (RequirePermission: not needed, admin check sufficient)
```

Nutzt bestehenden `OrganizationProvisioningService.ProvisionOrganization()`.

UI: Einfaches Inline-Formular oder Modal, das aus der Onboarding-Checklist geöffnet wird. Felder: Organization Name (Pflicht), Organization ID (auto-generated aus Name, editierbar).

### Feature 6: Timeout-Scope einschränken

**Vereinfachte Wizard-States:**

| Alt | Neu | Bedeutung |
|-----|-----|-----------|
| NotStarted | NotStarted | Kein Admin, Wizard nötig |
| AdminCreated | ~~entfällt~~ | — |
| OrganizationSet | ~~entfällt~~ | — |
| Installed | Installed | Admin existiert, Wizard abgeschlossen |

**GetWizardStatusHandler:**
```csharp
// Vereinfacht:
// 1. Timeout gelockt? → NotStarted
// 2. Admin existiert? → Installed
// 3. Sonst → NotStarted
```

**WizardTimeoutPreProcessor:**
- Bleibt auf CreateAdminEndpoint
- Wird von allen anderen Wizard-Endpoints entfernt (die werden entweder authentifiziert oder bleiben als Legacy-API)

## Offene Punkte

- [ ] Sollen die alten Wizard-Endpoints (SetOrganization, SetEnvironment, etc.) erhalten bleiben? → Ja, für API-Kompatibilität und E2E-Tests, aber nicht mehr im UI-Wizard
- [ ] Soll die Org-Erstellung ein Modal oder eine eigene Seite sein? → Klären in Feature 5
- [ ] Timeout-Dauer anpassen? 5 Min für nur Admin-Erstellung ist großzügig genug

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Phase-1-Scope | A) Admin+Org B) Nur Admin | **B) Nur Admin** | Ermöglicht künftige OAuth/Portal-Integration. Org gehört zur Konfiguration, nicht zur Authentifizierung. |
| Auto-Login | A) Redirect zu /login B) JWT im Response | **B) JWT im Response** | Kein zusätzlicher Klick, nahtloser Übergang. Gleiche Token-Logik wie LoginEndpoint. |
| Onboarding-UI | A) Eigene Seite B) Dashboard-Banner C) Modal | **B) Dashboard-Banner** | Nicht-blockierend, User sieht sofort das Dashboard. Banner ist prominent aber nicht aufdringlich. |
| Org-Pflicht | A) Alles optional B) Org Pflicht | **B) Org Pflicht** | Environments, Sources, Registries brauchen eine Org. Ohne Org ist nichts konfigurierbar. |
| Alte Wizard-Endpoints | A) Entfernen B) Behalten C) Deprecaten | **B) Behalten** | Abwärtskompatibilität für API-User und E2E-Tests. Kein Breaking Change. |
| Timeout nach Admin | A) Timeout läuft weiter B) Timeout wird gelöscht | **B) Timeout wird gelöscht** | Nach Admin-Erstellung ist die Instanz "claimed". Timeout hat seinen Zweck erfüllt. |

## Zukunfts-Kompatibilität

Diese Architektur ermöglicht direkt:

| Szenario | Phase 1 wird zu | Phase 2 ändert sich |
|----------|-----------------|---------------------|
| **OAuth (GitHub, Google)** | "Sign in with X" Button → erster User wird Admin | Unverändert |
| **Zentrales Portal** | "Authenticate with Portal" → Admin + Org wird vom Portal geliefert | Org-Item entfällt aus Checklist |
| **Distribution (v0.27)** | `ISetupWizardDefinitionProvider` liefert Auth-Methode | `IOnboardingDefinitionProvider` liefert Checklist-Items |
| **Multi-Tenant** | Portal-Auth → Org-Auswahl statt Org-Erstellung | Checklist pro Org |

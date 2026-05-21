<!-- GitHub Epic: #401 (Variant 2) -->
# Phase: PRTG Integration — Variant 2 (Active Push via PRTG API)

## Ziel

RSGO bekommt einen Deploy-Time-Schalter "Register in PRTG". Wenn aktiviert, ruft RSGO beim Anlegen eines Product Deployments die PRTG-HTTP-API auf, legt dort ein Device + passende SNMP-Sensoren an, und entfernt das Device wieder, wenn das Deployment removed wird. URL und API-Token werden **pro Deployment** im Deploy-Wizard erfasst.

Diese Variante liefert die maximale Bequemlichkeit für ad-hoc-Setups — der Admin muss in PRTG nichts mehr manuell anlegen — bei minimalem Domain-Modell-Footprint (keine wiederverwendbare Connection-Entity, das ist Variante 3).

> **Hinweis**: Variante 2 und Variante 3 sind funktional überlappend. Variante 3 ist die "richtige" Lösung mit Domain-Modell; Variante 2 ist die light-weight Brücke. In der Praxis sollte das Team entscheiden, ob Variante 2 als Zwischenschritt benötigt wird oder direkt zu Variante 3 gesprungen wird.

## Analyse

### Bestehende Architektur

- **ProductDeployment-Aggregate** ([ProductDeployment.cs](../../src/ReadyStackGo.Domain/Deployment/ProductDeployments/ProductDeployment.cs)) ist der Einstiegspunkt — wir hängen ein optionales `PrtgRegistration`-Value-Object an, das URL, API-Token-Hash und die in PRTG vergebene Device-ID hält.
- **Deployment-Events** ([ProductDeploymentEvents.cs](../../src/ReadyStackGo.Domain/Deployment/ProductDeployments/ProductDeploymentEvents.cs)) liefern alle Trigger: `ProductDeploymentCompleted` → Device anlegen, `ProductDeploymentRemoved` / `ProductDeploymentSuperseded` → Device entfernen, `ProductDeploymentFailed` → optional Status-Sensor flaggen.
- **CredentialEncryptionService** ([ICredentialEncryptionService.cs](../../src/ReadyStackGo.Application/Services/ICredentialEncryptionService.cs)) verschlüsselt heute Docker-Registry-Credentials und SNMPv3-Passphrasen — dasselbe Pattern für den PRTG-API-Token.
- **MediatR Notification-Handler** — Trap-Emission in v0.65 ([PLAN-snmp-completion.md](PLAN-snmp-completion.md), Feature 7) ist die direkte Vorlage: drei `INotificationHandler<TDomainEvent>` reagieren auf Deployment-Lifecycle und rufen den PRTG-Client auf.
- **DeployProductWizard** im rsgo-generic ([Deploy/AddProductDeployment.tsx](../../src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deploy/), falls vorhanden — sonst entsprechender Wizard) braucht einen neuen optionalen Schritt "Monitoring".

### PRTG REST/HTTP-API

PRTG bietet eine HTTP-API mit URL-basierten Calls und API-Token-Auth über `apitoken=<TOKEN>` Query-Parameter. Relevante Endpoints:

- `POST /api/duplicateobject.htm?id=<group_id>&name=<new_name>&host=<rsgo_ip>` — Device aus Template-Device duplizieren. Liefert neue Device-ID.
- `POST /api/setobjectproperty.htm?id=<device_id>&name=host&value=<ip>` — Host-Property setzen.
- `POST /api/deleteobject.htm?id=<device_id>` — Device löschen.
- `GET /api/table.json?content=devices&filter_objid=<id>` — Status prüfen.

API-Token-Setup auf PRTG-Seite: Admin legt im PRTG einen User mit "Use Passhash"-Token an und gibt RSGO den Token. PRTG empfiehlt Passhash über API-Key, aber neuere Versionen (≥ 23.x) bieten echte API-Tokens — RSGO unterstützt beide via gleichem Header.

> **Wichtig**: Die PRTG-API ist HTTP-basiert mit URL-Query-Parametern. TLS-Verifikation muss konfigurierbar sein (viele PRTG-Installationen nutzen Self-Signed-Certs).

### Template-Device-Konzept

Statt jeden Sensor einzeln per API anzulegen, definieren wir ein **Vorlagen-Pattern**: Der Admin legt einmalig ein "RSGO Template Device" in PRTG an (mit allen Sensoren konfiguriert, Status "paused") und gibt RSGO die ID dieses Devices. RSGO dupliziert dieses Device pro neuem ProductDeployment via `duplicateobject.htm`, setzt Host-IP und entpausiert. Vorteile:

- Keine sensorspezifische Logik in RSGO — Admin kontrolliert vollständig, welche Sensoren angelegt werden.
- Funktioniert mit beliebigen PRTG-Versionen (kein abhängiger Sensor-Erstellungs-API-Endpoint).
- Sensor-Definitionen leben in PRTG (Source of Truth ist eindeutig).

Empfohlen wird, das Template-Device aus dem Variant-1-Bundle zu erstellen — Variante 2 *braucht* Variante 1 also nicht streng, aber profitiert davon.

### Pro-Deployment-Credentials

URL + API-Token werden **pro Deployment** erfasst, nicht zentral. Das ist die explizite Abgrenzung zu Variante 3:

- Vorteil: kein neues Settings-Modell, keine globale Connection-Verwaltung.
- Nachteil: Admin gibt für jedes Deployment dieselben Credentials neu ein. Das ist okay für 1-2 Deployments, schmerzt bei 10+.

→ Genau deshalb ist Variante 3 in den meisten Fällen die bessere Wahl. Variante 2 macht primär Sinn, wenn pro Deployment unterschiedliche PRTG-Instanzen verwendet werden (Multi-Tenant / Customer-Hosted PRTG).

### Auto-Deregister auf Removal

`ProductDeploymentRemoved` und `ProductDeploymentSuperseded` lösen die Deregistrierung aus. Bei Network-Errors / nicht erreichbarem PRTG: best-effort + WARN-Log + persistierter `Pending-Delete`-Marker am Deployment, der von einem Background-Service periodisch retried wird, solange das Deployment-Record noch in der DB ist.

### Betroffene Bounded Contexts

- **Domain** — neues Value-Object `PrtgRegistration(PrtgUrl, EncryptedApiToken, TemplateDeviceId, RegisteredDeviceId, RegisteredAt)` als optionales Property auf `ProductDeployment`. Domain-Method `EnablePrtgRegistration(...)` / `DisablePrtgRegistration()`. Domain-Event `PrtgRegistrationRequested` / `PrtgDeregistrationRequested`.
- **Application** — neuer Service `IPrtgApiClient` mit Methoden `RegisterDeviceAsync` und `DeregisterDeviceAsync`. MediatR-Handler `PrtgRegistrationOnDeploymentCompletedHandler`, `PrtgDeregistrationOnDeploymentRemovedHandler`.
- **Infrastructure** — Implementation `PrtgApiClient` über `HttpClient` mit konfigurierbarer Cert-Validation. Polly-Retry-Policy für transient errors.
- **API** — neuer Wizard-Step im DeployProduct-Flow erfordert keinen neuen Endpoint, nur Erweiterung von `CreateProductDeploymentCommand` um `PrtgRegistration?`.
- **WebUI (rsgo-generic)** — neuer Schritt "Monitoring (Optional)" im Deploy-Wizard mit Toggle "Register in PRTG" + URL + API-Token + Template-Device-ID. Inline-Test-Connection-Button.

## AMS UI Counterpart

**Benötigt AMS UI eine Entsprechung?**

- [x] **Ja** — der Deploy-Wizard-Schritt muss in beiden Distributionen vorhanden sein. `useDeployProduct`-Hook in `@rsgo/core` wird erweitert; UI in ConsistentUI.

→ AMS-Counterpart-Plan: `C:\proj\ReadyStackGo.Ams\docs\Plans\PLAN-prtg-api-push.md`

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: PrtgRegistration Value-Object + Domain-Anbindung** — neues VO mit Validierung (URL-Format, Token-Mindestlänge). `ProductDeployment.EnablePrtgRegistration()`. Migration: optionale Spalten an `ProductDeployments`-Tabelle.
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/Deployment/ProductDeployments/PrtgRegistration.cs` (neu)
    - `src/ReadyStackGo.Domain/Deployment/ProductDeployments/ProductDeployment.cs` (extend)
    - `src/ReadyStackGo.Infrastructure/Persistence/Configurations/ProductDeploymentConfiguration.cs` (extend)
    - EF Migration `AddPrtgRegistrationToProductDeployment`
  - Abhängig von: -

- [ ] **Feature 2: IPrtgApiClient + Implementation** — Application-Interface, Infrastructure-Impl mit `HttpClient`, optional Cert-Validation skip per Setting (default: validate). Polly-Retry (3 Versuche, exponential backoff). Logging mit redacted API-Token.
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Prtg/IPrtgApiClient.cs`
    - `src/ReadyStackGo.Application/Prtg/PrtgRegistrationResult.cs`
    - `src/ReadyStackGo.Infrastructure/Prtg/PrtgApiClient.cs`
    - `src/ReadyStackGo.Infrastructure/Prtg/PrtgApiOptions.cs`
    - DI-Registrierung in `Infrastructure/DependencyInjection.cs`
  - Pattern-Vorlage: bestehende `RegistryClient`-Pattern für externe HTTP-APIs mit Credentials
  - Abhängig von: -

- [ ] **Feature 3: MediatR Notification-Handler für Register/Deregister** — drei Handler:
  - `PrtgRegisterOnDeploymentCompletedHandler` → `ProductDeploymentCompleted` → `IPrtgApiClient.RegisterDeviceAsync`
  - `PrtgDeregisterOnDeploymentRemovedHandler` → `ProductDeploymentRemoved` / `ProductDeploymentSuperseded` → `DeregisterDeviceAsync`
  - `PrtgRetryBackgroundService` für Pending-Delete-Markierungen
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Prtg/Handlers/*.cs`
    - `src/ReadyStackGo.Api/BackgroundServices/PrtgRetryBackgroundService.cs`
  - Pattern-Vorlage: Trap-Handler aus [PLAN-snmp-completion.md](PLAN-snmp-completion.md) Feature 7
  - Abhängig von: Feature 1, 2

- [ ] **Feature 4: CreateProductDeploymentCommand Extension + Test-Connection-Endpoint** — Command bekommt optionalen `PrtgRegistrationInput`-Block; Handler legt VO an. Zusätzlich neuer Endpoint `POST /api/prtg/test-connection` mit Body `{url, apiToken}` für den Test-Button im Wizard.
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/Deployment/CreateProductDeployment/CreateProductDeploymentCommand.cs` (extend)
    - `src/ReadyStackGo.Application/UseCases/Prtg/TestConnection/TestPrtgConnectionQuery.cs` (neu)
    - `src/ReadyStackGo.Api/Endpoints/Prtg/TestPrtgConnectionEndpoint.cs` (neu, Permission `Deployment:Create`)
  - Abhängig von: Feature 2

- [ ] **Feature 5: Core-Hook + API-Client** — `usePrtgTestConnection()` in `@rsgo/core`, plus Erweiterung der `useCreateProductDeployment`-Hook um `prtgRegistration`-Input.
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/core/src/api/prtg.ts` (neu)
    - `src/ReadyStackGo.WebUi/packages/core/src/hooks/usePrtg.ts` (neu)
  - Abhängig von: Feature 4

- [ ] **Feature 6: Deploy-Wizard "Monitoring"-Schritt** — neuer optionaler Schritt zwischen "Variables" und "Confirm". Toggle "Register in PRTG", URL-Feld, API-Token-Feld (masked), Template-Device-ID-Feld, Test-Connection-Button mit Inline-Feedback.
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deploy/Wizard/MonitoringStep.tsx` (neu)
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deploy/Wizard/DeployWizard.tsx` (Step-Liste extend)
  - Abhängig von: Feature 5

- [ ] **Feature 7: Product Deployment Detail Page — PRTG Status Section** — Wenn `PrtgRegistration` gesetzt: Section mit aktueller PRTG-Device-ID, Registered-At, Direkt-Link zur Device-Seite im PRTG, "Re-register"-Button bei Failure.
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deployments/PrtgStatusCard.tsx` (neu)
    - Detail-Page extend
  - Abhängig von: Feature 6

- [ ] **Feature 8: Public Website Doc + Setup-Anleitung** — Reference-Page mit dem "Template Device"-Pattern erklärt, PRTG-API-Token-Erstellung, Multi-Tenant-Pattern.

- [ ] **Phase abschließen** — alle Tests grün, manueller E2E-Test gegen lokale PRTG-Trial-Instanz, PR gegen main.

## Test-Strategie

- **Unit Tests**:
  - `PrtgRegistration` VO — Validierung (URL-Format, Token-Länge, Template-Device-ID > 0), Encryption-Roundtrip.
  - `PrtgApiClient` — gegen `HttpMessageHandler`-Mock; happy-path, 401-Unauthorized, 5xx mit Retry, Network-Timeout, TLS-Failure.
  - Handler — `ProductDeploymentCompleted` ohne `PrtgRegistration` macht nichts; mit Registration ruft Client auf; Idempotenz bei wiederholtem Event.

- **Integration Tests**:
  - Endpoint `POST /api/prtg/test-connection` — Permission-Check, ungültige URL, Network-Failure → 503.
  - DB-Roundtrip: ProductDeployment mit `PrtgRegistration` speichern und laden — verschlüsselter Token bleibt encrypted in DB, im Domain-Object korrekt entschlüsselt.

- **E2E Tests** (Playwright):
  - Deploy-Wizard: Monitoring-Schritt überspringbar (default off), Toggle aktivierbar, Test-Connection-Button mit Mock-PRTG-Server.
  - Deployment-Detail-Page: PRTG-Status-Card sichtbar wenn Registration gesetzt.

- **Manueller PRTG-Smoke-Test** (Acceptance):
  - Lokale PRTG-Trial. Template-Device anlegen mit RSGO-Sensoren. Deploy mit aktivierter PRTG-Registration. Device in PRTG erscheint, Status grün. Deployment removen — Device verschwindet.

## Offene Punkte

- [ ] **Sensor-Set-Anpassung** — Wie reagieren wir wenn PRTG-Admin später Sensoren am Template-Device ändert? Vermutlich: keine Sync, dokumentieren als bekannte Limitation (nur Devices, die nach Template-Änderung dupliziert werden, bekommen die neuen Sensoren).
- [ ] **PRTG Cloud vs. On-Premise** — Cloud-Variante hat dynamische URLs; Test mit beiden bevor Variante in Public-Doc beworben wird.
- [ ] **Token-Rotation** — Wenn ein PRTG-API-Token ungültig wird, müssen alle abhängigen ProductDeployments deren Token aktualisiert haben. Im Domain-Modell pro Deployment einzeln — dieses Pain Point ist genau der Treiber für Variante 3.

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Sensor-Erstellung | Per-Sensor-API-Calls / Template-Device duplizieren | **Template-Device duplizieren** | PRTG-Admin behält Kontrolle, weniger Bruchstellen bei PRTG-Updates, ein einziger API-Call statt N. |
| Credential-Storage | Plain in DB / Encrypted via CredentialEncryptionService | **Encrypted** | Bestehendes Muster (Registries, SNMPv3). Token sind sensible Credentials. |
| Cert-Validation-Default | Always strict / Configurable mit default strict | **Configurable, default strict** | PRTG-Installationen mit Self-Signed-Certs sind häufig; aber Default sicher. |
| Registrierungs-Zeitpunkt | DeploymentInitiated / DeploymentCompleted | **DeploymentCompleted** | Vermeidet Device-Anlage für später fehlschlagende Deployments. |
| Failure-Handling | Hard-fail Deployment / Best-Effort + Retry | **Best-Effort + Retry** | PRTG-Outage darf keinen Deployment blockieren. Background-Retry für transient errors. |
| Multi-Deployment-Credentials | Pro Deployment / Global / Pro Connection | **Pro Deployment** | Bewusste Abgrenzung zu Variante 3 — Variante 2 ist genau dann sinnvoll, wenn unterschiedliche PRTG-Server pro Deployment relevant sind. |
| IANA-PEN-Abhängigkeit | Blocken bis PEN da / Trotzdem ausliefern | **Blocken bis PEN da** | Variante 2 erzeugt persistente PRTG-Devices, die nach PEN-Migration broken sensors haben. Wir warten auf PEN. |

# Phase: Notifications Phase 2

## Ziel

Drei neue Notification-Typen für proaktive Benachrichtigungen hinzufügen: Container Health Changes (mit Throttling), API Key First-Use und TLS Certificate Expiry (gestaffelte Warnungen). Baut auf der bestehenden In-Memory-Notification-Infrastruktur aus v0.26 auf.

**Nicht-Ziel**: Persistente Notifications (DB-backed), E-Mail/Webhook-Benachrichtigungen, benutzerdefinierte Schwellwerte.

## Analyse

### Bestehende Notification-Infrastruktur

| Komponente | Datei | Status |
|---|---|---|
| `Notification` Model | `Application/Notifications/Notification.cs` | Vorhanden — `Id`, `Type`, `Title`, `Message`, `Severity`, `ActionUrl`, `Metadata` |
| `NotificationType` Enum | `Application/Notifications/Notification.cs` | 4 Werte: `UpdateAvailable`, `SourceSyncResult`, `DeploymentResult`, `ProductDeploymentResult` |
| `NotificationSeverity` Enum | `Application/Notifications/Notification.cs` | `Info`, `Success`, `Warning`, `Error` |
| `NotificationFactory` | `Application/Notifications/NotificationFactory.cs` | 3 statische Methoden: `CreateSyncResult`, `CreateDeploymentResult`, `CreateProductDeploymentResult` |
| `INotificationService` | `Application/Services/INotificationService.cs` | Interface mit `AddAsync`, `ExistsAsync` (Deduplizierung), `GetAllAsync`, `MarkAsReadAsync` etc. |
| `InMemoryNotificationService` | `Infrastructure/Services/InMemoryNotificationService.cs` | Singleton, FIFO max 50, `ConcurrentDictionary` |
| Notification API Endpoints | `Api/Endpoints/Notifications/` | List, Unread-Count, Mark-Read, Mark-All-Read, Dismiss |
| `NotificationDropdown` | `WebUi/src/components/header/NotificationDropdown.tsx` | Bell Icon, Badge, 60s Polling, Click-Navigation via `actionUrl` |

### Deduplizierungs-Pattern (Vorbild)

`GetVersionHandler.cs` (line 65): Prüft via `ExistsAsync(NotificationType.UpdateAvailable, "version", newVersion)` ob eine Notification für diese Version bereits existiert. Dieses Pattern wird für alle drei neuen Features wiederverwendet.

### Betroffene Systeme pro Feature

#### Feature 1: Container Health Change Notification
- **Health Collection**: `HealthCollectorBackgroundService` (alle 30s) → `HealthCollectorService.CollectAllHealthAsync()` → per Deployment: `IHealthMonitoringService.CaptureHealthSnapshotAsync()`
- **Aktuell nur SignalR**: `IHealthNotificationService.NotifyDeploymentHealthChangedAsync()` — kein In-App-Notification
- **Health Status**: `HealthStatus` Enumeration — `Healthy(0)`, `Degraded(1)`, `Unhealthy(2)`, `Unknown(3)`, `NotFound(4)`
- **Throttling-Vorbild**: `MaintenanceObserverService` nutzt `ConcurrentDictionary<Guid, DateTimeOffset>` für Cooldowns

#### Feature 2: API Key First-Use Notification
- **Domain Model**: `ApiKey` hat `LastUsedAt` (nullable) — `null` = noch nie benutzt
- **Auth Handler**: `ApiKeyAuthenticationHandler.HandleAuthenticateAsync()` ruft `apiKey.RecordUsage()` bei jedem Request auf
- **Erkennungspunkt**: Vor `RecordUsage()` prüfen: `if (apiKey.LastUsedAt == null)` → First Use
- **Problem**: `INotificationService` ist nicht im Auth Handler injiziert — muss über `HttpContext.RequestServices` aufgelöst werden

#### Feature 3: TLS Certificate Expiry Notification
- **Zertifikat-Info**: `ITlsConfigService.GetCertificateInfoAsync()` → `CertificateInfo` mit `ExpiresAt`, `Thumbprint`, `Subject`, `IsSelfSigned`
- **Bestehender Check**: `CertificateRenewalBackgroundService` (alle 12h, nur Let's Encrypt Renewal)
- **`CertificateInfo.IsExpiringSoon`**: Nur 30-Tage-Schwelle, keine gestaffelte Warnung
- **Alle TLS-Modi**: `SelfSigned`, `Custom`, `LetsEncrypt` — Expiry-Check muss für alle gelten

## Features / Schritte

### Grundlage

- [ ] **Feature 1: NotificationType Erweiterung** — Neue Enum-Werte und Factory-Methoden
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Notifications/Notification.cs` — 3 neue `NotificationType` Werte: `HealthChange`, `ApiKeyFirstUse`, `CertificateExpiry`
    - `src/ReadyStackGo.Application/Notifications/NotificationFactory.cs` — 3 neue statische Methoden
  - Neue Factory-Methoden:
    ```csharp
    CreateHealthChangeNotification(string stackName, string serviceName, string previousStatus, string currentStatus, string? deploymentId = null)
    CreateApiKeyFirstUseNotification(string keyName, string keyPrefix)
    CreateCertificateExpiryNotification(string subject, string thumbprint, DateTime expiresAt, int daysRemaining)
    ```
  - Severity-Mapping für Health: `Unhealthy`/`NotFound` → `Error`, `Degraded` → `Warning`
  - Severity-Mapping für Certificate: 30d/14d → `Warning`, 7d/3d/1d → `Error`
  - Pattern-Vorlage: Bestehende `CreateDeploymentResult` Methode
  - Abhängig von: -
  - Tests:
    - Unit: Alle neuen Factory-Methoden, Severity-Mapping, Metadata korrekt gesetzt, ActionUrl korrekt

### Health Change Notification

- [ ] **Feature 2: Health Change Notification mit Throttling** — In-App-Notification bei Health-Status-Änderungen
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure/Services/Health/HealthCollectorService.cs` — `INotificationService` injizieren, Status-Change-Detection + Throttling
  - Implementierung:
    - **Status-Tracking**: `ConcurrentDictionary<string, string>` als Singleton — Key: `"{deploymentId}:{serviceName}"`, Value: letzter `HealthStatus`
    - **Change-Detection**: Nach jedem Health-Snapshot pro Service: Wenn aktueller Status ≠ letzter Status → Notification erstellen
    - **Throttle**: Nur bei Verschlechterung notifizieren (Healthy→Unhealthy: ja, Unhealthy→Healthy: optional Info). Cooldown von 5 Minuten pro Service via `ConcurrentDictionary<string, DateTime>` — verhindert Notification-Spam bei flapping Services
    - **Deduplizierung**: `ExistsAsync(HealthChange, "serviceKey", "{deploymentId}:{serviceName}")` als zusätzliche Absicherung
    - **ActionUrl**: `/deployments/{stackName}` (falls `stackName` verfügbar) oder `/health`
  - Pattern-Vorlage: `MaintenanceObserverService` (Cooldown-Pattern)
  - Abhängig von: Feature 1
  - Tests:
    - Unit: Status-Change-Detection (Healthy→Unhealthy, Unhealthy→Healthy, no change → kein Notification)
    - Unit: Throttle-Cooldown (zweites Unhealthy innerhalb 5 Min → keine Notification)
    - Unit: Verschiedene Services unabhängig getracked
    - Unit: Flapping-Schutz (schnelle Wechsel → max 1 Notification pro Cooldown)

### API Key First-Use Notification

- [ ] **Feature 3: API Key First-Use Notification** — Benachrichtigung bei erstmaliger Verwendung eines API Keys
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure.Security/Authentication/ApiKeyAuthenticationHandler.cs` — First-Use-Check vor `RecordUsage()`
  - Implementierung:
    - Vor `apiKey.RecordUsage()` (line ~77): `if (apiKey.LastUsedAt == null)` prüfen
    - `INotificationService` via `Context.HttpContext.RequestServices.GetService<INotificationService>()` auflösen
    - `NotificationFactory.CreateApiKeyFirstUseNotification(apiKey.Name, apiKey.KeyPrefix)` erstellen
    - `await notificationService.AddAsync(notification)` — fire-and-forget im `try/catch`
    - **ActionUrl**: `/settings/api-keys` (oder null wenn kein Settings-Link gewünscht)
    - **Deduplizierung**: Nicht nötig — `LastUsedAt == null` triggert nur einmal pro Key
  - Pattern-Vorlage: `GetVersionHandler.cs` (Notification in bestehenden Flow einbauen)
  - Abhängig von: Feature 1
  - Tests:
    - Unit: First-Use erzeugt Notification (LastUsedAt == null)
    - Unit: Zweiter Use erzeugt KEINE Notification (LastUsedAt != null)
    - Unit: Notification enthält Key-Name und Prefix
    - Unit: `INotificationService` nicht verfügbar → kein Fehler (graceful degradation)

### TLS Certificate Expiry Notification

- [ ] **Feature 4: TLS Certificate Expiry Background Service** — Gestaffelte Warnungen bei ablaufenden Zertifikaten
  - Betroffene Dateien:
    - `src/ReadyStackGo.Api/BackgroundServices/CertificateExpiryCheckService.cs` (NEU) — Background Service
    - `src/ReadyStackGo.Api/Program.cs` — Service registrieren
  - Implementierung:
    - **Neuer Background Service** (nicht in `CertificateRenewalBackgroundService` einbauen — andere Verantwortung):
      - Läuft alle 12 Stunden (initial delay 1 Minute)
      - Ruft `ITlsConfigService.GetCertificateInfoAsync()` auf
      - Berechnet `daysRemaining = (cert.ExpiresAt - DateTime.UtcNow).TotalDays`
      - **Staged Thresholds**: `[30, 14, 7, 3, 1]` Tage
      - Für jeden überschrittenen Threshold: Prüfe via `ExistsAsync(CertificateExpiry, "threshold", "{thumbprint}:{days}")` ob bereits notifiziert
      - Falls nicht: Erstelle Notification mit passendem Severity
    - **Severity-Staffelung**:
      - 30d, 14d → `Warning`
      - 7d → `Warning`
      - 3d, 1d → `Error`
      - Abgelaufen (0d) → `Error` mit "Certificate has expired!"
    - **ActionUrl**: `/settings/tls`
    - **Deduplizierung**: `ExistsAsync` mit `"threshold:{thumbprint}:{days}"` → jeder Threshold feuert nur einmal pro Zertifikat
    - **Self-Signed Filter**: Optional — Self-Signed-Zertifikate sind meist auto-generiert und erneuern sich selbst. Entscheidung: Trotzdem warnen oder nur Custom/LE?
  - Pattern-Vorlage: `CertificateRenewalBackgroundService` (Background Service Pattern), `GetVersionHandler.cs` (Dedup-Pattern)
  - Abhängig von: Feature 1
  - Tests:
    - Unit: Alle Threshold-Stufen korrekt erkannt (31d → keine, 30d → Warning, 14d → Warning, 7d → Warning, 3d → Error, 1d → Error, 0d → Error)
    - Unit: Deduplizierung — gleicher Threshold wird nicht zweimal gesendet
    - Unit: Verschiedene Zertifikate (andere Thumbprints) werden unabhängig getracked
    - Unit: Self-Signed Zertifikate — Verhalten klären

### Abschluss

- [ ] **Dokumentation & Website** — Wiki, Public Website (DE/EN), Roadmap
- [ ] **Phase abschließen** — Alle Tests grün, PR gegen main

## Test-Strategie

### Unit Tests
- **NotificationFactory**: Alle 3 neuen Methoden, Severity-Mapping, Metadata, ActionUrl
- **Health Change**: Status-Tracking, Change-Detection, Throttle-Cooldown, Flapping
- **API Key First-Use**: First-Use vs. Subsequent-Use, graceful degradation
- **Certificate Expiry**: Threshold-Berechnung, Staged Severity, Dedup

### Integration Tests
- **Health Change**: End-to-End mit HealthCollectorService + InMemoryNotificationService
- **API Key First-Use**: Auth-Handler mit echtem Request + Notification-Überprüfung
- **Certificate Expiry**: Background Service mit Mock-Zertifikat

## Offene Punkte

- [ ] Sollen Self-Signed-Zertifikate Expiry-Warnungen erzeugen? (Sie werden meist automatisch neu generiert beim Neustart)
- [ ] Soll der Health-Change-Cooldown konfigurierbar sein oder fest auf 5 Minuten?
- [ ] Soll die Health-Recovery (Unhealthy→Healthy) auch eine Notification erzeugen? (Info-Severity "Service recovered")

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Health Throttle | A) ExistsAsync Dedup, B) ConcurrentDict Cooldown, C) Beides | **C) Beides** | ConcurrentDict für Echtzeit-Cooldown (5 Min), ExistsAsync als Backup gegen Restarts |
| TLS Service | A) CertificateRenewalService erweitern, B) Neuer Service | **B) Neuer Service** | Separation of Concerns — Renewal ≠ Expiry Notification |
| First-Use DI | A) Constructor Injection, B) ServiceProvider Resolve | **B) ServiceProvider** | Auth Handler hat keinen Zugriff auf DI-Container direkt, `HttpContext.RequestServices` ist der Standard-Weg |
| Health Notification Scope | A) Nur Verschlechterung, B) Verschlechterung + Recovery | - | **Offen — User fragen** |
| Self-Signed Expiry | A) Warnen, B) Ignorieren | - | **Offen — User fragen** |

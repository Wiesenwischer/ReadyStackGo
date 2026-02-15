# ReadyStackGo – Notification System & System Info UX

## 1. Ziel

ReadyStackGo soll ein leichtgewichtiges Notification-System erhalten, das den User über
wichtige Ereignisse informiert – ohne dass er aktiv auf jeder Seite nachsehen muss.

Zusätzlich soll die Versions- und Update-Information besser erreichbar sein:
- **Settings > System**: Vollständige Versionsinfo + manueller Update-Check
- **User-Dropdown**: Dezente Versionsanzeige + Badge bei verfügbarem Update

---

## 2. Notification-Infrastruktur

### 2.1 Datenmodell

```typescript
interface Notification {
  id: string;                    // UUID
  type: NotificationType;        // Enum (s.u.)
  title: string;                 // Kurztitel, z.B. "Update Available"
  message: string;               // Detail, z.B. "v0.23.0 is available"
  severity: 'info' | 'success' | 'warning' | 'error';
  createdAt: string;             // ISO 8601
  read: boolean;
  actionUrl?: string;            // Optional: Link zu relevanter Seite
  actionLabel?: string;          // Optional: "Update now", "View details"
  metadata?: Record<string, string>; // Typ-spezifische Daten
}

type NotificationType =
  | 'update-available'
  | 'source-sync-result'
  | 'deployment-result'
  | 'container-health'
  | 'api-key-event'
  | 'tls-certificate';
```

### 2.2 Backend: In-Memory Notification Store

Keine Datenbank-Persistenz nötig (pre-v1.0). Notifications sind **transient** und leben
nur solange der Server-Prozess läuft. Das ist akzeptabel, da:

- Notifications sind informativ, nicht kritisch
- Bei Container-Restart sind alte Notifications irrelevant
- Einfache Implementierung ohne Schema-Änderung

**`INotificationService`** (Application Layer):
```csharp
public interface INotificationService
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task MarkAsReadAsync(string id, CancellationToken ct = default);
    Task MarkAllAsReadAsync(CancellationToken ct = default);
    Task DismissAsync(string id, CancellationToken ct = default);
}
```

**Implementierung** (Infrastructure Layer):
- `ConcurrentDictionary<string, Notification>` als Store
- Max. 50 Notifications, älteste werden automatisch entfernt (FIFO)
- Singleton-Lifetime

### 2.3 Backend: API Endpoints

| Methode | Route | Beschreibung |
|---------|-------|-------------|
| GET | `/api/notifications` | Alle Notifications (neueste zuerst) |
| GET | `/api/notifications/unread-count` | Anzahl ungelesener Notifications |
| POST | `/api/notifications/{id}/read` | Einzelne als gelesen markieren |
| POST | `/api/notifications/read-all` | Alle als gelesen markieren |
| DELETE | `/api/notifications/{id}` | Einzelne Notification entfernen |

Alle Endpoints erfordern Authentication (JWT).

### 2.4 Frontend: NotificationDropdown

Bestehendes Bell-Icon im Header wird mit echten Daten gefüllt:

- **Badge**: Zeigt ungelesene Anzahl (roter Dot mit Zahl, animated ping nur bei neuen)
- **Dropdown**: Liste der Notifications mit Icon je Severity, Titel, Zeitstempel
- **Klick auf Notification**: Markiert als gelesen + navigiert zu `actionUrl` (falls vorhanden)
- **"Mark all as read"**: Button im Dropdown-Header
- **Polling**: Alle 60 Sekunden `GET /api/notifications/unread-count` (leichtgewichtig)
- **Leer-Zustand**: "No notifications" (wie aktuell, aber nur wenn wirklich keine da sind)

### 2.5 Frontend: Severity-Icons

| Severity | Icon | Farbe |
|----------|------|-------|
| info | Info-Circle | `text-blue-500` |
| success | Check-Circle | `text-green-500` |
| warning | Exclamation-Triangle | `text-amber-500` |
| error | X-Circle | `text-red-500` |

---

## 3. Notification-Typen

### 3.1 Update Available (Phase 1)

**Trigger**: Beim Start des Servers und danach alle 24h prüft `VersionCheckService` auf
GitHub Releases. Wenn `updateAvailable == true`, wird eine Notification erzeugt.

**Deduplizierung**: Nur eine Notification pro Version. Wenn bereits eine für dieselbe
`latestVersion` existiert, wird keine neue erstellt.

```
Severity: info
Title:    "Update Available"
Message:  "ReadyStackGo v0.23.0 is available. You are running v0.0.0."
Action:   "/update?version=0.23.0&releaseUrl=..."  →  "Update now"
Metadata: { currentVersion: "0.0.0", latestVersion: "0.23.0" }
```

**Manueller Check**: `GET /api/system/version?forceCheck=true` leert den 24h-Cache
und prüft erneut gegen GitHub. Erzeugt Notification falls Update verfügbar.

### 3.2 Stack Source Sync Result (Phase 1)

**Trigger**: Nach jeder Source-Synchronisation (manuell über UI, Webhook, oder automatisch).
Der bestehende Sync-Handler erzeugt eine Notification mit dem Ergebnis.

```
Severity: success | warning | error
Title:    "Source Sync Complete" | "Source Sync Warning" | "Source Sync Failed"
Message:  "'ReadyStackGo Stacks' synced — 3 new, 1 updated, 0 removed"
          "'My Source' sync failed: Repository not reachable"
Action:   "/settings/sources"  →  "View sources"
Metadata: { sourceId: "...", sourceName: "...", added: "3", updated: "1", removed: "0" }
```

**Severity-Logik**:
- `success`: Sync erfolgreich, mindestens 1 Änderung
- `info`: Sync erfolgreich, keine Änderungen ("already up to date")
- `warning`: Sync teilweise erfolgreich (einige Manifeste konnten nicht geparst werden)
- `error`: Sync komplett fehlgeschlagen (Repo nicht erreichbar, Auth-Fehler)

### 3.3 Deployment Result (Phase 1)

**Trigger**: Nach Abschluss eines Deployments (Deploy, Upgrade, Rollback, Remove).
Besonders relevant bei Webhook-Triggered Deployments, wo der User nicht aktiv zuschaut.

```
Severity: success | error
Title:    "Deployment Successful" | "Deployment Failed"
Message:  "'nginx-proxy' deployed on 'Local Docker'"
          "'postgres' upgrade failed: image pull error"
Action:   "/deployments/{id}"  →  "View deployment"
Metadata: { deploymentId: "...", stackName: "...", environmentName: "...", operation: "deploy" }
```

**Hinweis**: Deployment-Notifications werden nur erzeugt wenn der User **nicht** auf der
Deployment-Detail-Seite ist (dort sieht er den Progress bereits via SignalR).
→ Einfache Heuristik: Immer erzeugen. Die Notification verschwindet wenn gelesen.

### 3.4 Container Health Change (Phase 2)

**Trigger**: Wenn der `HealthCollectorService` einen Statuswechsel erkennt
(healthy → unhealthy, running → stopped). Nur bei **negativen** Änderungen notifizieren.

```
Severity: warning | error
Title:    "Container Unhealthy" | "Container Stopped"
Message:  "'postgres' on 'Local Docker' is unhealthy"
          "'redis' on 'Local Docker' stopped unexpectedly"
Action:   "/health/{deploymentId}"  →  "View health"
Metadata: { containerId: "...", containerName: "...", environmentName: "...", previousStatus: "healthy", newStatus: "unhealthy" }
```

**Deduplizierung**: Pro Container+Status nur eine Notification. Wenn bereits eine
für denselben Container im selben Status existiert (und ungelesen ist), keine neue.

**Throttling**: Max. 5 Health-Notifications pro Minute um Flapping zu vermeiden.

### 3.5 API Key Event (Phase 2)

**Trigger**: Bei sicherheitsrelevanten API-Key-Ereignissen.

```
Severity: info | warning
Title:    "API Key First Use" | "API Key Expiring"
Message:  "API Key 'CI Pipeline' was used for the first time"
          "API Key 'old-key' expires in 7 days"
Action:   "/settings/api-keys"  →  "Manage API keys"
Metadata: { keyId: "...", keyName: "...", prefix: "rsgo_..." }
```

**Events**:
- First Use: Beim ersten erfolgreichen Request mit einem API Key
- Expiry Warning: Wenn Expiry-Feature implementiert wird (aktuell kein Ablaufdatum)

**Hinweis**: "First Use" ist bereits jetzt umsetzbar. "Expiry Warning" erst wenn API Keys
ein Ablaufdatum bekommen.

### 3.6 TLS Certificate Expiry (Phase 2)

**Trigger**: Periodischer Check (z.B. einmal täglich) ob das TLS-Zertifikat demnächst
abläuft. `CertificateInfo.isExpiringSoon` existiert bereits im Backend.

```
Severity: warning | error
Title:    "Certificate Expiring Soon" | "Certificate Expired"
Message:  "Your TLS certificate expires in 14 days"
          "Let's Encrypt renewal failed: DNS validation timeout"
Action:   "/settings/tls"  →  "Manage TLS"
Metadata: { expiresAt: "...", daysRemaining: "14", issuer: "Let's Encrypt" }
```

**Deduplizierung**: Nur eine Notification pro Zertifikat-Status. Neue Notification erst
wenn sich `daysRemaining` um eine Stufe ändert (30d, 14d, 7d, 3d, 1d, 0d).

---

## 4. Settings > System (Phase 1)

Neuer Tab "System" in den Settings (neben General, TLS, Registries, Stack Sources, API Keys).

### Layout

```
┌─────────────────────────────────────────────────────┐
│ System Information                                   │
├─────────────────────────────────────────────────────┤
│                                                     │
│  Server Version     0.0.0-dev                       │
│  Latest Version     0.23.0          [Check now]     │
│  Runtime            .NET 9.0.13                     │
│  Git Commit         abc1234...                      │
│  Build Date         2026-02-15                      │
│                                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │ ℹ Update Available                          │   │
│  │ ReadyStackGo v0.23.0 is available.          │   │
│  │                                              │   │
│  │ [Update now]  [See what's new ↗]            │   │
│  └─────────────────────────────────────────────┘   │
│                                                     │
│  Last checked: 5 minutes ago                        │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### Verhalten

- **"Check now"** Button: Ruft `GET /api/system/version?forceCheck=true` auf
- **Update-Banner**: Nur sichtbar wenn `updateAvailable == true`
- **"Update now"**: Navigiert zu `/update?version=...`
- **"See what's new"**: Öffnet `latestReleaseUrl` in neuem Tab
- **"Last checked"**: Relative Zeitangabe, basierend auf Response-Timestamp

### Backend-Änderung

`GET /api/system/version` bekommt optionalen Query-Parameter `forceCheck`:
- `?forceCheck=true`: Löscht den MemoryCache-Eintrag bevor der Check läuft
- Default (ohne Parameter): Verwendet den 24h-Cache wie bisher

Änderung in `GetVersionQuery`:
```csharp
public record GetVersionQuery(bool ForceCheck = false) : IRequest<GetVersionResponse>;
```

Response erweitern um `checkedAt`:
```csharp
public DateTime? CheckedAt { get; set; } // Zeitpunkt des letzten GitHub-Checks
```

---

## 5. User-Dropdown Version Badge (Phase 1)

Im bestehenden `UserDropdown.tsx` wird unterhalb der Menu-Items ein dezenter
Versions-Hinweis eingebaut:

```
┌──────────────────────┐
│ 👤 Admin User        │
│    Administrator      │
├──────────────────────┤
│ Profile              │
│ Settings             │
├──────────────────────┤
│ v0.0.0  🟢 Update   │  ← Neu: Version + Badge
├──────────────────────┤
│ Logout               │
└──────────────────────┘
```

### Verhalten

- **Versionstext**: `v{serverVersion}` in `text-xs text-gray-400`
- **Update-Badge**: Kleiner grüner/blauer Dot + "Update" Text, nur wenn `updateAvailable`
- **Klick auf Version/Badge**: Navigiert zu `/settings/system` (wo der volle Check ist)
- **Daten**: Nutzt denselben API-Call wie SidebarWidget (oder shared Context/Hook)

### Shared Hook

Um doppelte API-Calls zu vermeiden, wird ein `useVersionInfo()` Hook erstellt:

```typescript
// hooks/useVersionInfo.ts
export function useVersionInfo() {
  // Singleton-Pattern: Einmal laden, überall nutzen
  // Cached im React Context oder SWR/React-Query-style
  return { versionInfo, isLoading, refetch };
}
```

Wird von `SidebarWidget`, `UserDropdown`, und `Settings > System` genutzt.

---

## 6. Phasen-Planung

### Phase 1 (nächste Version)

**Scope**: Notification-Infrastruktur + 3 Notification-Typen + System-Settings + User-Dropdown

| Feature | Aufwand | Beschreibung |
|---------|---------|-------------|
| Notification-Infrastruktur | Backend + Frontend | In-Memory Store, API Endpoints, NotificationDropdown mit echten Daten, Polling |
| Update Available | Backend | VersionCheckService erzeugt Notification, forceCheck-Parameter |
| Source Sync Result | Backend | Sync-Handler erzeugt Notification nach Abschluss |
| Deployment Result | Backend | Deployment-Handler erzeugt Notification nach Abschluss |
| Settings > System | Frontend | Neuer Settings-Tab mit Versionsinfo + manueller Update-Check |
| User-Dropdown Badge | Frontend | Versionsanzeige + Update-Badge im Profil-Menü |
| useVersionInfo Hook | Frontend | Shared Hook für Version-Daten (SidebarWidget, Dropdown, Settings) |

**Feature-Reihenfolge** (nach Abhängigkeiten):

1. **Notification-Infrastruktur** (Backend: Store + API, Frontend: Dropdown + Polling)
2. **Update Available Notification** (Backend: VersionCheckService → NotificationService, forceCheck)
3. **Settings > System** (Frontend: Neuer Tab, nutzt forceCheck)
4. **useVersionInfo Hook + User-Dropdown Badge** (Frontend: Shared Hook, Dropdown-Erweiterung)
5. **Source Sync Notification** (Backend: Sync-Handler erweitern)
6. **Deployment Result Notification** (Backend: Deployment-Handler erweitern)

### Phase 2 (spätere Version)

| Feature | Voraussetzung | Beschreibung |
|---------|---------------|-------------|
| Container Health Change | Health Collector läuft | Statuswechsel-Notifications mit Throttling |
| API Key Event | API Key Domain | First-Use Tracking, Expiry (wenn implementiert) |
| TLS Certificate Expiry | TLS-Management | Periodischer Check, Stufen-basierte Benachrichtigung |

Phase 2 hängt nicht von Phase 1 ab bezüglich Backend-Infrastruktur (die wird in Phase 1 gebaut),
aber die einzelnen Typen brauchen spezifische Trigger in ihren jeweiligen Domains.

---

## 7. Abgrenzung

**Nicht in Scope**:
- Datenbank-Persistenz für Notifications (In-Memory reicht pre-v1.0)
- Push-Notifications (Browser/Desktop) — evtl. post-v1.0
- SignalR für Echtzeit-Notification-Push — Polling reicht für den Anfang
- E-Mail-Benachrichtigungen
- Notification-Einstellungen (welche Typen ein/aus) — erst wenn es zu viele werden
- Sound-Effekte bei neuen Notifications

**Bewusste Vereinfachungen**:
- Max. 50 Notifications im Store (älteste fallen raus)
- 60-Sekunden Polling statt WebSocket-Push
- Keine Notification-Gruppen oder Threading
- Keine benutzerspezifischen Notifications (Single-User pre-v1.0)

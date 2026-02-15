# ReadyStackGo – Container-Registry-Wizard (UX & Flow)

## 1. Ziel dieses Dokuments

Dieses Dokument beschreibt, **wie** die Konfiguration der Container-Registries im
ReadyStackGo-Setup-Wizard umgesetzt werden soll – speziell unter der Randbedingung:

- Es gibt einen **festen Stepper oben** (Schritte mit Nummer + Titel),
- **jeder Schritt ist eine eigene Seite**,
- **keine Dialoge/Modals** im Wizard für die Authentifizierung.

Fokus:  
Wie binden wir die **Container-Registry-Authentifizierung** so ein, dass sie:

- zum bestehenden Wizard / Stepper passt,
- mehrere Registries (auch mit Patterns wie `amssolution/*`) unterstützt,
- für den Benutzer übersichtlich und verständlich bleibt.

---

## 2. Ausgangslage

ReadyStackGo hat bereits:

- eine **Container-Registry-Registry**  
  → hier werden Einträge für Container-Registries gepflegt (z. B. Docker Hub, GHCR, private Registries).  
  Jeder Eintrag kann:
  - einen `Host` haben (z. B. `docker.io`, `ghcr.io`),
  - ein oder mehrere **Patterns** (z. B. `amssolution/*`),
  - Authentifizierungs-Daten (Basic, Token, etc.).

- eine **Manifest-Registry-Registry**  
  → hier werden Quellen für Stack-Manifeste gepflegt (Git-Repos, etc.).

Stacks enthalten Images wie:

- `amssolution/ams-api:0.5.0` (implizit `docker.io`)
- `docker.io/amssolution/ams-worker:0.5.0`
- `ghcr.io/wiesenwischer/ams-project-api:0.5.0`

Du kannst in einer Container-Registry-Konfiguration Patterns hinterlegen, z. B.:

- `amssolution/*`  
- `wiesenwischer/*`

Damit kannst du z. B. **zwei verschiedene private Docker-Hub-Accounts** verwenden, obwohl der Host gleich ist (`docker.io`).

---

## 3. Grundentscheidung: Ein fester Wizard-Step statt Dialoge

### 3.1 Stepper-Design

Der Wizard hat einen **festen Stepper** mit Schritten wie z. B.:

1. Admin & TLS  
2. Organisationen & Environments  
3. Stack-/Manifest-Quellen  
4. Container-Registries  
5. Zusammenfassung  

Wichtige Design-Entscheidung:

> **Es soll keinen separaten Dialog für die Registry-Auth geben**,  
> weil das:
> - den Stepper visuell „bricht“,
> - den Flow komplizierter macht (Wizard-State + Dialog-State),
> - die UX verschlechtert (man sieht immer nur eine Registry, verliert Überblick).

Stattdessen:

- Es gibt **genau einen Wizard-Step „Container-Registries“** (z. B. Schritt 4).
- Auf dieser Seite werden **alle relevanten Registry-Bereiche** als **Cards**/Sektionen dargestellt.
- Die Authentifizierungsfelder werden **inline** in diesen Cards konfiguriert.

---

## 4. Ablauf im Wizard

### 4.1 Vorheriger Schritt: Manifest-Quellen wählen

In einem vorherigen Step (z. B. Schritt 3):

- Der Benutzer wählt, welche Manifest-Registries (Git-Repos etc.) er nutzen möchte.
- ReadyStackGo:
  - lädt die Manifeste aus diesen Quellen,
  - analysiert alle `image:`-Einträge,
  - extrahiert dabei:
    - `Host` (z. B. `docker.io`, `ghcr.io`),
    - `Path` (z. B. `amssolution/ams-api`).

Auf Basis dieser Informationen bildet RSGO **Registry-Bereiche**, z. B.:

- Host `docker.io`, Namespace `amssolution` → Vorschlags-Pattern: `amssolution/*`
- Host `ghcr.io`, Namespace `wiesenwischer` → Vorschlags-Pattern: `wiesenwischer/*`
- Host `docker.io`, Namespace `library` → Vorschlags-Pattern: `library/*` (Public Images)

Diese Registry-Bereiche bilden später die Cards auf der Seite „Container-Registries“.

---

### 4.2 Schritt „Container-Registries“ (ein fester Step)

Oben:

> **Schritt 4 von 5 – Container-Registries**  
> ReadyStackGo hat anhand der ausgewählten Stacks die folgenden Registry-Bereiche erkannt.  
> Bitte hinterlege ggf. Zugangsdaten für private Registries.

#### 4.2.1 Darstellung als Cards

Die Seite zeigt **eine Card pro Registry-Bereich**, z. B.:

**Card 1:**

- Titel: `docker.io – amssolution/*`
- Zusatzinfo:
  - „Verwendet in: amssolution/ams-api, amssolution/ams-worker, …“
- Status:
  - z. B. „Noch nicht konfiguriert“ oder „Wird als public verwendet“

Felder innerhalb der Card:

- **Anzeigename**  
  z. B. `Docker Hub – amssolution`
- **Patterns** (Liste):
  - `amssolution/*` (Textfeld, editierbar)
  - Button: „+ weiteres Pattern hinzufügen“
- **Authentifizierung**:
  - Radiobuttons:
    - `● Authentifizierung erforderlich`
    - `○ Ohne Authentifizierung (Public Images)`
  - Wenn „Authentifizierung erforderlich“:
    - Auth-Typ: `Basic` / `Token`
    - Username
    - Passwort / Token
    - optional: Button „Verbindung testen“

**Card 2:**

- Titel: `ghcr.io – wiesenwischer/*`
- etc. (gleiche Struktur)

**Card 3:**

- Titel: `docker.io – library/*`
- Default:
  - Radiobutton „Ohne Authentifizierung“ vorausgewählt
  - Hinweis: „Wird nur für Public Images wie `redis`, `postgres` verwendet“

Cards können optional collapsible sein (auf-/zuklappbar), um bei vielen Registries die Seite übersichtlich zu halten.

#### 4.2.2 Buttons am Seitenende

Am Ende der Seite:

- `Zurück` (zum vorherigen Step)
- `Weiter` (zum nächsten Step)

Beim Klick auf `Weiter`:

1. Validierung aller Cards:
   - Falls „Authentifizierung erforderlich“ gewählt ist:
     - Pflichtfelder (z. B. Username, Token) müssen befüllt sein.
2. Speichern:
   - Für jede Card wird/werden `ContainerRegistryConfig`-Eintrag/Einträge erzeugt bzw. aktualisiert:
     - `Host`
     - `Patterns`
     - `AuthMode`
     - `Credentials`
3. Weiterleitung zum nächsten Wizard-Step (z. B. Zusammenfassung).

---

## 5. Matching-Logik (Host + Pattern) bleibt unverändert

Der technische Matching-Mechanismus ist unabhängig vom Wizard-UX-Design und sieht so aus:

1. Ein Stack-Manifest enthält ein Image, z. B.:
   - `amssolution/ams-api:0.5.0` (→ Host `docker.io`, Path `amssolution/ams-api`)
   - `docker.io/amssolution/ams-api:0.5.0`
   - `ghcr.io/wiesenwischer/ams-project-api:0.5.0`

2. RSGO parst:
   - `host` (explizit, oder default `docker.io`),
   - `path` (alles nach Host, vor `:` → z. B. `amssolution/ams-api`).

3. RSGO sucht passende `ContainerRegistryConfig`:
   - `Host == parsedHost`
   - `Patterns` matchen den `path` (z. B. `amssolution/*`).

4. Falls mehrere Patterns passen:
   - der **spezifischste Pattern** gewinnt (z. B. längster String, weniger Wildcards etc.).

5. Falls **keine** Registry-Konfiguration passt:
   - je nach Modus:
     - Entwicklung/Einfach: anonymen Pull versuchen, Warnung loggen,
     - Produktion/Streng: Fehler „Keine passende Registry konfiguriert“.

Der Wizard-Step „Container-Registries“ ist damit einfach ein Komfort-Frontend, um diese `ContainerRegistryConfig`-Einträge zu erstellen.

---

## 6. Warum kein Dialog für Auth-Eingabe?

### 6.1 UX-Gründe

- Der Stepper oben würde weiterhin „Schritt 4“ anzeigen, während ein Dialog offen ist.
- Der Benutzer verliert leichter den Überblick:
  - „Wo bin ich jetzt? Wizard-Schritt oder Dialog?“
- Die Auth-Konfiguration ist kein „Nebenbei-Feature“, sondern ein **zentrales Setup-Thema**:
  - Sie gehört in einen **vollwertigen Wizard-Schritt**,
  - nicht in ein zusätzliches Popup.

### 6.2 Technische Gründe

- Ein **einziger Form-Step** ist deutlich simpler:
  - nur eine Validierung,
  - nur ein „Weiter“-Handling.
- Keine Doppelzustände:
  - kein „Wizard aktiv + Dialog aktiv“-Chaos,
  - keine Sonderfälle:
    - „Was passiert, wenn ich im Dialog auf Cancel klicke, aber im Wizard auf Zurück?“.

### 6.3 Übersicht für den Admin

- Mit Cards auf einer Seite sieht der Admin **alle Registry-Bereiche auf einen Blick**:
  - welche privaten Namespaces es gibt (`amssolution/*`, `wiesenwischer/*`, …),
  - welche davon Auth brauchen,
  - welche anonym laufen können (`library/*`).
- Das ist deutlich transparenter, als Registry für Registry in einem Dialog-Kontext durchzuklicken.

---

## 7. Zusammenfassung der Design-Entscheidung

**Beschlossene Linie für ReadyStackGo:**

- Es gibt **einen festen Wizard-Step „Container-Registries“**.
- Auf dieser Seite:
  - zeigt RSGO alle aus den Manifesten erkannten Registry-Bereiche,
  - jeder Bereich wird als **Card** mit:
    - Host,
    - vorgegebenem Pattern (z. B. `amssolution/*`),
    - Beispielen,
    - Auth-Einstellungen
    dargestellt.
  - Der Benutzer konfiguriert alle benötigten Registries **inline**.
- Es gibt **keine separaten Dialoge** für die Auth-Eingabe im Wizard.
- Die bereits vorhandene **Host+Pattern-Matching-Logik** wird exakt weiterverwendet – der Wizard ist nur die komfortable Oberfläche dafür.

Dieses Dokument kann direkt als Referenz in `/docs/REGISTRY-WIZARD-UX.md` im ReadyStackGo-Repository verwendet werden.

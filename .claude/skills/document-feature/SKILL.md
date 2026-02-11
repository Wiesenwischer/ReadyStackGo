---
name: document-feature
description: Create E2E tests with screenshots and build step-by-step documentation for the public website
argument-hint: "<feature-name>"
---

# Feature dokumentieren mit E2E Tests und Screenshots

Erstelle E2E-Tests, Screenshots und eine Schritt-für-Schritt-Anleitung für ein Feature auf der PublicWeb-Website.

**Feature**: $ARGUMENTS

---

## Übersicht

Dieser Skill automatisiert den gesamten Dokumentations-Workflow für ein Feature:
1. E2E-Tests erstellen die Screenshots für jeden Schritt erzeugen
2. Tests im Docker-Container ausführen
3. Schritt-für-Schritt-Anleitung (DE + EN) auf der PublicWeb-Website erstellen/aktualisieren
4. Optional: Feature als Highlight auf der Landing Page aufnehmen

---

## Schritt 1: Feature identifizieren

1. Falls `$ARGUMENTS` leer ist, frage den User welches Feature dokumentiert werden soll.
2. Identifiziere die relevanten **UI-Seiten und Workflows** des Features:
   - Lies den bestehenden Code unter `src/ReadyStackGo.WebUi/src/pages/` und `src/ReadyStackGo.WebUi/src/components/`
   - Verstehe den User-Flow: Welche Schritte durchläuft ein User?
   - Identifiziere die relevanten Routes/URLs
3. Prüfe ob bereits Dokumentation existiert:
   - PublicWeb: `src/ReadyStackGo.PublicWeb/src/content/docs/de/docs/` und `en/docs/`
   - Interne Docs: `docs/`
4. Prüfe ob bereits E2E-Tests existieren: `src/ReadyStackGo.WebUi/e2e/`

## Schritt 2: Highlight-Feature abfragen

Frage den User mit `AskUserQuestion`:

```
Soll dieses Feature als Highlight auf der Landing Page aufgenommen werden?
Highlight-Features erscheinen prominent in der Feature-Übersicht auf der Startseite.

Optionen:
1. Nein, nur Dokumentation erstellen (Recommended)
2. Ja, als Highlight-Feature auf der Landing Page aufnehmen
```

Falls Highlight: Frage nach **Titel** (kurz, DE + EN), **Beschreibung** (1 Satz, DE + EN) und ob ein **Badge** (z.B. "NEW") angezeigt werden soll.

## Schritt 3: E2E-Tests erstellen

Erstelle eine neue Test-Datei unter `src/ReadyStackGo.WebUi/e2e/<feature-name>.spec.ts`.

### Muster folgen (Referenz: `e2e/cicd-settings.spec.ts`):

```typescript
import { test, expect } from '@playwright/test';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const SCREENSHOT_DIR = path.join(__dirname, '..', '..', 'ReadyStackGo.PublicWeb', 'public', 'images', 'docs');

test.describe('<Feature Name>', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'Admin1234');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard)?$/, { timeout: 10000 });
  });

  test('should <step description>', async ({ page }) => {
    // Navigation + Assertions
    await page.goto('/target-page');
    await page.waitForLoadState('networkidle');

    // Screenshot an jedem wichtigen Schritt
    await page.screenshot({
      path: path.join(SCREENSHOT_DIR, '<feature>-01-<beschreibung>.png'),
      fullPage: false
    });
  });
});
```

### Screenshot-Konventionen:
- **Pfad:** `src/ReadyStackGo.PublicWeb/public/images/docs/`
- **Namensformat:** `<feature>-<##>-<beschreibung>.png` (z.B. `cicd-01-settings-nav.png`)
- **Fortlaufende Nummerierung** für die Schritt-Reihenfolge
- **`fullPage: false`** verwenden (nur Viewport)
- Screenshots in den Tests machen, die den **Happy-Path / Hauptworkflow** abbilden

### Test-Anforderungen:
- **Nicht nur Happy-Path!** Teste auch:
  - Leere Zustände (keine Daten vorhanden)
  - Validierung (Pflichtfelder, ungültige Eingaben)
  - Cancel/Abbrechen-Flows
  - Fehlerfälle
- Nutze `getByRole()` und `getByText({ exact: true })` statt `getByText()` um Strict-Mode-Fehler zu vermeiden
- Bei Texten die als Substring in anderen Elementen vorkommen: `{ exact: true }` verwenden
- Bei Buttons in Tabellenzeilen: `row.getByRole('button', { name: '...' })` statt `row.getByText('...')`

## Schritt 4: E2E-Tests ausführen

### Container starten (falls nicht laufend):
```bash
docker compose up -d --build
```

### Wizard durchlaufen (bei frischem Container):
```bash
curl -sf -X POST http://localhost:8080/api/wizard/admin \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin1234"}'

curl -sf -X POST http://localhost:8080/api/wizard/organization \
  -H "Content-Type: application/json" \
  -d '{"id":"e2e-test-org","name":"E2E Test Organization"}'

curl -sf -X POST http://localhost:8080/api/wizard/environment \
  -H "Content-Type: application/json" \
  -d '{"name":"default","socketPath":"/var/run/docker.sock"}'

curl -sf -X POST http://localhost:8080/api/wizard/install \
  -H "Content-Type: application/json" -d '{}'
```

### Tests ausführen:

Erstelle eine temporäre Playwright-Config die auf den Container zeigt:

```typescript
// playwright.temp.config.ts (nach Tests löschen!)
import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  reporter: 'list',
  timeout: 60 * 1000,
  expect: { timeout: 10 * 1000 },
  use: {
    baseURL: 'http://localhost:8080',
    actionTimeout: 15 * 1000,
    navigationTimeout: 30 * 1000,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
```

```bash
cd src/ReadyStackGo.WebUi
npx playwright test <feature-name> --config=playwright.temp.config.ts --reporter=list
```

### Bei Fehlern:
- Locator-Fehler (strict mode) → Spezifischere Selektoren verwenden
- Timing-Fehler → `waitForLoadState('networkidle')` oder `waitForTimeout()` einfügen
- Container mit frischen Volumes neu starten wenn Test-Daten kollidieren:
  ```bash
  docker compose down -v && docker compose up -d
  ```
  Dann Wizard erneut durchlaufen.

### Nach erfolgreichen Tests:
- Prüfe dass alle Screenshots in `src/ReadyStackGo.PublicWeb/public/images/docs/` erzeugt wurden
- Lösche die temporäre Config `playwright.temp.config.ts`

## Schritt 5: Dokumentation auf PublicWeb erstellen

### Datei anlegen oder aktualisieren:
- **Deutsch:** `src/ReadyStackGo.PublicWeb/src/content/docs/de/docs/<feature-name>.md`
- **Englisch:** `src/ReadyStackGo.PublicWeb/src/content/docs/en/docs/<feature-name>.md`

### Dokumentations-Struktur (Referenz: `ci-cd-integration.md`):

```markdown
---
title: <Feature-Titel>
description: <Kurzbeschreibung>
---

<Einleitungsparagraph: Was macht das Feature, wann braucht man es?>

## Übersicht / Overview

<Tabelle oder kurze Auflistung der Use Cases>

---

## Schritt für Schritt: <Hauptworkflow> / Step by Step: <Main Workflow>

### Schritt 1: <Aktion>

<Kurze Anleitung was zu tun ist>

![<Alt-Text>](/images/docs/<feature>-01-<beschreibung>.png)

---

### Schritt 2: <Aktion>

<Anleitung mit Details>

![<Alt-Text>](/images/docs/<feature>-02-<beschreibung>.png)

:::tip[<Tipp-Titel>]
<Hilfreicher Hinweis>
:::

---

(... weitere Schritte ...)

## <Zusätzliche Sektionen je nach Feature>

<z.B. API-Parameter, Konfiguration, Sicherheitshinweise>

---

## Fehlerbehandlung / Error Handling

<HTTP-Status-Tabelle oder häufige Fehler und deren Lösung>
```

### Regeln:
- **Deutsch:** Volle Dokumentation in Deutsch mit englischen Fachbegriffen
- **Englisch:** Vollständige Übersetzung, gleiche Struktur
- **Screenshots:** Gleiche Bilder für beide Sprachen, unterschiedliche Alt-Texte
- **Starlight-Features** nutzen: `:::tip`, `:::note`, `:::caution` Callouts
- **Parameter-Tabellen** für API-Endpunkte: Feld, Typ, Pflicht/Required, Beschreibung

### Docs-Index aktualisieren:

Falls die Datei neu ist, prüfe ob sie in die Sidebar-Navigation aufgenommen werden muss:
- `src/ReadyStackGo.PublicWeb/src/content/docs/de/docs/index.md`
- `src/ReadyStackGo.PublicWeb/src/content/docs/en/docs/index.md`

## Schritt 6: Landing Page erweitern (nur bei Highlight-Feature)

**Nur wenn in Schritt 2 als Highlight bestätigt wurde!**

### 6a: Translations erweitern

Datei: `src/ReadyStackGo.PublicWeb/src/i18n/translations.ts`

Füge neue Translation Keys hinzu (in beide Sprachen):

```typescript
// DE
'features.<feature>.title': '<Titel auf Deutsch>',
'features.<feature>.desc': '<Beschreibung auf Deutsch (1 Satz)>',

// EN
'features.<feature>.title': '<Title in English>',
'features.<feature>.desc': '<Description in English (1 sentence)>',
```

### 6b: Feature-Kachel in Features.astro hinzufügen

Datei: `src/ReadyStackGo.PublicWeb/src/components/Features.astro`

Füge das Feature zum `highlightedFeatures` Array hinzu:

```typescript
{
    icon: `<SVG path data>`,
    titleKey: 'features.<feature>.title' as const,
    descKey: 'features.<feature>.desc' as const,
    badge: 'NEW',  // Optional
    link: `/${lang}/docs/<feature-name>`,
},
```

**SVG Icons:** Verwende Heroicons (https://heroicons.com/) – nur den `<path>` Tag kopieren. Stil: `outline`, 24x24.

### 6c: TypeScript-Typen prüfen

Die Translation Keys müssen im `Translations` Type in `translations.ts` vorhanden sein, sonst compiliert die Website nicht. Prüfe mit:

```bash
cd src/ReadyStackGo.PublicWeb && npm run build
```

## Schritt 7: Commit und Push

```bash
git add <alle neuen/geänderten Dateien>
git commit -m "<Aussagekräftige Commit-Message>"
git push
```

**Commit-Regeln** (siehe CLAUDE.md):
- Englische Commit-Messages
- Kein Footer (kein Co-Authored-By, kein Generated with Claude Code)

## Checkliste

Vor dem Abschluss prüfen:

- [ ] E2E-Tests laufen alle grün (gegen Container auf Port 8080)
- [ ] Screenshots wurden erzeugt in `public/images/docs/<feature>-*.png`
- [ ] Deutsche Dokumentation erstellt/aktualisiert
- [ ] Englische Dokumentation erstellt/aktualisiert (gleiche Struktur)
- [ ] Screenshots in beiden Sprach-Versionen referenziert
- [ ] Docs-Index-Seiten aktualisiert (falls neue Datei)
- [ ] (Falls Highlight) Translation Keys in DE + EN hinzugefügt
- [ ] (Falls Highlight) Feature-Kachel in Features.astro hinzugefügt
- [ ] Temporäre Playwright-Config gelöscht
- [ ] Committed und gepusht

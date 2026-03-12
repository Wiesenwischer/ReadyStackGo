# Phase: Inline Documentation (Embedded Docs in Container)

## Ziel

Die RSGO-Dokumentation (PublicWeb — Astro/Starlight) wird aktuell nur auf Cloudflare Pages gehostet. Nutzer müssen die App verlassen um Hilfe zu finden. Das Ziel ist, die gebuildete Dokumentation in das Docker-Image zu bundlen und unter `/docs/` aus dem Container heraus zu servieren — zugänglich über einen "Documentation"-Link direkt in der Sidebar.

## Analyse

### Bestehende Architektur

- **PublicWeb** (`src/ReadyStackGo.PublicWeb/`) — Astro/Starlight static site, deployed auf Cloudflare Pages
- **`UseStaticFiles()`** in `Program.cs` (lines 201–211) läuft vor dem SPA `MapFallback` → `wwwroot/docs/*` wird automatisch unter `/docs/*` serviert, ohne ASP.NET-Änderungen
- **Dockerfile** hat bereits eine Frontend-Build-Stage (Node 20 + pnpm); PublicWeb braucht eine eigene Stage
- **`DocsIcon`** existiert bereits in `src/ReadyStackGo.WebUi/packages/ui-generic/src/icons/` und wird über `index.ts` exportiert
- **Astro `base` option**: Wenn `base: '/docs'` gesetzt ist, werden alle generierten Pfade mit `/docs` geprefixed. Ohne `ASTRO_BASE` → `base: '/'` → Cloudflare-Build bleibt unverändert.

### Betroffene Bounded Contexts

- **Infrastructure / Hosting**: Dockerfile (neuer Build-Stage)
- **PublicWeb**: `astro.config.mjs` (base path)
- **WebUI**: `AppSidebar.tsx` (neuer Nav-Link)

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: Astro base path** — `ASTRO_BASE` env var in `astro.config.mjs` einlesen; `base` an `defineConfig` übergeben; inline script für `siteTitle.href` anpassen
  - Betroffene Dateien: `src/ReadyStackGo.PublicWeb/astro.config.mjs`
  - Abhängig von: -
- [ ] **Feature 2: Dockerfile publicweb-build Stage** — neue Stage zwischen frontend-build und backend-build; `ASTRO_BASE=/docs npm run build`; `dist/` nach `wwwroot/docs` kopieren
  - Betroffene Dateien: `Dockerfile`
  - Abhängig von: Feature 1
- [ ] **Feature 3: Sidebar Documentation Link** — `DocsIcon` importieren; `externalHref` zu `NavItem` Type hinzufügen; Documentation-Eintrag in `configItems`; `<a href="/docs/">` (kein `target="_blank"`, kein React Router `<Link>`)
  - Betroffene Dateien: `src/ReadyStackGo.WebUi/packages/ui-generic/src/layout/AppSidebar.tsx`
  - Abhängig von: Feature 2

## Test-Strategie

- **Manuell**: `docker compose build && docker compose up -d` → Sidebar zeigt "Documentation" → Klick navigiert zu `/docs/` mit Starlight-Site → interne Doc-Links funktionieren
- **Regression**: Lokaler `npm run build` in PublicWeb ohne `ASTRO_BASE` → Cloudflare-kompatibler Output

## Offene Punkte

- [ ] Prüfen ob PublicWeb npm oder pnpm verwendet (package-lock.json vs. pnpm-lock.yaml) → Dockerfile-Stage entsprechend anpassen

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Docs-Link Navigation | Gleicher Tab / Neuer Tab | Gleicher Tab | Docs sind in der App eingebettet, Back-Button führt zurück |
| Link-Typ in Sidebar | `<Link>` (React Router) / `<a>` | `<a>` | `/docs/` wird von ASP.NET static files serviert, nicht React Router — plain navigation nötig |
| Astro base env var | Hardcoded `/docs` / Env var | Env var (`ASTRO_BASE`) | Cloudflare Pages build bleibt unverändert (kein `ASTRO_BASE` → `base: '/'`) |

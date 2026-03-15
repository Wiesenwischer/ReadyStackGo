# Phase: Documentation Link in Sidebar

## Ziel

Nutzer sollen die RSGO-Dokumentation direkt aus der App heraus aufrufen können — über einen "Documentation"-Link in der Sidebar, der auf die Online-Docs unter `readystackgo.pages.dev` verweist.

## Analyse

### Bestehende Architektur

- **PublicWeb** (`src/ReadyStackGo.PublicWeb/`) — Astro/Starlight static site, deployed auf Cloudflare Pages (`readystackgo.pages.dev`)
- **`DocsIcon`** existiert bereits in `src/ReadyStackGo.WebUi/packages/ui-generic/src/icons/` und wird über `index.ts` exportiert
- **`AppSidebar.tsx`** hat zwei Item-Listen: `operationalItems` (requiresEnvironment) und `configItems`; alle Links nutzen React Router `<Link>` — für externe URLs muss `<a>` verwendet werden

### Betroffene Bounded Contexts

- **WebUI (rsgo-generic)**: `AppSidebar.tsx` (neuer Nav-Link)

## AMS UI Counterpart

- [x] **Ja** — AMS UI Sidebar (`AppSidebar` equivalent in ConsistentUI) muss ebenfalls einen "Documentation"-Link erhalten
  - AMS Repo: Entsprechende Sidebar-Komponente anpassen
  - Zeitpunkt: Separater Epic im AMS Repo (nach rsgo-generic)

## Features / Schritte

- [ ] **Sidebar Documentation Link** — `DocsIcon` importieren; `externalHref?: string` zu `NavItem` Type hinzufügen; Documentation-Eintrag in `configItems`; `<a href="https://readystackgo.pages.dev/" target="_blank">` rendern
  - Betroffene Dateien: `src/ReadyStackGo.WebUi/packages/ui-generic/src/layout/AppSidebar.tsx`

## Test-Strategie

- **Manuell**: Sidebar zeigt "Documentation"-Link → Klick öffnet `readystackgo.pages.dev` in neuem Tab

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Link-Ziel | Inline (embedded) / Online-Docs | Online-Docs (readystackgo.pages.dev) | Einfachste Lösung, immer aktuell |
| Navigation | Gleicher Tab / Neuer Tab | Neuer Tab (`target="_blank"`) | Externe URL — User bleibt in der App |
| Link-Typ | `<Link>` (React Router) / `<a>` | `<a>` | Externe URL, kein SPA-Routing nötig |

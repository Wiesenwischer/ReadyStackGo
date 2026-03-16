<!-- GitHub Epic: #313 -->
# Phase: Stack Catalog — Multi-Source & Version Selection

## Ziel

Produkte aus verschiedenen Stack Sources sollen **separat** im Stack Catalog angezeigt werden, auch wenn sie dasselbe Produkt definieren (gleicher Name/GroupId). Innerhalb einer Source soll ein **Version-Picker** die Auswahl zwischen verfügbaren Versionen ermöglichen. Kein Cross-Source Version-Picker.

## Analyse

### Problem (Ist-Zustand)

- `InMemoryProductCache` gruppiert Produkte nach `GroupId` (äußerer Dictionary-Key)
- Wenn zwei Sources dasselbe Produkt mit gleicher expliziter `productId` definieren, landen beide im selben Group-Dictionary
- `GetAllProducts()` gibt nur die **neueste Version pro GroupId** zurück — Produkte aus der "älteren" Source verschwinden
- UI zeigt nur ein Produkt statt zwei, kein Version-Picker vorhanden

### Gewünschtes Verhalten

```
Stack Catalog:
┌──────────────────────────┐  ┌──────────────────────────┐
│ ams.project              │  │ ams.project              │
│ Source: production       │  │ Source: staging           │
│ Version: 2.0.0           │  │ Version: 1.5.0           │
│ [v2.0.0 ▼]              │  │ [v1.5.0 ▼]  ← Picker    │
│                          │  │  v1.5.0                  │
│ [View Details]           │  │  v1.4.0                  │
└──────────────────────────┘  └──────────────────────────┘
```

### Betroffene Architektur

**Cache (`InMemoryProductCache`):**
- `GetAllProducts()` muss latest per (SourceId, GroupId) statt per GroupId zurückgeben
- Neue Methode: `GetProductVersionsBySource(sourceId, groupId)` für Source-scoped Versionen

**API (`ListProductsHandler`):**
- Muss verfügbare Versionen (Source-scoped) pro Produkt mitgeben

**Frontend:**
- Stack Catalog: Produkte sind jetzt Source-scoped, Card zeigt Source-Badge und Version
- ProductDetail: Version-Dropdown zum Umschalten
- DeployProduct: Nimmt die ausgewählte Version, nicht immer "latest"

### Pattern-Vorbilder

- `InMemoryProductCache.GetProductVersions(groupId)` — bestehende Versions-Abfrage (aber nicht Source-scoped)
- `ListProductsHandler` — bestehende Produkt-Auflistung
- `StackCatalog.tsx` / `ProductDetail.tsx` — bestehende UI-Komponenten

## AMS UI Counterpart

- [x] **Ja (deferred)** — AMS-Counterpart wird später geplant (AMS Catalog Page muss Version-Picker ebenfalls bekommen)

## Features / Schritte

- [ ] **Feature 1: Cache — Source-scoped Product Retrieval** – `GetAllProducts()` gibt latest per (SourceId, GroupId) zurück
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure/Caching/InMemoryProductCache.cs`
    - `src/ReadyStackGo.Application/Services/IProductCache.cs`
  - Neue Methode: `GetProductVersionsBySource(string sourceId, string groupId)`
  - `GetAllProducts()` Logik ändern: Gruppierung nach `(SourceId, GroupId)` statt nur `GroupId`
  - Abhängig von: -

- [ ] **Feature 2: API — Verfügbare Versionen in Product Response** – Version-Liste pro Source mitliefern
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/Stacks/ListProducts/ListProductsHandler.cs`
    - `src/ReadyStackGo.Application/UseCases/Stacks/ListProducts/ListProductsQuery.cs` (DTOs)
    - `src/ReadyStackGo.WebUi/packages/core/src/api/stacks.ts` (Frontend-Types)
  - `ProductItem` DTO erweitern: `AvailableVersions: List<VersionInfo>` (Version, ProductId)
  - Pattern: `GetProductVersionsBySource(sourceId, groupId)` aufrufen
  - Abhängig von: Feature 1

- [ ] **Feature 3: Frontend — Stack Catalog Source-separiert** – Produkte pro Source anzeigen
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Catalog/StackCatalog.tsx`
    - `src/ReadyStackGo.WebUi/packages/core/src/hooks/useCatalogStore.ts`
  - Product Cards zeigen Source-Name prominent
  - Gleiches Produkt aus 2 Sources = 2 Cards
  - Abhängig von: Feature 2

- [ ] **Feature 4: Frontend — Version-Picker im ProductDetail** – Version auswählen und Detail-Seite aktualisieren
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Catalog/ProductDetail.tsx`
    - `src/ReadyStackGo.WebUi/packages/core/src/hooks/useProductDetailStore.ts`
  - Dropdown mit verfügbaren Versionen (aus `availableVersions`)
  - Version-Wechsel lädt Produkt-Details für gewählte Version
  - Deploy-Button nutzt gewählte Version
  - Abhängig von: Feature 2

- [ ] **Feature 5: Tests** – Unit Tests für Cache-Änderungen
  - Betroffene Dateien:
    - `tests/ReadyStackGo.UnitTests/Infrastructure/Caching/InMemoryProductCacheTests.cs` (erweitern oder neu)
  - Testfälle:
    - Gleiches Produkt aus 2 Sources → 2 Einträge in GetAllProducts()
    - Gleiches Produkt, gleiche Source, 2 Versionen → 1 Eintrag (latest), aber beide in GetProductVersionsBySource()
    - GetProductVersionsBySource filtert korrekt nach SourceId
  - Abhängig von: Feature 1

- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

## Test-Strategie

- **Unit Tests**: Cache-Gruppierung (Source-scoped), Version-Sortierung, GetProductVersionsBySource
- **E2E Tests**: Zwei Sources mit gleichem Produkt → beide im Catalog sichtbar, Version-Picker funktioniert

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Cache-Gruppierung | A: Outer Key ändern zu SourceId:GroupId, B: GetAllProducts() Logik ändern | B | Weniger invasiv, bestehende Upgrade-Logik (per GroupId) bleibt unberührt |
| Version-Picker Scope | A: Cross-Source, B: Source-scoped | B | User-Entscheidung: Versionen nur innerhalb einer Source wählbar |
| Version-Picker Position | A: Im Catalog Card, B: Nur in ProductDetail | B | Catalog Cards sollen übersichtlich bleiben, Detail-Seite hat Platz |

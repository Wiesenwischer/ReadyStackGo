# Implementierungsplan: Produkt-Detail-Seite und Multi-Stack Deployment

## Ziel
Produkte (nicht einzelne Stacks) als primäre Einheit in der UI anzeigen. Multi-Stack Produkte zeigen ihre Sub-Stacks, die einzeln oder zusammen deployed werden können.

## Architektur-Entscheidung

### Option A: StackDefinition erweitern (gewählt)
- `StackDefinition` erhält `ProductName` und `ProductDisplayName` Properties
- Multi-Stack Stacks haben den Produkt-Namen gesetzt
- Single-Stack Stacks: ProductName = StackName
- `GroupStacksIntoProducts()` gruppiert nach `ProductName`

**Vorteile:**
- Minimale Änderungen am bestehenden Code
- Stacks bleiben die primäre Cache-Einheit
- Rückwärtskompatibel

## Änderungen

### 1. Domain Layer

#### StackDefinition.cs
```csharp
// Neue Properties hinzufügen:
public string? ProductName { get; }           // Name des Parent-Produkts (für Multi-Stack)
public string? ProductDisplayName { get; }    // Anzeigename des Produkts
public string? ProductDescription { get; }    // Beschreibung des Produkts
public string? ProductVersion { get; }        // Version des Produkts
public string? Category { get; }              // Kategorie (Database, Web, etc.)
public IReadOnlyList<string> Tags { get; }    // Tags für Suche/Filter

// Konstruktor erweitern
```

### 2. Infrastructure Layer

#### LocalDirectoryStackSourceProvider.cs
- `CreateStackDefinitionsFromMultiStack()`: Produkt-Info an jeden Sub-Stack übergeben
- `LoadStacksFromFolderAsync()`: Für Single-Stack Manifeste ProductName = StackName setzen

#### StackSourceService.cs
- `GroupStacksIntoProducts()` komplett neu schreiben:
  - Gruppiere Stacks nach `ProductName`
  - Erstelle ein `ProductDefinition` pro Gruppe
  - Nutze Produkt-Metadaten vom ersten Stack der Gruppe

### 3. API Layer

#### Neuer Endpoint: GetProductEndpoint.cs
- `GET /api/products/{productId}` - Gibt Produkt-Details mit allen Stacks zurück

### 4. Frontend

#### api/stacks.ts
- `getProduct(productId)` Funktion hinzufügen

#### pages/ProductDetail.tsx (Neue Seite)
- Zeigt Produkt-Header (Name, Description, Version, Category)
- Listet alle Stacks mit Services/Variables
- "Deploy" Button pro Stack
- "Deploy All" Button für gesamtes Produkt

#### pages/Stacks.tsx
- ProductCard: Multi-Stack Produkte → Link zu Detail-Seite statt Expand
- Oder: Modal für Produkt-Details (einfacher, kein Routing nötig)

#### App.tsx
- Route `/stacks/:productId` für ProductDetail hinzufügen

### 5. Komponenten

#### ProductDetailModal.tsx (Alternative zu eigener Seite)
- Modal das Produkt-Details zeigt
- Stacks-Liste mit Deploy-Buttons
- Einfacher als neue Route

## Implementierungs-Reihenfolge

1. **StackDefinition erweitern** - Neue Properties + Konstruktor
2. **LocalDirectoryStackSourceProvider anpassen** - Produkt-Info setzen
3. **GroupStacksIntoProducts neu schreiben** - Korrekte Gruppierung
4. **Backend testen** - Logs prüfen, API-Response validieren
5. **Frontend ProductDetailModal** - Modal für Multi-Stack Produkte
6. **Stacks.tsx anpassen** - Modal öffnen statt Expand

## Test-Szenario

Nach Implementation:
- WordPress (Single-Stack): 1 Produkt mit 1 Stack, direkter Deploy-Button
- IdentityAccess (Multi-Stack): 1 Produkt mit 1 Stack (identity), "Details" öffnet Modal
- Modal zeigt Stack mit 4 Services, Deploy-Button

## Offene Fragen

1. **Deploy All**: Wie soll "Deploy All" funktionieren?
   - Sequentiell jeden Stack deployen?
   - Paralleles Deployment?
   - Ein kombiniertes docker-compose.yml generieren?

2. **UI-Entscheidung**: Modal oder eigene Seite für Produkt-Details?
   - Modal: Einfacher, schneller implementiert
   - Seite: Mehr Platz, bessere UX für komplexe Produkte

## Empfehlung

**Phase 1 (jetzt):**
- Modal für Produkt-Details
- Einzelne Stack-Deployments funktionieren

**Phase 2 (später):**
- "Deploy All" mit kombiniertem Compose
- Eigene Produkt-Detail-Seite wenn nötig

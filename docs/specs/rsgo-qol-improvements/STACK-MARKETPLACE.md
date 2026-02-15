# ReadyStackGo – Stack Marketplace Specification

## 1. Ziel & Überblick

Dieses Dokument beschreibt den **Stack Marketplace** für ReadyStackGo (RSGO).

Der Marketplace ist eine **UX-Schicht über den bestehenden Mechanismen**:

- Manifest-Quellen (Stack Sources / Manifest Registries)
- Container-Registries

Ziel:

- Administratoren können **Stacks wie in einem App-Store entdecken**, Details ansehen und pro Organisation/Environment installieren.
- Technisch bleiben Manifeste, Stack Sources und Container-Registries weiterhin die Grundlage – der Marketplace referenziert diese nur.

Use Cases:

- Offizielle Stacks bereitstellen (z. B. `ams.project`, IdentityAccess, Monitoring, Demo).
- Interne Stacks für einzelne Kunden/Organisationen verfügbar machen.
- Zukünftig optionale 3rd-Party-Stacks integrieren.

---

## 2. Begriffe & Domänenmodell

### 2.1 Bereits existierende Konzepte (RSGO)

- **Manifest Source / Stack Source**  
  Quelle für Stack-Manifeste (z. B. Git-Repo, HTTP, lokaler Pfad).  
  In den Settings: „Stack Sources“.

- **Container Registry Config**  
  Beschreibung einer Container-Registry inkl. Host, Patterns (z. B. `amssolution/*`) und Auth-Daten.  
  In den Settings: „Container Registries“.

- **Stack Manifest**  
  RSGO-spezifisches Manifest, das beschreibt:
  - Services, Migrations, Volumes etc.
  - Parameter, Secrets,
  - Policies (Start-Reihenfolge, Dependencies, …).

- **Stack Installation** (bereits geplant/teils vorhanden)  
  „Konkreter Stack“ installiert für eine Organisation + Environment:
  - welches Manifest,
  - welche Version,
  - Status (Running, Stopped, Failed, Upgrading),
  - Healthinformationen etc.

### 2.2 Neue Konzepte für den Marketplace

#### 2.2.1 StackCatalogSource

Repräsentiert eine **Quelle** für Marketplace-Einträge.

Beispiele:

- Eingebauter JSON-Katalog aus der RSGO-Distribution
- JSON-Datei in einem Git-Repo (Organisation erstellt eigenen Katalog)
- HTTP-Endpunkt, der Katalogeinträge liefert (für später evtl. „öffentlichen“ Marketplace)

Eigenschaften (Domänenmodell):

```csharp
public sealed class StackCatalogSource
{
    public string Id { get; init; }              // interne ID (z. B. "rsgo-official")
    public string Name { get; init; }            // Anzeigename ("ReadyStackGo Official Stacks")
    public string Type { get; init; }            // "embedded", "git-json", "http-json", ...
    public string? Location { get; init; }       // URL, Pfad, Repo-URL je nach Type
    public bool IsEnabled { get; set; }

    // Optional: scope / visibility
    public string Scope { get; init; }           // z. B. "System", "Org:<OrgId>"
}
```

#### 2.2.2 StackCatalogEntry

Repräsentiert **einen Eintrag** im Marketplace („ein installierbarer Stack“).

Eigenschaften:

```csharp
public sealed class StackCatalogEntry
{
    public string Id { get; init; }                  // Katalog-weiter eindeutiger Schlüssel
    public string SourceId { get; init; }            // Referenz auf StackCatalogSource.Id

    public string Name { get; init; }                // z. B. "ams.project Core Stack"
    public string Slug { get; init; }                // URL-freundlich (z. B. "ams-project-core")

    public string? ShortDescription { get; init; }   // Kurzbeschreibung für Kachel
    public string? LongDescription { get; init; }    // Detailbeschreibung für Detailseite (Markdown?)

    public string Category { get; init; }            // z. B. "Business", "Infrastructure", "Monitoring", "Demo"
    public IReadOnlyList<string> Tags { get; init; } // z. B. ["ams", "nservicebus", "project", "identity"]

    public string? LogoUrl { get; init; }            // Icon/Logo für UI
    public string? BannerUrl { get; init; }          // Optionales Banner (Detailseite)

    // Verweis auf das eigentliche Stack-Manifest
    public string ManifestSourceId { get; init; }    // ID der Manifest-Quelle
    public string ManifestPath { get; init; }        // Pfad/Key zum Manifest innerhalb der Quelle

    // Optionale Hinweise zu benötigten Container-Registries
    public IReadOnlyList<RequiredRegistryHint> RequiredRegistries { get; init; }

    // Anforderungen / Hinweise
    public IReadOnlyList<string> Requirements { get; init; }   // "Requires ams.erp database connection", ...

    // Versionierung (optional, erste Ausbaustufe kann mit einfachem String arbeiten)
    public string? Version { get; init; }             // z. B. "0.5.0"
    public string? MinRsgoVersion { get; init; }      // z. B. "0.3.0"
    public string? MaxRsgoVersion { get; init; }      // optional

    // Mögliche Flags für UX
    public bool IsFeatured { get; init; }             // "auf der ersten Seite hervorheben"
    public bool IsDeprecated { get; init; }           // "nicht mehr empfehlen"
}

public sealed class RequiredRegistryHint
{
    public string Host { get; init; }                 // z. B. "docker.io", "ghcr.io"
    public IReadOnlyList<string> Patterns { get; init; } // z. B. ["amssolution/*", "wiesenwischer/*"]
    public bool AuthRequired { get; init; }           // Hinweis, ob Auth sehr wahrscheinlich ist
    public string? Description { get; init; }         // Hinweistext für UI
}
```

---

## 3. Katalogformat (JSON) – Beispiel

Ein `StackCatalogSource` kann z. B. auf eine JSON-Datei zeigen, die eine Liste von `StackCatalogEntry` beschreibt.

Beispiel `rsgo-official-catalog.json`:

```json
{
  "version": 1,
  "entries": [
    {
      "id": "ams-project-core",
      "name": "ams.project Core Stack",
      "slug": "ams-project-core",
      "shortDescription": "Zentraler Fach-Stack von ams.project (API, BFF, Worker, WebFrontends).",
      "longDescription": "Dieser Stack enthält die Kernkomponenten von ams.project ...",
      "category": "Business",
      "tags": ["ams", "project", "nservicebus", "business"],

      "logoUrl": "https://example.com/logos/ams-project.png",
      "bannerUrl": null,

      "manifestSourceId": "ams-manifests-git",
      "manifestPath": "stacks/ams/ams-project-core.yaml",

      "requiredRegistries": [
        {
          "host": "docker.io",
          "patterns": ["amssolution/*"],
          "authRequired": true,
          "description": "Private Docker-Hub-Images im Namespace 'amssolution'."
        },
        {
          "host": "docker.io",
          "patterns": ["library/*"],
          "authRequired": false,
          "description": "Public Base-Images (redis, postgres, ...)."
        }
      ],

      "requirements": [
        "Requires ams.erp database connection",
        "Requires NServiceBus transport configuration",
        "Recommended: separate 'IdentityAccess' stack installed first"
      ],

      "version": "0.5.0",
      "minRsgoVersion": "0.3.0",
      "maxRsgoVersion": null,
      "isFeatured": true,
      "isDeprecated": false
    }
  ]
}
```

---

## 4. UI & UX – Marketplace-Ansichten

### 4.1 Hauptnavigation

Im UI gibt es für Administratoren einen Bereich, z. B.:

- **Stacks**
  - Tab „Installiert“ (installierte Stack-Instanzen)
  - Tab „Marketplace“ (Katalog aller verfügbaren Stacks)

Die bestehenden Settings-Kacheln bleiben erhalten:

- **Settings → Stack Sources** (Manifest sources)
- **Settings → Container Registries** (Image-Quellen)

Der Marketplace ist eine **Arbeitsansicht**, keine Settings-Seite.

### 4.2 Marketplace-Übersicht („Kacheln“)

Auf der Seite „Stacks → Marketplace“:

- **Suchfeld**:
  - Suche nach Name, Tags, Kategorie.
- **Filter**:
  - Kategorie (Business, Infrastructure, Monitoring, Demo, …)
  - Quelle (Offizielle Stacks, Organisationseigene, 3rd Party)
  - Status (Installiert, Nicht installiert, Update verfügbar – optional später)

- **Kacheln pro StackCatalogEntry**:

  Anzeige z. B.:

  - Logo/Icon
  - Name
  - Kurzbeschreibung
  - Kategorie-Label
  - Tags (kleine Chips)
  - Status:
    - „Nicht installiert“
    - „Installiert (Org X, Env Y)“
    - „Update verfügbar“ (falls später Versionierung implementiert wird)
  - Buttons:
    - `Details anzeigen`
    - optional Direkt-Button `Installieren` (öffnet Install-Flow).

### 4.3 Detail-Ansicht eines Stacks

Beim Klick auf `Details anzeigen`:

- Banner/Logo.
- Name, Kategorie, Tags.
- Info „Bereitgestellt von: Wiesenwischer / Official / Third-Party XYZ“.
- Langbeschreibung (Markdown gerendert).
- Technischer Abschnitt:
  - „Besteht aus: API, BFF, Worker, Web-Frontend, Migrations-Container, Monitoring-Container …“
  - Verweis auf das Manifest: `ManifestSourceId + ManifestPath` (Link „Manifest anzeigen“).

- **Registries-Abschnitt**:
  - Auflistung von `requiredRegistries`:
    - z. B. `docker.io – amssolution/*` (Status: Registry konfiguriert / nicht konfiguriert)
  - Bei fehlender Registry:
    - CTA: „Container-Registry konfigurieren“ (Link auf Settings → Container Registries mit Filter).

- Button:
  - `Installieren` oder `Für Organisation installieren`.

---

## 5. Installation eines Stacks aus dem Marketplace

### 5.1 Auswahl von Organisation & Environment

Beim Klick auf `Installieren`:

- Seite oder Modal:

  - **Organisation** wählen.
  - **Environment** wählen (`Dev`, `Test`, `Prod`, …).
  - Optional: „Stackinstanz-Name“ (falls mehrere Instanzen pro Org erlaubt sind).

- Backend erzeugt eine neue `StackInstallation`:

```csharp
public sealed class StackInstallation
{
    public Guid Id { get; init; }

    public string OrgId { get; init; }
    public string EnvironmentId { get; init; }

    public string CatalogEntryId { get; init; }       // Referenz auf StackCatalogEntry
    public string ManifestSourceId { get; init; }
    public string ManifestPath { get; init; }

    public string? InstalledVersion { get; set; }     // aus CatalogEntry.Version
    public StackInstallationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastUpdatedAt { get; set; }
}
```

### 5.2 Übergang in den bestehenden Deployment-Flow

Danach läuft der bekannte Deployment-Flow:

1. **Parameter & Secrets**:
   - Ermittlung aus Manifest.
   - Eingabemaske (inkl. `.env`-Import, falls vorhanden).

2. **Registry-Check anhand `requiredRegistries`**:
   - Für jede `RequiredRegistryHint`:
     - Prüfen, ob eine passende `ContainerRegistryConfig` existiert.
   - Wenn `AuthRequired == true` und keine passende Config:
     - Installation blockieren mit klarer Meldung:
       - „Für diesen Stack wird eine Registry für `docker.io/amssolution/*` benötigt. Bitte konfigurieren Sie eine entsprechende Registry.“
   - Wenn `AuthRequired == false` und keine passende Config:
     - anonym versuchen, aber Warnung loggen.

3. **Deployment-Plan erstellen und ausführen**:
   - Analog zu manueller Manifest-Installation:
     - Migrations-Container als Jobs,
     - Services mit Restart-Policies,
     - Health/Status-Überwachung.

4. **Status aktualisieren**:
   - `StackInstallation.Status` je nach Ergebnis (`Deploying`, `Running`, `Failed`, …).

---

## 6. Zusammenspiel mit Stack Sources & Container Registries

### 6.1 Stack Sources

- `StackCatalogEntry.ManifestSourceId` muss auf eine existierende Manifest-Quelle zeigen.
- Beim Laden eines Katalogs:
  - Wenn die Manifest-Quelle nicht existiert:
    - Option A: Warnung, dass dieser Katalog-Eintrag (noch) nicht installierbar ist.
    - Option B: automatisches Anlegen der Manifest-Quelle anhand von Metadaten der `StackCatalogSource`.

Damit bleiben:

- Stack Sources = **technische Konfiguration der Manifestquellen**,
- Marketplace = **UX-Schicht, die diese Quellen konsumiert**.

### 6.2 Container Registries

- `RequiredRegistryHint` sind nur „Hints“, keine harte Kopplung.
- Der Deployment-Prozess nutzt weiterhin das bestehende Matching:
  - Host + Pattern → passende `ContainerRegistryConfig`.
- Der Marketplace nutzt Hints nur für:
  - Vorwarnungen in der UI,
  - „Fehlende Registry“-Hinweise,
  - bessere Diagnose bei fehlgeschlagenem Deployment.

---

## 7. Security & RBAC

### 7.1 Rollen & Rechte

Empfehlung:

- **SystemOwner / OrgAdmin**:
  - Marketplace sehen,
  - Stacks installieren/deinstallieren,
  - eigene `StackCatalogSource`s anlegen (org-bezogen).

- **Operator**:
  - Installierte Stacks sehen,
  - Start/Stop/Restart im Rahmen seiner Rechte,
  - optional: Marketplace lesen, aber nicht installieren.

- **Read-Only / Beobachter**:
  - nur installierte Stacks einsehen,
  - Marketplace ggf. ausgeblendet.

### 7.2 Sichtbarkeit von Catalog Sources

- `StackCatalogSource.Scope`:
  - `System` → für alle Organisationen sichtbar.
  - `Org:<OrgId>` → nur für spezifische Organisation.

Der Marketplace filtert die sichtbaren `StackCatalogEntry` anhand des Scopes und der aktuellen Benutzer-/Org-Kontexte.

---

## 8. Roadmap-Vorschlag für die Implementierung

### Phase 1 – Interner Marketplace

- Eine eingebaute `StackCatalogSource`:
  - Typ `embedded` oder `git-json` (z. B. eigenes Repo mit `stack-catalog.json`).
- Ziel:
  - Erste Version mit:
    - ams.project-Core-Stack,
    - IdentityAccess-Stack,
    - Monitoring-Stack,
    - ggf. Demo-Stack.
- Kein Schreibzugriff (nur Lesen), kein Upload/Publishing.

### Phase 2 – Organisationseigene Kataloge

- Zusätzliche `StackCatalogSource`-Types:
  - `git-json`:
    - Jede Organisation kann in ihren Settings ein Repo hinterlegen, in dem ein Katalog liegt.
- Der Marketplace zeigt:
  - Offizielle Stacks,
  - plus Org-spezifische Stacks für die aktuelle Organisation.

### Phase 3 – Erweiterter Marketplace

- Optionale, zentrale HTTP-basierte Katalog-API (z. B. „RSGO Hub“).
- Features:
  - automatische Updates von Katalogeinträgen,
  - optional: Abhängigkeiten, Kompatibilitätsmatrix, Supportinfos.

---

## 9. Zusammenfassung

Der geplante **Stack Marketplace** für ReadyStackGo:

- Ergänzt dein bestehendes Konzept von:
  - Stack Sources (Manifeste),
  - Container Registries (Images),
  - Stack Installations (konkrete Deployments).
- Führt zwei neue Kerntypen ein:
  - `StackCatalogSource` – Quelle für Marketplace-Einträge,
  - `StackCatalogEntry` – „App Store“-Eintrag für einen Stack.
- Bietet Administratoren:
  - eine übersichtliche „App Store“-ähnliche Oberfläche,
  - Detailansichten je Stack,
  - direkten Install-Flow für Organisation & Environment.
- Bleibt strikt kompatibel mit:
  - deinem Manifest-Format,
  - deiner Registry-/Pattern-Logik,
  - deinem Deployment-/Health-Modell.

Diese Spezifikation kann als `STACK-MARKETPLACE.md` im `/docs`-Verzeichnis deines ReadyStackGo-Repositories abgelegt und als Grundlage für die Implementierung in Backend (.NET 9/FastEndpoints) und Frontend (React + Tailwind/TailAdmin) verwendet werden.

# ReadyStackGo – OCI Stack Sources & Bundles

Dieses Dokument fasst alles rund um **OCI-basierte Stack-Verteilung** für ReadyStackGo (RSGO) zusammen – so, dass Claude das direkt als Grundlage für Implementierung nutzen kann.

Es beschreibt:

- wie **OCI Stack Bundles** aussehen (Bundle-Format),
- wie eine **`OciStackSource`** funktioniert (inkl. `Sync()`),
- wie das Ganze mit `ImportStackSource`, Marketplace und Deployment zusammenspielt,
- und wie Versionierung über Tags in der Registry läuft.

---

## 1. Überblick

Ziel:

- Stacks (z. B. `ams.project`) sollen als **versionierte Artefakte in einer Container-Registry** (Docker Hub, GHCR, …) veröffentlicht werden.
- ReadyStackGo soll diese Stacks:
  - im **Katalog** anzeigen (Marketplace / Stack-Liste),
  - bei Bedarf **importieren** (Snapshot),
  - und daraus Deployments erstellen.
- Die eigentlichen **Container-Images** werden weiterhin als normale Images in der Registry verwaltet (mit Tags & Digests) – **sie werden nicht ins Bundle eingebettet**, sondern per Digest referenziert.

Kernkonzepte:

- **OCI Stack Bundle** – Bündel aus `stack.yaml` + `lock.json` + optionalen Metadaten.
- **OciStackSource** – `StackSource`-Implementierung, die ein Registry-Repo durchsucht.
- **ImportStackSource** – verwaltet lokal importierte (geschnappschusste) Stack-Manifeste.
- **Sync()** – baut aus der Registry einen internen Stack-Katalog (welche Stacks, welche Versionen).

---

## 2. StackSource-Typen (Erweiterung)

Bereits vorhanden (konzeptionell):

- `FileSystemStackSource`
- `GitStackSource`
- ggf. `HttpStackSource`

Neu:

- `ImportStackSource`
- `OciStackSource`

### 2.1 `ImportStackSource`

- Zweck: verwaltet **lokale Snapshots** von Stack-Manifesten.
- Dient als **einheitliche Quelle** für alle importierten Stacks, egal woher sie kommen:
  - aus Git,
  - aus Filesystem,
  - aus OCI (Stack-Bundle),
  - über Upload im UI.

Domänenmodell (vereinfacht):

```csharp
public sealed class ImportedStackManifest
{
    public Guid Id { get; init; }

    public string StackId { get; init; }            // z.B. "ams-project-core"
    public string Version { get; init; }           // z.B. "1.0.0"

    public string SourceType { get; init; }        // "Git", "FileSystem", "OCI", "Upload"
    public string? SourceReference { get; init; }  // z.B. OCI-Ref oder Git-URL
    public DateTimeOffset ImportedAt { get; init; }

    public string LocalPathOrKey { get; init; }    // Pfad oder Key zum gespeicherten Manifest
    public string? LockDataPathOrKey { get; init; } // falls `lock.json` gespeichert wird
}
```

**Wichtig:**

- Nach dem Import arbeitet RSGO **nur noch** mit den Daten aus `ImportStackSource`.
- Die ursprüngliche Quelle (Git/OCI/etc.) wird nur für neue Versionen / Re-Imports genutzt.

---

### 2.2 `OciStackSource`

Neue `StackSource`-Implementierung, die eine Registry als Quelle nutzt.

Konfiguration (Beispiel):

```csharp
public sealed class OciStackSourceConfig
{
    public string Id { get; init; }                // "oci-wiesenwischer"
    public string RegistryHost { get; init; }      // "docker.io"
    public string Repository { get; init; }        // "wiesenwischer/rsgo-stacks"
    public string TagPattern { get; init; }        // "ams-project-core-*"
    public bool IsEnabled { get; set; }
}
```

Aufgabe:

- `Sync()` verbindet sich mit der Registry,
- listet Tags,
- leitet daraus StackId + Version ab,
- liest leichte Metadaten (Labels/Meta-Datei),
- aktualisiert den **Stack-Katalog**.

---

## 3. OCI Stack Bundle – Format

Ein **OCI Stack Bundle** ist das Artefakt, das in der Registry liegt.

**Wichtig:** Es enthält:

- `stack.yaml` – RSGO-Stack-Manifest,
- `lock.json` – Liste aller benötigten Container-Images mit Digests,
- optional `readme.md`, `meta.json`, `schema.json`.

Es enthält **nicht** die eigentlichen Images (die liegen separat als normale Container-Images).

### 3.1 Dateistruktur

Empfohlene Struktur:

```text
/stack.yaml               # Pflicht – RSGO Stack Manifest
/lock.json                # Pflicht – Lock-Datei mit Image-Infos
/readme.md                # Optional – Beschreibung
/schema.json              # Optional – JSON-Schema für stack.yaml
/meta.json                # Optional – weitere Metadaten
```

#### `stack.yaml` (Beispiel, vereinfacht)

```yaml
apiVersion: rsgo.stack/v1
kind: Stack
metadata:
  name: ams-project-core
  version: 1.0.0
spec:
  services:
    - name: ams-api
      image: docker.io/amssolution/ams-api:1.0.0
    - name: ams-bff
      image: docker.io/amssolution/ams-bff:1.0.0
  # ...
```

#### `lock.json` (Beispiel)

```json
{
  "apiVersion": "rsgo.stack.lock/v1",
  "stackName": "ams-project-core",
  "stackVersion": "1.0.0",
  "images": [
    {
      "name": "ams-api",
      "image": "docker.io/amssolution/ams-api",
      "tag": "1.0.0",
      "digest": "sha256:abc123...",
      "role": "api"
    },
    {
      "name": "ams-bff",
      "image": "docker.io/amssolution/ams-bff",
      "tag": "1.0.0",
      "digest": "sha256:def456...",
      "role": "bff"
    }
  ]
}
```

Regel:

- Beim Deployment nach Möglichkeit **immer Digest aus `lock.json`** verwenden.
- Tag ist nice-to-have / Fallback.

#### `meta.json` (optional, für Marketplace)

```json
{
  "displayName": "ams.project Core Stack",
  "description": "Zentraler Fachstack von ams.project ...",
  "category": "Business",
  "tags": ["ams", "project", "nservicebus", "business"],
  "vendor": "Wiesenwischer",
  "contact": "support@example.com"
}
```

---

## 4. Versionierung & Tag-Konventionen

**Wichtiger Punkt:**

> Ein **Bundle** in der Registry repräsentiert **genau eine Version** eines Stacks.  
> Versionen werden über **Tags** unterschieden.

Typische Varianten:

### Variante A – Ein Repo für alle Stacks

- Repo:  
  `docker.io/wiesenwischer/rsgo-stacks`

- Tags / Artefakte:

  - `ams-project-core-1.0.0`
  - `ams-project-core-1.1.0`
  - `ams-project-core-2.0.0`
  - `identity-access-1.0.0`
  - usw.

**Tag-Parsing:**

- `ams-project-core-1.0.0` → `StackId = "ams-project-core"`, `Version = "1.0.0"`.

### Variante B – Pro Stack ein eigenes Repo

- Repo: `wiesenwischer/rsgo-stack-ams-project-core`
  - Tags:
    - `1.0.0`
    - `1.1.0`
    - `2.0.0`

RSGO / `OciStackSource` sollte beide Varianten unterstützen können, z. B. über Konfiguration `TagPattern`.

---

## 5. `OciStackSource.Sync()` – Ablauf

Ziel:  
**Genau wie bei Git/File** eine `Sync()`-Methode, die:

- die Quelle neu einliest,
- neue/aktualisierte/veraltete Stack-Versionen erkennt,
- den internen Stack-Katalog aktualisiert.

### 5.1 Pseudocode-Skizze

```csharp
public async Task SyncAsync()
{
    // 1. Alle Tags des Repositories holen
    var tags = await registryClient.ListTagsAsync(config.RegistryHost, config.Repository);

    // 2. Nach Pattern filtern (z.B. "ams-project-core-*")
    var relevantTags = tags
        .Where(t => MatchesPattern(t, config.TagPattern))
        .ToList();

    var seenKeys = new HashSet<string>();

    foreach (var tag in relevantTags)
    {
        // 3. StackId + Version aus Tag ableiten
        var (stackId, version) = ParseTag(tag);

        // 4. Leichte Metadaten lesen (Labels oder meta.json)
        var meta = await registryClient.ReadStackMetaAsync(
            config.RegistryHost,
            config.Repository,
            tag
        );

        var catalogKey = $"{stackId}@{version}";
        seenKeys.Add(catalogKey);

        // 5. Katalogeintrag updaten
        stackCatalog.Upsert(new StackCatalogEntryInternal
        {
            SourceId = config.Id,
            StackId = stackId,
            Version = version,
            OciRef = $"{config.RegistryHost}/{config.Repository}:{tag}",
            DisplayName = meta.DisplayName ?? stackId,
            Description = meta.Description,
            Category = meta.Category,
            Tags = meta.Tags?.ToList() ?? new List<string>()
        });
    }

    // 6. Veraltete Einträge aus diesem Source entfernen/markieren
    stackCatalog.RemoveEntriesFromSourceNotIn(config.Id, seenKeys);
}
```

### 5.2 Metadaten lesen, ohne alles zu ziehen

Möglichkeiten:

- **Labels** auf dem Image/Artefakt (Variante „normales Image“):
  - `org.readystackgo.stack.name`, `org.readystackgo.stack.version`, `org.readystackgo.displayName`, `org.readystackgo.category` etc.
- **Mini-Layer mit `meta.json`**:
  - sehr kleiner Layer, der bei Bedarf gelesen wird.

Wichtig: `Sync()` sollte **nicht** jedes Mal `stack.yaml` + `lock.json` holen, sondern nur für die Anzeige relevante Metadaten.

---

## 6. Import aus OCI in `ImportStackSource`

Wenn ein Admin im UI einen Stack aus der OciSource (oder Marketplace) „installiert“:

1. **Stack auswählen**  
   - aus Katalog (`stackCatalog`), Eintrag hat `OciRef`.

2. **`OciStackSource.Import(ociRef)` aufrufen**:
   - `ociRef` z. B. `docker.io/wiesenwischer/rsgo-stacks:ams-project-core-1.0.0`.

3. `OciStackSource`:

   - PULL:
     - lädt das Bundle (Image oder OCI-Artefakt).
   - EXTRACT:
     - liest `stack.yaml` und `lock.json` (+ optional `meta.json`/`readme.md`).
   - RETURN:
     - `StackManifestContent` (string),
     - `LockDataContent` (string),
     - `Meta` (optional).

4. `ImportStackSource`:

   - speichert `stack.yaml` unter einem lokalen Pfad/Key,
   - speichert `lock.json` (falls vorhanden),
   - legt `ImportedStackManifest` an:

   ```csharp
   var imported = new ImportedStackManifest
   {
       Id = Guid.NewGuid(),
       StackId = stackId,
       Version = version,
       SourceType = "OCI",
       SourceReference = ociRef,
       ImportedAt = DateTimeOffset.UtcNow,
       LocalPathOrKey = localManifestKey,
       LockDataPathOrKey = localLockKey
   };
   ```

5. RSGO erzeugt eine `StackInstallation` für Organisation + Environment, die auf `ImportedStackManifest` referenziert.

---

## 7. Deployment mit Lockfile

Beim Deployment einer `StackInstallation`:

1. Manifest (`stack.yaml`) aus `ImportStackSource` laden.
2. Lock-Daten (`lock.json`) laden.
3. Für jeden Service im Manifest:
   - passenden `images[]`-Eintrag in `lock.json` suchen (z. B. über `name` oder `image` + `tag`).
   - Image-Ref als `image@digest` zusammensetzen.

4. Host aus `image` extrahieren:
   - z. B. `docker.io`, `ghcr.io`.

5. Passende **Container-Registry-Config** suchen:
   - Host + Pattern (z. B. `amssolution/*`).
   - Auth-Daten aus dieser Config verwenden.

6. Images mit Digest ziehen und Container starten.

**Vorteil:**

- Du hast sowohl:
  - eine robust versionierte Stackdefinition,
  - als auch deterministische Image-Referenzen,
- ohne Images doppelt zu speichern.

---

## 8. Zusammenspiel mit Marketplace

Der Marketplace kann zwei Ebenen verbinden:

1. **Katalog-Ebene (StackCatalogEntry)**:
   - kommt aus:
     - einem JSON-Katalog (StackCatalogSource) **oder**
     - direkt aus OCI-Metadaten (`meta.json`, Labels).
   - enthält:
     - Name, Beschreibung, Kategorie, Tags,
     - Hinweis auf benötigte Container-Registries,
     - `OciRef` zum Bundle.

2. **Source-Ebene (OciStackSource)**:
   - liefert die „technische“ Sicht:
     - welche Stacks/Versionen existieren,
     - ihre Refs.

Installations-Flow:

- User klickt im Marketplace auf „Installieren“.
- Marketplace gibt `StackId`, `Version`, `OciRef` an Backend.
- Backend verwendet `OciStackSource.Import(ociRef)` + `ImportStackSource` wie oben beschrieben.

---

## 9. CI/CD – Erstellen & Veröffentlichen von Bundles

Typische Pipeline:

1. Code bauen, Docker-Images bauen & pushen:
   - `docker.io/amssolution/ams-api:1.0.0`
   - `docker.io/amssolution/ams-bff:1.0.0`

2. Digests der Images ermitteln:
   - `docker inspect` oder Registry-API.

3. `stack.yaml` generieren/aktualisieren:
   - z. B. aus Templates, Skripten etc.

4. `lock.json` generieren:
   - für jeden Service/Container:
     - Name, Image, Tag, Digest.

5. Bundle bauen:

   - Variante A (normales Image mit Dateien unter `/rsgo` + Labels):
     - Dockerfile, das `stack.yaml`, `lock.json`, `meta.json`, `readme.md` ins Image legt.
   - Variante B (OCI-Artefakt via ORAS):
     - TAR mit genannten Dateien.

6. Bundle in die Registry pushen:
   - `docker push docker.io/wiesenwischer/rsgo-stacks:ams-project-core-1.0.0`.

7. Optional:
   - Marketplace-Katalog aktualisieren:
     - `StackCatalogEntry` mit `OciRef` + Metadaten.

---

## 10. Zusammenfassung

Dieses Dokument definiert:

- **OCI Stack Bundles**:
  - Artefakte in einer Registry,
  - enthalten `stack.yaml` + `lock.json` + optional Metadaten,
  - **eine Version pro Bundle** (unterschiedliche Tags für Versionen).

- **OciStackSource**:
  - `Sync()`:
    - listet Tags aus der Registry,
    - mappt sie auf `StackId` + `Version`,
    - hält einen internen Katalog aktuell.
  - `Import(ociRef)`:
    - zieht ein Bundle,
    - extrahiert `stack.yaml` + `lock.json`,
    - gibt diese an `ImportStackSource` weiter.

- **ImportStackSource**:
  - verwaltet lokale, versionierte Snapshots von Stack-Manifesten,
  - ist Basis für `StackInstallation`.

- **Deployment**:
  - nutzt Manifest + Lockfile,
  - zieht Images über Container-Registry-Config (Host + Pattern + Auth),
  - verwendet Digests für reproduzierbare Deployments.

Damit ist der komplette Weg abgedeckt:

> CI/CD → OCI-Stack-Bundle → OciStackSource.Sync() → Marketplace/Katalog → ImportStackSource → Deployment

Diese Datei kannst du als `OCI-STACKS-SPEC.md` in dein `/docs`-Verzeichnis legen und Claude damit sowohl Backend- als auch Pipeline-Implementierung starten lassen.

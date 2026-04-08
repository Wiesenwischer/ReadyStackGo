---
title: OCI Stack Bundles
description: Stack-Definitionen aus Docker/OCI Container-Registries laden – versionierte, CI/CD-gesteuerte Stack-Verteilung
---

ReadyStackGo kann Stack-Definitionen direkt aus **OCI Container-Registries** wie Docker Hub, GitHub Container Registry (GHCR) oder Azure Container Registry laden. Dies ermöglicht versionierte Stack-Verteilung über CI/CD Pipelines und deterministische Deployments mit Lock Files.

## Übersicht

| Feature | Beschreibung |
|---------|-------------|
| **OCI Registry Source** | Container-Registry als Stack Source hinzufügen |
| **Tag-basierte Versionierung** | Jeder Registry-Tag repräsentiert eine Stack-Version |
| **Glob Tag-Filterung** | Tags mit Patterns filtern wie `v*`, `ams-*` |
| **Lock File Support** | Images auf Digests pinnen für deterministische Deployments |
| **Test Connection** | Registry-Zugriff vor dem Hinzufügen prüfen |
| **Authentifizierung** | Unterstützung für private Registries mit Username/Password |

---

## Schritt für Schritt: OCI Registry Source hinzufügen

### Schritt 1: Stack Sources öffnen

Navigieren Sie zu **Settings** → **Stack Sources** → **Add Source**.

### Schritt 2: OCI Registry auswählen

Wählen Sie **OCI Registry** aus der Source-Type-Auswahl.

### Schritt 3: Source konfigurieren

Füllen Sie die Registry-Details aus:

- **Source ID** – Eindeutiger Bezeichner (z.B. `my-oci-stacks`)
- **Display Name** – Anzeigename
- **Registry Host** – Registry-Hostname ohne Protokoll (z.B. `docker.io`, `ghcr.io`, `myregistry.azurecr.io`)
- **Repository** – Vollständiger Repository-Pfad (z.B. `myorg/rsgo-stacks`)
- **Tag Pattern** – Glob-Pattern zum Filtern von Tags (Standard: `*` = alle Tags)

:::tip[Tag Pattern Beispiele]
- `*` – Alle Tags
- `v*` – Nur Tags die mit "v" beginnen (z.B. `v1.0.0`, `v2.1.3`)
- `ams-*` – Nur Tags mit "ams-" Prefix
- `?.*.*` – Tags wie `1.0.0`, `2.1.3` (einstellige Major-Version)
:::

### Schritt 4: Authentifizierung konfigurieren (Optional)

Für private Registries Zugangsdaten eingeben:

- **Username** – Registry-Benutzername
- **Password / Token** – Access Token oder Passwort

| Registry | Username | Password |
|----------|----------|----------|
| Docker Hub | Docker Hub Benutzername | Access Token |
| GHCR | GitHub Benutzername | PAT mit `read:packages` |
| Azure CR | Service Principal ID | Service Principal Secret |

### Schritt 5: Verbindung testen

Klicken Sie auf **Test Connection** um den Zugriff zu prüfen. Der Test listet verfügbare Tags und zeigt eine Vorschau der ersten 10 Tags.

### Schritt 6: Source erstellen

Klicken Sie auf **Create Source**. RSGO synchronisiert sofort und lädt Stacks von passenden Tags.

---

## OCI Stack Bundle Format

Ein OCI Stack Bundle ist ein Container-Image mit Stack-Definitions-Dateien als Layer.

### Bundle-Struktur

| Layer | Media Type | Inhalt | Pflicht |
|-------|-----------|--------|---------|
| 1 | `application/vnd.rsgo.stack.manifest.v1+yaml` | `stack.yaml` – RSGO Manifest | Ja |
| 2 | `application/vnd.rsgo.stack.lock.v1+json` | `lock.json` – Image Digests | Nein |
| 3 | `application/vnd.rsgo.stack.meta.v1+json` | `meta.json` – Marketplace Metadaten | Nein |

### Alternative: Standard Docker Image

Sie können auch ein Standard Docker Image verwenden mit Stack-Dateien an bekannten Pfaden:

```
/rsgo/stack.yaml    # oder stack.yml
/rsgo/lock.json     # optional
/rsgo/meta.json     # optional
```

RSGO extrahiert diese Dateien automatisch aus tar.gz Layern beim Sync.

---

## Lock Files

Ein Lock File pinnt jedes Service-Image auf einen bestimmten **Digest** (`sha256:...`) anstatt eines veränderbaren Tags. Dies stellt sicher, dass Deployments exakt dasselbe Image verwenden, unabhängig von Tag-Änderungen.

### lock.json Format

```json
{
  "apiVersion": "1",
  "stackName": "my-app",
  "stackVersion": "1.0.0",
  "images": [
    {
      "name": "web",
      "image": "nginx",
      "tag": "1.25-alpine",
      "digest": "sha256:a8281ce42034b078dc7d88a5bfe6cb40...",
      "role": "main"
    },
    {
      "name": "cache",
      "image": "redis",
      "tag": "7-alpine",
      "digest": "sha256:e422889e156a60c4e7f0ba0c3e5b..."
    }
  ]
}
```

### Wie Digest-Auflösung funktioniert

1. Wenn ein Lock File existiert und einen Eintrag für einen Service enthält → `image@sha256:digest`
2. Wenn kein Lock-Eintrag für einen Service existiert → Fallback auf `image:tag`
3. Lock Files sind optional — Deployments funktionieren auch ohne

---

## OCI Stack Bundles veröffentlichen

### Mit Dockerfile

```dockerfile
FROM scratch
COPY stack.yaml /rsgo/stack.yaml
COPY lock.json /rsgo/lock.json
```

```bash
docker build -t ghcr.io/myorg/rsgo-stacks:v1.0.0 .
docker push ghcr.io/myorg/rsgo-stacks:v1.0.0
```

### Mit ORAS CLI

```bash
oras push ghcr.io/myorg/rsgo-stacks:v1.0.0 \
  stack.yaml:application/vnd.rsgo.stack.manifest.v1+yaml \
  lock.json:application/vnd.rsgo.stack.lock.v1+json
```

### GitHub Actions Beispiel

```yaml
name: Publish Stack Bundle
on:
  push:
    tags: ['v*']

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Log in to GHCR
        run: echo "${{ secrets.GITHUB_TOKEN }}" | docker login ghcr.io -u ${{ github.actor }} --password-stdin

      - name: Build and push bundle
        run: |
          docker build -t ghcr.io/${{ github.repository }}/stacks:${{ github.ref_name }} .
          docker push ghcr.io/${{ github.repository }}/stacks:${{ github.ref_name }}
```

---

## Caching und Sync-Verhalten

- Heruntergeladene Manifeste werden in `~/.rsgo/oci-cache/{sourceId}/{tag}/` gecacht
- Beim Sync prüft RSGO den Manifest-**Digest** — bei Gleichheit wird der Cache verwendet
- Nur neue oder geänderte Tags lösen einen erneuten Download aus
- Pagination wird für Repositories mit mehr als 100 Tags unterstützt

---

## API-Referenz

### Verbindung testen

```
POST /api/stack-sources/test-oci-connection
```

| Feld | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `registryUrl` | string | Ja | Registry-Hostname |
| `repository` | string | Ja | Repository-Pfad |
| `username` | string | Nein | Registry-Benutzername |
| `password` | string | Nein | Registry-Passwort |

**Response:**

```json
{
  "success": true,
  "message": "Connection successful. Found 15 tag(s).",
  "tagCount": 15,
  "sampleTags": ["v1.0.0", "v1.1.0", "v2.0.0"]
}
```

### OCI Registry Source erstellen

```
POST /api/stack-sources
```

```json
{
  "id": "my-oci-stacks",
  "name": "My OCI Stacks",
  "type": "OciRegistry",
  "registryUrl": "ghcr.io",
  "repository": "myorg/rsgo-stacks",
  "tagPattern": "v*",
  "registryUsername": "user",
  "registryPassword": "token"
}
```

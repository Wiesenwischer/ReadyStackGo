# Pipeline-Beispiele

Konkrete Beispiele für die Integration von ReadyStackGo in CI/CD Pipelines.

## curl

### Redeploy (frische Images)

```bash
curl -sf -X POST https://rsgo.example.com/api/hooks/redeploy \
  -H "X-Api-Key: rsgo_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6" \
  -H "Content-Type: application/json" \
  -d '{"stackName": "ams-project"}'
```

### Katalog synchronisieren

```bash
curl -sf -X POST https://rsgo.example.com/api/hooks/sync-sources \
  -H "X-Api-Key: rsgo_..."
```

### Upgrade auf neue Version

```bash
curl -sf -X POST https://rsgo.example.com/api/hooks/upgrade \
  -H "X-Api-Key: rsgo_..." \
  -H "Content-Type: application/json" \
  -d '{"stackName": "ams-project", "targetVersion": "6.5.0"}'
```

Das `-sf` Flag sorgt dafür, dass curl bei HTTP-Fehlern (401, 403, 500) mit einem Fehlercode abbricht – wichtig damit die Pipeline bei Fehlern stoppt.

## GitHub Actions

### Redeploy nach Docker Build

```yaml
name: Build & Deploy

on:
  push:
    branches: [main]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Build and push Docker image
        uses: docker/build-push-action@v5
        with:
          push: true
          tags: myregistry/myapp:latest

      - name: Trigger Redeploy
        run: |
          curl -sf -X POST "${{ secrets.RSGO_URL }}/api/hooks/redeploy" \
            -H "X-Api-Key: ${{ secrets.RSGO_API_KEY }}" \
            -H "Content-Type: application/json" \
            -d '{"stackName": "${{ vars.STACK_NAME }}"}'
```

### Release mit Sync + Upgrade

```yaml
name: Release & Upgrade

on:
  release:
    types: [published]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Build and push Docker image
        uses: docker/build-push-action@v5
        with:
          push: true
          tags: myregistry/myapp:${{ github.event.release.tag_name }}

      - name: Sync Catalog Sources
        run: |
          curl -sf -X POST "${{ secrets.RSGO_URL }}/api/hooks/sync-sources" \
            -H "X-Api-Key: ${{ secrets.RSGO_API_KEY }}"

      - name: Trigger Upgrade
        run: |
          VERSION="${{ github.event.release.tag_name }}"
          VERSION="${VERSION#v}"  # Remove 'v' prefix
          curl -sf -X POST "${{ secrets.RSGO_URL }}/api/hooks/upgrade" \
            -H "X-Api-Key: ${{ secrets.RSGO_API_KEY }}" \
            -H "Content-Type: application/json" \
            -d "{\"stackName\": \"${{ vars.STACK_NAME }}\", \"targetVersion\": \"${VERSION}\"}"
```

**Benötigte Secrets:**
| Secret | Beschreibung |
|--------|-------------|
| `RSGO_URL` | ReadyStackGo Server URL (z.B. `https://rsgo.example.com`) |
| `RSGO_API_KEY` | API Key mit den benötigten Permissions |

**Benötigte Variables:**
| Variable | Beschreibung |
|----------|-------------|
| `STACK_NAME` | Name des zu deployenden Stacks |

## Azure DevOps

### Redeploy nach Build

```yaml
trigger:
  branches:
    include:
      - main

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: Docker@2
    displayName: Build and push Docker image
    inputs:
      command: buildAndPush
      repository: myregistry/myapp
      tags: latest

  - script: |
      curl -sf -X POST "$(RSGO_URL)/api/hooks/redeploy" \
        -H "X-Api-Key: $(RSGO_API_KEY)" \
        -H "Content-Type: application/json" \
        -d '{"stackName": "$(STACK_NAME)"}'
    displayName: Trigger Redeploy on ReadyStackGo
```

### Release Pipeline: Sync + Upgrade

```yaml
trigger: none

resources:
  pipelines:
    - pipeline: build
      source: 'Build Pipeline'
      trigger: true

pool:
  vmImage: 'ubuntu-latest'

steps:
  - script: |
      curl -sf -X POST "$(RSGO_URL)/api/hooks/sync-sources" \
        -H "X-Api-Key: $(RSGO_API_KEY)"
    displayName: Sync Catalog Sources

  - script: |
      curl -sf -X POST "$(RSGO_URL)/api/hooks/upgrade" \
        -H "X-Api-Key: $(RSGO_API_KEY)" \
        -H "Content-Type: application/json" \
        -d '{"stackName": "$(STACK_NAME)", "targetVersion": "$(Build.BuildNumber)"}'
    displayName: Trigger Upgrade
```

**Pipeline-Variablen:**
| Variable | Typ | Beschreibung |
|----------|-----|-------------|
| `RSGO_URL` | Secret | ReadyStackGo Server URL (z.B. `https://rsgo.example.com`) |
| `RSGO_API_KEY` | Secret | API Key mit den benötigten Permissions |
| `STACK_NAME` | Normal | Name des zu deployenden Stacks |

### YAML Templates (wiederverwendbar)

Für häufig wiederkehrende Aufrufe können wiederverwendbare YAML Templates erstellt werden:

**`templates/rsgo-redeploy.yml`:**
```yaml
parameters:
  - name: stackName
    type: string

steps:
  - script: |
      curl -sf -X POST "$(RSGO_URL)/api/hooks/redeploy" \
        -H "X-Api-Key: $(RSGO_API_KEY)" \
        -H "Content-Type: application/json" \
        -d '{"stackName": "${{ parameters.stackName }}"}'
    displayName: Redeploy ${{ parameters.stackName }}
```

**`templates/rsgo-sync-upgrade.yml`:**
```yaml
parameters:
  - name: stackName
    type: string
  - name: targetVersion
    type: string

steps:
  - script: |
      curl -sf -X POST "$(RSGO_URL)/api/hooks/sync-sources" \
        -H "X-Api-Key: $(RSGO_API_KEY)"
    displayName: Sync Catalog Sources

  - script: |
      curl -sf -X POST "$(RSGO_URL)/api/hooks/upgrade" \
        -H "X-Api-Key: $(RSGO_API_KEY)" \
        -H "Content-Type: application/json" \
        -d '{"stackName": "${{ parameters.stackName }}", "targetVersion": "${{ parameters.targetVersion }}"}'
    displayName: Upgrade ${{ parameters.stackName }} to ${{ parameters.targetVersion }}
```

**Verwendung in der Pipeline:**
```yaml
steps:
  - template: templates/rsgo-redeploy.yml
    parameters:
      stackName: ams-project
```

```yaml
steps:
  - template: templates/rsgo-sync-upgrade.yml
    parameters:
      stackName: ams-project
      targetVersion: '6.5.0'
```

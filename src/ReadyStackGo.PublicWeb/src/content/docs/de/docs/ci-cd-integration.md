---
title: CI/CD Integration
description: Automatisierte Deployments über API Keys und Webhooks in CI/CD Pipelines
---

ReadyStackGo lässt sich in automatisierte Build- und Release-Prozesse integrieren. Über **API Keys** und **Webhook-Endpoints** können CI/CD Pipelines Deployments auslösen, Upgrades durchführen und den Stack-Katalog synchronisieren.

## Übersicht

| Use Case | Webhook | Beschreibung |
|----------|---------|--------------|
| **Dev/Test** | `/api/hooks/redeploy` | Nach jedem Build automatisch den Stack redeployen (frische Images) |
| **Release** | `/api/hooks/upgrade` | Auf eine neue Katalog-Version upgraden |
| **Katalog-Sync** | `/api/hooks/sync-sources` | Nach Manifest-Änderungen den Katalog aktualisieren |

---

## Schritt für Schritt: API Key erstellen

Pipeline-Zugriff erfolgt über **API Keys** statt JWT Tokens. So erstellen Sie einen Key:

### Schritt 1: Settings öffnen

Navigieren Sie zu **Settings** im Hauptmenü. Sie sehen dort den Eintrag **CI/CD Integration**.

![Settings-Seite mit CI/CD Integration](/images/docs/cicd-01-settings-nav.png)

---

### Schritt 2: CI/CD Integration

Klicken Sie auf **CI/CD Integration**. Wenn noch keine Keys erstellt wurden, sehen Sie eine leere Seite mit dem Button **Create API Key**.

![CI/CD Seite ohne API Keys](/images/docs/cicd-02-empty-state.png)

---

### Schritt 3: Key konfigurieren

Klicken Sie auf **Create API Key** und füllen Sie das Formular aus:

- **Name** – Ein beschreibender Name (z.B. "Azure DevOps Deploy")
- **Permissions** – Wählen Sie die benötigten Berechtigungen:
  - **Redeploy** – Stack mit frischen Images neu starten
  - **Upgrade** – Stack auf neue Katalog-Version upgraden
  - **Sync Sources** – Katalog-Quellen synchronisieren
- **Environment** (optional) – Beschränkt den Key auf ein bestimmtes Environment
- **Expiry** (optional) – Ablaufdatum setzen

![Create API Key Modal mit ausgefülltem Formular](/images/docs/cicd-03-create-modal.png)

:::tip[Environment-Scope]
Wird ein API Key mit einem bestimmten Environment verknüpft, muss die `environmentId` nicht in jedem Webhook-Request mitgesendet werden – sie wird automatisch aus dem Key ermittelt.
:::

---

### Schritt 4: Key kopieren

Nach dem Erstellen wird der vollständige API Key **einmalig** angezeigt. **Kopieren Sie ihn sofort** – er wird danach nicht mehr angezeigt!

![API Key erstellt – Kopieren Sie den Key sofort](/images/docs/cicd-04-key-created.png)

Speichern Sie den Key als Secret in Ihrem CI/CD System (z.B. Azure DevOps Variable Group, GitHub Secret).

---

### Schritt 5: Key in der Liste

Der erstellte Key erscheint in der Übersicht mit Name, Permissions, Environment, Ablaufdatum und Status.

![API Key Liste mit dem neuen Key](/images/docs/cicd-05-key-list.png)

---

## Webhook-Endpoints

Alle Endpoints befinden sich unter `/api/hooks/` und erfordern API Key Authentifizierung via `X-Api-Key` Header.

### POST /api/hooks/redeploy

Triggert ein Redeployment eines laufenden Stacks. Stoppt die bestehenden Container, pullt frische Images und startet neu – mit denselben Variablen und Einstellungen.

**Parameter:**

| Feld | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `stackName` | string | Ja | Name des deployten Stacks (wie in der Deployments-Übersicht angezeigt) |
| `environmentId` | string | Nein* | ID des Environments. *Entfällt bei environment-gebundenem API Key. |

**Request:**
```json
{
  "stackName": "ams-project",
  "environmentId": "abc123-def4-..."
}
```

**Response (200):**
```json
{
  "success": true,
  "message": "Successfully triggered redeploy of 'ams-project'.",
  "deploymentId": "d4f8b2...",
  "stackName": "ams-project",
  "stackVersion": "6.4.0"
}
```

**Fehler-Responses:**
```json
// 400 – Stack nicht gefunden
{ "success": false, "message": "No deployment found for stack 'xyz' in environment '...'" }

// 400 – Stack nicht im Status Running
{ "success": false, "message": "Deployment is in status 'Failed', only running deployments can be redeployed." }
```

**Permission:** `Hooks.Redeploy`

---

### POST /api/hooks/upgrade

Upgradet einen Stack auf eine bestimmte Katalog-Version. Prüft die Version im Katalog, merged optionale neue Variablen und delegiert an den bestehenden Upgrade-Flow.

**Parameter:**

| Feld | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `stackName` | string | Ja | Name des deployten Stacks |
| `targetVersion` | string | Ja | Zielversion aus dem Katalog (z.B. `"6.5.0"`) |
| `environmentId` | string | Nein* | ID des Environments. *Entfällt bei environment-gebundenem API Key. |
| `variables` | object | Nein | Zusätzliche oder geänderte Variablen als Key-Value-Paare |

**Request:**
```json
{
  "stackName": "ams-project",
  "targetVersion": "6.5.0",
  "environmentId": "abc123-def4-...",
  "variables": {
    "NEW_SETTING": "value"
  }
}
```

**Response (200):**
```json
{
  "success": true,
  "message": "Successfully upgraded 'ams-project' from 6.4.0 to 6.5.0.",
  "deploymentId": "d4f8b2...",
  "previousVersion": "6.4.0",
  "newVersion": "6.5.0"
}
```

**Fehler-Responses:**
```json
// 400 – Version nicht im Katalog
{ "success": false, "message": "Version '9.9.9' not found in catalog. Available versions: 6.4.0, 6.5.0" }

// 400 – Stack nicht im Status Running
{ "success": false, "message": "Deployment is in status 'Failed', only running deployments can be upgraded." }
```

**Permission:** `Hooks.Upgrade`

---

### POST /api/hooks/sync-sources

Synchronisiert alle Stack-Katalog-Quellen (lokale Verzeichnisse und Git Repositories). Nützlich nach einem Git Push mit aktualisierten Manifests.

**Parameter:** Kein Request Body erforderlich.

**Response (200):**
```json
{
  "success": true,
  "stacksLoaded": 12,
  "sourcesSynced": 3,
  "message": "Synced 3 source(s), loaded 12 stack(s)."
}
```

**Permission:** `Hooks.SyncSources`

---

## Fehlerbehandlung

| HTTP Status | Bedeutung |
|-------------|-----------|
| 200 | Erfolg |
| 400 | Ungültige Anfrage (Stack nicht gefunden, ungültige Version, Stack nicht Running) |
| 401 | Nicht authentifiziert (fehlender oder ungültiger API Key) |
| 403 | Nicht autorisiert (API Key hat nicht die benötigte Permission) |
| 500 | Server-Fehler |

---

## Pipeline-Beispiele

### curl

```bash
# Redeploy (frische Images)
curl -sf -X POST https://rsgo.example.com/api/hooks/redeploy \
  -H "X-Api-Key: rsgo_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6" \
  -H "Content-Type: application/json" \
  -d '{"stackName": "ams-project"}'

# Katalog synchronisieren
curl -sf -X POST https://rsgo.example.com/api/hooks/sync-sources \
  -H "X-Api-Key: rsgo_..."

# Upgrade auf neue Version
curl -sf -X POST https://rsgo.example.com/api/hooks/upgrade \
  -H "X-Api-Key: rsgo_..." \
  -H "Content-Type: application/json" \
  -d '{"stackName": "ams-project", "targetVersion": "6.5.0"}'
```

Das `-sf` Flag sorgt dafür, dass curl bei HTTP-Fehlern (401, 403, 500) mit einem Fehlercode abbricht – wichtig damit die Pipeline bei Fehlern stoppt.

### GitHub Actions

```yaml
- name: Trigger Redeploy
  run: |
    curl -sf -X POST "${{ secrets.RSGO_URL }}/api/hooks/redeploy" \
      -H "X-Api-Key: ${{ secrets.RSGO_API_KEY }}" \
      -H "Content-Type: application/json" \
      -d '{"stackName": "${{ vars.STACK_NAME }}"}'
```

### Azure DevOps

#### Inline-Aufruf

```yaml
- script: |
    curl -sf -X POST "$(RSGO_URL)/api/hooks/redeploy" \
      -H "X-Api-Key: $(RSGO_API_KEY)" \
      -H "Content-Type: application/json" \
      -d '{"stackName": "$(STACK_NAME)"}'
  displayName: Trigger Redeploy on ReadyStackGo
```

#### YAML Templates (wiederverwendbar)

Erstellen Sie wiederverwendbare Templates in Ihrem Repository:

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

**Benötigte Pipeline-Variablen:**

| Variable | Typ | Beschreibung |
|----------|-----|--------------|
| `RSGO_URL` | Secret | ReadyStackGo Server URL (z.B. `https://rsgo.example.com`) |
| `RSGO_API_KEY` | Secret | API Key mit den benötigten Permissions |
| `STACK_NAME` | Normal | Name des zu deployenden Stacks (nur bei Inline-Aufrufen) |

---

## Sicherheit

- API Keys werden als **SHA-256 Hash** in der Datenbank gespeichert
- Keys haben das Format `rsgo_` + 32 alphanumerische Zeichen (~190 Bit Entropie)
- Keys können jederzeit widerrufen werden (Settings → CI/CD → Revoke)
- Optional: Environment-Scope beschränkt den Key auf ein bestimmtes Environment
- Ablaufdatum kann pro Key konfiguriert werden

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

## API Key erstellen

Pipeline-Zugriff erfolgt über **API Keys** statt JWT Tokens.

1. Navigiere zu **Settings → CI/CD Integration**
2. Klicke **Create API Key**
3. Vergib einen beschreibenden Namen (z.B. "GitHub Actions Deploy")
4. Wähle die benötigten Permissions:
   - **Redeploy** – Stack mit frischen Images neu starten
   - **Upgrade** – Stack auf neue Katalog-Version upgraden
   - **Sync Sources** – Katalog-Quellen synchronisieren
5. Optional: Ablaufdatum und Environment-Scope setzen
6. **Kopiere den Key sofort** – er wird nur einmal angezeigt!

### Environment-Scope

Wird ein API Key mit einem bestimmten Environment verknüpft, muss die `environmentId` nicht in jedem Request mitgesendet werden – sie wird automatisch aus dem Key ermittelt.

---

## Webhook-Endpoints

Alle Endpoints befinden sich unter `/api/hooks/` und erfordern API Key Authentifizierung via `X-Api-Key` Header.

### Redeploy

`POST /api/hooks/redeploy` – Triggert ein Redeployment eines laufenden Stacks mit frischen Images.

**Request:**
```json
{
  "stackName": "ams-project",
  "environmentId": "optional-wenn-key-environment-gebunden"
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

**Permission:** `Hooks.Redeploy`

### Upgrade

`POST /api/hooks/upgrade` – Upgradet einen Stack auf eine bestimmte Katalog-Version.

**Request:**
```json
{
  "stackName": "ams-project",
  "targetVersion": "6.5.0",
  "environmentId": "optional",
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

**Permission:** `Hooks.Upgrade`

### Katalog synchronisieren

`POST /api/hooks/sync-sources` – Synchronisiert alle Stack-Katalog-Quellen.

**Request:** Kein Body erforderlich.

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
| 400 | Ungültige Anfrage (Stack nicht gefunden, ungültige Version, etc.) |
| 401 | Nicht authentifiziert (fehlender oder ungültiger API Key) |
| 403 | Nicht autorisiert (API Key hat nicht die benötigte Permission) |
| 500 | Server-Fehler |

---

## Pipeline-Beispiele

### curl

```bash
# Redeploy (frische Images)
curl -X POST https://rsgo.example.com/api/hooks/redeploy \
  -H "X-Api-Key: rsgo_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6" \
  -H "Content-Type: application/json" \
  -d '{"stackName": "ams-project"}'

# Katalog synchronisieren
curl -X POST https://rsgo.example.com/api/hooks/sync-sources \
  -H "X-Api-Key: rsgo_..."

# Upgrade
curl -X POST https://rsgo.example.com/api/hooks/upgrade \
  -H "X-Api-Key: rsgo_..." \
  -H "Content-Type: application/json" \
  -d '{"stackName": "ams-project", "targetVersion": "6.5.0"}'
```

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

```yaml
- script: |
    curl -sf -X POST "$(RSGO_URL)/api/hooks/redeploy" \
      -H "X-Api-Key: $(RSGO_API_KEY)" \
      -H "Content-Type: application/json" \
      -d '{"stackName": "$(STACK_NAME)"}'
  displayName: Trigger Redeploy on ReadyStackGo
```

---

## Sicherheit

- API Keys werden als **SHA-256 Hash** in der Datenbank gespeichert
- Keys haben das Format `rsgo_` + 32 alphanumerische Zeichen (~190 Bit Entropie)
- Keys können jederzeit widerrufen werden (Settings → CI/CD → Revoke)
- Optional: Environment-Scope beschränkt den Key auf ein bestimmtes Environment
- Ablaufdatum kann pro Key konfiguriert werden

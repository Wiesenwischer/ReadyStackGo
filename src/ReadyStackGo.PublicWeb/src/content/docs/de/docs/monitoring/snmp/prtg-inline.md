---
title: PRTG Inline-Registration — Ad-hoc Credentials pro Deployment
description: Wie der Saved-Connection-Pfad (Variant 3), aber URL und API-Token direkt am ProductDeployment statt in einer wiederverwendbaren Connection — für Multi-Tenant / Customer-PRTG.
---

**Variant 2** ist die "leichte Schwester" von [Variant 3 (PrtgConnection)](/de/docs/monitoring/snmp/prtg-connection): RSGO trägt sich ebenfalls selbst als PRTG-Device ein und räumt beim Remove wieder auf, aber URL + Token werden **pro Deployment** abgelegt statt in einer zentralen Connection.

:::tip[Wann nimmst du V2 statt V3?]
- **Multi-Tenant**: Jedes ProductDeployment soll in ein *anderes* PRTG (z. B. das des jeweiligen Customers).
- **Einmaliges Setup**: Du willst die Credentials nicht in der Settings-Seite parken, weil sie nur für genau diesen Deployment relevant sind.
- **Quick-Hack**: Schnell testen, ohne in Settings → PRTG Connections gehen zu müssen.

In allen anderen Fällen ist **Variant 3** (saved connection) die bessere Wahl: ein zentraler Punkt für Credential-Rotation und Wiederverwendung.
:::

## Bedienung in der UI

Auf der **Deployment-Detail-Seite** (`/deployments/<id>`) gibt es eine Card **"PRTG monitoring"** mit zwei Tabs:

- **Saved connection** — Dropdown der konfigurierten PRTG-Connections (Variant 3, [Doku](/de/docs/monitoring/snmp/prtg-connection))
- **Inline (ad-hoc)** — diese Variante

Die Felder im Inline-Tab spiegeln die der [Saved-Connection-Variante](/de/docs/monitoring/snmp/prtg-connection) — nur dass sie pro Deployment statt zentral gespeichert werden. Layout-Vergleich mit dem V3-Add-Formular:

![V3 Add-PRTG-Connection-Formular (Name/URL/Token/Template-Device-ID/Verify-TLS) — die Inline-Tab-Variante zeigt die gleichen Felder ohne den Namen](/images/docs/prtg-v3-add-form.png)

Im Inline-Tab trägst du ein:

| Feld | Wert |
|------|------|
| **URL** | `https://prtg.example.local` |
| **API token / passhash** | PRTG-Passhash oder API-Token |
| **Template Device ID** | PRTG-Device-ID, die RSGO klont (leer = Auto-Register aus) |
| **Verify TLS certificate** | Aushaken für Self-Signed-Certs |

**Save inline** speichert die Credentials verschlüsselt am ProductDeployment. Eine bestehende Saved-Connection-Verlinkung wird dabei **gelöscht** — es kann immer nur **ein** PRTG-Ziel aktiv sein.

## Bedienung per API

```bash
# Inline-Registration setzen
curl -X PUT https://rsgo.local/api/deployments/<deployment-id>/prtg-inline \
  -H "X-Api-Key: rsgo_..." \
  -H "Content-Type: application/json" \
  -d '{
    "id": "<deployment-id>",
    "url": "https://prtg.example.local",
    "apiToken": "PRTG_TOKEN_123",
    "templateDeviceId": 4221,
    "verifyTls": false
  }'

# Inline-Registration löschen (url: null)
curl -X PUT https://rsgo.local/api/deployments/<deployment-id>/prtg-inline \
  -H "X-Api-Key: rsgo_..." \
  -H "Content-Type: application/json" \
  -d '{"id":"<deployment-id>","url":null,"verifyTls":true}'
```

## Was passiert genau?

Identisch zu Variant 3 — nur die Quelle der Credentials ist anders:

| Event | RSGO ruft PRTG-API | Daten-Quelle |
|-------|---------------------|--------------|
| `Running` | `duplicateobject.htm` + `pause.htm?action=1` | Inline-URL + entschlüsselter Inline-Token + Inline-TemplateDeviceId |
| `Removed` / `Superseded` | `deleteobject.htm` | Gleiche Inline-Daten |

Der Resolver in RSGO ([PrtgRegisterDeviceOnCompletedHandler.ResolvePrtgTarget](src/ReadyStackGo.Application/Integrations/Prtg/V3/PrtgLifecycleHandlers.cs)) bevorzugt eine Saved-Connection wenn sowohl `PrtgConnectionId` als auch `InlinePrtgUrl` gesetzt sind — in der Praxis wird das aber nie passieren, weil "Save inline" die Connection beim Setzen rauswirft (und umgekehrt).

## Sicherheit

- **Token verschlüsselt at-rest** mit demselben `ICredentialEncryptionService` wie alle anderen Credentials in RSGO.
- **Token niemals in Read-DTOs** — `HasInlinePrtgApiToken: bool` ist der einzige Hinweis, dass ein Token gesetzt ist.
- **TLS-Verify per Deployment** konfigurierbar (für Customer-PRTG mit Self-Signed-Certs).
- **Best-effort**: ein PRTG-Ausfall blockiert den RSGO-Deploy nicht, nur die Sync-Operation wird gewarnt.

## Migration zwischen V2 und V3

Du kannst jederzeit zwischen den beiden Varianten wechseln:

- **V2 → V3**: Connection in Settings anlegen → Detail-Page → Tab "Saved connection" → Dropdown auswählen → Save. Das löscht die Inline-Daten.
- **V3 → V2**: Detail-Page → Tab "Inline" → URL/Token/TemplateID eintragen → Save inline. Das löscht den Connection-Link.

Beim Wechsel **löscht** RSGO den PRTG-Device-Eintrag nicht — das passiert erst beim nächsten `Removed`/`Superseded`-Event. Wenn du das alte PRTG-Device sofort weg willst, lösche es manuell in PRTG.

## Verwandte Seiten

- [Variant 3 — Saved PrtgConnection](/de/docs/monitoring/snmp/prtg-connection) — der "richtige Weg" für mehrere Deployments
- [Variant 4 — HTTP Data Advanced Sensor](/de/docs/monitoring/snmp/prtg-http-sensor) — passive Variante, PRTG pollt RSGO
- [Variant 1 — Device Template Bundle](/de/docs/monitoring/snmp/prtg) — Deep Integration mit Per-Stack-Sensoren

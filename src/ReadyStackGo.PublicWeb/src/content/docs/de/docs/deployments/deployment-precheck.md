---
title: Deployment Precheck
description: Automatische Infrastruktur-Prüfungen vor dem Deployment – Images, Ports, Netzwerke und mehr
---

Bevor ein Stack deployed wird, führt ReadyStackGo automatisch einen **Deployment Precheck** durch. Dabei werden alle Voraussetzungen geprüft, um Probleme zu erkennen, bevor Container entfernt oder neu erstellt werden.

## Übersicht

| Prüfung | Beschreibung | Schweregrad |
|---------|-------------|-------------|
| **Variable Validation** | Sind alle Pflicht-Variablen gesetzt und valide? | Error |
| **Existing Deployment** | Gibt es bereits ein Deployment mit diesem Stack-Namen? | Warning / Error |
| **Image Availability** | Sind alle Docker-Images lokal oder remote verfügbar? | Error |
| **Port Conflicts** | Werden Host-Ports bereits von anderen Containern belegt? | Error |
| **Network Availability** | Existieren externe Netzwerke, die vom Stack benötigt werden? | Error |
| **Volume Status** | Existieren bereits Volumes für diesen Stack? | Warning |

---

## Schritt für Schritt: Precheck beim Deployment

### Schritt 1: Stack im Katalog auswählen

Öffnen Sie den **Stack Catalog** und wählen Sie ein Produkt aus. Klicken Sie auf **View Details**, um die verfügbaren Stacks zu sehen.

![Stack Catalog mit verfügbaren Produkten](/images/docs/precheck-01-catalog.png)

---

### Schritt 2: Stack konfigurieren

Auf der Deploy-Seite konfigurieren Sie **Stack Name** und **Environment Variables**. Der Precheck startet automatisch, sobald ein gültiger Stack-Name eingegeben wurde.

![Deploy-Seite mit Stack-Konfiguration](/images/docs/precheck-02-configure.png)

---

### Schritt 3: Precheck-Ergebnisse prüfen

Das **Deployment Precheck** Panel erscheint unterhalb der Konfiguration und zeigt die Ergebnisse aller Prüfungen:

- ✓ **Grün** – Prüfung bestanden
- ⚠ **Gelb** – Warnung (Deployment möglich, aber Hinweis beachten)
- ✗ **Rot** – Fehler (Deployment blockiert)

![Precheck-Panel mit Ergebnissen – alle Prüfungen bestanden](/images/docs/precheck-03-results.png)

:::tip[Service-Badges]
Bei service-spezifischen Prüfungen (z.B. Image Availability) wird der betroffene Service-Name als Badge angezeigt. So erkennen Sie sofort, welcher Service das Problem verursacht.
:::

---

### Schritt 4: Re-Check ausführen

Über den **Re-Check** Button können Sie die Prüfungen jederzeit erneut ausführen – z.B. nachdem Sie eine Variable geändert oder ein fehlendes Docker-Image gepullt haben.

![Precheck-Panel mit Re-Check Button](/images/docs/precheck-04-recheck-button.png)

---

### Schritt 5: Deploy starten

Wenn alle Prüfungen bestanden sind (keine Fehler), ist der **Deploy**-Button aktiv. Bei Fehlern wird der Button deaktiviert und Sie müssen die Probleme zuerst beheben.

![Deploy-Button aktiviert nach erfolgreichem Precheck](/images/docs/precheck-05-deploy-button.png)

:::caution[Fehler blockieren das Deployment]
Wenn der Precheck Fehler meldet, kann das Deployment nicht gestartet werden. Beheben Sie die gemeldeten Probleme und klicken Sie auf **Re-Check**, um die Prüfungen erneut auszuführen.
:::

---

## Prüfungen im Detail

### Variable Validation

Prüft, ob alle als **Required** markierten Variablen einen Wert haben und ob die Werte den definierten Patterns entsprechen. Jede fehlende oder ungültige Variable wird einzeln als Fehler gemeldet.

### Existing Deployment

Erkennt, ob bereits ein Deployment mit demselben Stack-Namen existiert:

| Status des bestehenden Deployments | Ergebnis |
|---|---|
| **Running** | Warning – Upgrade-Szenario |
| **Installing / Upgrading** | Error – Deployment blockiert |
| **Failed** | Warning – Retry-Szenario |
| **Removed** | OK – Frische Installation |

### Image Availability

Prüft für jeden Service, ob das Docker-Image lokal vorhanden ist oder remote gezogen werden kann. Bei privaten Registries wird auch die Authentifizierung geprüft.

### Port Conflicts

Erkennt, ob Host-Ports bereits von laufenden Containern belegt werden. Container, die zum selben Stack gehören (Upgrade-Szenario), werden dabei ausgeschlossen.

### Network Availability

Prüft, ob externe Netzwerke existieren, die vom Stack benötigt werden. Stack-eigene Netzwerke werden automatisch beim Deployment erstellt.

### Volume Status

Meldet, ob benannte Volumes bereits existieren:
- **Neues Volume** → OK (wird beim Deployment erstellt)
- **Existierendes Volume bei Upgrade** → OK (Daten bleiben erhalten)
- **Existierendes Volume bei Neuinstallation** → Warning (vorhandene Daten werden wiederverwendet)

---

## Hooks API: Dry Run

Der Precheck kann auch über die **Hooks API** ausgeführt werden, ohne ein tatsächliches Deployment zu starten:

```bash
curl -X POST http://rsgo:8080/api/hooks/deploy \
  -H "X-API-Key: rsgo_your_key" \
  -H "Content-Type: application/json" \
  -d '{
    "stackName": "my-stack",
    "stackId": "source:stack:version",
    "dryRun": true
  }'
```

Bei `dryRun: true` wird nur der Precheck ausgeführt und das Ergebnis zurückgegeben. Bei Fehlern antwortet die API mit `422 Unprocessable Entity`.

---

## Timeout und Fehlerbehandlung

- **Timeout**: Der gesamte Precheck hat ein 30-Sekunden-Timeout
- **Einzelne Regel-Fehler**: Wenn eine einzelne Prüfung fehlschlägt, wird dies als Warning gemeldet – andere Prüfungen laufen weiter
- **Docker-Verbindungsfehler**: Wird sofort als Error gemeldet
- **Precheck-Fehler**: Wenn der Precheck selbst fehlschlägt (z.B. Netzwerkprobleme), kann trotzdem deployed werden

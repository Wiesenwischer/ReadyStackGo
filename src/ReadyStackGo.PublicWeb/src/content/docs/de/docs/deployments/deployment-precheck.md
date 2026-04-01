---
title: Deployment Precheck
description: Infrastruktur-Prüfungen vor dem Deployment – Images, Ports, Netzwerke und mehr
---

Vor jedem Deployment können Sie einen **Deployment Precheck** ausführen. Dabei werden alle Voraussetzungen geprüft, um Probleme zu erkennen, bevor Container entfernt oder neu erstellt werden. Der Precheck wird nicht automatisch ausgeführt, sondern manuell über den **Run Precheck** Button gestartet.

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

Auf der Deploy-Seite konfigurieren Sie **Stack Name** und **Environment Variables**. In der rechten Sidebar finden Sie den **Run Precheck** Button.

![Deploy-Seite mit Stack-Konfiguration](/images/docs/precheck-02-configure.png)

---

### Schritt 3: Precheck starten

Klicken Sie auf den **Run Precheck** Button in der Sidebar. Sie werden auf eine eigene Precheck-Seite weitergeleitet, auf der die Infrastruktur-Prüfungen automatisch gestartet werden.

![Sidebar mit Run Precheck Button](/images/docs/precheck-03-run-button.png)

:::tip[Precheck ist optional]
Der Precheck ist eine Empfehlung, kein Pflichtschritt. Sie können auch ohne Precheck deployen – der **Deploy** Button ist immer aktiv.
:::

---

### Schritt 4: Ergebnisse prüfen

Die **Precheck-Ergebnisseite** zeigt die Ergebnisse aller Prüfungen:

- ✓ **Grün** – Prüfung bestanden
- ⚠ **Gelb** – Warnung (Deployment möglich, aber Hinweis beachten)
- ✗ **Rot** – Fehler (sollte vor dem Deployment behoben werden)

![Precheck-Ergebnisseite mit Check-Ergebnissen](/images/docs/precheck-04-results.png)

:::tip[Service-Badges]
Bei service-spezifischen Prüfungen (z.B. Image Availability) wird der betroffene Service-Name als Badge angezeigt. So erkennen Sie sofort, welcher Service das Problem verursacht.
:::

---

### Schritt 5: Re-Check oder zurück zur Konfiguration

Über den **Re-Check** Button können Sie die Prüfungen erneut ausführen – z.B. nachdem Sie ein fehlendes Docker-Image gepullt haben. Über **Back to Configure** gelangen Sie zurück zur Deploy-Seite.

![Precheck-Seite mit Re-Check Button](/images/docs/precheck-05-recheck.png)

---

### Schritt 6: Deploy starten

Zurück auf der Deploy-Seite können Sie das Deployment jederzeit starten. Der **Deploy** Button ist unabhängig vom Precheck-Ergebnis immer aktiv.

![Deploy-Button aktiv ohne Precheck-Pflicht](/images/docs/precheck-06-deploy-enabled.png)

---

## Produkt-Deployment Precheck

Bei einem **Produkt-Deployment** (mehrere Stacks gleichzeitig) führt der Precheck die Prüfungen für **alle Stacks parallel** durch. Die Ergebnisse werden pro Stack gruppiert angezeigt:

- Stacks mit Fehlern oder Warnungen werden automatisch aufgeklappt
- Stacks ohne Probleme sind eingeklappt
- Die Zusammenfassung zeigt den Gesamtstatus über alle Stacks

---

## Prüfungen im Detail

### Variable Validation

Prüft, ob alle als **Required** markierten Variablen einen Wert haben und ob die Werte den definierten Patterns entsprechen. Variablen wie `${FRONTEND_PORT}` werden dabei korrekt aufgelöst.

### Existing Deployment

Erkennt, ob bereits ein Deployment mit demselben Stack-Namen existiert:

| Status des bestehenden Deployments | Ergebnis |
|---|---|
| **Running** | Warning – Upgrade-Szenario |
| **Installing / Upgrading** | Error – Deployment blockiert |
| **Failed** | Warning – Retry-Szenario |
| **Removed** | OK – Frische Installation |

### Image Availability

Prüft für jeden Service, ob das Docker-Image lokal vorhanden ist oder remote gezogen werden kann. Image-Namen mit Variablen (z.B. `${REGISTRY}/app:${TAG}`) werden vor der Prüfung aufgelöst.

### Port Conflicts

Erkennt, ob Host-Ports bereits von laufenden Containern belegt werden. Port-Definitionen mit Variablen (z.B. `${WEB_PORT}:80`) werden korrekt aufgelöst. Container, die zum selben Stack gehören (Upgrade-Szenario), werden ausgeschlossen.

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

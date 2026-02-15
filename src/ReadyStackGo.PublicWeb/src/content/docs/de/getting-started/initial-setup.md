---
title: Ersteinrichtung
description: ReadyStackGo Setup-Wizard und Onboarding-Checkliste durchlaufen
---

Nach der [Installation](/getting-started/installation/) besteht die Ersteinrichtung aus zwei Phasen:

1. **Setup-Wizard** — Admin-Account erstellen (unauthentifiziert, zeitbegrenzt)
2. **Onboarding-Checkliste** — Organisation, Environments und Stack Sources konfigurieren (authentifiziert, in eigenem Tempo)

---

## Phase 1: Setup-Wizard

Der Setup-Wizard startet automatisch beim ersten Zugriff auf die Web-Oberfläche. Er besteht aus einem einzigen Schritt: der Erstellung des Admin-Accounts.

:::caution[Zeitlimit]
Der Setup-Wizard hat ein **5-Minuten-Timeout** aus Sicherheitsgründen. Falls der Timer abläuft, starte den Container neu:
```bash
docker restart readystackgo
```
:::

### Admin-Account erstellen

Gib deine Administrator-Zugangsdaten ein:

![Setup-Wizard: Admin-Account erstellen](/images/docs/wizard-01-admin-form.png)

| Feld | Anforderung |
|------|-------------|
| **Username** | Mindestens 3 Zeichen |
| **Password** | Mindestens 8 Zeichen |
| **Confirm Password** | Muss mit dem Passwort übereinstimmen |

Das Formular validiert deine Eingaben vor dem Absenden:

![Setup-Wizard: Validierungsfehler](/images/docs/wizard-02-validation-error.png)

### Was passiert nach dem Absenden?

Wenn du auf **"Continue"** klickst:

1. Der Admin-Account wird erstellt
2. Du wirst **automatisch eingeloggt** (kein separater Login-Schritt)
3. Der Wizard markiert sich als abgeschlossen
4. Du wirst zur Anwendung weitergeleitet

:::caution[Wichtig]
Dieser Account ist der einzige Weg, auf ReadyStackGo zuzugreifen. Bewahre die Zugangsdaten sicher auf — sie können später nicht wiederhergestellt werden!
:::

---

## Phase 2: Onboarding-Checkliste

Nach Abschluss des Wizards erscheint eine **Onboarding-Checkliste** auf dem Dashboard, die dich durch die weitere Konfiguration führt. Im Gegensatz zum Wizard gibt es kein Zeitlimit — du kannst diese Schritte in eigenem Tempo abarbeiten.

Die Checkliste enthält folgende Punkte:

| Punkt | Pflicht | Beschreibung |
|-------|---------|-------------|
| **Admin-Account** | Ja | Immer abgehakt — wurde im Wizard erledigt |
| **Organisation** | Ja | Organisationsidentität einrichten |
| **Environment** | Nein | ReadyStackGo mit einem Docker-Daemon verbinden |
| **Stack Sources** | Nein | Git-Repositories mit Stack-Definitionen hinzufügen |
| **Container Registries** | Nein | Authentifizierung für private Container-Registries konfigurieren |

### Organisation einrichten

Die Organisation ist der **erste Pflichtschritt** nach dem Wizard. Bis eine Organisation konfiguriert ist, sind Environment, Stack Sources und Registry-Einträge deaktiviert.

Klicke auf **"Configure"** neben "Set up your organization", um zur Organisationseinstellungsseite zu navigieren.

### Weitere Schritte abschließen

Jeder Checklisten-Punkt hat einen **"Configure"**-Link, der dich direkt zur entsprechenden Einstellungsseite führt. Die Punkte werden progressiv freigeschaltet — die Organisation muss zuerst konfiguriert werden.

### Checkliste ausblenden

Wenn du die benötigten Schritte abgeschlossen hast (oder die Konfiguration später vornehmen möchtest), kannst du die Checkliste ausblenden:

- Klicke auf das **✕** oben rechts, oder
- Klicke auf **"Dismiss checklist"** unten

Die Checkliste wird nach dem Ausblenden nicht mehr angezeigt.

---

## Nach dem Setup

### Nächste Schritte

1. **Organisation konfigurieren** — Pflicht, um alle Features freizuschalten
2. **Environment hinzufügen** — Gehe zu *Environments* und klicke auf *Add Environment*
3. **Stack Sources konfigurieren** — Füge Git-Repositories als Stack-Quellen hinzu
4. **Ersten Stack deployen** — Siehe [Ersten Stack deployen](/getting-started/first-deployment/)

---

## Troubleshooting

### Wizard startet nicht

Falls der Wizard nicht automatisch startet:

```bash
# Container-Logs prüfen
docker logs readystackgo

# Container neustarten
docker restart readystackgo
```

### Wizard zurücksetzen

Falls du den Wizard erneut durchlaufen möchtest, musst du die Daten zurücksetzen:

```bash
# Container mit Volumes stoppen und löschen
docker compose down -v

# Container neu starten
docker compose up -d
```

:::danger[Warnung]
Das Löschen der Volumes entfernt alle Konfigurationen und Deployment-Informationen!
:::

---
title: Ersteinrichtung
description: ReadyStackGo Setup-Wizard und Onboarding durchlaufen
---

Nach der [Installation](/getting-started/installation/) besteht die Ersteinrichtung aus zwei Phasen:

1. **Setup-Wizard** — Admin-Account erstellen (unauthentifiziert, zeitbegrenzt)
2. **Onboarding** — Organisation, Environment und Stack Sources konfigurieren (authentifiziert, geführt)

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
4. Du wirst automatisch zum **Onboarding** weitergeleitet

:::caution[Wichtig]
Dieser Account ist der einzige Weg, auf ReadyStackGo zuzugreifen. Bewahre die Zugangsdaten sicher auf — sie können später nicht wiederhergestellt werden!
:::

---

## Phase 2: Onboarding

Nach dem Wizard startet automatisch das **geführte Onboarding**. Es führt dich in drei Schritten durch die Grundkonfiguration deiner ReadyStackGo-Instanz.

:::note[Pflicht]
Das Onboarding ist **nicht überspringbar**. Erst wenn mindestens die Organisation konfiguriert ist, wirst du zur Anwendung weitergeleitet. Bei jedem Login prüft ReadyStackGo, ob das Onboarding abgeschlossen ist.
:::

### Schritt 1: Organisation erstellen

Die Organisation ist die **Top-Level-Identität** deiner ReadyStackGo-Instanz. Alle Environments, Stacks und Einstellungen gehören zu ihr. Dieser Schritt ist **Pflicht** — es gibt keinen "Überspringen"-Button.

![Onboarding: Organisation erstellen](/images/docs/wizard-03-onboarding-start.png)

| Feld | Anforderung |
|------|-------------|
| **Organization Name** | Mindestens 2 Zeichen |

Gib einen Namen ein und klicke auf **"Continue"**.

:::tip[Tipp]
Du kannst den Organisationsnamen später jederzeit in den Einstellungen ändern.
:::

---

### Schritt 2: Docker Environment hinzufügen

In diesem Schritt verbindest du ReadyStackGo mit einem Docker-Daemon. Die Felder sind **vorausgefüllt** mit sinnvollen Standardwerten:

![Onboarding: Docker Environment](/images/docs/wizard-04-onboarding-env.png)

| Feld | Standardwert | Beschreibung |
|------|-------------|-------------|
| **Environment Name** | `Local Docker` | Frei wählbarer Name für dieses Environment |
| **Docker Socket Path** | `unix:///var/run/docker.sock` | Pfad zum Docker-Daemon-Socket auf dem Host |

Du hast zwei Möglichkeiten:
- **"Continue"** — Erstellt das Environment mit den angegebenen Werten
- **"Skip for now"** — Überspringt diesen Schritt (du kannst später Environments in den Einstellungen hinzufügen)

:::tip[Docker Socket]
Wenn du ReadyStackGo als Docker-Container betreibst, muss der Docker-Socket des Hosts in den Container gemountet sein (standardmäßig konfiguriert in der `docker-compose.yml`).
:::

---

### Schritt 3: Stack Sources auswählen

Stack Sources sind Repositories mit vorkonfigurierten Stack-Definitionen, die du direkt deployen kannst. ReadyStackGo zeigt dir eine **kuratierte Liste** verfügbarer Quellen an:

![Onboarding: Stack Sources](/images/docs/wizard-05-onboarding-sources.png)

- Empfohlene Quellen (**Featured**) sind automatisch vorausgewählt
- Du kannst Quellen an- und abwählen
- Klicke auf **"Add sources"**, um die ausgewählten Quellen hinzuzufügen
- Oder klicke auf **"Skip for now"**, um diesen Schritt zu überspringen

:::tip[Stack Sources]
Du kannst Stack Sources jederzeit unter *Settings → Stack Sources* hinzufügen, entfernen oder eigene Git-Repositories als Quelle konfigurieren.
:::

---

### Schritt 4: Zusammenfassung

Nach Abschluss der Schritte zeigt dir das Onboarding eine **Zusammenfassung** aller konfigurierten (und übersprungenen) Punkte:

![Onboarding: Zusammenfassung](/images/docs/wizard-06-onboarding-complete.png)

Klicke auf **"Go to Dashboard"**, um zum Dashboard zu gelangen.

---

## Nach dem Setup

Nach Abschluss des Onboardings wirst du auf das **Dashboard** weitergeleitet:

![Dashboard nach dem Onboarding](/images/docs/wizard-07-dashboard.png)

Falls du im Onboarding optionale Schritte übersprungen hast (Environment oder Stack Sources), zeigt das Dashboard einen **Hinweis** mit Empfehlungen zur weiteren Konfiguration. Dieser Hinweis verschwindet automatisch, sobald alle Punkte konfiguriert sind.

### Nächste Schritte

1. **Environment hinzufügen** (falls übersprungen) — Gehe zu *Environments* und erstelle ein neues Environment
2. **Stack Sources konfigurieren** (falls übersprungen) — Unter *Settings → Stack Sources*
3. **Ersten Stack deployen** — Siehe [Ersten Stack deployen](/getting-started/first-deployment/)

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

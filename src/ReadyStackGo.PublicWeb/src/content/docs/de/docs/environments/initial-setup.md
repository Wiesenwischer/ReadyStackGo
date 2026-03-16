---
title: Ersteinrichtung
description: Ersteinrichtung mit Setup-Wizard und geführtem Onboarding für ReadyStackGo
---

Beim ersten Start von ReadyStackGo führt ein zweiphasiger Setup-Prozess durch die initiale Konfiguration: der **Wizard** zur Admin-Erstellung, gefolgt vom **Onboarding** für Environment- und Stack-Source-Einrichtung.

## Übersicht

| Phase | Zweck | Pflicht |
|-------|-------|---------|
| **Wizard** | Admin-Konto erstellen | Ja (5-Minuten-Timeout) |
| **Onboarding Schritt 1** | Organisation erstellen | Ja |
| **Onboarding Schritt 2** | Docker Environment hinzufügen | Überspringbar |
| **Onboarding Schritt 3** | Stack Sources auswählen | Überspringbar |
| **Onboarding Schritt 4** | Container Registries konfigurieren | Überspringbar |

---

## Phase 1: Admin-Erstellung (Wizard)

Beim ersten Start öffnet ReadyStackGo die Wizard-Seite. Sie haben **5 Minuten** Zeit, das Admin-Konto zu erstellen, bevor das Setup-Fenster gesperrt wird.

![Wizard Admin-Erstellungsseite](/images/docs/onboarding-01-wizard-admin.png)

Geben Sie einen Benutzernamen und ein sicheres Passwort (mindestens 8 Zeichen) ein und bestätigen Sie das Passwort.

![Ausgefülltes Admin-Formular](/images/docs/onboarding-02-wizard-filled.png)

Klicken Sie auf **Continue**, um das Admin-Konto zu erstellen. Sie werden automatisch eingeloggt und zum Onboarding weitergeleitet.

:::caution[Timeout]
Wenn das 5-Minuten-Fenster abläuft, ohne dass ein Admin erstellt wurde, wird das Setup gesperrt. Starten Sie den Container mit `docker restart readystackgo` neu, um ein neues 5-Minuten-Fenster zu erhalten.
:::

---

## Phase 2: Geführtes Onboarding

### Schritt 1: Organisation erstellen

Jede ReadyStackGo-Instanz gehört zu einer Organisation. Geben Sie eine **Organization ID** (Kleinbuchstaben, Zahlen, Bindestriche) und einen **Anzeigenamen** ein.

![Organisations-Setup-Schritt](/images/docs/onboarding-03-organization.png)

Dieser Schritt ist Pflicht — er kann nicht übersprungen werden.

---

### Schritt 2: Docker Environment hinzufügen

Verbinden Sie ReadyStackGo mit einem Docker Daemon. Der Docker Socket Path wird automatisch vom Server erkannt.

![Docker Environment Setup-Schritt](/images/docs/onboarding-04-environment.png)

:::tip[Später einrichten]
Sie können diesen Schritt überspringen und Environments später über **Settings → Environments** hinzufügen.
:::

---

### Schritt 3: Stack Sources auswählen

Wählen Sie kuratierte Stack Sources für Ihre Instanz aus. Empfohlene Sources sind vorausgewählt. Diese stellen fertig deploybare Stack-Definitionen bereit.

![Stack Sources Auswahl-Schritt](/images/docs/onboarding-05-sources.png)

Stack Sources können später in **Settings → Stack Sources** hinzugefügt oder entfernt werden.

---

### Schritt 4: Container Registries

ReadyStackGo erkennt Container Registries aus Ihren Stack Sources und prüft den Zugang. Öffentliche Registries werden automatisch verifiziert. Für private Registries geben Sie Zugangsdaten ein.

![Container Registries Konfigurationsschritt](/images/docs/onboarding-06-registries.png)

Sie können diesen Schritt überspringen und Registries später in **Settings → Container Registries** konfigurieren.

---

### Schritt 5: Setup abgeschlossen

Die Abschlussseite zeigt eine Zusammenfassung der konfigurierten Punkte. Übersprungene Schritte werden entsprechend markiert.

![Onboarding Abschlussseite](/images/docs/onboarding-07-complete.png)

Klicken Sie auf **Go to Dashboard**, um ReadyStackGo zu verwenden.

![Dashboard nach der Ersteinrichtung](/images/docs/onboarding-08-dashboard.png)

---

## Nach dem Setup

Alle während des Onboardings konfigurierten Einstellungen können später geändert werden:

- **Organisation**: Settings → Organization
- **Environments**: Settings → Environments
- **Stack Sources**: Settings → Stack Sources
- **Container Registries**: Settings → Container Registries

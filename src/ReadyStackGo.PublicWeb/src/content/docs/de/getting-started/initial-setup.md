---
title: Ersteinrichtung
description: ReadyStackGo Setup-Wizard durchlaufen
---

Nach der [Installation](/getting-started/installation/) führt dich der Setup-Wizard durch die initiale Konfiguration von ReadyStackGo. Der Wizard startet automatisch beim ersten Zugriff auf die Web-Oberfläche.

## Übersicht

Der Setup-Wizard besteht aus vier Schritten:

1. **Admin-Account erstellen** – Lege den primären Administrator an
2. **Organisation konfigurieren** – Definiere deine Organisationsdaten
3. **Environment einrichten** – Verbinde ReadyStackGo mit Docker (optional)
4. **Setup abschließen** – Finalisiere die Konfiguration

---

## Schritt 1: Admin-Account erstellen

Im ersten Schritt erstellst du den Administrator-Account, mit dem du dich künftig bei ReadyStackGo anmeldest.

![Wizard Schritt 1: Admin-Account erstellen](/images/screenshots/wizard-admin.png)

### Eingabefelder

| Feld | Anforderung |
|------|-------------|
| **Username** | Mindestens 3 Zeichen |
| **Password** | Mindestens 8 Zeichen |
| **Confirm Password** | Muss mit dem Passwort übereinstimmen |

### Tipps

- Wähle einen sicheren Benutzernamen (nicht nur `admin`)
- Verwende ein starkes Passwort mit Buchstaben, Zahlen und Sonderzeichen
- Notiere dir die Zugangsdaten – sie können später nicht wiederhergestellt werden

:::caution[Wichtig]
Dieser Account ist der einzige Weg, auf ReadyStackGo zuzugreifen. Bewahre die Zugangsdaten sicher auf!
:::

---

## Schritt 2: Organisation konfigurieren

Hier definierst du die Identität deiner ReadyStackGo-Instanz.

![Wizard Schritt 2: Organisation konfigurieren](/images/screenshots/wizard-org.png)

### Eingabefelder

| Feld | Beschreibung | Beispiel |
|------|--------------|----------|
| **Organization ID** | Technischer Bezeichner (nur Kleinbuchstaben, Zahlen, Bindestriche) | `my-company` |
| **Organization Name** | Anzeigename für deine Organisation | `My Company Inc.` |

### Verwendung

- Die **Organization ID** wird intern für Dateistrukturen und API-Zugriffe verwendet
- Der **Organization Name** wird in der Web-Oberfläche angezeigt

---

## Schritt 3: Environment einrichten

In diesem Schritt kannst du ein Docker-Environment konfigurieren. Ein Environment repräsentiert eine Docker-Installation, die ReadyStackGo verwalten soll.

:::tip[Optional]
Du kannst diesen Schritt überspringen und später Environments hinzufügen. Klicke dazu auf **"Skip for now"**.
:::

### Eingabefelder

| Feld | Beschreibung | Standard |
|------|--------------|----------|
| **Environment ID** | Technischer Bezeichner | `local` |
| **Display Name** | Anzeigename für das Environment | `Local Docker` |
| **Docker Socket Path** | Pfad zum Docker-Socket | `unix:///var/run/docker.sock` |

### Docker Socket Path

Der Socket-Pfad wird automatisch für dein System erkannt:

| System | Socket Path |
|--------|-------------|
| Linux | `unix:///var/run/docker.sock` |
| Windows | `npipe:////./pipe/docker_engine` |
| macOS | `unix:///var/run/docker.sock` |

### Wann überspringen?

Du kannst diesen Schritt überspringen, wenn:

- Du mehrere Docker-Hosts später hinzufügen möchtest
- Du zuerst nur die Web-Oberfläche kennenlernen willst
- Du ReadyStackGo in einer Entwicklungsumgebung testest

---

## Schritt 4: Setup abschließen

Im letzten Schritt siehst du eine Zusammenfassung deiner Konfiguration:

- ✓ Admin account configured
- ✓ Organization details set
- ✓ Environment configured (falls nicht übersprungen)

### Was passiert beim Abschluss?

Wenn du auf **"Complete Setup"** klickst:

1. Deine Konfiguration wird gespeichert
2. Der Admin-Account wird aktiviert
3. Du wirst zur Login-Seite weitergeleitet

---

## Nach dem Setup

### Login

Nach Abschluss des Wizards wirst du zur Login-Seite weitergeleitet. Melde dich mit dem erstellten Admin-Account an.

### Dashboard

Nach dem Login siehst du das Dashboard mit:

- **Übersicht** über verbundene Environments
- **Status** der deployten Stacks
- **Quick Actions** für häufige Aufgaben

### Nächste Schritte

1. **Environment hinzufügen** (falls übersprungen) – Gehe zu *Environments* und klicke auf *Add Environment*
2. **Stack Sources konfigurieren** – Füge Git-Repositories oder lokale Pfade als Stack-Quellen hinzu
3. **Ersten Stack deployen** – Siehe [Ersten Stack deployen](/getting-started/first-deployment/)

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
# Container stoppen
docker stop readystackgo

# Datenverzeichnis löschen
sudo rm -rf /var/readystackgo/*

# Container neu starten
docker start readystackgo
```

:::danger[Warnung]
Das Löschen des Datenverzeichnisses entfernt alle Konfigurationen und Deployment-Informationen!
:::

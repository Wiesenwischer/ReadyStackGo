---
title: Stack Sources
description: Stack-Quellen verwalten – Katalog, Import/Export und Onboarding
---

ReadyStackGo bezieht seine deploybare Stacks aus konfigurierbaren **Stack Sources** (Quellen). Quellen können lokale Verzeichnisse, Git Repositories oder kuratierte Einträge aus dem **Source Catalog** sein. Sie lassen sich importieren, exportieren und bereits beim Erstsetup im **Onboarding** konfigurieren.

## Übersicht

| Feature | Beschreibung |
|---------|--------------|
| **Source Catalog** | Kuratierte, vorkonfigurierte Git-Quellen mit einem Klick hinzufügen |
| **Import / Export** | Quell-Konfigurationen als JSON sichern und auf andere Instanzen übertragen |
| **Onboarding-Integration** | Beim Erstsetup empfohlene Quellen direkt auswählen |
| **Manuelle Quellen** | Lokale Verzeichnisse oder beliebige Git Repositories hinzufügen |

---

## Schritt für Schritt: Quellen verwalten

### Schritt 1: Settings öffnen

Navigieren Sie zu **Settings** im Hauptmenü. Dort finden Sie die Karte **Stack Sources**.

![Settings-Seite mit Stack Sources Karte](/images/docs/sources-01-settings-nav.png)

---

### Schritt 2: Stack Sources Übersicht

Klicken Sie auf **Stack Sources**. Sie sehen die Liste aller konfigurierten Quellen mit Aktions-Buttons:

- **Add Source** – Neue Quelle hinzufügen
- **Export** – Alle Quellen als JSON exportieren
- **Import** – Quellen aus einer JSON-Datei importieren
- **Sync All** – Alle aktiven Quellen synchronisieren

![Stack Sources Übersichtsseite mit Aktions-Buttons](/images/docs/sources-02-list-page.png)

Jede Quelle zeigt Typ (Git / Local), Status (Enabled / Disabled) und bietet die Aktionen **Sync**, **Disable/Enable** und **Delete**.

---

## Quelle aus dem Katalog hinzufügen

Der **Source Catalog** enthält kuratierte Git Repositories, die mit einem Klick hinzugefügt werden können.

### Schritt 1: Quelltyp wählen

Klicken Sie auf **Add Source**. Sie sehen drei Optionen:

- **Local Directory** – Lokales Verzeichnis auf dem Server
- **Git Repository** – Beliebiges Git Repository (URL + Branch)
- **From Catalog** – Vorkonfigurierte Quelle aus dem Katalog

Wählen Sie **From Catalog** und klicken Sie **Continue**.

![Quelltyp-Auswahl mit drei Optionen](/images/docs/sources-03-add-type-select.png)

---

### Schritt 2: Katalog durchsuchen

Sie sehen die verfügbaren Katalog-Einträge mit Name, Beschreibung und Anzahl der enthaltenen Stacks. Bereits hinzugefügte Quellen werden mit einem **Already added** Badge gekennzeichnet.

Klicken Sie **Add** bei der gewünschten Quelle.

![Katalog-Seite mit verfügbaren Quellen](/images/docs/sources-04-catalog-browse.png)

:::tip[Duplikat-Erkennung]
ReadyStackGo erkennt bereits hinzugefügte Quellen anhand der Git URL (case-insensitive, ignoriert Trailing Slashes). Doppelte Quellen werden automatisch verhindert.
:::

---

### Schritt 3: Quelle hinzugefügt

Nach dem Hinzufügen werden Sie zur Quellen-Übersicht weitergeleitet. Die neue Quelle erscheint in der Liste und wird automatisch synchronisiert.

![Quellen-Liste nach dem Hinzufügen einer Katalog-Quelle](/images/docs/sources-05-source-added.png)

---

## Import und Export

Stack Source Konfigurationen lassen sich als JSON-Datei exportieren und importieren. Das ist nützlich um Quellen zwischen Instanzen zu übertragen oder als Backup.

### Export

Klicken Sie auf **Export** in der Quellen-Übersicht. Eine JSON-Datei mit dem Namensformat `rsgo-sources-YYYY-MM-DD.json` wird heruntergeladen.

### Import

Klicken Sie auf **Import** und wählen Sie eine zuvor exportierte JSON-Datei. Die Import-Logik:

- **Neue Quellen** werden angelegt
- **Duplikate** (gleiche Git URL oder gleicher Pfad) werden übersprungen
- **Unbekannte Typen** werden ignoriert
- Bei Git-Quellen ohne Branch wird `main` als Default gesetzt
- Bei lokalen Quellen ohne File Pattern wird `*.yml;*.yaml` als Default gesetzt

![Export und Import Buttons in der Quellen-Übersicht](/images/docs/sources-06-export-import.png)

:::note[Import-Format]
Die JSON-Datei enthält Version, Zeitstempel und ein Array von Quellen mit Name, Typ, Enabled-Status und typ-spezifischen Feldern (Git URL/Branch oder Pfad/File Pattern).
:::

---

## Onboarding-Integration

Beim Erstsetup von ReadyStackGo wird im **Onboarding** (Schritt 3) die Konfiguration von Stack Sources angeboten. Das Onboarding zeigt die verfügbaren Katalog-Einträge mit Checkboxen an:

- **Featured** Quellen sind vorausgewählt
- Sie können beliebig viele Quellen an- oder abwählen
- Mit **Add N source(s)** werden die ausgewählten Quellen hinzugefügt
- Mit **Skip for now** überspringen Sie den Schritt

![Onboarding Schritt 3 – Stack Sources Auswahl](/images/docs/wizard-05-onboarding-sources.png)

:::tip[Nachträglich ergänzen]
Quellen können jederzeit über **Settings → Stack Sources** hinzugefügt, geändert oder entfernt werden – der Onboarding-Schritt ist optional.
:::

---

## Manuelle Quellen

Neben dem Katalog können Sie auch manuell Quellen hinzufügen:

### Local Directory

Ein Verzeichnis auf dem Server, das Stack-Manifest-Dateien enthält.

| Feld | Beschreibung |
|------|--------------|
| **Name** | Anzeigename der Quelle |
| **Path** | Absoluter Pfad zum Verzeichnis (z.B. `/opt/stacks`) |
| **File Pattern** | Glob-Pattern für Manifest-Dateien (Default: `*.yml;*.yaml`) |

### Git Repository

Ein Git Repository, das Stack-Manifest-Dateien enthält.

| Feld | Beschreibung |
|------|--------------|
| **Name** | Anzeigename der Quelle |
| **Git URL** | Repository URL (z.B. `https://github.com/org/stacks.git`) |
| **Branch** | Branch-Name (Default: `main`) |
| **SSL Verify** | SSL-Zertifikatsprüfung (Default: aktiviert) |

---

## Fehlerbehandlung

| Situation | Verhalten |
|-----------|-----------|
| Git-Quelle ohne URL | Quelle wird beim Import übersprungen |
| Lokale Quelle ohne Pfad | Quelle wird beim Import übersprungen |
| Duplikat erkannt | Quelle wird übersprungen (kein Fehler) |
| Unbekannter Quelltyp | Quelle wird übersprungen |
| Sync fehlgeschlagen | Fehlermeldung in der Quellen-Übersicht, andere Quellen werden weiter synchronisiert |

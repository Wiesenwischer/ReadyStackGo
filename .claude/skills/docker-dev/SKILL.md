---
name: docker-dev
description: Build and start ReadyStackGo container for manual testing
argument-hint: "[clean]"
---

# Docker Container bauen und starten

Baue das Docker-Image und starte den Container für manuelle Tests.

**Argument**: $ARGUMENTS

---

## Ablauf

### Schritt 1: Clean-Modus bestimmen

Prüfe ob der Container mit frischen Volumes gestartet werden soll:

- Falls `$ARGUMENTS` den Wert `clean` enthält → **Clean-Modus aktiv** (Volumes löschen, Setup-Wizard läuft erneut)
- Falls `$ARGUMENTS` leer ist → Frage den User:

```
Sollen die Volumes gelöscht werden?
Dies ist nötig wenn sich Config, Datenbank-Schema oder Seed-Daten geändert haben.
Der Setup-Wizard muss dann erneut durchlaufen werden.

Optionen:
1. Nein, nur neu bauen und starten (Recommended) - Daten und Config bleiben erhalten
2. Ja, Volumes löschen (Clean Start) - Setup-Wizard läuft erneut, alle Daten gehen verloren
```

Nutze die `AskUserQuestion` Tool dafür.

### Schritt 2: Container stoppen (falls laufend)

```bash
docker compose down
```

Falls Clean-Modus:
```bash
docker compose down -v
```

Das `-v` Flag entfernt die Volumes `rsgo-config` und `rsgo-data`.

### Schritt 3: Container bauen

```bash
docker compose build
```

Warte auf Erfolg. Bei Fehler: Abbrechen und Fehler anzeigen.

### Schritt 4: Container starten

```bash
docker compose up -d
```

### Schritt 5: Status prüfen

```bash
docker compose ps
```

Zeige dem User:
- Container-Status
- URL: **http://localhost:8080**
- Falls Clean-Modus: Hinweis dass der Setup-Wizard durchlaufen werden muss
- Falls Fehler: `docker compose logs readystackgo` ausgeben

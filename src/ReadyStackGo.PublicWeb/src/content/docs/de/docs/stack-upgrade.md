---
title: Stack Upgrade & Rollback
description: Anleitung zum Aktualisieren von Stacks und automatischem Rollback bei Fehlern
---

Diese Anleitung erklärt, wie Sie bestehende Deployments auf neuere Versionen aktualisieren und wie der automatische Rollback-Mechanismus Ihre Deployments schützt.

## Überblick

ReadyStackGo bietet integrierte Unterstützung für:

1. **Versionserkennung** - Automatische Prüfung auf verfügbare Updates
2. **Stack Upgrade** - Aktualisierung eines laufenden Deployments auf eine neue Version
3. **Automatisches Rollback** - Wiederherstellung des vorherigen Zustands bei Upgrade-Fehlern

---

## Upgrade-Prozess

### Wie Upgrades funktionieren

Bei einem Upgrade führt ReadyStackGo folgende Schritte durch:

1. **Snapshot erstellen** - Erfasst den aktuellen Zustand (Version, Variablen, Services)
2. **Neue Images laden** - Lädt die Container-Images der neuen Version herunter
3. **Neue Container starten** - Startet die aktualisierten Services
4. **Point of No Return** - Sobald die Container laufen, ist das Upgrade committed
5. **Snapshot löschen** - Entfernt den Snapshot nach erfolgreichem Upgrade

### Auf Updates prüfen

Auf der Deployment-Detailseite prüft ReadyStackGo automatisch, ob eine neuere Version im Katalog verfügbar ist. Bei verfügbarem Upgrade erscheint ein blaues Banner:

**"Upgrade verfügbar: 1.0.0 → 2.0.0"**

Das Banner zeigt außerdem:
- **Neue Variablen** - In der neuen Version hinzugefügte Variablen
- **Entfernte Variablen** - Variablen, die nicht mehr existieren

### Ein Upgrade durchführen

1. Navigieren Sie zu **Deployments** und wählen Sie Ihr Deployment
2. Wenn ein Upgrade verfügbar ist, klicken Sie auf den **Upgrade**-Button
3. Bestätigen Sie das Upgrade im Dialog
4. Überwachen Sie den Fortschritt in Echtzeit über die Live-Fortschrittsanzeige

:::caution[Voraussetzungen]
Ein Deployment muss im Status **Running** sein, um aktualisiert zu werden. Gestoppte, fehlgeschlagene oder ausstehende Deployments können nicht direkt aktualisiert werden.
:::

### Echtzeit-Fortschrittsverfolgung

ReadyStackGo bietet Live-Fortschrittsupdates während Upgrades via SignalR:

- **Fortschrittsbalken** - Zeigt den Gesamtfortschritt in Prozent
- **Aktuelle Phase** - Zeigt die aktuelle Operation an (Images laden, Services starten, etc.)
- **Service-Zähler** - Zeigt, wie viele Services bereits verarbeitet wurden
- **Live-Status** - Grüner Indikator bestätigt Echtzeit-Verbindung

Die Fortschrittsupdates erfolgen automatisch ohne Seitenaktualisierung. Bei temporärem Verbindungsverlust zeigt die UI "Verbindung wird wiederhergestellt..." an und setzt die Updates nach Wiederherstellung fort.

### Variablenbehandlung beim Upgrade

Beim Upgrade auf eine neue Version:

- **Bestehende Variablen** werden beibehalten und mit der neuen Konfiguration zusammengeführt
- **Neue Pflichtfelder** müssen vor dem Upgrade-Start konfiguriert werden
- **Entfernte Variablen** werden nicht mehr verwendet, aber die Werte bleiben im Snapshot erhalten

---

## Rollback-Mechanismus

### Point of No Return Semantik

ReadyStackGo verwendet ein "Point of No Return"-Modell für Upgrades:

| Phase | Rollback möglich? | Beschreibung |
|-------|-------------------|--------------|
| Snapshot erstellt | Nein | Upgrade hat noch nicht begonnen |
| Images laden | Ja | Bei Fehler beim Image-Pull ist Rollback möglich |
| Container starten | Ja | Bei Fehler beim Container-Start ist Rollback möglich |
| **Container laufen** | **Nein** | Point of No Return überschritten |
| Upgrade abgeschlossen | Nein | Snapshot wurde gelöscht |

### Wann ein Rollback verfügbar ist

Ein Rollback ist verfügbar, wenn:
- Das Upgrade **vor dem Containerstart** fehlgeschlagen ist
- Ein **Snapshot existiert** vom fehlgeschlagenen Upgrade-Versuch

Wenn Sie ein bernsteinfarbenes "Rollback verfügbar"-Banner auf der Deployment-Detailseite sehen, können Sie den vorherigen Zustand wiederherstellen.

### Ein Rollback durchführen

1. Navigieren Sie zum fehlgeschlagenen Deployment
2. Klicken Sie auf den **Rollback**-Button im bernsteinfarbenen Banner
3. Bestätigen Sie den Rollback auf der dedizierten Rollback-Seite
4. Überwachen Sie den Rollback-Fortschritt in Echtzeit
5. Das Deployment wird auf die vorherige Version zurückgesetzt

:::note[Was wird wiederhergestellt]
Ein Rollback stellt wieder her:
- **Stack Version** - Kehrt zur vorherigen Version zurück
- **Variablen** - Stellt die ursprünglichen Variablenwerte wieder her
- **Services** - Stellt die vorherigen Container-Images wieder bereit
:::

### Rollback-Fortschrittsverfolgung

Genau wie bei Upgrades bieten auch Rollbacks Echtzeit-Fortschrittsupdates:

- **Fortschrittsbalken** - Zeigt den Rollback-Fortschritt in Prozent
- **Aktuelle Phase** - Zeigt Operationen wie "Alte Container entfernen", "Images laden", "Services starten"
- **Live-Updates** - Automatische Updates via SignalR-Verbindung

Die Rollback-Seite zeigt vor dem Start einen Bestätigungsbildschirm, der Ihnen ermöglicht zu überprüfen, auf welche Version zurückgesetzt wird.

---

## API-Endpunkte

### Auf Upgrade prüfen

```http
GET /api/environments/{environmentId}/deployments/{deploymentId}/upgrade/check
```

**Antwort:**
```json
{
  "success": true,
  "upgradeAvailable": true,
  "currentVersion": "1.0.0",
  "latestVersion": "2.0.0",
  "latestStackId": "source:product:stack",
  "newVariables": ["NEW_VAR"],
  "removedVariables": ["OLD_VAR"],
  "canUpgrade": true,
  "cannotUpgradeReason": null
}
```

### Upgrade durchführen

```http
POST /api/environments/{environmentId}/deployments/{deploymentId}/upgrade
Content-Type: application/json

{
  "stackId": "source:product:stack",
  "variables": {
    "NEW_VAR": "value"
  },
  "sessionId": "signalr-session-id"
}
```

### Rollback-Info abrufen

```http
GET /api/environments/{environmentId}/deployments/{deploymentId}/rollback
```

**Antwort:**
```json
{
  "canRollback": true,
  "rollbackTargetVersion": "1.0.0",
  "snapshotDescription": "Before upgrade to v2.0.0",
  "snapshotCreatedAt": "2024-01-15T10:30:00Z"
}
```

### Rollback durchführen

```http
POST /api/environments/{environmentId}/deployments/{deploymentId}/rollback
Content-Type: application/json

{
  "sessionId": "signalr-session-id"
}
```

Die `sessionId` ist optional, aber für Echtzeit-Fortschrittsverfolgung empfohlen. Wenn angegeben, sollte der Client sich beim SignalR Deployment Hub mit dieser Session-ID anmelden, bevor der Endpunkt aufgerufen wird.

---

## Best Practices

### Vor dem Upgrade

1. **Release Notes prüfen** - Überprüfen Sie, was sich in der neuen Version geändert hat
2. **Daten sichern** - Wenn Ihr Stack persistente Volumes verwendet, erwägen Sie ein Backup
3. **In Entwicklungsumgebung testen** - Probieren Sie das Upgrade zuerst in einer Nicht-Produktionsumgebung
4. **Wartung planen** - Verwenden Sie den Wartungsmodus, um Alarme während des Upgrades zu vermeiden

### Umgang mit fehlgeschlagenen Upgrades

Wenn ein Upgrade fehlschlägt:

1. **Fehlermeldung prüfen** - Verstehen Sie, warum das Upgrade fehlgeschlagen ist
2. **Logs überprüfen** - Prüfen Sie Container-Logs für detaillierte Fehlerinformationen
3. **Rollback durchführen** - Nutzen Sie die Rollback-Funktion, um den vorherigen Zustand wiederherzustellen
4. **Problem beheben** - Beheben Sie die Ursache, bevor Sie das Upgrade erneut versuchen

### Versionsstrategie

- **Aktuell bleiben** - Regelmäßige Updates enthalten Sicherheitsfixes und Verbesserungen
- **Erst testen** - Testen Sie Upgrades immer in Nicht-Produktionsumgebungen
- **Änderungen dokumentieren** - Behalten Sie den Überblick, welche Versionen wo eingesetzt sind

---

## Fehlerbehebung

### "Upgrade nicht möglich" Meldung

| Grund | Lösung |
|-------|--------|
| "Deployment muss laufen" | Starten Sie das Deployment zuerst, dann upgraden |
| "Keine neuere Version verfügbar" | Die aktuelle Version ist bereits die neueste |
| "Produkt nicht im Katalog gefunden" | Stellen Sie sicher, dass die Stack-Quelle noch konfiguriert ist |

### Rollback nicht verfügbar

Rollback ist nur verfügbar, wenn:
- Das Deployment im Status **Failed** ist
- Ein **Snapshot existiert** vom fehlgeschlagenen Upgrade

Wenn das Upgrade den Point of No Return passiert hat (Container wurden gestartet), ist der Snapshot bereits gelöscht und ein Rollback nicht mehr möglich.

---

## Weiterführende Dokumentation

- [Stack Deployment](/de/docs/stack-deployment/)
- [RSGo Manifest Format](/de/reference/manifest-format/)
- [Variable Types](/de/reference/variable-types/)

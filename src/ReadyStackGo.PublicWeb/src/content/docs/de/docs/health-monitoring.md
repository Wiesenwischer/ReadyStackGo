---
title: Health Monitoring
description: Echtzeit-Überwachung aller Deployments mit Status-Dashboard, Service-Details und Health History Timeline
---

ReadyStackGo überwacht kontinuierlich den Zustand aller deployten Stacks. Das **Health Dashboard** zeigt den aktuellen Status aller Services in Echtzeit, erkennt Probleme automatisch und bietet eine detaillierte Timeline mit Statusverläufen.

## Übersicht

| Funktion | Beschreibung |
|----------|--------------|
| **Health Dashboard** | Gesamtübersicht aller Stacks mit Statusfiltern und Suche |
| **Summary Cards** | Schnelle Übersicht: Healthy, Degraded, Unhealthy, Total |
| **Stack Cards** | Aufklappbare Karten pro Stack mit Service-Details |
| **Health History** | Uptime-Donut und farbcodierte Status-Timeline |
| **Per-Service Timeline** | Swim-Lane-Diagramm für jeden einzelnen Service |
| **Service Detail** | Detailansicht mit Health Check Entries und Response Times |
| **Maintenance Mode** | Geplante Wartungsfenster visuell unterscheidbar |
| **Echtzeit-Updates** | Live-Verbindung via SignalR für sofortige Status-Änderungen |

---

## Schritt für Schritt: Health Dashboard

### Schritt 1: Dashboard öffnen

Navigieren Sie über die Sidebar zu **Health**. Das Dashboard zeigt eine Übersicht aller Deployments im aktiven Environment.

![Health Dashboard Übersicht](/images/docs/health-01-dashboard-overview.png)

---

### Schritt 2: Summary Cards lesen

Die vier Summary Cards am oberen Rand zeigen auf einen Blick:

- **Healthy** (grün) — Anzahl der Stacks, bei denen alle Services laufen
- **Degraded** (gelb) — Stacks mit teilweisen Problemen
- **Unhealthy** (rot) — Stacks mit kritischen Problemen
- **Total** — Gesamtanzahl der überwachten Stacks

![Summary Cards mit Statusübersicht](/images/docs/health-02-summary-cards.png)

---

### Schritt 3: Stack-Details aufklappen

Klicken Sie auf eine Stack-Karte, um die einzelnen Services zu sehen. Jeder Service zeigt:
- **Name** und Container-Name
- **Status** als farbige Badge (Healthy/Degraded/Unhealthy)
- **Response Time** für HTTP Health Checks
- **Restart Count** bei Problemen

![Stack-Karte aufgeklappt mit Service-Details](/images/docs/health-03-stack-expanded.png)

:::tip[View Details]
Klicken Sie auf **View Details** am Ende der aufgeklappten Karte, um zur vollständigen Deployment-Detailseite zu navigieren.
:::

---

### Schritt 4: Nach Status filtern

Nutzen Sie die Statusfilter-Buttons, um nur Stacks eines bestimmten Status anzuzeigen:
- **All** — Alle Stacks anzeigen
- **Healthy** — Nur gesunde Stacks
- **Degraded** — Nur degradierte Stacks
- **Unhealthy** — Nur problematische Stacks

![Dashboard mit aktivem Statusfilter](/images/docs/health-07-filter-status.png)

---

### Schritt 5: Stacks suchen

Das Suchfeld filtert Stacks in Echtzeit nach Name. Tippen Sie den Stack-Namen ein, um schnell einen bestimmten Stack zu finden.

![Suche nach Stack-Namen](/images/docs/health-08-search.png)

---

## Deployment Detail & Health History

Auf der Deployment-Detailseite finden Sie umfassende Health-Informationen:

### Health Summary

Die Summary-Karte zeigt den aktuellen Status auf einen Blick: Anzahl gesunder Services, Operation Mode und Statusnachricht.

![Deployment Detail mit Health-Informationen](/images/docs/health-04-deployment-detail.png)

---

### Health History Timeline

Die Health History zeigt den Statusverlauf des gesamten Deployments:

- **Uptime Donut** (links) — Prozentuale Verteilung der Betriebszeit nach Status
- **Status-Band** — Farbcodierte Timeline: Grün = Healthy, Gelb = Degraded, Rot = Unhealthy, Blau = Maintenance
- **Per-Service Swim Lanes** — Individuelle Timeline pro Service (nur bei mehreren Services)
- **Tooltip** — Hover über die Timeline zeigt Details: Zeitpunkt, Status und Zustand jedes einzelnen Services

![Health History mit Uptime-Donut und Timeline](/images/docs/health-05-history-chart.png)

:::note[Dynamische Timeline]
Die Timeline zeigt nur **Status-Übergänge** — Zeitpunkte, an denen sich der Health-Status geändert hat. Ein stabiles Deployment zeigt wenige Einträge über lange Zeiträume, ein instabiles zeigt viele schnelle Wechsel.
:::

---

### Services-Liste

Unterhalb der Health History sehen Sie die aktuelle Liste aller Services mit ihrem Status, Response Time und Restart Count.

![Services-Liste auf der Deployment-Detailseite](/images/docs/health-06-services-list.png)

---

## Maintenance Mode

ReadyStackGo unterscheidet zwischen **geplanten Wartungsfenstern** und **echten Problemen**:

- **Enter Maintenance** — Button auf der Deployment-Detailseite, stoppt Container planmäßig
- **Exit Maintenance** — Startet Container wieder und kehrt zum normalen Betrieb zurück
- **Visuelle Unterscheidung** — Maintenance-Perioden werden in der Timeline **blau** dargestellt
- **Uptime-Berechnung** — Maintenance-Zeit wird separat ausgewiesen und beeinflusst die Uptime-% nicht negativ

:::caution[Container-Stopp]
Im Maintenance Mode werden Container tatsächlich gestoppt (außer solche mit dem Label `rsgo.maintenance=ignore`). Services erscheinen als Unhealthy, da die Container nicht laufen — dies ist beabsichtigt.
:::

---

## Echtzeit-Updates

Das Health Dashboard nutzt **SignalR** für Echtzeit-Updates:

- **Live** (grüner Punkt) — Verbindung aktiv, Updates werden sofort angezeigt
- **Connecting...** (gelber Punkt) — Verbindung wird aufgebaut
- **Offline** (grauer Punkt) — Keine Verbindung, manuelle Aktualisierung über **Refresh** Button

Health Checks werden standardmäßig alle **30 Sekunden** durchgeführt. Änderungen am Status lösen sofort eine Notification aus.

---

## Health Check Konfiguration

Services können HTTP Health Checks über Docker Labels konfigurieren:

```yaml
services:
  api:
    image: myapp/api:latest
    labels:
      rsgo.healthcheck.path: /hc
      rsgo.healthcheck.port: "8080"
```

| Label | Beschreibung |
|-------|-------------|
| `rsgo.healthcheck.path` | HTTP-Pfad für den Health Check (z.B. `/hc`, `/health`) |
| `rsgo.healthcheck.port` | Port für den Health Check (muss exposed sein) |

Services **ohne** Health Check Labels werden über den Docker Container Status überwacht (Running/Stopped/Restarting).

Services **mit** Health Check Labels erhalten zusätzlich HTTP-basierte Prüfungen und können detaillierte Health Check Entries liefern (z.B. Datenbank-Konnektivität, Disk Space, externe Services).

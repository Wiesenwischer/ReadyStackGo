---
title: Stack Deployment
description: Schritt-für-Schritt Anleitung zum Deployen eines Stacks mit Screenshots
---

Diese Anleitung zeigt Ihnen, wie Sie einen Stack aus dem Katalog auswählen, konfigurieren und deployen. Die Screenshots zeigen jeden Schritt im Detail.

## Übersicht

Das Deployment eines Stacks in ReadyStackGo erfolgt in wenigen einfachen Schritten:

1. Anmeldung am System
2. Navigation zum Stack Catalog
3. Auswahl eines Produkts
4. Konfiguration der Variablen
5. Deployment starten
6. Status überwachen

---

## Schritt 1: Anmeldung

Öffnen Sie die ReadyStackGo Web-Oberfläche in Ihrem Browser. Sie werden mit dem Login-Bildschirm begrüßt, wo Sie Ihre Zugangsdaten eingeben.

![ReadyStackGo Login-Seite](/images/docs/01-login.png)

- Geben Sie Ihren **Benutzernamen** ein
- Geben Sie Ihr **Passwort** ein
- Klicken Sie auf **Sign In**

---

## Schritt 2: Dashboard

Nach erfolgreicher Anmeldung gelangen Sie zum Dashboard. Hier sehen Sie eine Übersicht Ihrer Umgebungen (Environments) und aktiven Deployments.

![ReadyStackGo Dashboard](/images/docs/02-dashboard.png)

---

## Schritt 3: Stack Catalog

Navigieren Sie zum **Stack Catalog** über das Hauptmenü. Der Katalog zeigt alle verfügbaren Produkte, die Sie deployen können.

![Stack Catalog mit verfügbaren Produkten](/images/docs/03-catalog.png)

Jedes Produkt zeigt:

- **Name** und **Version**
- **Beschreibung** des Produkts
- **Kategorie** zur einfachen Filterung
- **Tags** für die Suche

---

## Schritt 4: Produkt-Details

Klicken Sie auf ein Produkt, um die Detail-Seite zu öffnen. Hier finden Sie ausführliche Informationen sowie verfügbare Versionen.

![Produkt-Detailseite](/images/docs/04-product-detail.png)

Auf dieser Seite können Sie:

- Die vollständige **Produktbeschreibung** lesen
- Die enthaltenen **Stacks** sehen
- Eine **Version** auswählen
- Mit **Deploy** zum Deployment übergehen

---

## Schritt 5: Deployment konfigurieren

Auf der Deploy-Seite konfigurieren Sie alle notwendigen Variablen für Ihr Deployment. Die Variablen sind nach Gruppen organisiert.

![Deploy-Konfigurationsseite](/images/docs/05-deploy-configure.png)

### Stack Name

Geben Sie einen eindeutigen **Stack Name** ein. Dieser Name wird verwendet, um das Deployment zu identifizieren und muss eindeutig sein.

### Variablen konfigurieren

Füllen Sie die erforderlichen Variablen aus. Je nach Variablentyp werden verschiedene Eingabefelder angezeigt:

- **String:** Einfaches Textfeld
- **Password:** Maskiertes Passwortfeld
- **Port:** Portauswahl mit Validierung (1-65535)
- **Boolean:** Toggle-Schalter
- **Select:** Dropdown-Auswahl
- **Connection String:** Builder-Dialog für Datenbankverbindungen

:::tip[.env Import]
Haben Sie bereits eine `.env` Datei mit Ihren Konfigurationswerten? Klicken Sie auf **Import .env** in der Sidebar, um alle passenden Variablen automatisch zu übernehmen!

Unterstützte Formate:
- Zeilen mit `#` werden als Kommentare ignoriert
- Werte können in Anführungszeichen stehen: `"value"` oder `'value'`
- Nur im Manifest definierte Variablen werden importiert
:::

### Deployment starten

Sobald alle Pflichtfelder ausgefüllt sind, klicken Sie auf den **Deploy** Button in der Sidebar. Das Deployment wird gestartet und Sie werden zur Deployment-Übersicht weitergeleitet.

---

## Schritt 6: Deployments überwachen

In der **Deployments**-Übersicht sehen Sie alle aktiven und vergangenen Deployments mit ihrem aktuellen Status.

![Deployments-Übersicht](/images/docs/08-deployments-list.png)

Für jedes Deployment sehen Sie:

- **Stack Name:** Der von Ihnen vergebene Name
- **Status:** Running, Stopped, Error
- **Services:** Anzahl der Container
- **Environment:** Die Ziel-Umgebung

---

## Nächste Schritte

Nach dem erfolgreichen Deployment können Sie:

- Den Stack-Status in Echtzeit überwachen
- Container-Logs einsehen
- Den Stack stoppen oder neu starten
- Variablen ändern und neu deployen
- Den Stack löschen

### Weiterführende Dokumentation

- [RSGo Manifest Format](/de/docs/reference/manifest-format/)
- [Variablentypen](/de/docs/reference/variable-types/)

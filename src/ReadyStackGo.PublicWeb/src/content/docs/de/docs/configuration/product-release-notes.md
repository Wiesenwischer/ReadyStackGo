---
title: Produkt-Updates & Release Notes
description: Sichtbar machen, wenn eine neuere Version eines installierten Produkts verfügbar ist, und die Release Notes direkt im UI anzeigen — inkl. Manifest-Felder (releaseNotesUrl) und CHANGELOG.md-Konvention.
---

ReadyStackGo zeigt an, wenn für ein installiertes **Produkt-Deployment** eine neuere Version
im Katalog verfügbar ist, und macht die zugehörigen **Release Notes** direkt im UI
zugänglich. So bekommt die Entscheidung „upgraden ja/nein" Kontext.

## Übersicht

| Element | Beschreibung |
|---------|--------------|
| Update-Badge | Auf der Produkt-Deployment-Detailseite, wenn eine neuere Version vorliegt |
| Release Notes | Eigene `CHANGELOG.md` (gerendert) **oder** externe `releaseNotesUrl` (als Link) |
| Benachrichtigung | Einmalige Notification nach dem Source-Sync, wenn ein Update erscheint |

---

## Release Notes im Manifest hinterlegen

Release Notes werden pro Produktversion aus zwei Quellen bezogen — eine **eigene
`CHANGELOG.md`** (bevorzugt) und eine **externe URL** als Fallback.

### Variante A: CHANGELOG.md neben dem Manifest (empfohlen)

Lege eine `CHANGELOG.md` **im selben Verzeichnis** wie die Produkt-Manifest-Datei ab. Beim
Sync wird sie automatisch eingelesen und im UI als formatiertes Markdown angezeigt.

```
my-product/
├── rsgo.yaml          # Produkt-Manifest (productVersion: "1.1.0")
└── CHANGELOG.md       # wird automatisch als Release Notes übernommen
```

### Variante B: releaseNotesUrl im Manifest

Alternativ (oder zusätzlich) kann im Manifest-Metadatenblock eine URL angegeben werden, z. B.
auf ein GitHub-Release:

```yaml
metadata:
  name: "My Product"
  productId: "com.example.myproduct"
  productVersion: "1.1.0"
  releaseNotesUrl: "https://github.com/example/my-product/releases/tag/v1.1.0"
```

:::note[Vorrang]
Ist eine `CHANGELOG.md` vorhanden, wird sie **bevorzugt** und direkt im Viewer gerendert.
Eine `releaseNotesUrl` wird nur als **externer Link** angezeigt — sie wird aus
Sicherheitsgründen **nicht** serverseitig geladen (SSRF-Schutz).
:::

Details zum Manifest-Format: siehe [Manifest-Format](/de/reference/manifest-format/).

---

## Schritt für Schritt: Update entdecken & Release Notes lesen

### Schritt 1: Update-Badge auf der Deployment-Detailseite

Liegt im Katalog eine neuere Version desselben Produkts vor (ermittelt per SemVer beim
Source-Sync), erscheint auf der Produkt-Deployment-Detailseite ein **„Update available"**-Badge
mit der Zielversion. Hat die Zielversion Release Notes, wird zusätzlich ein
**„Release notes"**-Link angezeigt.

![Update-Badge auf der Produkt-Deployment-Detailseite](/images/docs/product-release-notes-01-badge.png)

### Schritt 2: Release Notes ansehen

Ein Klick auf **„Release notes"** öffnet den Viewer. Liegt eine eigene `CHANGELOG.md` vor,
wird sie als formatiertes Markdown dargestellt; bei einer externen `releaseNotesUrl` wird ein
Link angezeigt.

![Release-Notes-Viewer mit gerendertem Changelog](/images/docs/product-release-notes-02-viewer.png)

:::tip[Benachrichtigung]
Nach jedem Source-Sync prüft ReadyStackGo aktive Produkt-Deployments auf neuere Versionen und
erzeugt **einmalig** eine Benachrichtigung pro Deployment und Zielversion (kein Spam bei
wiederholtem Sync).
:::

---

## Wie die Update-Erkennung funktioniert

1. Ein **Source-Sync** lädt die Produktversionen aus den konfigurierten Quellen (lokal, Git,
   OCI-Registry). Mehrere Versionen desselben Produkts können nebeneinander existieren.
2. Pro aktivem Produkt-Deployment vergleicht RSGO die installierte mit der neuesten
   verfügbaren Version (**strikt SemVer-höher**, kein Downgrade).
3. Gibt es eine neuere Version, erscheint der Badge und — einmalig — eine Benachrichtigung.
4. Die Release Notes der Zielversion werden bei Bedarf nachgeladen.

---

## Sicherheit

- Eigene `CHANGELOG.md` aus den konfigurierten Quellen wird serverseitig geladen und als
  **bereinigtes** Markdown gerendert.
- Externe `releaseNotesUrl` werden **niemals** serverseitig abgerufen, sondern nur als Link
  angezeigt (Schutz vor SSRF).

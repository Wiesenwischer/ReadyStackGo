---
title: Produkt-Updates & Release Notes
description: Sichtbar machen, wenn eine neuere Version eines installierten Produkts verfügbar ist, und die Release Notes auf einer eigenen, lokalisierten Seite lesen — inkl. Manifest-Felder (releaseNotesUrl) und CHANGELOG.md-Konvention.
---

ReadyStackGo zeigt an, wenn für ein installiertes **Produkt** eine neuere Version im Katalog
verfügbar ist, und macht die zugehörigen **Release Notes** auf einer eigenen, teilbaren Seite
zugänglich. So bekommt die Entscheidung „upgraden ja/nein" Kontext. Release Notes können
**lokalisiert** sein (z. B. Deutsch und Englisch) — mit einem Sprachumschalter direkt auf der
Seite.

## Übersicht

| Element | Beschreibung |
|---------|--------------|
| Update-Badge | Auf der Produkt-Deployment-Detailseite, wenn eine neuere Version vorliegt |
| Einstieg über Katalog | Ein **Release Notes**-Button auf der Produktseite im Stack-Katalog |
| Eigene Seite | Eine vollwertige Seite unter `/release-notes/:productId` — bookmarkbar, mit der gewählten Sprache in der URL |
| Release Notes | Eigene `CHANGELOG.md` (gerendertes Markdown, inkl. GFM-Tabellen) **oder** externe `releaseNotesUrl` (als Link) |
| Lokalisierung | Pro Sprache eine `CHANGELOG.<locale>.md` mit Sprachumschalter auf der Seite |
| Benachrichtigung | Einmalige Notification nach dem Source-Sync, wenn ein Update erscheint |

---

## Release Notes im Manifest hinterlegen

Release Notes werden pro Produktversion aus zwei Quellen bezogen — einer **eigenen
`CHANGELOG.md`** (bevorzugt) und einer **externen URL** als Fallback.

### Variante A: CHANGELOG.md neben dem Manifest (empfohlen)

Lege eine `CHANGELOG.md` **im selben Verzeichnis** wie die Produkt-Manifestdatei ab. Sie wird
beim Sync automatisch eingelesen und im UI als formatiertes Markdown gerendert.

```
my-product/
├── rsgo.yaml              # Produkt-Manifest (productVersion: "1.1.0")
├── CHANGELOG.md           # neutraler Fallback, wird automatisch übernommen
├── CHANGELOG.de.md        # optional: deutsche Release Notes
└── CHANGELOG.en.md        # optional: englische Release Notes
```

Existieren eine oder mehrere `CHANGELOG.<locale>.md`, zeigt der Viewer einen **Sprachumschalter**
und wählt standardmäßig die Browsersprache des Lesers; fehlt die angeforderte Sprache, dient die
neutrale `CHANGELOG.md` als Fallback.

### Variante B: releaseNotesUrl im Manifest

Alternativ (oder zusätzlich) kann eine URL im Metadata-Block des Manifests angegeben werden,
z. B. zu einem GitHub-Release:

```yaml
metadata:
  name: "My Product"
  productId: "com.example.myproduct"
  productVersion: "1.1.0"
  releaseNotesUrl: "https://github.com/example/my-product/releases/tag/v1.1.0"
```

:::note[Vorrang]
Ist eine `CHANGELOG.md` vorhanden, wird sie **bevorzugt** und direkt auf der Seite gerendert.
Eine `releaseNotesUrl` wird nur als **externer Link** angezeigt — sie wird aus Sicherheitsgründen
**nicht** serverseitig abgerufen (SSRF-Schutz).
:::

Das vollständige Manifest-Schema findest du unter [Manifest-Format](/de/reference/manifest-format/).

---

## Schritt für Schritt: Update entdecken & Release Notes lesen

### Schritt 1: Release Notes aus dem Stack-Katalog öffnen

Jede Katalog-Produktversion, deren Changelog verfügbar ist, zeigt auf ihrer Stack-Katalog-Seite
einen **Release Notes**-Button — so lassen sich die Notes schon vor dem Deploy oder Upgrade
lesen, ganz ohne bestehendes Deployment.

![Release-Notes-Button auf der Stack-Katalog-Produktseite](/images/docs/product-release-notes-01-catalog-button.png)

### Schritt 2: Notes auf der eigenen Seite lesen

Der Button öffnet die eigene **Release-Notes-Seite**. Eine eigene `CHANGELOG.md` wird als
formatiertes Markdown gerendert — inklusive **GFM-Tabellen**, Listen und Überschriften.

![Eigene Release-Notes-Seite mit gerendertem Changelog und Tabelle](/images/docs/product-release-notes-02-page.png)

### Schritt 3: Sprache umschalten (bei Lokalisierung)

Liefert das Produkt mehr als eine `CHANGELOG.<locale>.md`, erscheint im Kopfbereich ein
**Sprachumschalter**. Die gewählte Sprache wird in der URL gespeichert (`?locale=`), sodass die
Seite teil- und bookmarkbar bleibt.

![Release-Notes-Seite mit Sprachumschalter](/images/docs/product-release-notes-03-language.png)

:::tip[Standardsprache]
Die Seite öffnet in der Browsersprache des Lesers, sofern verfügbar; andernfalls dient die
neutrale `CHANGELOG.md` als Fallback.
:::

### Schritt 4: Der Update-Badge als zweiter Einstieg

Existiert im Katalog eine neuere Version eines installierten Produkts (per SemVer beim
Source-Sync ermittelt), zeigt die Produkt-Deployment-Detailseite einen **„Update verfügbar"**-
Badge mit der Zielversion. Hat diese Version Release Notes, öffnet eine **Release Notes**-Aktion
dieselbe eigene Seite.

![Update-Badge auf der Produkt-Deployment-Detailseite](/images/docs/product-release-notes-04-update-badge.png)

:::tip[Benachrichtigung]
Nach jedem Source-Sync prüft ReadyStackGo aktive Produkt-Deployments auf neuere Versionen und
löst **einmalig** pro Deployment und Zielversion eine Benachrichtigung aus (kein Spam bei
wiederholten Syncs).
:::

---

## Wie die Update-Erkennung funktioniert

1. Ein **Source-Sync** lädt Produktversionen aus den konfigurierten Quellen (lokal, Git,
   OCI-Registry). Mehrere Versionen desselben Produkts können koexistieren.
2. Für jedes aktive Produkt-Deployment vergleicht RSGO die installierte Version mit der neuesten
   verfügbaren (**strikt SemVer-neuer**, kein Downgrade).
3. Liegt eine neuere Version vor, erscheint der Badge und — einmalig — eine Benachrichtigung.
4. Die Release Notes der Zielversion werden bei Bedarf auf der eigenen Seite geladen.

---

## Sicherheit

- Eine eigene `CHANGELOG.md` (bzw. `CHANGELOG.<locale>.md`) aus den konfigurierten Quellen wird
  serverseitig geladen und als **sanitized** Markdown gerendert.
- Externe `releaseNotesUrl`-Werte werden **nie** serverseitig abgerufen, sondern nur als Link
  angezeigt (SSRF-Schutz).

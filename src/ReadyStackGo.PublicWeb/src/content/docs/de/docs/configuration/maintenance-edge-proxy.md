---
title: Maintenance Edge-Proxy
description: Überlebender Reverse-Proxy pro Produkt, der während Redeploy und Wartung eine kontrollierte Seite plus maschinenlesbaren Status ausliefert
---

Der **Maintenance Edge-Proxy** („Edge") ist ein von ReadyStackGo verwalteter Reverse-Proxy-Container, der **pro Produkt** vor dessen öffentlichem Eingang läuft. Er **überlebt Redeploys** des Produkts und liefert — gesteuert durch RSGOs autoritativen Deploy-Zustand und das Maintenance-Flag — entweder transparent zum Upstream durch oder eine **kontrollierte Wartungsseite** plus einen **maschinenlesbaren Status**.

Das Feature ist **opt-in pro Produkt** (per Manifest) und **per Default inert**: Ohne `edge:`-Block ändert sich am bestehenden Verhalten nichts.

## Warum gibt es das Feature?

Beim Redeploy oder Wartungsmodus entfernt RSGO die Produkt-Container — inklusive des einzigen öffentlichen Eingangs (z. B. der BFF/Gateway). Clients und Browser bekommen dann **„Connection refused"** statt einer verständlichen Seite. Launcher/Clients haben keine verlässliche Möglichkeit zu erkennen, *warum* der Dienst gerade weg ist.

Der Edge schließt diese Lücke und trennt sauber zwei Ebenen:

| Ebene | Verantwortung | Wer löst sie |
|-------|---------------|--------------|
| **Kontroll-Ebene** | „Soll der Dienst in Wartung?" — das Umschalten | [Maintenance Mode](/de/docs/monitoring/maintenance-mode/) (Flag/Observer) — bereits vorhanden |
| **Erlebnis-Ebene** | „Was sieht der Client, während der Dienst unten ist?" | **Maintenance Edge-Proxy** (diese Seite) |

Vorteile:

- **Kein Connection-Refused** mehr während Redeploy oder Wartung — der Edge bleibt durchgehend erreichbar.
- **Kontrollierte Wartungsseite** statt Browser-Fehlerseite, voll anpassbar.
- **Maschinenlesbarer Status** (`GET /__status`), an dem ein Launcher/Client robust erkennt, ob „läuft", „Wartung" oder „wird deployt".
- **Generisch**: nichts produktspezifisches — jedes Produkt aktiviert es nur per Manifest.

---

## Wie es funktioniert (für Entwickler)

RSGO betreibt den Edge als **separaten Container** (Caddy), den es im Lifecycle verwaltet — er läuft **nicht** im RSGO-Prozess (saubere Control-/Data-Plane-Trennung; ein RSGO-Self-Update erzeugt keinen Traffic-Abriss).

### Routing — aus autoritativem Zustand, nicht geraten

Der Edge rät **nicht** per Health-Check. Eingaben sind ausschließlich RSGOs Deploy-Zustand (`ProductDeployment.Status`) und das Maintenance-Flag (`OperationMode`):

| Deploy-Zustand | Maintenance-Flag | Edge-Verhalten | `/__status` `state` |
|----------------|------------------|----------------|---------------------|
| `Running` | aus | Transparenter Proxy zum Upstream | `running` |
| `Running` | **an** | Wartungsseite, **geplante Wartung** | `maintenance` |
| `Deploying` / `Redeploying` / `Upgrading` | beliebig | Wartungsseite, **vorübergehend nicht erreichbar** | `deploying` |
| `Failed` / `Stopped` / sonst | aus | Wartungsseite, **vorübergehend nicht erreichbar** | `maintenance` |

Das Flag steuert nur die **Wortwahl** (geplante Wartung vs. vorübergehend weg); *ob* proxyt wird, entscheidet allein der Deploy-Zustand.

:::note[Health-Endpunkte werden immer durchgereicht]
`/hc` und `/liveness` werden **auch im Wartungsmodus** zum Upstream durchgereicht (sie erhalten nicht die 503-Seite). Ist der Upstream gestoppt, antwortet er entsprechend (z. B. 502) — der Edge blockiert die Health-Checks aber nie mit seiner eigenen Seite.
:::

### Survival — der Edge überlebt den Redeploy

Beim Teardown eines Stacks entfernt RSGO alle Container mit dem passenden Stack-Label. Der Edge (und ein optionaler produkt-beigesteuerter Wartungs-Container) tragen **Survival-Labels** und werden davon ausgenommen:

| Label | Wert | Bedeutung |
|-------|------|-----------|
| `rsgo.scope` | `edge` | Container gehört zum Edge-Scope → vom Stack-Teardown ausgeschlossen |
| `rsgo.redeploy` | `ignore` | Generischer Opt-out → vom Stack-Teardown ausgeschlossen |
| `rsgo.role` | `maintenance-page` | Produkt-beigesteuerter Wartungs-Container (siehe unten) |

RSGO ist außerdem der **einzige Schreiber** der Edge-Konfiguration: Es berechnet den Soll-Zustand und pusht ihn **atomar und verbindungs-erhaltend** über die Caddy-Admin-API (`POST /load`) — ohne den Edge neu zu starten.

---

## Schritt für Schritt: Edge aktivieren

### Schritt 1: `edge:`-Block ins Produkt-Manifest

Füge den optionalen `edge:`-Block auf Produkt-Ebene hinzu (Geschwister von `metadata:` / `services:`). Der Upstream-Service muss am gemeinsamen externen Netz hängen, damit der Edge ihn per DNS-Alias erreicht:

```yaml
metadata:
  name: ams.project
  productVersion: "1.0.0"

networks:
  ams-project-edge-net:
    external: true            # gemeinsames Netz Edge <-> Upstream

services:
  web-bff:                    # öffentlicher Eingang (BFF/Gateway)
    image: ams/bff:1.0.0
    networks: [ams-project-edge-net]

edge:
  enabled: true
  publicHostname: project.kunde.tld
  publicPort: 443
  upstream:
    service: web-bff          # = Service-/Alias-Name im Netz
    port: 8080
  network: ams-project-edge-net
  tls:
    mode: selfsigned
  maintenancePage:
    mode: default
    branding:
      productName: "ams.project"
      supportContact: support@kunde.tld
      locales: [de, en]
```

### Schritt 2: Produkt deployen

Deploye das Produkt wie gewohnt. RSGO erkennt den `edge:`-Block, provisioniert den Edge-Container und schaltet ihn nach `Running` auf Proxy. Im laufenden Betrieb reicht der Edge transparent durch.

### Schritt 3: Verhalten beobachten

Während eines **Redeploys** bleibt der Edge stehen und liefert die Wartungsseite (`state: deploying`) — kein Connection-Refused. Bei aktivem **Maintenance-Flag** zeigt er die Seite mit „geplanter Wartung" und einem `reason`:

![Default-Wartungsseite des Edge-Proxy](/images/docs/edge-01-maintenance-page.png)

Der maschinenlesbare Status unter `/__status` unterscheidet die Zustände eindeutig:

![/__status im Wartungsmodus](/images/docs/edge-02-status-maintenance.png)

![/__status im Normalbetrieb](/images/docs/edge-04-status-running.png)

---

## a) Wartungsseite anpassen

Es gibt drei Stufen. Der Edge löst sie in der Reihenfolge **container → bundle → default** auf und fällt jeweils zur nächsten Stufe zurück.

### Standard-Seite (Branding-Variablen)

`maintenancePage.mode: default` liefert die mitgelieferte, zweisprachige Seite. Über `branding` passt du sie ohne eigenes HTML an:

```yaml
maintenancePage:
  mode: default
  branding:
    productName: "ams.project"
    logoUrl: https://kunde.tld/logo.svg
    supportContact: support@kunde.tld
    locales: [de, en]
```

| Feld | Beschreibung |
|------|--------------|
| `productName` | Anzeigename auf der Seite / im Browser-Tab |
| `logoUrl` | Optionales Logo (absolute URL) |
| `supportContact` | Optionale Support-Adresse, unten eingeblendet |
| `locales` | Sprachen für die Texte (z. B. `[de, en]`) |

### Eigenes HTML (Bundle)

`maintenancePage.mode: bundle` liefert eine eigene HTML-Seite inline aus. RSGO liest sie **zur Deploy-Zeit** aus dem Manifest-Repository (`bundlePath/index.html`):

```yaml
maintenancePage:
  mode: bundle
  bundlePath: ./maintenance/   # liest ./maintenance/index.html
```

:::caution[Aktuell: einzelne HTML-Seite]
Der Bundle-Modus liefert derzeit eine einzelne, inline ausgelieferte `index.html`. Externe Assets (separate CSS-/Bilddateien) werden noch nicht mitgeliefert — für volle Kontrolle mit eigenen Assets nutze den Container-Modus.
:::

---

## b) Durch eigenen Container ersetzen (volle Kontrolle)

Für volle Anpassbarkeit (eigene Assets, dynamische Inhalte, eigener Webserver) liefert das Produkt **einen eigenen Wartungs-Container** bei, an den der Edge im Wartungsfall durchproxt.

So geht's:

1. `maintenancePage.mode: container` setzen und Service + Port angeben.
2. Den Wartungs-Service mit den Labels `rsgo.role: maintenance-page` **und** `rsgo.redeploy: ignore` versehen (damit er — wie der Edge — Redeploys überlebt).
3. Den Service ans gemeinsame Edge-Netz hängen, damit der Edge ihn per Alias erreicht.

```yaml
networks:
  ams-project-edge-net:
    external: true

services:
  maintenance-web:
    image: ams/maintenance-page:1.0
    networks: [ams-project-edge-net]
    labels:
      rsgo.role: maintenance-page
      rsgo.redeploy: ignore          # überlebt den Redeploy

edge:
  enabled: true
  publicHostname: project.kunde.tld
  upstream: { service: web-bff, port: 8080 }
  network: ams-project-edge-net
  maintenancePage:
    mode: container
    container:
      service: maintenance-web       # = Alias im Netz
      port: 80
```

Im Wartungsfall reicht der Edge die Catch-All-Anfragen an `maintenance-web:80` weiter. Der `GET /__status`-Vertrag und der `/hc`-Durchlass bleiben dabei **unverändert** — nur die sichtbare Seite stammt aus deinem Container.

---

## Manifest-Referenz: der `edge:`-Block

| Feld | Pflicht | Default | Beschreibung |
|------|---------|---------|--------------|
| `enabled` | ja | `false` | Hauptschalter. `false`/fehlend → Feature inert. |
| `publicHostname` | ja | — | Hostname, den der Edge bedient (Cert-CN; SNI-Schlüssel). |
| `publicPort` | nein | `443` | Öffentlicher Port, auf dem der Edge lauscht. |
| `image` | nein | gepinntes `caddy` | Edge-Image; in Produktion per Digest pinnen. |
| `upstream.service` | ja | — | Interner DNS-Alias des öffentlichen Eingangs (z. B. der BFF). |
| `upstream.port` | nein | `8080` | Upstream-Port. |
| `network` | ja | — | Gemeinsames externes Netz, das Edge und Upstream verbindet (`external: true`). |
| `tls.mode` | nein | keine | TLS-Terminierung: `selfsigned`, `custom` (`certRef`), `reuse` (RSGO-Cert), `letsencrypt`. |
| `tls.certRef` | bei `custom` | — | Referenz auf das hochgeladene Zertifikat. |
| `tls.letsencrypt.email` / `.dnsChallenge` | bei `letsencrypt` | — | ACME-Einstellungen. |
| `maintenancePage.mode` | nein | `default` | `default`, `bundle` oder `container`. |
| `maintenancePage.bundlePath` | bei `bundle` | — | Asset-Verzeichnis im Manifest-Repo (`index.html`). |
| `maintenancePage.container.service` / `.port` | bei `container` | `80` | Service-Alias + Port des Wartungs-Containers. |
| `maintenancePage.branding.*` | nein | — | `productName`, `logoUrl`, `supportContact`, `locales`. |

### TLS-Terminierung

Ist `tls.mode` gesetzt, **terminiert** der Edge HTTPS auf `publicPort` mit einem Zertifikat, das **RSGO verwaltet** — der Edge führt **nie selbst ACME** aus. Erneuerte Zertifikate werden ohne Edge-Neustart nachgeladen.

| `tls.mode` | Zertifikatsquelle |
|------------|-------------------|
| `selfsigned` | RSGO erzeugt ein per-Hostname-Selfsigned-Zertifikat und erneuert es automatisch. |
| `custom` | Vom Operator bereitgestelltes Zertifikat (`certRef`). |
| `reuse` | RSGOs eigenes Endpoint-Zertifikat (Single-Host-Fall). |
| `letsencrypt` | Nutzt RSGOs ACME-verwaltetes Zertifikat. |

Vollständige Schema-Details: [RSGo Manifest Format](/de/docs/reference/manifest-format/).

---

## Status-Vertrag: `GET /__status`

Der Edge liefert unter `/__status` ein **stabiles, versioniertes** JSON — identisch in allen Branding-Modi, damit ein Client/Launcher es robust parsen kann:

```json
{
  "schema": 1,
  "state": "running",
  "reason": null,
  "until": null,
  "productVersion": "1.0.0"
}
```

| Feld | Bedeutung |
|------|-----------|
| `schema` | Vertragsversion. Konsumenten sollten darauf verzweigen. |
| `state` | `running` (Proxy), `maintenance` (geplant, `reason` gesetzt) oder `deploying` (Redeploy/Upgrade läuft). |
| `reason` | Grund der geplanten Wartung (aus dem Flag), sonst `null`. |
| `until` | Angekündigtes Ende (ISO-8601), falls verfügbar, sonst `null`. |
| `productVersion` | Aktuelle Produktversion, falls bekannt. |

Damit unterscheidet ein Client zuverlässig „geplante Wartung" (Flag) von „Redeploy/temporär weg" (Deploy-Zustand) — ohne zu raten.

---

## Verwandte Themen

- [Maintenance Mode](/de/docs/monitoring/maintenance-mode/) — das Umschalten (Kontroll-Ebene), das den Edge steuert.
- [Product Redeploy](/de/docs/deployments/product-redeploy/) — währenddessen hält der Edge die Stellung.
- [RSGo Manifest Format](/de/docs/reference/manifest-format/) — vollständige Manifest-Referenz.

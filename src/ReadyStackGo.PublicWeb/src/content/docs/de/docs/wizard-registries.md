---
title: Wizard – Container Registries
description: Container Registry Erkennung und Verifizierung im Setup-Wizard
---

Im fünften Schritt des Setup-Wizards erkennt ReadyStackGo automatisch, welche Container Registries Ihre gewählten Stacks benötigen, und prüft den Zugriff auf jede Registry.

## Übersicht

Nachdem Sie Stack Sources im vorherigen Schritt ausgewählt haben, analysiert ReadyStackGo alle Image-Referenzen der verfügbaren Stacks und gruppiert sie nach Registry und Namespace. Der Wizard zeigt dann eine Zwei-Spalten-Ansicht:

| Spalte | Beschreibung |
|--------|--------------|
| **Action Required** | Registries, die noch nicht verifiziert sind — hier können Sie Credentials eingeben oder den Zugriff prüfen |
| **Verified** | Registries, deren Zugriff bestätigt wurde — öffentlich oder mit gültigen Credentials |

---

## Schritt für Schritt: Registries konfigurieren

### Schritt 1: Zwei-Spalten-Layout

Nach der automatischen Erkennung sehen Sie alle benötigten Registries in zwei Spalten. Öffentliche Registries (z.B. Docker Hub `library/*`) werden automatisch geprüft und bei Erfolg direkt in die "Verified"-Spalte verschoben.

![Wizard Registries Step mit Zwei-Spalten-Layout](/images/docs/wizard-reg-01-columns.png)

Jede Karte in der "Action Required"-Spalte zeigt:
- **Registry-Name** (Host und Namespace)
- **Anzahl Images** aus dieser Registry
- **Credential-Felder** (Username und Password/Token)
- **Check Access** Button zum Prüfen des Zugriffs
- **Skip** Button zum Überspringen

---

### Schritt 2: Zugriff prüfen

Für öffentliche Registries klicken Sie direkt auf **Check Access** — ohne Credentials einzugeben. ReadyStackGo prüft über die Docker Registry v2 API, ob anonymer Zugriff möglich ist.

![Registry-Karten mit Credential-Feldern und Buttons](/images/docs/wizard-reg-02-action-card.png)

Für private Registries:
1. Geben Sie **Username** und **Password / Token** ein
2. Klicken Sie auf **Check Access**
3. Bei erfolgreicher Authentifizierung wird die Registry in die "Verified"-Spalte verschoben

:::tip[Registry-Typen]
In der "Verified"-Spalte zeigt ein **Globus-Icon** öffentliche Registries und ein **Schloss-Icon** authentifizierte Registries an.
:::

---

### Schritt 3: Registry überspringen

Falls Sie eine Registry nicht sofort konfigurieren möchten, klicken Sie auf **Skip**. Die Registry wird in den "Skipped"-Bereich verschoben und mit einem **Undo**-Button versehen.

![Registry übersprungen mit Undo-Option](/images/docs/wizard-reg-03-skipped.png)

:::note[Später konfigurieren]
Übersprungene Registries können jederzeit über **Settings → Container Registries** nachträglich konfiguriert werden.
:::

---

### Schritt 4: Setup abschließen

Klicken Sie auf **Continue**, um zum letzten Wizard-Schritt zu gelangen. Alternativ können Sie mit **Skip for now** den gesamten Registries-Schritt überspringen.

![Complete Setup Schritt nach den Registries](/images/docs/wizard-reg-07-complete.png)

---

## Automatische Erkennung

ReadyStackGo erkennt Registries anhand der Image-Referenzen in den Stack-Definitionen:

| Image-Referenz | Erkannte Registry | Namespace |
|----------------|-------------------|-----------|
| `nginx:latest` | `docker.io` | `library` |
| `ghcr.io/myorg/myapp:v1` | `ghcr.io` | `myorg` |
| `registry.gitlab.com/team/project:latest` | `registry.gitlab.com` | `team` |
| `myacr.azurecr.io/services/api:2.0` | `myacr.azurecr.io` | `services` |

### Heuristiken

- Images ohne Host-Angabe (z.B. `nginx`) werden automatisch `docker.io/library/` zugeordnet
- `docker.io/library/*` wird als "wahrscheinlich öffentlich" markiert
- `mcr.microsoft.com` wird ebenfalls als öffentlich erkannt
- Alle anderen Registries werden zunächst als "Action Required" eingestuft

---

## Zugriffsprüfung (v2 API Flow)

Die Prüfung folgt dem Docker Registry v2 Authentifizierungs-Protokoll:

1. **GET /v2/** — wenn 200 → Registry ist vollständig öffentlich
2. **401 mit Bearer Challenge** → Token-Flow starten
3. **Token anfordern** mit `scope=repository:{namespace}/{repo}:pull`
4. **Tags auflisten** mit dem erhaltenen Token
5. Wenn Tags-Abfrage erfolgreich → öffentlich / authentifiziert bestätigt

:::caution[Netzwerk erforderlich]
Die Zugriffsprüfung benötigt eine Netzwerkverbindung vom ReadyStackGo-Container zu den jeweiligen Registries. In isolierten Netzwerken kann die Prüfung fehlschlagen.
:::

---

## Weiterführende Links

- [Registry Management](/de/docs/registry-management/) — Registries über die Settings-Seite verwalten
- [Stack Sources](/de/docs/stack-sources/) — Stack Sources konfigurieren
- [Stack Deployment](/de/docs/stack-deployment/) — Stacks deployen

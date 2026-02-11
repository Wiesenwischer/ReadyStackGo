---
title: Registry Management
description: Docker Registries verwalten und Image Patterns konfigurieren
---

Diese Anleitung zeigt Ihnen, wie Sie Docker Registries in ReadyStackGo verwalten, um Images aus privaten Registries zu pullen.

## Übersicht

ReadyStackGo unterstützt das Pullen von Docker Images aus beliebigen OCI-kompatiblen Registries. Über die Settings-Seite können Sie Registry-Credentials zentral verwalten und mit **Image Patterns** automatisch den richtigen Registries zuordnen.

| Funktion | Beschreibung |
|----------|--------------|
| **Registry hinzufügen** | Docker Hub, GHCR, GitLab, Quay.io oder Custom Registry konfigurieren |
| **Credentials verwalten** | Username/Password oder Token für private Registries hinterlegen |
| **Image Patterns** | Automatische Zuordnung von Images zu Registries via Glob-Patterns |
| **Default Registry** | Fallback-Registry für Images ohne Pattern-Match |
| **Registry bearbeiten** | Name, URL, Credentials und Patterns nachträglich ändern |
| **Registry löschen** | Registry-Konfiguration inkl. Credentials entfernen |

### Credential-Auflösung

Beim Pullen eines Images sucht ReadyStackGo Credentials in folgender Reihenfolge:

1. **Datenbank-Registries** - Registries aus den Settings mit passenden Image Patterns
2. `DOCKER__CONFIGPATH` - Pfad aus Umgebungsvariable oder appsettings.json
3. `DOCKER_CONFIG` - Standard Docker-Konvention (Verzeichnis)
4. `/root/.docker/config.json` - Standard-Pfad im Linux-Container
5. **Keine Auth** - Für öffentliche Images

---

## Schritt für Schritt: Registry hinzufügen

### Schritt 1: Navigieren Sie zu Settings

Öffnen Sie die **Settings**-Seite über die Sidebar-Navigation. Dort finden Sie die Kachel **Container Registries**.

![Settings-Seite mit Container Registries Kachel](/images/docs/registry-01-settings-nav.png)

---

### Schritt 2: Registries-Übersicht öffnen

Klicken Sie auf die **Container Registries** Kachel. Beim ersten Öffnen wird der leere Zustand angezeigt – noch keine Registries konfiguriert.

![Leerer Zustand ohne konfigurierte Registries](/images/docs/registry-02-empty-state.png)

Klicken Sie auf **Add Your First Registry** oder den **Add Registry** Button oben rechts.

---

### Schritt 3: Registry konfigurieren

Füllen Sie das Formular aus. ReadyStackGo bietet Vorlagen für bekannte Registries:

| Registry | URL |
|----------|-----|
| Docker Hub | `https://index.docker.io/v1/` |
| GitHub Container Registry | `https://ghcr.io` |
| GitLab Container Registry | `https://registry.gitlab.com` |
| Quay.io | `https://quay.io` |
| Custom | Beliebige URL eingeben |

Wählen Sie einen Registry-Typ aus dem Dropdown – Name und URL werden automatisch ausgefüllt. Optional können Sie **Credentials** (Username + Password/Token) und **Image Patterns** konfigurieren.

![Registry-Formular mit Docker Hub und Credentials ausgefüllt](/images/docs/registry-03-add-form.png)

:::tip[Image Patterns]
Image Patterns bestimmen automatisch, welche Registry für welches Image verwendet wird. Ohne Patterns wird die Registry nur als Default oder manuell zugeordnet.
:::

---

### Schritt 4: Registry in der Liste

Nach dem Speichern erscheint die Registry in der Übersicht. Badges zeigen den Status:
- **Authenticated** – Credentials sind hinterlegt
- **Default** – Diese Registry wird als Fallback verwendet

![Registry-Liste mit neu angelegter Docker Hub Registry](/images/docs/registry-04-list-with-registry.png)

Von hier aus können Sie Registries **bearbeiten**, als **Default setzen** oder **löschen**.

---

## Registry bearbeiten

Klicken Sie auf **Edit** bei einer Registry, um Name, URL, Credentials oder Image Patterns zu ändern.

![Edit-Formular einer Registry mit geänderten Daten](/images/docs/registry-05-edit-form.png)

:::note[Credentials aktualisieren]
Bestehende Passwörter werden nicht angezeigt. Lassen Sie das Feld leer, um das aktuelle Passwort beizubehalten, oder aktivieren Sie **Clear existing credentials** um die Credentials komplett zu entfernen.
:::

---

## Registry löschen

Klicken Sie auf **Delete** bei einer Registry. Es erscheint eine Bestätigungsseite mit den Details der Registry.

![Lösch-Bestätigung mit Registry-Details und Warnung](/images/docs/registry-06-delete-confirm.png)

:::caution[Unwiderruflich]
Das Löschen einer Registry entfernt alle gespeicherten Credentials und Konfigurationen. Diese Aktion kann nicht rückgängig gemacht werden.
:::

---

## Image Patterns

Image Patterns bestimmen, welche Registry-Credentials für welche Images verwendet werden. Sie verwenden Glob-Syntax:

### Pattern-Syntax

| Pattern | Beschreibung | Beispiel-Matches |
|---------|--------------|------------------|
| `library/*` | Ein Pfad-Segment | `library/nginx`, `library/redis` |
| `myorg/**` | Beliebig viele Pfad-Segmente | `myorg/app`, `myorg/team/app` |
| `ghcr.io/**` | Registry-spezifisch | `ghcr.io/owner/repo` |
| `nginx` | Exakte Übereinstimmung | Nur `nginx` |

### Pattern-Regeln

- `*` matcht beliebige Zeichen innerhalb **eines** Pfad-Segments
- `**` matcht beliebige Zeichen über **mehrere** Pfad-Segmente
- Patterns sind **case-insensitive**
- Tags und Digests werden beim Matching **ignoriert**

### Beispiel-Konfiguration

| Registry | Image Patterns | Verwendet für |
|----------|----------------|---------------|
| Docker Hub (Firma) | `meinefirma/*`, `meinefirma/**` | Firmen-Images auf Docker Hub |
| GitHub Container Registry | `ghcr.io/**` | Alle GitHub Packages |
| Azure Container Registry | `meinacr.azurecr.io/**` | Azure-gehostete Images |
| Default (Docker Hub) | *(keine - als Default markiert)* | Alle anderen öffentlichen Images |

---

## Default Registry

Sie können eine Registry als **Default** markieren. Diese wird für alle Images verwendet, die keinem Pattern entsprechen. Klicken Sie dazu auf **Set Default** in der Registry-Liste.

:::note[Nur eine Default Registry]
Es kann nur eine Registry als Default markiert sein. Beim Setzen einer neuen Default wird die vorherige automatisch deaktiviert.
:::

---

## Unterstützte Registries

ReadyStackGo funktioniert mit jeder OCI-kompatiblen Registry:

| Registry | URL-Format |
|----------|------------|
| Docker Hub | `https://index.docker.io/v1/` |
| GitHub Container Registry | `https://ghcr.io` |
| Azure Container Registry | `https://<name>.azurecr.io` |
| Google Container Registry | `https://gcr.io` |
| Amazon ECR | `https://<account>.dkr.ecr.<region>.amazonaws.com` |
| Self-hosted | `https://registry.example.com` |

---

## Fehlerbehebung

### "pull access denied" Fehler

```
Failed to pull image 'meinefirma/myimage:latest' and no local copy exists.
Error: pull access denied for meinefirma/myimage
```

**Ursachen:**
1. Keine Registry-Credentials konfiguriert
2. Falsche Credentials
3. Image Pattern matcht nicht
4. Image existiert nicht in der Registry

**Lösungen:**
1. Registry über Settings hinzufügen
2. Image Pattern konfigurieren, das zu Ihrem Image passt
3. Credentials überprüfen
4. Image-Namen verifizieren

### Registry wird nicht verwendet

Falls konfigurierte Credentials nicht verwendet werden:

1. **Image Patterns prüfen** - Passt das Pattern zu Ihrem Image?
2. **Pattern-Syntax prüfen** - `*` für ein Segment, `**` für mehrere
3. **Spezifischere Patterns** - Hat eine andere Registry ein spezifischeres Pattern?

### Debug-Logging aktivieren

Für detaillierte Logs zur Credential-Auflösung:

```yaml
environment:
  - Logging__LogLevel__ReadyStackGo.Infrastructure.Docker=Debug
```

---

## Sicherheitshinweise

- Passwörter werden in der SQLite-Datenbank gespeichert (nicht verschlüsselt at-rest)
- Beschränken Sie den Zugriff auf die ReadyStackGo-Instanz
- Die Datenbank-Datei sollte nur vom ReadyStackGo-Prozess lesbar sein
- Verwenden Sie Service Accounts statt persönlicher Credentials

---

## Weiterführende Links

- [Stack Deployment](/de/docs/stack-deployment/) - Stacks deployen
- [Installation](/de/getting-started/installation/) - ReadyStackGo installieren

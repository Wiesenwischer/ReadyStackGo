---
title: Registry Management
description: Docker Registries verwalten und Image Patterns konfigurieren
---

Diese Anleitung zeigt Ihnen, wie Sie Docker Registries in ReadyStackGo verwalten, um Images aus privaten Registries zu pullen.

## Übersicht

ReadyStackGo unterstützt das Pullen von Docker Images aus beliebigen OCI-kompatiblen Registries. Über die Settings-Seite können Sie Registry-Credentials zentral verwalten und mit **Image Patterns** automatisch den richtigen Registries zuordnen.

### Credential-Auflösung

Beim Pullen eines Images sucht ReadyStackGo Credentials in folgender Reihenfolge:

1. **Datenbank-Registries** - Registries aus den Settings mit passenden Image Patterns
2. `DOCKER__CONFIGPATH` - Pfad aus Umgebungsvariable oder appsettings.json
3. `DOCKER_CONFIG` - Standard Docker-Konvention (Verzeichnis)
4. `/root/.docker/config.json` - Standard-Pfad im Linux-Container
5. **Keine Auth** - Für öffentliche Images

---

## Registry hinzufügen

1. Navigieren Sie zu **Settings** in der Sidebar
2. Klicken Sie auf **Add Registry**
3. Füllen Sie die Felder aus:

| Feld | Beschreibung |
|------|--------------|
| **Name** | Anzeigename (z.B. "Docker Hub - Firma") |
| **URL** | Registry-URL (z.B. `https://index.docker.io/v1/`) |
| **Username** | Optional - für private Registries |
| **Password** | Optional - für private Registries |
| **Image Patterns** | Optional - Glob-Patterns für automatische Zuordnung |

4. Klicken Sie auf **Save**

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

Sie können eine Registry als **Default** markieren. Diese wird für alle Images verwendet, die keinem Pattern entsprechen:

1. Öffnen Sie die Registry in den Settings
2. Aktivieren Sie **Set as Default**
3. Speichern Sie

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

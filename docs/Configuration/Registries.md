# Docker Registry Konfiguration

ReadyStackGo unterstützt das Pullen von Docker Images aus privaten Registries. Diese Seite beschreibt die verschiedenen Möglichkeiten zur Konfiguration von Registry-Credentials.

## Übersicht

Beim Deployment eines Stacks versucht ReadyStackGo, die benötigten Images zu pullen. Für private Registries werden Credentials benötigt.

**Reihenfolge der Credential-Suche (v0.5):**
1. `Docker:ConfigPath` aus IConfiguration (appsettings.json oder `DOCKER__CONFIGPATH` Environment Variable)
2. `DOCKER_CONFIG` Environment Variable (Standard Docker Convention)
3. `/root/.docker/config.json` (Linux Container)
4. `~/.docker/config.json` (User Profile Fallback)
5. Kein Auth (für öffentliche Images)

## Fehlerbehandlung

Ab v0.5 gilt:
- Wenn ein Image-Pull fehlschlägt und **kein lokales Image** existiert → **Fehler** (Deployment wird abgebrochen)
- Wenn ein Image-Pull fehlschlägt aber ein **lokales Image existiert** → **Warnung** (lokales Image wird verwendet)

Dies verhindert unbeabsichtigte Deployments mit veralteten Images.

## Aktueller Stand (v0.5)

### Docker Config Mount (empfohlen)

Die einfachste Methode ist das Mounten der Docker-Config-Datei in den Container:

**Docker Compose Beispiel:**
```yaml
services:
  readystackgo:
    image: wiesenwischer/readystackgo:latest
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ~/.docker/config.json:/root/.docker/config.json:ro
      - rsgo-config:/app/config
      - ./stacks:/app/stacks
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - ConfigPath=/app/config
    ports:
      - "8080:8080"
```

**Wichtig:**
- Die Datei muss nach `/root/.docker/config.json` gemountet werden (nicht in ein anderes Verzeichnis)
- Das `:ro` Flag macht den Mount read-only (empfohlen für Sicherheit)
- Der User auf dem Host muss vorher `docker login` ausgeführt haben

### Konfiguration über IConfiguration

Alternativ kann der Pfad zur Docker-Config über IConfiguration gesetzt werden:

**Via Environment Variable:**
```yaml
environment:
  - DOCKER__CONFIGPATH=/custom/path/config.json
```

**Via appsettings.json:**
```json
{
  "Docker": {
    "ConfigPath": "/custom/path/config.json"
  }
}
```

### DOCKER_CONFIG Environment Variable

Die Standard Docker Convention wird ebenfalls unterstützt:

```yaml
environment:
  - DOCKER_CONFIG=/docker-config
volumes:
  - ~/.docker:/docker-config:ro
```

**Hinweis:** `DOCKER_CONFIG` zeigt auf das Verzeichnis, nicht die Datei. ReadyStackGo fügt automatisch `/config.json` hinzu.

## Unterstützte Registries

ReadyStackGo erkennt automatisch die richtige Registry anhand des Image-Namens:

| Image | Registry |
|-------|----------|
| `nginx:latest` | Docker Hub (`https://index.docker.io/v1/`) |
| `amssolution/myimage:v1` | Docker Hub (`https://index.docker.io/v1/`) |
| `ghcr.io/owner/image:tag` | GitHub Container Registry (`ghcr.io`) |
| `myregistry.azurecr.io/image` | Azure Container Registry (`myregistry.azurecr.io`) |
| `localhost:5000/image` | Local Registry (`localhost:5000`) |

## Geplant: Registry-Konfiguration (v0.6)

### Konfigurationsdatei

Registries werden in `rsgo.registries.json` konfiguriert:

```json
{
  "registries": [
    {
      "id": "dockerhub-ams",
      "name": "AMS Docker Hub",
      "url": "https://index.docker.io/v1/",
      "username": "ams-service-user",
      "password": "base64-encoded-password",
      "isDefault": true,
      "imagePatterns": ["amssolution/*"]
    },
    {
      "id": "ghcr",
      "name": "GitHub Container Registry",
      "url": "ghcr.io",
      "username": "github-user",
      "password": "ghp_token...",
      "imagePatterns": ["ghcr.io/*"]
    }
  ]
}
```

### Felder

| Feld | Beschreibung |
|------|-------------|
| `id` | Eindeutige ID der Registry |
| `name` | Anzeigename |
| `url` | Registry URL (ohne Protokoll für custom, mit Protokoll für Docker Hub) |
| `username` | Benutzername |
| `password` | Passwort (Base64-encoded) |
| `isDefault` | Wird für alle Images verwendet, die keinem Pattern entsprechen |
| `imagePatterns` | Glob-Patterns für Image-Matching (z.B. `amssolution/*`, `ghcr.io/myorg/*`) |

### Image-Matching

ReadyStackGo ordnet Images anhand der `imagePatterns` einer Registry zu:

1. Image `amssolution/identityaccess:latest` → Matched `amssolution/*` → Registry `dockerhub-ams`
2. Image `ghcr.io/myorg/myimage:v1` → Matched `ghcr.io/*` → Registry `ghcr`
3. Image `nginx:latest` → Kein Match → Default Registry (falls vorhanden) oder Docker Hub public

## Geplant: Registry Management UI (v0.8)

Eine Web-Oberfläche zur Verwaltung von Registries:

- **Settings → Registries**
  - Liste aller konfigurierten Registries
  - Hinzufügen/Bearbeiten/Löschen
  - Test-Button zum Prüfen der Credentials
  - Default-Registry festlegen

## Sicherheitshinweise

- Passwörter werden Base64-encoded gespeichert (nicht verschlüsselt)
- Die Konfigurationsdatei sollte nur für den ReadyStackGo-Prozess lesbar sein
- Verwende `:ro` für read-only Mounts
- Für höhere Sicherheit: Environment-Variablen oder Secret Management verwenden (geplant für spätere Versionen)

## Troubleshooting

### "pull access denied" Fehler

```
Failed to pull image 'amssolution/myimage:latest' and no local copy exists.
Error: pull access denied for amssolution/myimage, repository does not exist or may require 'docker login'
```

**Ursachen:**
1. Keine Registry-Credentials konfiguriert
2. Falsche Credentials
3. Image existiert nicht in der Registry
4. Docker-Config nicht korrekt gemountet

**Lösungen:**
1. `docker login` auf dem Host ausführen
2. Docker-Config korrekt mounten (siehe oben)
3. Image-Namen prüfen
4. Logs prüfen für Details zur Credential-Suche

### Docker Config wird nicht gefunden

Wenn ReadyStackGo die Docker-Config nicht findet, prüfe die Logs:

```
Looking for credentials for image amssolution/myimage, registry: https://index.docker.io/v1/
Docker config path: /root/.docker/config.json
Docker config file not found at /root/.docker/config.json
```

**Lösungen:**
1. Volume-Mount hinzufügen: `~/.docker/config.json:/root/.docker/config.json:ro`
2. Prüfen ob `~/.docker/config.json` auf dem Host existiert
3. `DOCKER__CONFIGPATH` Environment Variable setzen

### Credentials werden nicht erkannt

Wenn die Config gefunden aber keine Credentials erkannt werden:

```
Available registries in config: https://index.docker.io/v1/
Found credentials for registry https://index.docker.io/v1/
Using credentials for user myuser
```

Falls diese Zeilen **nicht** erscheinen:
1. Prüfen ob der Registry-Key in der config.json korrekt ist
2. Docker Hub verwendet `https://index.docker.io/v1/` als Key
3. Andere Registries verwenden ihre Domain (z.B. `ghcr.io`)

### Debug-Logging aktivieren

Für detaillierte Logs setze das Log-Level auf Debug:

```yaml
environment:
  - Logging__LogLevel__ReadyStackGo.Infrastructure.Docker=Debug
```

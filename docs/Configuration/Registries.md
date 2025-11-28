# Docker Registry Konfiguration

ReadyStackGo unterstützt das Pullen von Docker Images aus privaten Registries. Diese Seite beschreibt die verschiedenen Möglichkeiten zur Konfiguration von Registry-Credentials.

## Übersicht

Beim Deployment eines Stacks versucht ReadyStackGo, die benötigten Images zu pullen. Für private Registries werden Credentials benötigt.

**Reihenfolge der Credential-Suche:**
1. Konfigurierte Registries in `rsgo.registries.json` (ab v0.6)
2. Docker Config aus `~/.docker/config.json` (Fallback)
3. Kein Auth (für öffentliche Images)

## Fehlerbehandlung

Ab v0.5 gilt:
- Wenn ein Image-Pull fehlschlägt und **kein lokales Image** existiert → **Fehler** (Deployment wird abgebrochen)
- Wenn ein Image-Pull fehlschlägt aber ein **lokales Image existiert** → **Warnung** (lokales Image wird verwendet)

Dies verhindert unbeabsichtigte Deployments mit veralteten Images.

## Aktueller Stand (v0.5)

### Docker Config Fallback

ReadyStackGo liest automatisch die Docker-Credentials aus der Datei `~/.docker/config.json` (oder `DOCKER_CONFIG` Environment Variable).

**Voraussetzungen:**
- Der User, unter dem ReadyStackGo läuft, muss `docker login` ausgeführt haben
- Bei Docker-Container-Deployment: Die Config-Datei muss in den Container gemountet werden

**Docker Compose Beispiel:**
```yaml
services:
  readystackgo:
    image: amssolution/readystackgo:latest
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - /home/dokkeruser/.docker/config.json:/root/.docker/config.json:ro
    # Alternativ mit DOCKER_CONFIG:
    # environment:
    #   - DOCKER_CONFIG=/docker-config
    # volumes:
    #   - /home/dokkeruser/.docker:/docker-config:ro
```

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
4. Netzwerkprobleme

**Lösungen:**
1. Registry-Credentials konfigurieren (siehe oben)
2. `docker login` auf dem Host ausführen und Container neu starten
3. Image-Namen prüfen
4. Netzwerkverbindung zur Registry prüfen

### Docker Config wird nicht gefunden

Wenn ReadyStackGo die Docker-Config nicht findet:
1. Prüfen ob `~/.docker/config.json` existiert
2. Bei Container-Deployment: Volume-Mount prüfen
3. `DOCKER_CONFIG` Environment-Variable setzen

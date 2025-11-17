# Feature Flags

Feature Flags ermöglichen die dynamische Aktivierung/Deaktivierung fachlicher Funktionen ohne Code-Deployment.

## Konzept

Feature Flags sind **kontextübergreifende** Schalter, die:
- Zentral in `rsgo.features.json` definiert werden
- Als Environment Variables an alle Container übergeben werden
- Zur Laufzeit von den Services ausgewertet werden
- Ohne Neudeployment geändert werden können

## Definition

### In `rsgo.features.json`
```json
{
  "newColorTheme": true,
  "discussionV2": false,
  "memoRichEditor": true,
  "advancedSearch": true,
  "betaFeatures": false
}
```

### Als Environment Variables
Diese Konfiguration wird an Container übergeben als:
```bash
RSGO_FEATURE_newColorTheme=true
RSGO_FEATURE_discussionV2=false
RSGO_FEATURE_memoRichEditor=true
RSGO_FEATURE_advancedSearch=true
RSGO_FEATURE_betaFeatures=false
```

## Verwendung im Code

### Backend (C#)
```csharp
public class ProjectService
{
    private readonly IConfiguration _config;

    public ProjectService(IConfiguration config)
    {
        _config = config;
    }

    public async Task<Project> GetProjectAsync(string id)
    {
        var project = await _repository.GetAsync(id);

        // Feature Flag prüfen
        if (_config.GetValue<bool>("RSGO_FEATURE_advancedSearch"))
        {
            project.SearchMetadata = await _searchService.GetMetadataAsync(id);
        }

        return project;
    }
}
```

### Frontend (TypeScript/React)
```typescript
// Feature Flags vom Backend laden
const features = await api.getFeatureFlags();

// Komponente konditional rendern
{features.newColorTheme && <NewThemeComponent />}
{!features.newColorTheme && <LegacyThemeComponent />}
```

## Manifest-Integration

Feature Flags können im Manifest Standardwerte haben:

```json
{
  "manifestVersion": "1.0.0",
  "stackVersion": "4.3.0",
  "features": {
    "newColorTheme": {
      "default": true,
      "description": "Neues UI-Theme mit verbesserter UX"
    },
    "discussionV2": {
      "default": false,
      "description": "Neue Diskussions-API (Beta)"
    }
  }
}
```

Diese Defaults werden beim Deployment in `rsgo.features.json` übernommen, können aber später vom Admin überschrieben werden.

## Admin-UI

Die Admin-UI bietet eine Verwaltungsseite für Feature Flags:

### Übersichtsseite
| Feature          | Aktiv | Beschreibung                  | Aktion   |
|------------------|-------|-------------------------------|----------|
| newColorTheme    | ✅    | Neues UI-Theme                | Toggle   |
| discussionV2     | ❌    | Neue Diskussions-API (Beta)   | Toggle   |
| memoRichEditor   | ✅    | Rich Text Editor für Memos    | Toggle   |
| advancedSearch   | ✅    | Erweiterte Suchfunktionen     | Toggle   |

### Toggle-Aktion
```
POST /api/v1/features/newColorTheme/toggle
```

Response:
```json
{
  "feature": "newColorTheme",
  "enabled": false,
  "updatedAt": "2025-11-17T10:30:00Z"
}
```

## API-Endpunkte

### Feature Flags abrufen
```http
GET /api/v1/features
```

Response:
```json
{
  "newColorTheme": true,
  "discussionV2": false,
  "memoRichEditor": true
}
```

### Feature Flag setzen
```http
PUT /api/v1/features/discussionV2
Content-Type: application/json

{
  "enabled": true
}
```

### Feature Flag löschen
```http
DELETE /api/v1/features/oldFeature
```

## Best Practices

### 1. Klare Benennung
```
✅ newColorTheme
✅ discussionV2
❌ feature1
❌ temp
```

### 2. Dokumentation
Immer eine Beschreibung im Manifest angeben:
```json
"features": {
  "newFeature": {
    "default": false,
    "description": "Was macht dieses Feature?"
  }
}
```

### 3. Cleanup
Alte/ungenutzte Feature Flags regelmäßig entfernen.

### 4. Testing
Feature Flags in Tests berücksichtigen:
```csharp
[Theory]
[InlineData(true)]
[InlineData(false)]
public async Task TestWithFeatureFlag(bool featureEnabled)
{
    // Test beide Szenarien
}
```

### 5. Monitoring
Loggen Sie Feature-Flag-Nutzung:
```csharp
_logger.LogInformation(
    "Feature {Feature} is {Status}",
    "discussionV2",
    isEnabled ? "enabled" : "disabled"
);
```

## Anwendungsfälle

### 1. Schrittweise Rollouts
Neues Feature zunächst deaktiviert deployen, später aktivieren.

### 2. A/B-Testing
Verschiedene Implementierungen parallel testen.

### 3. Kill Switch
Feature bei Problemen schnell deaktivieren ohne Rollback.

### 4. Kundenseitiges Customizing
Kunden können Features nach Bedarf aktivieren.

### 5. Beta-Features
Experimentelle Features für Early Adopters.

## Limitierungen

- **Keine granulare Steuerung**: Feature Flags sind global, nicht pro Benutzer/Rolle
- **Kein Percentage Rollout**: Entweder an oder aus, keine 50% der Benutzer
- **Keine Zeitsteuerung**: Keine automatische Aktivierung zu bestimmten Zeiten

Diese Features sind für zukünftige Versionen geplant.

## Nächste Schritte

- [Configuration Overview](Overview.md)
- [Config Files](Config-Files.md)
- [Manifest Specification](Manifest-Specification.md)

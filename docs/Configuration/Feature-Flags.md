# Feature Flags

Feature flags enable dynamic activation/deactivation of functional features without code deployment.

## Concept

Feature flags are **cross-context** switches that:
- Are centrally defined in `rsgo.features.json`
- Are passed as environment variables to all containers
- Are evaluated at runtime by services
- Can be changed without redeployment

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

### As Environment Variables
This configuration is passed to containers as:
```bash
RSGO_FEATURE_newColorTheme=true
RSGO_FEATURE_discussionV2=false
RSGO_FEATURE_memoRichEditor=true
RSGO_FEATURE_advancedSearch=true
RSGO_FEATURE_betaFeatures=false
```

## Usage in Code

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

        // Check feature flag
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
// Load feature flags from backend
const features = await api.getFeatureFlags();

// Conditionally render component
{features.newColorTheme && <NewThemeComponent />}
{!features.newColorTheme && <LegacyThemeComponent />}
```

## Manifest Integration

Feature flags can have default values in the manifest:

```json
{
  "manifestVersion": "1.0.0",
  "stackVersion": "4.3.0",
  "features": {
    "newColorTheme": {
      "default": true,
      "description": "New UI theme with improved UX"
    },
    "discussionV2": {
      "default": false,
      "description": "New discussion API (Beta)"
    }
  }
}
```

These defaults are applied to `rsgo.features.json` during deployment but can be overridden by the admin later.

## Admin UI

The admin UI provides a management page for feature flags:

### Overview Page
| Feature          | Active | Description                   | Action   |
|------------------|--------|-------------------------------|----------|
| newColorTheme    | Yes    | New UI theme                  | Toggle   |
| discussionV2     | No     | New discussion API (Beta)     | Toggle   |
| memoRichEditor   | Yes    | Rich text editor for memos    | Toggle   |
| advancedSearch   | Yes    | Advanced search features      | Toggle   |

### Toggle Action
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

## API Endpoints

### Get Feature Flags
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

### Set Feature Flag
```http
PUT /api/v1/features/discussionV2
Content-Type: application/json

{
  "enabled": true
}
```

### Delete Feature Flag
```http
DELETE /api/v1/features/oldFeature
```

## Best Practices

### 1. Clear Naming
```
Good: newColorTheme
Good: discussionV2
Bad: feature1
Bad: temp
```

### 2. Documentation
Always provide a description in the manifest:
```json
"features": {
  "newFeature": {
    "default": false,
    "description": "What does this feature do?"
  }
}
```

### 3. Cleanup
Regularly remove old/unused feature flags.

### 4. Testing
Consider feature flags in tests:
```csharp
[Theory]
[InlineData(true)]
[InlineData(false)]
public async Task TestWithFeatureFlag(bool featureEnabled)
{
    // Test both scenarios
}
```

### 5. Monitoring
Log feature flag usage:
```csharp
_logger.LogInformation(
    "Feature {Feature} is {Status}",
    "discussionV2",
    isEnabled ? "enabled" : "disabled"
);
```

## Use Cases

### 1. Gradual Rollouts
Deploy new feature initially disabled, enable later.

### 2. A/B Testing
Test different implementations in parallel.

### 3. Kill Switch
Quickly disable feature in case of problems without rollback.

### 4. Customer-side Customization
Customers can enable features as needed.

### 5. Beta Features
Experimental features for early adopters.

## Limitations

- **No granular control**: Feature flags are global, not per user/role
- **No percentage rollout**: Either on or off, not 50% of users
- **No time control**: No automatic activation at specific times

These features are planned for future versions.

## Next Steps

- [Configuration Overview](Overview.md)
- [Config Files](Config-Files.md)
- [Manifest Specification](Manifest-Specification.md)

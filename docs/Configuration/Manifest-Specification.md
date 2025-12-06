# Manifest Specification

The manifest describes the complete target state of a stack.

## Contents
1. Goals
2. Structure
3. JSON Schema
4. Examples
5. Best Practices

---

## 1. Goals

- Complete, declarative description of a release
- Unique versioning (`stackVersion`)
- Clear mapping contexts → Images/Tags

---

## 2. Structure (simplified)

```json
{
  "manifestVersion": "1.0.0",
  "stackVersion": "4.3.0",
  "schemaVersion": 12,
  "gateway": { ... },
  "contexts": { ... },
  "features": { ... },
  "metadata": { ... }
}
```

---

## 3. JSON Schema

See technical specification (chapter Manifest Schema).
This document references the same schema.

---

## 4. Example

```json
{
  "stackVersion": "4.3.0",
  "contexts": {
    "project": {
      "image": "registry/ams.project-api",
      "version": "6.4.0",
      "containerName": "ams-project",
      "internal": true
    }
  }
}
```

---

## 5. Best Practices

- Strictly follow SemVer
- Use suffixes like `-alpha`, `-beta`, `-rc` for pre-releases
- Keep context names stable

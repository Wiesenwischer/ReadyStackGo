# Manifest Specification

Das Manifest beschreibt den gesamten Zielzustand eines Stacks.

## Inhalt
1. Ziele
2. Struktur
3. JSON Schema
4. Beispiele
5. Best Practices

---

## 1. Ziele

- Vollständige, deklarative Beschreibung eines Releases
- Eindeutige Versionierung (`stackVersion`)
- Klare Zuordnung Kontexte → Images/Tags

---

## 2. Struktur (vereinfacht)

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

Siehe technische Spezifikation (Kapitel Manifest-Schema).  
Dieses Dokument referenziert das gleiche Schema.

---

## 4. Beispiel

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

- SemVer strikt einhalten
- Für Pre-Releases Suffixe wie `-alpha`, `-beta`, `-rc` nutzen
- Kontextnamen stabil halten

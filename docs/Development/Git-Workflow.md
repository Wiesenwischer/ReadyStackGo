# Git Workflow

Dieses Dokument beschreibt den Git-Workflow für die Entwicklung von ReadyStackGo.

## Branches

| Branch | Zweck | Deployment |
|--------|-------|------------|
| `main` | Production-ready Code | Docker Hub (`latest`) |
| `develop` | Aktuelle Entwicklung | Docker Hub (`develop`) |
| `feature/*` | Neue Features | - |
| `bugfix/*` | Bugfixes | - |
| `hotfix/*` | Dringende Production-Fixes | - |

## Workflows

### Feature entwickeln

```
develop ──► feature/xyz ──► PR nach develop ──► (später) Release nach main
```

1. **Branch erstellen** von `develop`:
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b feature/mein-feature
   ```

2. **Entwickeln und committen**:
   ```bash
   git add .
   git commit -m "Add: Beschreibung des Features"
   ```

3. **Push und PR erstellen**:
   ```bash
   git push origin feature/mein-feature
   ```
   → PR nach `develop` erstellen

4. **Nach Review**: PR mergen in `develop`

---

### Bugfix (nicht dringend)

```
develop ──► bugfix/xyz ──► PR nach develop ──► (später) Release nach main
```

1. **Branch erstellen** von `develop`:
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b bugfix/mein-bugfix
   ```

2. **Fix entwickeln und committen**:
   ```bash
   git add .
   git commit -m "Fix: Beschreibung des Bugs"
   ```

3. **Push und PR erstellen**:
   ```bash
   git push origin bugfix/mein-bugfix
   ```
   → PR nach `develop` erstellen

4. **Nach Review**: PR mergen in `develop`

---

### Hotfix (dringend, Production betroffen)

```
main ──► hotfix/xyz ──► PR nach main ──► Tag erstellen ──► merge zurück nach develop
```

Nur verwenden wenn Production akut betroffen ist!

1. **Branch erstellen** von `main`:
   ```bash
   git checkout main
   git pull origin main
   git checkout -b hotfix/kritischer-bug
   ```

2. **Fix entwickeln und committen**:
   ```bash
   git add .
   git commit -m "Hotfix: Beschreibung des kritischen Bugs"
   ```

3. **Push und PR erstellen**:
   ```bash
   git push origin hotfix/kritischer-bug
   ```
   → PR nach `main` erstellen

4. **Nach Review**: PR mergen in `main`

5. **Tag erstellen** (Patch-Version):
   ```bash
   git checkout main
   git pull origin main
   git tag v0.6.1
   git push origin v0.6.1
   ```

6. **Zurück nach develop mergen**:
   ```bash
   git checkout develop
   git merge main
   git push origin develop
   ```

---

## Release erstellen

Wenn `develop` bereit für ein Release ist:

1. **PR erstellen**: `develop` → `main`

2. **Nach Merge**: Tag erstellen
   ```bash
   git checkout main
   git pull origin main
   git tag v0.7.0
   git push origin v0.7.0
   ```

3. Der Docker Workflow baut automatisch:
   - `latest` Tag (von `main`)
   - Version Tag (z.B. `0.7.0`)

---

## PR Labels

Labels werden für Release Notes und Versionierung verwendet. Der **Autolabeler** setzt Labels automatisch basierend auf Branch-Namen und Dateien.

> **Wichtig:** Labels werden auf **PRs** gesetzt, nicht auf Commits!
> Der Autolabeler prüft den **Branch-Namen des PRs**, nicht die Commits darin.

### Warum Branches wichtig sind

```
❌ FALSCH: Direkt auf develop committen
develop ──► commit ──► PR nach main
                            │
                            └── Kein Label! (Branch ist "develop", nicht "bugfix/*")

✅ RICHTIG: Feature/Bugfix Branch verwenden
develop ──► bugfix/xyz ──► PR nach develop ──► PR nach main
                                │
                                └── Label "bug" (automatisch)
```

### Automatische Labels (Autolabeler)

| Branch-Pattern | Label |
|----------------|-------|
| `feature/*` | `feature` |
| `fix/*`, `bugfix/*`, `hotfix/*` | `bug` |

| Dateien | Label |
|---------|-------|
| `*.md`, `docs/**` | `documentation` |
| `package*.json`, `*.csproj` | `dependencies` |

### Manuelle Labels

Falls der Autolabeler nicht greift (z.B. PR von `develop` → `main`), setze das passende Label **manuell** auf den PR:

| Label | Verwendung | Version-Bump |
|-------|------------|--------------|
| `feature` / `enhancement` | Neues Feature | Minor (0.6.0 → 0.7.0) |
| `bug` / `bugfix` / `fix` | Bugfix | Patch (0.6.0 → 0.6.1) |
| `security` | Sicherheitsfix | - |
| `documentation` / `docs` | Nur Doku | Patch |
| `chore` / `maintenance` / `refactor` | Wartung | Patch |
| `dependencies` | Dependency Updates | - |
| `breaking` / `major` | Breaking Change | Major (0.x → 1.0) |
| `skip-changelog` | Nicht in Release Notes | - |

### Beispiel PR

```
Branch:   bugfix/fix-data-volume
Titel:    Fix: SQLite database path configuration
Labels:   bug (automatisch durch Autolabeler)
```

→ Erscheint in Release Notes unter "🐛 Bug Fixes"
→ Version-Bump: Patch

---

## Commit Message Konventionen

| Prefix | Verwendung |
|--------|------------|
| `Add:` | Neues Feature |
| `Fix:` | Bugfix |
| `Hotfix:` | Kritischer Production-Fix |
| `Update:` | Verbesserung/Änderung |
| `Remove:` | Entfernung von Code/Features |
| `Refactor:` | Code-Refactoring (keine Funktionsänderung) |
| `Docs:` | Dokumentation |
| `Test:` | Tests hinzufügen/ändern |

**Beispiele:**
```
Add: User Management UI
Fix: Database path not using DataPath configuration
Hotfix: Login fails after container restart
Update: Improve error messages in deployment engine
Refactor: Extract Docker service into separate class
Docs: Add Git workflow documentation
```

---

## Versionierung (SemVer)

ReadyStackGo verwendet [Semantic Versioning](https://semver.org/):

```
MAJOR.MINOR.PATCH
  │     │     │
  │     │     └── Bugfixes (0.6.0 → 0.6.1)
  │     └──────── Features (0.6.x → 0.7.0)
  └────────────── Breaking Changes (0.x.x → 1.0.0)
```

**GitVersion** ermittelt die Version automatisch basierend auf:
- Branch-Name
- Tags
- Commit-Historie

---

## CI/CD Pipeline

| Trigger | Workflow | Ergebnis |
|---------|----------|----------|
| Push auf `main`/`develop` | CI | Build + Tests |
| CI erfolgreich auf `main`/`develop` | Docker | Image auf Docker Hub |
| Tag `v*` | Docker | Image mit Version-Tag |
| Push auf `main` (docs/) | Wiki Sync | GitHub Wiki aktualisiert |
| Push auf `main` (PublicWeb/) | Cloudflare | PublicWeb deployed |

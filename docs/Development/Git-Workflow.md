# Git Workflow

This document describes the Git workflow for developing ReadyStackGo.

## Branches

| Branch | Purpose | Deployment |
|--------|---------|------------|
| `main` | Production-ready code | Docker Hub (`latest`) |
| `develop` | Current development | Docker Hub (`develop`) |
| `feature/*` | New features | - |
| `bugfix/*` | Bugfixes | - |
| `hotfix/*` | Urgent production fixes | - |

## Workflows

### Developing a Feature

```
develop ──► feature/xyz ──► PR to develop ──► (later) Release to main
```

1. **Create branch** from `develop`:
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b feature/my-feature
   ```

2. **Develop and commit**:
   ```bash
   git add .
   git commit -m "Add: Description of the feature"
   ```

3. **Push and create PR**:
   ```bash
   git push origin feature/my-feature
   ```
   → Create PR to `develop`

4. **After review**: Merge PR into `develop`

---

### Bugfix (not urgent)

```
develop ──► bugfix/xyz ──► PR to develop ──► (later) Release to main
```

1. **Create branch** from `develop`:
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b bugfix/my-bugfix
   ```

2. **Develop fix and commit**:
   ```bash
   git add .
   git commit -m "Fix: Description of the bug"
   ```

3. **Push and create PR**:
   ```bash
   git push origin bugfix/my-bugfix
   ```
   → Create PR to `develop`

4. **After review**: Merge PR into `develop`

---

### Hotfix (urgent, production affected)

```
main ──► hotfix/xyz ──► PR to main ──► Create tag ──► merge back to develop
```

Only use when production is acutely affected!

1. **Create branch** from `main`:
   ```bash
   git checkout main
   git pull origin main
   git checkout -b hotfix/critical-bug
   ```

2. **Develop fix and commit**:
   ```bash
   git add .
   git commit -m "Hotfix: Description of the critical bug"
   ```

3. **Push and create PR**:
   ```bash
   git push origin hotfix/critical-bug
   ```
   → Create PR to `main`

4. **After review**: Merge PR into `main`

5. **Create tag** (patch version):
   ```bash
   git checkout main
   git pull origin main
   git tag v0.6.1
   git push origin v0.6.1
   ```

6. **Merge back to develop**:
   ```bash
   git checkout develop
   git merge main
   git push origin develop
   ```

---

## Creating a Release (Draft + manual Publish)

PRs are collected and can be released bundled:

```
PR merge to main ──► Release Drafter Draft ──► (more PRs) ──► Manual Publish ──► Docker + PublicWeb
                              │                                           │
                              └── Version calculated from PR labels ──────┘
```

### Process

1. **Create and merge PRs**: `develop` → `main`
   - Set labels (determines the version)
   - Multiple PRs can be collected

2. **Check draft**: GitHub → Releases → View draft
   - All PRs since last release are listed
   - Version is automatically calculated

3. **Publish release**: Click "Publish release"
   - Tag is created
   - Docker workflow builds images
   - PublicWeb is deployed

### Benefits

- **Bundle**: Multiple PRs in one release
- **Control**: Doc changes don't trigger a release
- **No manual version**: Automatically calculated
- **No manual tag**: Created on publish

### Version Determination by Labels

| PR Labels | Version Bump | Example |
|-----------|--------------|---------|
| `breaking` or `major` | Major | 0.6.0 → 1.0.0 |
| `feature` or `enhancement` | Minor | 0.6.0 → 0.7.0 |
| `bug`, `fix`, `docs`, etc. | Patch | 0.6.0 → 0.6.1 |

> **Important:** Set at least one label on the PR so the version is calculated correctly!

---

## PR Labels

Labels are used for release notes and versioning. The **autolabeler** sets labels automatically based on branch names and files.

> **Important:** Labels are set on **PRs**, not on commits!
> The autolabeler checks the **PR's branch name**, not the commits within it.

### Why Branches Matter

```
❌ WRONG: Commit directly to develop
develop ──► commit ──► PR to main
                            │
                            └── No label! (Branch is "develop", not "bugfix/*")

✅ RIGHT: Use feature/bugfix branch
develop ──► bugfix/xyz ──► PR to develop ──► PR to main
                                │
                                └── Label "bug" (automatic)
```

### Automatic Labels (Autolabeler)

| Branch Pattern | Label |
|----------------|-------|
| `feature/*` | `feature` |
| `fix/*`, `bugfix/*`, `hotfix/*` | `bug` |

| Files | Label |
|-------|-------|
| `*.md`, `docs/**` | `documentation` |
| `package*.json`, `*.csproj` | `dependencies` |

### Manual Labels

If the autolabeler doesn't work (e.g., PR from `develop` → `main`), set the appropriate label **manually** on the PR:

| Label | Usage | Version Bump |
|-------|-------|--------------|
| `feature` / `enhancement` | New feature | Minor (0.6.0 → 0.7.0) |
| `bug` / `bugfix` / `fix` | Bugfix | Patch (0.6.0 → 0.6.1) |
| `security` | Security fix | - |
| `documentation` / `docs` | Docs only | Patch |
| `chore` / `maintenance` / `refactor` | Maintenance | Patch |
| `dependencies` | Dependency updates | - |
| `breaking` / `major` | Breaking change | Major (0.x → 1.0) |
| `skip-changelog` | Not in release notes | - |

### Example PR

```
Branch:   bugfix/fix-data-volume
Title:    Fix: SQLite database path configuration
Labels:   bug (automatic via autolabeler)
```

→ Appears in release notes under "Bug Fixes"
→ Version bump: Patch

---

## Commit Message Conventions

| Prefix | Usage |
|--------|-------|
| `Add:` | New feature |
| `Fix:` | Bugfix |
| `Hotfix:` | Critical production fix |
| `Update:` | Improvement/change |
| `Remove:` | Removal of code/features |
| `Refactor:` | Code refactoring (no functional change) |
| `Docs:` | Documentation |
| `Test:` | Add/change tests |

**Examples:**
```
Add: User Management UI
Fix: Database path not using DataPath configuration
Hotfix: Login fails after container restart
Update: Improve error messages in deployment engine
Refactor: Extract Docker service into separate class
Docs: Add Git workflow documentation
```

---

## Versioning (SemVer)

ReadyStackGo uses [Semantic Versioning](https://semver.org/):

```
MAJOR.MINOR.PATCH
  │     │     │
  │     │     └── Bugfixes (0.6.0 → 0.6.1)
  │     └──────── Features (0.6.x → 0.7.0)
  └────────────── Breaking Changes (0.x.x → 1.0.0)
```

**GitVersion** determines the version automatically based on:
- Branch name
- Tags
- Commit history

---

## CI/CD Pipeline

| Trigger | Workflow | Result |
|---------|----------|--------|
| Push to `main`/`develop` | CI | Build + Tests |
| CI successful on `develop` | Docker Dev | Image on ghcr.io (`develop`) |
| Push to `main` | Release Drafter | GitHub Release published |
| Tag `v*` | Docker | Image on Docker Hub (`latest`, `0.7.3`, `0.7`) |
| Tag `v*` | Cloudflare | PublicWeb deployed |
| Push to `main` (docs/) | Wiki Sync | GitHub Wiki updated |

### Release Workflow in Detail

```
PR with labels ──► merge to main
                      │
                      ▼
              Release Drafter
                      │
           ┌─────────┴─────────┐
           ▼                   ▼
       Git Tag          GitHub Release
       created          published
           │
           ▼
    ┌──────┴──────┐
    ▼             ▼
 Docker       Cloudflare
 Workflow       Pages
    │             │
    ▼             ▼
 Images       PublicWeb
(Docker Hub) (Release Notes
             from GitHub API)
```

# Roadmap

Grober Ausblick auf geplante Versionen und Features.

## Released

- **v0.1** – Container Ansicht + Start/Stop
- **v0.2** – Local Admin + Hardcoded Stack
- **v0.3** – Wizard + TLS-Bootstrap + Manifest Deploy (2025-01-19)
- **v0.4** – Multi-Environment + Docker Compose Deployment (2025-01-28)
  - Polymorphe Environment-Typen (Docker Socket)
  - Docker Compose Stack-Deployment (Portainer-style)
  - Automatische Environment Variable Detection
  - Stack Sources System mit rekursiver Discovery
  - 404 Page, 4-Step Wizard, Environment Management UI
  - Registry Authentication für Image Pulls
  - Deployment Warnings bei lokalem Image-Fallback
- **v0.5** – Environment Setup Flow + PublicWeb (2025-11-30)
  - Environment-Erstellung als optionaler Wizard-Schritt
  - EnvironmentGuard für environment-abhängige Seiten
  - SetupEnvironment-Seite wenn kein Environment existiert
  - PublicWeb Landing Page (DE/EN) mit Astro/Starlight
  - ams.project Showcase, Impressum, Datenschutz
  - Install-Script mit Docker Hub Image
- **v0.6** – Security Hardening (2025-12-01)
  - RBAC (Role-Based Access Control)
  - Wizard Timeout (5-Minuten-Zeitfenster für Admin-Erstellung nach Serverstart)
  - API-Lockdown nach Timeout-Ablauf (wie Portainer)
- **v0.7 - v0.9** – CI/CD Improvements (2025-12-03)
  - Draft-only Releases mit manuellem Publish
  - Docker Dev Builds auf ghcr.io
  - Dynamische Release Notes und Roadmap auf PublicWeb
  - Example Stacks im Docker Image

## Planned

### v0.10 – RSGo Manifest Format
- Spezifikation für eigenes Manifest-Format
- Docker Compose Import (Konvertierung beim Import)
- Type Validation (Number, Boolean, String, Select)
- Regex Validation für Eingabefelder

### v0.11 – Health Monitoring + Rollback
- Container Health Monitoring (Status, Health, RestartCount)
- Stack Health Dashboard
- Deployment Rollback (vorherige Stack-Version wiederherstellen)

### v0.12 – Registry Management + Git Stack Sources
- Settings-Seite für Registry-Verwaltung
- Mehrere Docker Registries konfigurierbar (Name, URL, Username, Password)
- GitRepository StackSourceProvider (Stacks aus Git-Repos laden)

### v0.13 – Release & TLS Management
- Release Management UI
- Custom TLS Certificates

### v0.14 – Metrics & Audit
- Metrics & Alerting
- Audit Logs

### v0.15 – Docker Volumes Management
- Docker Volumes Ansicht (Liste aller Volumes pro Environment)
- Volume Details (Size, Mount Points, Labels)
- Volume erstellen/löschen
- Orphaned Volumes erkennen

### v0.16 – CI/CD Integration
- Webhooks für externe CI/CD Systeme
- API für automatisierte Deployments

### v0.17 – Multi-User Support
- User Management UI
- Benutzer anlegen/bearbeiten
- Password Reset Flow

### v1.0 – Production Ready
- Dokumentation überarbeiten und vervollständigen
- Code Cleanup und Refactorings
- Performance Optimierungen

## Post v1.0

### Future Features
- Feature Flags UI
- Advanced Connection Mode
- Plugin-System
- Multi-Node-Support (mehrere Docker Hosts pro Environment)
- Kubernetes Environment Support

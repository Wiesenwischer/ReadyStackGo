# Roadmap

Grober Ausblick auf geplante Versionen und Features.

## Released

- **v0.1** – Container Ansicht + Start/Stop
- **v0.2** – Local Admin + Hardcoded Stack
- **v0.3** – Wizard + TLS-Bootstrap + Manifest Deploy ✅ (2025-01-19)
- **v0.4** – Multi-Environment + Docker Compose Deployment ✅ (2025-01-28)
  - Polymorphe Environment-Typen (Docker Socket)
  - Docker Compose Stack-Deployment (Portainer-style)
  - Automatische Environment Variable Detection
  - Stack Sources System mit rekursiver Discovery
  - 404 Page, 4-Step Wizard, Environment Management UI
  - Registry Authentication für Image Pulls
  - Deployment Warnings bei lokalem Image-Fallback
- **v0.5** – Environment Setup Flow + PublicWeb ✅ (2025-11-30)
  - Environment-Erstellung als optionaler Wizard-Schritt
  - EnvironmentGuard für environment-abhängige Seiten
  - SetupEnvironment-Seite wenn kein Environment existiert
  - PublicWeb Landing Page (DE/EN) mit Astro/Starlight
  - ams.project Showcase, Impressum, Datenschutz
  - Install-Script mit Docker Hub Image
- **v0.6** – Security Hardening ✅ (2025-12-01)
  - RBAC (Role-Based Access Control)
  - Wizard Timeout (5-Minuten-Zeitfenster für Admin-Erstellung nach Serverstart)
  - API-Lockdown nach Timeout-Ablauf (wie Portainer)

## Planned

### v0.7 – Multi-User + Health Monitoring
- SQLite-Migration für Multi-User-Support
- Container Health Monitoring (Status, Health, RestartCount)

### v0.8 – Registry Management UI + Rollback
- Settings-Seite für Registry-Verwaltung
- Mehrere Docker Registries konfigurierbar (Name, URL, Username, Password)
- Systemweite Konfiguration (gilt für alle Environments)
- Deployment Rollback (vorherige Stack-Version wiederherstellen)

### v0.9 – Release Management
- Release Management UI
- Feature Flags UI
- Advanced Connection Mode
- Custom TLS Certificates

### v0.10 – CI/CD & Monitoring
- CI/CD Pipeline Integration
- Stack Health Dashboard
- Metrics & Alerting
- Audit Logs

### v0.11 – Enhanced Stack Format
- Custom Manifest Format (enhanced validation)
- Beide Stack-Formate gleichzeitig unterstützt (Docker Compose + Custom Manifest)
- Type Validation (Numbers, Booleans, Strings, Select)
- Regex Validation für URL-Formate

### v0.12 – Docker Volumes Management
- Docker Volumes Ansicht (Liste aller Volumes pro Environment)
- Volume Details (Size, Mount Points, Labels)
- Volume erstellen/löschen
- Orphaned Volumes erkennen und aufräumen
- Volume Backup/Export Funktionalität

### v1.0 – Production Ready
- Dokumentation überarbeiten und vervollständigen
- Code Cleanup und Refactorings
- Docker Hub Dokumentation (Overview Page mit README.md)
- Wiki überarbeiten und aktualisieren
- SBOM (Software Bill of Materials) generieren
- Plugin-System
- Performance Optimierungen
- Security Hardening

### Post v1.0 – Stack Sources Management
- Stack Sources Management UI (add/remove/configure sources)
- Git Repository Stack Source Provider

### Post v2.0 – Advanced Features
- Multi-Node-Support (mehrere Docker Hosts pro Environment)
- Zusätzliche Environment-Typen (DockerApi mit TLS, DockerAgent)
- Kubernetes Environment Support
- Advanced Networking Features

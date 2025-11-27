# Roadmap

Grober Ausblick auf geplante Versionen und Features.

## Released

- **v0.1** – Container Ansicht + Start/Stop
- **v0.2** – Local Admin + Hardcoded Stack
- **v0.3** – Wizard + TLS-Bootstrap + Manifest Deploy ✅ (2025-01-19)

## Planned

### v0.4 – Multi-Environment + Simplified Wizard + Docker Compose Deployment
- Polymorphe Environment-Typen (Docker Socket)
- Organisationen ohne Environments (optional)
- Wizard-Vereinfachung (4 Steps → 3 Steps)
- Docker Compose Stack-Deployment (Portainer-style)
- Automatische Environment Variable Detection
- Keine globalen Connection Strings mehr

### v0.5 – Multi-User + Database Migration
- SQLite-Migration für Multi-User-Support
- Container Health Monitoring & Rollback
- RBAC (Role-Based Access Control)

### v0.6 – Release Management
- Release Management UI
- Feature Flags UI
- Advanced Connection Mode
- Custom TLS Certificates

### v0.7 – CI/CD & Monitoring
- CI/CD Pipeline Integration
- Stack Health Dashboard
- Metrics & Alerting
- Audit Logs

### v0.8 – Enhanced Stack Format
- Custom Manifest Format (enhanced validation)
- Beide Stack-Formate gleichzeitig unterstützt (Docker Compose + Custom Manifest)
- Type Validation (Numbers, Booleans, Strings, Select)
- Regex Validation für URL-Formate

### v0.9 – Docker Volumes Management
- Docker Volumes Ansicht (Liste aller Volumes pro Environment)
- Volume Details (Size, Mount Points, Labels)
- Volume erstellen/löschen
- Orphaned Volumes erkennen und aufräumen
- Volume Backup/Export Funktionalität

### v1.0 – Production Ready
- Plugin-System
- Performance Optimierungen
- Comprehensive Documentation
- Security Hardening

### Post v1.0 – Stack Sources Management
- Stack Sources Management UI (add/remove/configure sources)
- Git Repository Stack Source Provider

### Post v2.0 – Advanced Features
- Multi-Node-Support (mehrere Docker Hosts pro Environment)
- Zusätzliche Environment-Typen (DockerApi mit TLS, DockerAgent)
- Kubernetes Environment Support
- Advanced Networking Features

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

### v0.5 – Enhanced Stacks + Multi-User
- Custom Manifest Format (enhanced validation)
- Beide Stack-Formate gleichzeitig unterstützt (Docker Compose + Custom Manifest)
- SQLite-Migration für Multi-User-Support
- Container Health Monitoring & Rollback

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

### v1.0 – Production Ready
- Plugin-System
- Performance Optimierungen
- Comprehensive Documentation
- Security Hardening

### Post v2.0 – Advanced Features
- Multi-Node-Support (mehrere Docker Hosts pro Environment)
- Zusätzliche Environment-Typen (DockerApi mit TLS, DockerAgent)
- Kubernetes Environment Support
- Advanced Networking Features

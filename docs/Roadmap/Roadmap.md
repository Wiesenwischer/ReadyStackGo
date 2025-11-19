# Roadmap

Grober Ausblick auf geplante Versionen und Features.

## Released

- **v0.1** – Container Ansicht + Start/Stop
- **v0.2** – Local Admin + Hardcoded Stack
- **v0.3** – Wizard + TLS-Bootstrap + Manifest Deploy ✅ (2025-01-19)

## Planned

### v0.4 – Multi-Environment + Simplified Wizard + Stack-Config
- Polymorphe Environment-Typen (Docker Socket, Docker API, Docker Agent)
- Organisationen ohne Environments (optional)
- Wizard-Vereinfachung (4 Steps → 3 Steps)
- Stack-spezifische Konfiguration (manifest-driven)
- Keine globalen Connection Strings mehr

### v0.5 – Deployment + Multi-User + Multi-Node
- Zusätzliche Environment-Typen (DockerApi, DockerAgent)
- Vollständige Stack-Deployment-Implementierung
- SQLite-Migration für Multi-User-Support
- Multi-Node-Support (mehrere Docker Hosts pro Environment)
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
- Kubernetes Environment Support
- Plugin-System
- Performance Optimierungen
- Comprehensive Documentation

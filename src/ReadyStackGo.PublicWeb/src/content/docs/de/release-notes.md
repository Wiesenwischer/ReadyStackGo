---
title: Release Notes
description: Änderungen und neue Features in ReadyStackGo
---

## Version 1.0.0

*Veröffentlicht: November 2024*

Dies ist das erste öffentliche Release von ReadyStackGo.

### Neue Features

- **Web-basierte Verwaltungsoberfläche**
  - Dashboard mit Übersicht aller Deployments
  - Stack-Management mit Start/Stop/Restart Funktionen
  - Container-Logs in Echtzeit
  - Environment-Verwaltung

- **Stack Deployment**
  - Ein-Klick Deployment von Docker Compose Stacks
  - Unterstützung für Git-basierte Stack Sources
  - Lokale Stack-Definitionen

- **Multi-Environment Support**
  - Mehrere Docker-Hosts verwalten
  - Verschiedene Umgebungen (Dev/Staging/Prod)
  - Zentrale Konfiguration

- **Setup Wizard**
  - Geführte Ersteinrichtung
  - Admin-Account Erstellung
  - Organisation konfigurieren
  - Environment verbinden

### Systemanforderungen

- Ubuntu 20.04+ (offiziell unterstützt)
- Docker 20.10+
- Mindestens 1 GB RAM
- 5 GB freier Speicherplatz

### Bekannte Einschränkungen

- Keine High-Availability Unterstützung in dieser Version
- Einzelner Admin-Benutzer (Multi-User in zukünftiger Version)

---

## Roadmap

Geplante Features für zukünftige Versionen:

- **v1.1** - Multi-User Support mit Rollenverwaltung
- **v1.2** - Automatische Stack-Updates
- **v1.3** - Backup & Restore Funktionalität
- **v2.0** - Kubernetes Support

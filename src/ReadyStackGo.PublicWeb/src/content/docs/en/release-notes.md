---
title: Release Notes
description: Changes and new features in ReadyStackGo
---

## Version 1.0.0

*Released: November 2024*

This is the first public release of ReadyStackGo.

### New Features

- **Web-based Management Interface**
  - Dashboard with overview of all deployments
  - Stack management with Start/Stop/Restart functions
  - Real-time container logs
  - Environment management

- **Stack Deployment**
  - One-click deployment of Docker Compose stacks
  - Support for Git-based stack sources
  - Local stack definitions

- **Multi-Environment Support**
  - Manage multiple Docker hosts
  - Different environments (Dev/Staging/Prod)
  - Centralized configuration

- **Setup Wizard**
  - Guided initial setup
  - Admin account creation
  - Organization configuration
  - Environment connection

### System Requirements

- Ubuntu 20.04+ (officially supported)
- Docker 20.10+
- Minimum 1 GB RAM
- 5 GB free disk space

### Known Limitations

- No High-Availability support in this version
- Single admin user (Multi-user in future version)

---

## Roadmap

Planned features for future versions:

- **v1.1** - Multi-user support with role management
- **v1.2** - Automatic stack updates
- **v1.3** - Backup & Restore functionality
- **v2.0** - Kubernetes support

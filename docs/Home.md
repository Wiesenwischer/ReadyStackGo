# ReadyStackGo Documentation

Welcome to the documentation of **ReadyStackGo** – a self-hosted platform for easy installation, updating, and management of complex microservice stacks based on Docker.

---

## Project Status

- **CI/CD:** [GitHub Actions](https://github.com/Wiesenwischer/ReadyStackGo/actions)
- **Docker Hub:** [wiesenwischer/readystackgo](https://hub.docker.com/r/wiesenwischer/readystackgo)
- **Repository:** [GitHub](https://github.com/Wiesenwischer/ReadyStackGo)
- **License:** MIT - Copyright © 2025 Marcus Dammann

---

## What is ReadyStackGo?

ReadyStackGo (RSGO) is a self-hosted platform that greatly simplifies the deployment of complex microservice stacks. With a **single admin container**, a **modern web UI**, and **manifest-based deployments**, on-premise customers can install and manage software stacks without direct interaction with Docker Compose or Kubernetes.

### Key Features

- **Single Admin Container** manages the entire stack
- **Guided Setup Wizard** for easy initial installation
- **TLS Handling** (Self-Signed Bootstrap & Custom Certificates)
- **Manifest-based Deployments** for complete stacks (SemVer)
- **Centralized Configuration** via `rsgo.*.json`
- **Feature Flags** (functional switches, cross-context)
- **CI/CD Integration** for automatic stack releases
- **Security** with Admin/Operator roles, JWT & later OIDC
- **Plugin-capable** (planned plugin system)

---

## Quick Start

For a quick introduction:

1. [Getting Started/Overview](Getting-Started/Overview.md) - Project overview
2. [Getting Started/Quick-Start](Getting-Started/Quick-Start.md) - Quick start guide
3. [Getting Started/Installation](Getting-Started/Installation.md) - Installation guide

---

## Documentation Structure

### [Getting Started](Getting-Started/Overview.md)
First steps, quick start, and installation
- [Overview](Getting-Started/Overview.md)
- [Quick Start](Getting-Started/Quick-Start.md)
- [Installation](Getting-Started/Installation.md)

### [Architecture](Architecture/Overview.md)
System architecture and components
- [Architecture Overview](Architecture/Overview.md)
- [Components](Architecture/Components.md)
- [Container Lifecycle](Architecture/Container-Lifecycle.md)
- [Deployment Engine](Architecture/Deployment-Engine.md)

### [Configuration](Configuration/Overview.md)
Configuration management and manifests
- [Configuration Overview](Configuration/Overview.md)
- [Config Files](Configuration/Config-Files.md)
- [Manifest Specification](Configuration/Manifest-Specification.md)
- [Feature Flags](Configuration/Feature-Flags.md)

### [Setup Wizard](Setup-Wizard/Wizard-Flow.md)
Guided installation assistant
- [Wizard Flow](Setup-Wizard/Wizard-Flow.md)

### [Security](Security/Overview.md)
Authentication, authorization, and TLS
- [Security Overview](Security/Overview.md)
- [Authentication](Security/Authentication.md)
- [Authorization](Security/Authorization.md)
- [TLS Configuration](Security/TLS-Configuration.md)

### [Operations](Operations/Release-Management.md)
Operations, updates, and troubleshooting
- [Release Management](Operations/Release-Management.md)
- [Updates](Operations/Updates.md)
- [Troubleshooting](Operations/Troubleshooting.md)

### [Development](Development/Setup-Environment.md)
Development environment and contribution guidelines
- [Development Environment](Development/Setup-Environment.md)
- [Contributing](Development/Contributing.md)
- [Coding Guidelines](Development/Coding-Guidelines.md)
- [Testing](Development/Testing.md)

### [CI/CD](CI-CD/Pipeline-Integration.md)
Continuous Integration & Deployment
- [Pipeline Integration](CI-CD/Pipeline-Integration.md)

### [Reference](Reference/Full-Specification.md)
Technical reference and specifications
- [Full Specification](Reference/Full-Specification.md)
- [Technical Specification](Reference/Technical-Specification.md)
- [API Reference](Reference/API-Reference.md)
- [Configuration Reference](Reference/Configuration-Reference.md)
- [Manifest Schema](Reference/Manifest-Schema.md)

### [Roadmap](Reference/Roadmap.md)
Future plans and features
- [Roadmap](Reference/Roadmap.md)
- [Plugin System](Reference/Plugin-System.md)

---

## For Different Roles

### I am an **Administrator/Operator**
- Start: [Quick Start Guide](Getting-Started/Quick-Start.md)
- Setup: [Setup Wizard](Setup-Wizard/Wizard-Flow.md)
- Operations: [Operations](Operations/Release-Management.md)

### I am a **Developer**
- Setup: [Development Environment](Development/Setup-Environment.md)
- Guidelines: [Contributing](Development/Contributing.md)
- Architecture: [Architecture Overview](Architecture/Overview.md)

### I am an **Architect**
- Architecture: [Architecture](Architecture/Overview.md)
- Specification: [Technical Specification](Reference/Technical-Specification.md)
- Security: [Security](Security/Overview.md)

---

## Help & Support

- **Contributing**: See [Contributing Guide](Development/Contributing.md)
- **Troubleshooting**: See [Troubleshooting](Operations/Troubleshooting.md)
- **Issues**: Create an issue in the repository

---

**Version**: 0.1.0
**Last Updated**: 2025-11-17

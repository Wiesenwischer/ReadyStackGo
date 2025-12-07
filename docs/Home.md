# ReadyStackGo

**ReadyStackGo** (RSGO) is a self-hosted platform for deploying and managing Docker-based microservice stacks. One admin container, a modern web UI, manifest-based deployments – no direct Docker Compose or Kubernetes interaction needed.

| | |
|---|---|
| **Repository** | [GitHub](https://github.com/Wiesenwischer/ReadyStackGo) |
| **Docker Hub** | [wiesenwischer/readystackgo](https://hub.docker.com/r/wiesenwischer/readystackgo) |
| **CI/CD** | [GitHub Actions](https://github.com/Wiesenwischer/ReadyStackGo/actions) |
| **License** | MIT – © 2025 Marcus Dammann |

---

## Quick Start

```bash
curl -fsSL https://raw.githubusercontent.com/Wiesenwischer/ReadyStackGo/main/install.sh | bash
```

Or follow the detailed [Installation Guide](Getting-Started/Installation.md).

---

## Key Features

| Feature | Description |
|---------|-------------|
| **Single Admin Container** | Manages entire stacks from one container |
| **Web UI** | Modern interface for deployment and management |
| **Manifest-based Deployments** | YAML manifests with SemVer versioning |
| **Multi-Stack Products** | Modular products with shared variables and fragments |
| **Setup Wizard** | Guided initial configuration |
| **TLS Support** | Self-signed bootstrap & custom certificates |
| **Feature Flags** | Functional switches across contexts |
| **Role-based Security** | Admin/Operator roles with JWT authentication |

---

## Core Concepts

Understanding these concepts is essential for working with ReadyStackGo:

| Concept | Description | Learn More |
|---------|-------------|------------|
| **Product** | A deployable unit with version, services, and configuration | [Products](Concepts/Products.md) |
| **Multi-Stack** | Products composed of multiple stacks with shared variables | [Multi-Stack](Concepts/Multi-Stack.md) |
| **Fragment** | Reusable, includable stack components | [Stack Fragments](Concepts/Stack-Fragments.md) |
| **Variables** | Configurable parameters with types and validation | [Products → Variables](Concepts/Products.md#variablen) |

→ [Best Practices](Concepts/Best-Practices.md) for stack definitions

---

## Documentation

### Getting Started
| Topic | Description |
|-------|-------------|
| [Overview](Getting-Started/Overview.md) | Project introduction |
| [Quick Start](Getting-Started/Quick-Start.md) | Fast-track setup |
| [Installation](Getting-Started/Installation.md) | Detailed installation guide |

### Concepts
| Topic | Description |
|-------|-------------|
| [Products](Concepts/Products.md) | Deployable units with services and variables |
| [Multi-Stack](Concepts/Multi-Stack.md) | Modular products with multiple stacks |
| [Stack Fragments](Concepts/Stack-Fragments.md) | Reusable stack components |
| [Best Practices](Concepts/Best-Practices.md) | Recommendations for stack design |

### Architecture
| Topic | Description |
|-------|-------------|
| [Overview](Architecture/Overview.md) | System architecture |
| [Components](Architecture/Components.md) | Core components |
| [Container Lifecycle](Architecture/Container-Lifecycle.md) | Container management |
| [Deployment Engine](Architecture/Deployment-Engine.md) | How deployments work |

### Configuration
| Topic | Description |
|-------|-------------|
| [Overview](Configuration/Overview.md) | Configuration concepts |
| [Config Files](Configuration/Config-Files.md) | rsgo.*.json files |
| [Manifest Specification](Configuration/Manifest-Specification.md) | YAML manifest format |
| [Feature Flags](Configuration/Feature-Flags.md) | Feature toggle system |
| [Registries](Configuration/Registries.md) | Docker registry configuration |

### Security
| Topic | Description |
|-------|-------------|
| [Overview](Security/Overview.md) | Security concepts |
| [Authentication](Security/Authentication.md) | Login and JWT |
| [Authorization](Security/Authorization.md) | Roles and permissions |
| [TLS Configuration](Security/TLS-Configuration.md) | HTTPS setup |
| [Initial Setup](Security/Initial-Setup.md) | First-time security setup |

### Operations
| Topic | Description |
|-------|-------------|
| [Release Management](Operations/Release-Management.md) | Version management |
| [Updates](Operations/Updates.md) | Updating RSGO and stacks |
| [Troubleshooting](Operations/Troubleshooting.md) | Common issues |

### Reference
| Topic | Description |
|-------|-------------|
| [Manifest Schema](Reference/Manifest-Schema.md) | Complete YAML schema |
| [API Reference](Reference/API-Reference.md) | REST API documentation |
| [Configuration Reference](Reference/Configuration-Reference.md) | All config options |
| [Technical Specification](Reference/Technical-Specification.md) | Implementation details |

### Development
| Topic | Description |
|-------|-------------|
| [Setup Environment](Development/Setup-Environment.md) | Dev environment setup |
| [Git Workflow](Development/Git-Workflow.md) | Branching strategy |
| [Coding Guidelines](Development/Coding-Guidelines.md) | Code standards |
| [Testing](Development/Testing.md) | Test strategy |
| [Contributing](Development/Contributing.md) | How to contribute |

### CI/CD
| Topic | Description |
|-------|-------------|
| [Pipeline Integration](CI-CD/Pipeline-Integration.md) | CI/CD setup |
| [Workflows](CI-CD/Workflows.md) | GitHub Actions workflows |

---

## By Role

### Administrator / Operator
1. [Installation](Getting-Started/Installation.md) – Install ReadyStackGo
2. [Setup Wizard](Setup-Wizard/Wizard-Flow.md) – Initial configuration
3. [Operations](Operations/Release-Management.md) – Day-to-day management

### Stack Developer
1. [Products](Concepts/Products.md) – Create deployable products
2. [Multi-Stack](Concepts/Multi-Stack.md) – Build modular stacks
3. [Manifest Schema](Reference/Manifest-Schema.md) – YAML reference

### Platform Developer
1. [Setup Environment](Development/Setup-Environment.md) – Development setup
2. [Architecture](Architecture/Overview.md) – System design
3. [Contributing](Development/Contributing.md) – Contribution guide

---

## Roadmap

See [Roadmap](Reference/Roadmap.md) for planned features and [Plugin System](Reference/Plugin-System.md) for extensibility plans.

---

## Help & Support

- **Issues**: [GitHub Issues](https://github.com/Wiesenwischer/ReadyStackGo/issues)
- **Contributing**: [Contributing Guide](Development/Contributing.md)
- **Troubleshooting**: [Troubleshooting](Operations/Troubleshooting.md)

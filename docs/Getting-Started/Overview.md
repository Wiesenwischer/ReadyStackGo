# Getting Started - Overview

ReadyStackGo is a self-hosted platform for easy installation, updating, and management of complex microservice stacks based on Docker.

## What is ReadyStackGo?

ReadyStackGo (RSGO) solves the problem of complex deployment of microservice architectures in on-premise environments. Instead of manual Docker commands, complex Compose files, or Kubernetes configurations, you get:

- **A single admin container** that manages the entire stack
- **A guided setup wizard** for initial installation
- **A modern web UI** for management
- **Manifest-based deployments** for reproducible installations

## Who is ReadyStackGo for?

### On-Premise Customers
Customers who operate software in their own data centers and need a simple installation solution.

### Partners & Resellers
Partners who need to install and maintain software stacks for their customers.

### Development Teams
Teams that need identical environments for development, testing, and production.

### Edge & IoT
Installations in isolated or restricted network environments.

## Core Concepts

### Admin Container
The central container that manages all other containers. It is the only container you need to start manually.

### Manifests
Declarative descriptions of the desired state of a software stack. Manifests define which containers should be deployed in which versions.

### Setup Wizard
A guided installation assistant that simplifies the initial setup (admin user, organization, connections, installation).

### Feature Flags
Cross-context switches for enabling/disabling functional features.

## Technical Requirements

- **Docker Engine** (version 20.10 or higher)
- **Linux, Windows, or macOS** with Docker support
- **Access to Docker Socket** (`/var/run/docker.sock`)
- **Ports**: 8080 (HTTP), 8443 (HTTPS)
- Optional: **Custom TLS certificate** (otherwise self-signed)

## Next Steps

1. [Quick Start](Quick-Start.md) - Quick start guide
2. [Installation](Installation.md) - Detailed installation guide
3. [Setup Wizard](../Setup-Wizard/Wizard-Flow.md) - Wizard walkthrough

## Helpful Resources

- [Architecture Overview](../Architecture/Overview.md) - System architecture
- [Configuration](../Configuration/Config-Files.md) - Configuration files
- [Security](../Security/Overview.md) - Security concepts

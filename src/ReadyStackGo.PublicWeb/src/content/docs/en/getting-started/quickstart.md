---
title: Quickstart
description: Get ReadyStackGo running in 5 minutes
---

This guide will help you get ReadyStackGo up and running on your server in just a few minutes.

## Prerequisites

- Linux server (Ubuntu 20.04+ recommended)
- Root or sudo access
- Internet connection

## Installation

Run this command on your server:

```bash
curl -fsSL https://readystackgo.pages.dev/install.sh | sudo bash
```

This script automatically:
- Installs Docker (if not present)
- Creates required directories
- Starts the ReadyStackGo container

## Access ReadyStackGo

After installation, open your browser and navigate to:

```
http://<your-server-ip>:8080
```

## Initial Setup

On first access, the Setup Wizard will guide you through:

1. **Create Admin Account** - Set up your administrator credentials
2. **Configure Organization** - Define your organization details
3. **Add Environment** - Connect to your Docker host

## Next Steps

- [Detailed Installation Guide](/en/getting-started/installation/) - More installation options
- [Initial Setup](/en/getting-started/initial-setup/) - Detailed setup wizard walkthrough
- [First Deployment](/en/getting-started/first-deployment/) - Deploy your first container stack

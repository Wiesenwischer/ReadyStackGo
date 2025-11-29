---
title: Bootstrap Script
description: Automatic installation with the convenience script
---

The easiest way is to install with our bootstrap script. This script takes care of everything – including Docker installation if needed.

## Quick Installation

**Standard installation (Port 8080):**

```bash
curl -fsSL https://get.readystackgo.io/install.sh | sudo bash
```

**With alternative port:**

```bash
curl -fsSL https://get.readystackgo.io/install.sh | sudo bash -s 3000
```

:::note
Docker is automatically installed if not present.
:::

---

## What Does the Script Do?

The bootstrap script automatically performs the following steps:

| Step | Description |
|------|-------------|
| **1. System Check** | Detects your Linux distribution and package manager |
| **2. Docker Installation** | Installs Docker if not present (via `get.docker.com`) |
| **3. Docker Start** | Starts and enables the Docker service |
| **4. Directory Structure** | Creates `/var/readystackgo` for persistent data |
| **5. Container Start** | Downloads and starts the ReadyStackGo container |

---

## Successful Installation

After successful installation, the script displays the URL:

```
[OK] Docker installed
[OK] ReadyStackGo is now running!
URL: http://192.168.1.100:8080
```

---

## Verify Installation

```bash
# Show container status
docker ps | grep readystackgo

# Show container logs
docker logs readystackgo
```

Successful output:

```
CONTAINER ID   IMAGE                              STATUS         PORTS                    NAMES
abc123...      ghcr.io/ams/readystackgo:latest    Up 2 minutes   0.0.0.0:8080->8080/tcp   readystackgo
```

---

## Next Steps

After installation, you can access ReadyStackGo at `http://<server-ip>:8080`.

➡️ Continue to [Initial Setup](/en/getting-started/initial-setup/)

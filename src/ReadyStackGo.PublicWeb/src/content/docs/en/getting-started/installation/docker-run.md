---
title: Docker Run
description: Manual installation with docker run
---

If Docker is already installed, you can start ReadyStackGo directly with `docker run`.

## Prerequisite

Docker must already be installed and running:

```bash
# Check Docker version
docker --version

# Start Docker service (if needed)
sudo systemctl start docker
```

---

## Installation

### Step 1: Create Data Directory

```bash
sudo mkdir -p /var/readystackgo
```

### Step 2: Start Container

```bash
docker run -d \
  --name readystackgo \
  --restart unless-stopped \
  -p 8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /var/readystackgo:/data \
  ghcr.io/ams/readystackgo:latest
```

---

## Parameters Explained

| Parameter | Description |
|-----------|-------------|
| `-d` | Run container in background |
| `--name readystackgo` | Container name |
| `--restart unless-stopped` | Auto-restart after system reboot |
| `-p 8080:8080` | Port mapping (Host:Container) |
| `-v /var/run/docker.sock:...` | Docker socket for container management |
| `-v /var/readystackgo:/data` | Persistent data (configuration, deployments) |

---

## Alternative Port

To use a different port, change the port mapping:

```bash
docker run -d \
  --name readystackgo \
  --restart unless-stopped \
  -p 3000:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /var/readystackgo:/data \
  ghcr.io/ams/readystackgo:latest
```

In this example, ReadyStackGo is accessible on port `3000`.

---

## Next Steps

After installation, you can access ReadyStackGo at `http://<server-ip>:8080`.

➡️ Continue to [Initial Setup](/en/getting-started/initial-setup/)

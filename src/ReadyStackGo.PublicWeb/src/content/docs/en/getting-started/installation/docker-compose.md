---
title: Docker Compose
description: Manual installation with Docker Compose
---

For a more structured configuration, you can use Docker Compose. This method is particularly suitable if you want to version or extend the configuration.

## Prerequisite

Docker and Docker Compose must be installed:

```bash
# Check Docker version
docker --version

# Check Docker Compose version
docker compose version
```

---

## Installation

### Step 1: Create Directory

```bash
sudo mkdir -p /opt/readystackgo
cd /opt/readystackgo
```

### Step 2: Create docker-compose.yml

Create a file `docker-compose.yml` with the following content:

```yaml
services:
  readystackgo:
    image: ghcr.io/ams/readystackgo:latest
    container_name: readystackgo
    restart: unless-stopped
    ports:
      - "8080:8080"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - readystackgo-data:/data

volumes:
  readystackgo-data:
```

### Step 3: Start Container

```bash
docker compose up -d
```

---

## Extended Configuration

You can extend the Compose file as needed:

```yaml
services:
  readystackgo:
    image: ghcr.io/ams/readystackgo:latest
    container_name: readystackgo
    restart: unless-stopped
    ports:
      - "8080:8080"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - readystackgo-data:/data
    environment:
      - TZ=Europe/Berlin
    deploy:
      resources:
        limits:
          memory: 512M

volumes:
  readystackgo-data:
```

---

## Container Management

```bash
# Start container
docker compose up -d

# Stop container
docker compose down

# View logs
docker compose logs -f

# Restart container
docker compose restart

# Update to latest version
docker compose pull && docker compose up -d
```

---

## Next Steps

After installation, you can access ReadyStackGo at `http://<server-ip>:8080`.

➡️ Continue to [Initial Setup](/en/getting-started/initial-setup/)

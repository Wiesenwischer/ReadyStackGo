# Quick Start

This guide walks you through the fastest way to get ReadyStackGo up and running.

## Prerequisites

Make sure Docker is installed:

```bash
docker --version
# Docker version 20.10.0 or higher
```

## Step 1: Start the Admin Container

Start the ReadyStackGo admin container:

```bash
docker run -d \
  --name readystackgo-admin \
  -p 8443:8443 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v rsgo-config:/app/config \
  --restart unless-stopped \
  your-registry/readystackgo/admin:0.1.0
```

### Parameter Explanation

- `-p 8443:8443` - HTTPS port for the web UI
- `-v /var/run/docker.sock:/var/run/docker.sock` - Docker socket access
- `-v rsgo-config:/app/config` - Persistent configuration
- `--restart unless-stopped` - Automatic restart

## Step 2: Open the Web UI

Open in your browser:

```
https://<your-server-ip>:8443
```

**Note**: On first start, a self-signed certificate is used. Accept the security warning in your browser.

## Step 3: Complete the Setup Wizard

The wizard guides you through 4 steps:

### 1. Create Admin User
- **Username**: e.g., `admin`
- **Password**: Choose a secure password

### 2. Define Organization
- **ID**: Technical ID (e.g., `customer-a`)
- **Name**: Display name (e.g., `Customer A Inc.`)

### 3. Configure Connections (Simple Mode)
- **Transport**: Connection string for message queue
- **Persistence**: Database connection string
- **EventStore**: EventStore connection string (optional)

Examples:
```
Transport: amqp://rabbitmq:5672
Persistence: Server=sqlserver;Database=ams;User=sa;Password=***
EventStore: esdb://eventstore:2113
```

### 4. Install Stack
- Select manifest (e.g., `v4.3.0`)
- Start installation
- Wait until all containers are started

## Step 4: Log In

After successful installation:

1. Wizard ends
2. Login page appears
3. Log in with admin credentials

## Done!

You can now:

- View container status
- Manage feature flags
- Upload TLS certificate
- Perform updates

## Next Steps

- [Installation (detailed)](Installation.md)
- [Wizard Flow](../Setup-Wizard/Wizard-Flow.md)
- [Configuration](../Configuration/Config-Files.md)

## Troubleshooting

### Container won't start
```bash
docker logs readystackgo-admin
```

### Port already in use
Change the port mapping:
```bash
-p 9443:8443
```

### Docker Socket Permission denied
Make sure the Docker socket is accessible:
```bash
sudo chmod 666 /var/run/docker.sock
```

More help: [Troubleshooting](../Operations/Troubleshooting.md)

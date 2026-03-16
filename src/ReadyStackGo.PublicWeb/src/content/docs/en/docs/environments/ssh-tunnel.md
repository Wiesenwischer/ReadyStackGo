---
title: SSH Tunnel Environments
description: Connect remote Docker servers as deployment targets via SSH tunnel
---

ReadyStackGo can use Docker hosts on remote servers as deployment targets via **SSH tunnels**. No exposed Docker port needed, no agent on the remote host — just SSH access.

## Overview

| Environment Type | Connection | Use Case |
|-----------------|-----------|----------|
| **Docker Socket** | Local Unix socket | ReadyStackGo runs on the same host as Docker |
| **SSH Tunnel** | SSH → Remote Docker Socket | Docker on a remote server, reachable via SSH |

:::tip[Why SSH Tunnel?]
SSH tunnel is the simplest way to connect remote Docker hosts. Every Linux server has SSH — no additional software, no firewall rules, no TLS certificates needed.
:::

---

## Step by Step: Creating an SSH Tunnel Environment

### Step 1: Open Environments

Navigate to **Environments** in the main menu. Here you can see all configured Docker environments.

![Environments list](/images/docs/environments/ssh-tunnel-01-environments-list.png)

---

### Step 2: Select Type

Click **Add Environment**. On the creation page you'll see the **Connection Type Selector** with two options:

- **Local Docker Socket** – Direct access to the local Docker socket
- **SSH Tunnel** – Connection to a remote Docker host via SSH

![Type selector on the Add Environment page](/images/docs/environments/ssh-tunnel-02-type-selector.png)

Click **SSH Tunnel** to open the SSH form.

---

### Step 3: Enter SSH Connection Details

The form displays the SSH-specific fields:

![SSH Tunnel form](/images/docs/environments/ssh-tunnel-03-ssh-form.png)

| Field | Required | Description |
|-------|----------|-------------|
| **Environment Name** | Yes | Descriptive name (e.g. "Production Server") |
| **SSH Host** | Yes | IP address or hostname of the remote server |
| **SSH Port** | No | Default: 22 |
| **SSH Username** | Yes | Username for the SSH connection (e.g. "root", "deploy") |
| **Authentication Method** | Yes | Private Key (recommended) or Password |
| **Private Key / Password** | Yes | SSH key in PEM format or password |
| **Remote Docker Socket Path** | No | Default: `/var/run/docker.sock` |

---

### Step 4: Configure Authentication

Choose your preferred authentication method:

**Private Key** (recommended): Paste the complete private SSH key in PEM format.

**Password**: Enter the SSH password.

![Password authentication](/images/docs/environments/ssh-tunnel-04-password-auth.png)

:::caution[Security]
SSH credentials are stored encrypted with **AES-256-CBC** in the database. The master key can be set via the environment variable `RSGO_ENCRYPTION_KEY`. Without this variable, a key is automatically generated and stored in `/data/.encryption-key`.
:::

---

### Step 5: Test Connection

Fill in all fields and click **Test Connection** to verify the SSH connection and Docker access.

![Filled SSH form](/images/docs/environments/ssh-tunnel-05-filled-form.png)

The test:
1. Establishes an SSH connection
2. Creates a TCP tunnel to the Docker socket
3. Retrieves Docker System Info
4. Displays Docker version and success/failure

![Test Connection button](/images/docs/environments/ssh-tunnel-06-test-connection.png)

---

### Step 6: Create Environment

Click **Create Environment**. The SSH Tunnel environment is immediately ready for deployments, health monitoring, and container management.

---

## Technical Details

### How Does the SSH Tunnel Work?

```
RSGO Controller                         Remote Host
┌─────────────────┐    SSH Tunnel       ┌─────────────────┐
│ SshTunnelManager │────Port 22────────→│ sshd             │
│ (SSH.NET)        │                    │                  │
│                  │                    │  socat bridge    │
│ DockerService    │    TCP-Tunnel      │  TCP → Unix      │
│ (Docker.DotNet)  │──localhost:random──→│  docker.sock    │
└─────────────────┘                    └─────────────────┘
```

1. **SSH Connection**: SSH.NET establishes an SSH connection to the remote host
2. **Socat Bridge**: On the remote host, `socat` creates a bridge from TCP to Unix socket
3. **Port Forwarding**: SSH Local Port Forwarding routes `localhost:random-port` to the socat port
4. **Docker Client**: Docker.DotNet connects via `tcp://localhost:random-port` through the tunnel

### Prerequisites on the Remote Host

- **SSH access** (port 22 or custom)
- **Docker installed** and running
- **socat installed** (`apt install socat` / `yum install socat`)
- SSH user must have access to the Docker socket (group `docker`)

### Encryption

SSH credentials (private keys and passwords) are stored encrypted with AES-256-CBC:

| Configuration | Description |
|--------------|-------------|
| `RSGO_ENCRYPTION_KEY` | Environment variable for the master key |
| `/data/.encryption-key` | Auto-generated key (fallback) |

:::note[Production]
In production environments, always set `RSGO_ENCRYPTION_KEY` as an environment variable so that encryption survives container restarts.
:::

---

## Error Handling

| Error | Cause | Solution |
|-------|-------|----------|
| Connection refused | SSH server not reachable | Check host/port, verify firewall |
| Authentication failed | Wrong credentials | Check username/key/password |
| socat not available | socat not installed | `apt install socat` on the remote host |
| Permission denied on socket | No Docker socket access | Add user to `docker` group |
| Connection timeout | Network issue | Verify SSH reachability (`ssh user@host`) |

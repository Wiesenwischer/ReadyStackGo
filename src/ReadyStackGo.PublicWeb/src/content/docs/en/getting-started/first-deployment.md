---
title: First Deployment
description: Deploy your first container stack with ReadyStackGo
---

After completing the [initial setup](/en/getting-started/initial-setup/), you can deploy your first container stack. This guide walks you through the process step by step.

## Prerequisites

- ReadyStackGo is installed and running
- Setup wizard has been completed
- At least one environment is configured

---

## Step 1: Add Stack Source

Stack sources define where ReadyStackGo finds your stack definitions (docker-compose files).

1. Navigate to **Stack Sources** in the sidebar
2. Click **Add Stack Source**
3. Choose a source type:
   - **Git Repository** - For stacks stored in Git
   - **Local Path** - For local stack definitions

### Example: Git Repository

```
Repository URL: https://github.com/your-org/stacks.git
Branch: main
Path: /stacks
```

---

## Step 2: Browse Available Stacks

After adding a stack source:

1. Navigate to **Stacks** in the sidebar
2. You'll see all available stacks from your sources
3. Click on a stack to view details

---

## Step 3: Deploy a Stack

1. Select the stack you want to deploy
2. Choose the target **Environment**
3. Review the configuration
4. Click **Deploy**

ReadyStackGo will:
- Pull required images
- Create the network
- Start all containers
- Monitor the deployment

---

## Step 4: Monitor Deployment

After deployment:

1. The stack appears in your **Deployments** list
2. View container status in real-time
3. Access logs for troubleshooting
4. Manage individual containers

---

## Example Stack

Here's a simple example stack you can use for testing:

```yaml
# docker-compose.yml
services:
  nginx:
    image: nginx:alpine
    ports:
      - "8080:80"
    restart: unless-stopped
```

---

## Next Steps

- Explore the **Dashboard** for an overview of all deployments
- Configure **multiple environments** for dev/staging/production
- Set up **automatic updates** for your stacks

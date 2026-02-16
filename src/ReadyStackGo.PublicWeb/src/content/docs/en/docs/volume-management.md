---
title: Volume Management
description: Manage Docker Volumes per environment, detect orphaned volumes and clean up
---

This guide shows you how to manage Docker Volumes in ReadyStackGo — from overview and details to creating and deleting volumes.

## Overview

ReadyStackGo provides centralized management for Docker Volumes per environment. You can monitor the persistent storage of your stacks and identify orphaned volumes.

| Feature | Description |
|---------|-------------|
| **Volume List** | Display all Docker Volumes of the active environment |
| **Orphaned Detection** | Detect volumes without container references |
| **Volume Details** | View size, mountpoint, labels and referencing containers |
| **Create Volume** | Create new volumes with name and optional driver |
| **Delete Volume** | Remove individual or all orphaned volumes |
| **Orphaned Filter** | Filter the list to show only orphaned volumes |

---

## Step by Step: Managing Volumes

### Step 1: Open the Volumes Page

Navigate to **Volumes** in the sidebar. The page displays all Docker Volumes of the active environment in a table with name, driver, container count, status and creation date.

![Volume list with overview of all volumes](/images/docs/volumes-01-list.png)

---

### Step 2: Create a Volume

Click **Create Volume** to reveal the creation form. Enter a **Volume Name** and optionally a **Driver** (default: `local`).

![Create form for a new volume](/images/docs/volumes-02-create-form.png)

Click **Create** to provision the volume. It will then appear in the list.

![Newly created volume visible in the list](/images/docs/volumes-03-volume-created.png)

:::tip[Driver]
The default `local` driver stores data on the host filesystem. For special requirements (NFS, CIFS etc.) you can specify a different driver.
:::

---

### Step 3: View Volume Details

Click on the **volume name** in the list to open the detail page. There you'll find:

- **Volume Information**: Name, driver, scope, mountpoint, size and creation date
- **Referenced by Containers**: List of all containers using this volume

![Volume detail page with information and container references](/images/docs/volumes-04-detail.png)

---

### Step 4: Detect Orphaned Volumes

Volumes without container references are automatically marked as **orphaned** (yellow badge). These volumes occupy storage space but are not used by any container.

![Orphaned badge on an unused volume](/images/docs/volumes-05-orphaned-badge.png)

Use the **Orphaned only** filter to show only orphaned volumes:

![Orphaned filter active — only orphaned volumes visible](/images/docs/volumes-06-orphaned-filter.png)

:::note[Orphaned Detection]
The orphaned detection checks whether a volume is referenced as a mount in any running or stopped container. Volumes without references are considered orphaned.
:::

---

### Step 5: Delete a Volume

Click **Remove** on a volume. An inline confirmation appears with **Confirm** and **Cancel**:

![Delete confirmation with Confirm/Cancel buttons](/images/docs/volumes-07-delete-confirm.png)

Click **Confirm** to permanently remove the volume.

:::caution[Irreversible]
Deleting a volume removes all data stored within it. This action cannot be undone.
:::

**Bulk Delete**: If orphaned volumes exist, a **Remove Orphaned** button appears that lets you delete all orphaned volumes at once (after confirmation).

---

## Deleting from the Detail Page

On the detail page, you can delete a volume via the **Remove Volume** button. For volumes still referenced by containers, force mode is used.

:::caution[In-Use Volumes]
When deleting a volume that is still referenced by containers, a warning is displayed. Removing in force mode may cause data loss in running containers.
:::

---

## API Reference

Volume management is also available via REST API:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/volumes?environment={id}` | GET | List all volumes |
| `/api/volumes/{name}?environment={id}` | GET | Get volume details |
| `/api/volumes?environment={id}` | POST | Create a volume |
| `/api/volumes/{name}?environment={id}&force={bool}` | DELETE | Delete a volume |

---

## Related Links

- [Stack Deployment](/en/docs/stack-deployment/) - Deploy stacks
- [CI/CD Integration](/en/docs/ci-cd-integration/) - Automated deployments

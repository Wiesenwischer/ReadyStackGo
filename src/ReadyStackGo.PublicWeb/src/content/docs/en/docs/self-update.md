---
title: Self-Update
description: Update ReadyStackGo with a single click — directly from the web interface
---

ReadyStackGo can update itself without manually running `docker compose pull` on the server. When a new version is available, an **update banner** appears in the sidebar. Clicking **Update now** starts the entire update process automatically.

## Overview

| Step | What happens |
|------|-------------|
| **Detection** | RSGO checks the GitHub Releases API for newer versions |
| **Pull** | The new Docker image is downloaded automatically |
| **Swap** | A helper container takes over the running container replacement |
| **Update page** | A maintenance page is shown on port 8080 during the restart |
| **Done** | The browser automatically detects when the new version is running and reloads |

---

## Step by Step: Performing an Update

### Step 1: Recognize the Update Banner

When a new version is available, a banner automatically appears in the left sidebar below the navigation. The banner shows the available version number and two actions:

- **Update now** — Starts the update immediately
- **See what's new** — Opens the release notes on GitHub

![Sidebar with update banner](/images/docs/self-update-01-update-banner.png)

:::tip[Dismissing the Banner]
You can close the banner with the **X** button. It will only reappear when an even newer version becomes available.
:::

---

### Step 2: Start the Update

Click **Update now**. The banner switches to the update status with a spinner and the message:

> *Updating to vX.Y.Z... RSGO will restart momentarily.*

![Update in progress](/images/docs/self-update-02-updating.png)

Behind the scenes, the following happens:

1. The new image (`wiesenwischer/readystackgo:X.Y.Z`) is downloaded
2. A new container with the same configuration is prepared
3. A helper container (`rsgo-updater`) takes over the swap
4. The old container is stopped and removed
5. The new container is renamed and started

---

### Step 3: Automatic Restart

During the container swap, RSGO is briefly unavailable. During this time:

- The **helper container** serves a maintenance page with auto-refresh
- The web UI **polls automatically** every 3 seconds to check if the new server is ready
- Once the new version responds, the page **reloads automatically**

You don't need to do anything — just wait for the page to refresh.

---

## Error Handling

If the update fails (e.g., because the image cannot be downloaded), RSGO displays an error message:

![Update error](/images/docs/self-update-03-error.png)

Common error causes:

| Error | Cause | Solution |
|-------|-------|----------|
| Docker error: unable to pull image | No internet connection or image not found | Check network, verify version exists on Docker Hub |
| Docker error: connection refused | Docker socket not accessible | Verify `/var/run/docker.sock` is mounted as a volume |
| Update timeout | New container fails to start | Check container logs: `docker logs readystackgo` |

:::caution[Prerequisites]
For self-update to work, the RSGO container must have access to the **Docker socket** (`/var/run/docker.sock`). This is already configured in the default installation.
:::

---

## Technical Details

### Helper Container

The update is performed by a separate helper container (`wiesenwischer/rsgo-updater`). It:

- Is based on `docker:cli` (Alpine with Docker CLI)
- Runs with `--network=host` and `AutoRemove`
- Stops the old RSGO container, renames the new one, and starts it
- Serves a maintenance page during the swap
- Cleans itself up automatically when done

### Version Detection

RSGO detects new versions via the **GitHub Releases API**. The current version is embedded as assembly metadata at build time. Local development builds use `0.0.0-dev` as the version number.

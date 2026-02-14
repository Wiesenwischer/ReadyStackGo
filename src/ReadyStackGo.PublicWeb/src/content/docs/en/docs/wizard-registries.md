---
title: Wizard – Container Registries
description: Container registry detection and verification in the Setup Wizard
---

In the fifth step of the Setup Wizard, ReadyStackGo automatically detects which container registries your selected stacks require and verifies access to each registry.

## Overview

After selecting Stack Sources in the previous step, ReadyStackGo analyzes all image references from the available stacks and groups them by registry and namespace. The wizard then displays a two-column view:

| Column | Description |
|--------|-------------|
| **Action Required** | Registries that have not been verified yet — enter credentials or check access here |
| **Verified** | Registries with confirmed access — either public or with valid credentials |

---

## Step by Step: Configuring Registries

### Step 1: Two-Column Layout

After automatic detection, you see all required registries in two columns. Public registries (e.g., Docker Hub `library/*`) are automatically checked and moved to the "Verified" column on success.

![Wizard Registries Step with two-column layout](/images/docs/wizard-reg-01-columns.png)

Each card in the "Action Required" column shows:
- **Registry name** (host and namespace)
- **Image count** from this registry
- **Credential fields** (Username and Password/Token)
- **Check Access** button to verify access
- **Skip** button to skip configuration

---

### Step 2: Verify Access

For public registries, click **Check Access** directly — without entering credentials. ReadyStackGo checks via the Docker Registry v2 API whether anonymous access is possible.

![Registry cards with credential fields and buttons](/images/docs/wizard-reg-02-action-card.png)

For private registries:
1. Enter **Username** and **Password / Token**
2. Click **Check Access**
3. On successful authentication, the registry moves to the "Verified" column

:::tip[Registry Types]
In the "Verified" column, a **globe icon** indicates public registries and a **lock icon** indicates authenticated registries.
:::

---

### Step 3: Skip a Registry

If you don't want to configure a registry right away, click **Skip**. The registry moves to the "Skipped" section with an **Undo** button.

![Registry skipped with undo option](/images/docs/wizard-reg-03-skipped.png)

:::note[Configure Later]
Skipped registries can be configured at any time via **Settings → Container Registries**.
:::

---

### Step 4: Complete Setup

Click **Continue** to proceed to the final wizard step. Alternatively, use **Skip for now** to skip the entire registries step.

![Complete Setup step after registries](/images/docs/wizard-reg-07-complete.png)

---

## Automatic Detection

ReadyStackGo detects registries from image references in stack definitions:

| Image Reference | Detected Registry | Namespace |
|-----------------|-------------------|-----------|
| `nginx:latest` | `docker.io` | `library` |
| `ghcr.io/myorg/myapp:v1` | `ghcr.io` | `myorg` |
| `registry.gitlab.com/team/project:latest` | `registry.gitlab.com` | `team` |
| `myacr.azurecr.io/services/api:2.0` | `myacr.azurecr.io` | `services` |

### Heuristics

- Images without a host (e.g., `nginx`) are automatically mapped to `docker.io/library/`
- `docker.io/library/*` is marked as "likely public"
- `mcr.microsoft.com` is also recognized as public
- All other registries are initially classified as "Action Required"

---

## Access Verification (v2 API Flow)

The verification follows the Docker Registry v2 authentication protocol:

1. **GET /v2/** — if 200 → registry is fully public
2. **401 with Bearer Challenge** → start token flow
3. **Request token** with `scope=repository:{namespace}/{repo}:pull`
4. **List tags** with the obtained token
5. If tag listing succeeds → public / authenticated confirmed

:::caution[Network Required]
Access verification requires a network connection from the ReadyStackGo container to the respective registries. In isolated networks, verification may fail.
:::

---

## Related Links

- [Registry Management](/en/docs/registry-management/) — Manage registries via the Settings page
- [Stack Sources](/en/docs/stack-sources/) — Configure Stack Sources
- [Stack Deployment](/en/docs/stack-deployment/) — Deploy stacks

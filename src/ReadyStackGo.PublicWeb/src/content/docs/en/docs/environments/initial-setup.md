---
title: Initial Setup
description: First-time setup wizard and guided onboarding for ReadyStackGo
---

When you start ReadyStackGo for the first time, a two-phase setup process guides you through the initial configuration: the **Wizard** for admin account creation, followed by the **Onboarding** for environment and stack source setup.

## Overview

| Phase | Purpose | Required |
|-------|---------|----------|
| **Wizard** | Create admin account | Yes (5-minute timeout) |
| **Onboarding Step 1** | Create organization | Yes |
| **Onboarding Step 2** | Add Docker environment | Skippable |
| **Onboarding Step 3** | Select stack sources | Skippable |
| **Onboarding Step 4** | Configure container registries | Skippable |

---

## Phase 1: Admin Creation (Wizard)

On first start, ReadyStackGo opens the Wizard page. You have **5 minutes** to create the admin account before the setup window locks.

![Wizard admin creation page](/images/docs/onboarding-01-wizard-admin.png)

Enter a username and a strong password (minimum 8 characters), then confirm the password.

![Filled admin creation form](/images/docs/onboarding-02-wizard-filled.png)

Click **Continue** to create the admin account. You'll be automatically logged in and redirected to the onboarding.

:::caution[Timeout]
If the 5-minute window expires without creating an admin, the setup locks. Restart the container with `docker restart readystackgo` to get a new 5-minute window.
:::

---

## Phase 2: Guided Onboarding

### Step 1: Create Organization

Every ReadyStackGo instance belongs to one organization. Enter an **Organization ID** (lowercase, letters/numbers/hyphens) and a **Display Name**.

![Organization setup step](/images/docs/onboarding-03-organization.png)

This step is mandatory — you cannot skip it.

---

### Step 2: Add Docker Environment

Connect ReadyStackGo to a Docker daemon. The Docker socket path is auto-detected from the server.

![Docker environment setup step](/images/docs/onboarding-04-environment.png)

:::tip[Skip for now]
You can skip this step and add environments later via **Settings → Environments**.
:::

---

### Step 3: Select Stack Sources

Choose curated stack sources to add to your instance. Featured sources are pre-selected. These provide ready-to-deploy stack definitions.

![Stack sources selection step](/images/docs/onboarding-05-sources.png)

You can add or remove stack sources later in **Settings → Stack Sources**.

---

### Step 4: Container Registries

ReadyStackGo detects container registries used by your stack sources and checks access. Public registries are automatically verified. For private registries, enter credentials.

![Container registries configuration step](/images/docs/onboarding-06-registries.png)

You can skip this step and configure registries later in **Settings → Container Registries**.

---

### Step 5: Setup Complete

The completion screen shows a summary of what was configured. Skipped items are marked accordingly.

![Onboarding completion screen](/images/docs/onboarding-07-complete.png)

Click **Go to Dashboard** to start using ReadyStackGo.

![Dashboard after initial setup](/images/docs/onboarding-08-dashboard.png)

---

## After Setup

All settings configured during onboarding can be changed later:

- **Organization**: Settings → Organization
- **Environments**: Settings → Environments
- **Stack Sources**: Settings → Stack Sources
- **Container Registries**: Settings → Container Registries

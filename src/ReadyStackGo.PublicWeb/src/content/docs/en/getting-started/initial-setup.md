---
title: Initial Setup
description: Complete the ReadyStackGo Setup Wizard
---

After [installation](/en/getting-started/installation/), the setup wizard guides you through the initial configuration of ReadyStackGo. The wizard starts automatically on first access to the web interface.

## Overview

The setup wizard consists of four steps:

1. **Create Admin Account** - Set up the primary administrator
2. **Configure Organization** - Define your organization details
3. **Set Up Environment** - Connect ReadyStackGo to Docker (optional)
4. **Complete Setup** - Finalize the configuration

---

## Step 1: Create Admin Account

In the first step, you create the administrator account for logging into ReadyStackGo.

### Input Fields

| Field | Requirement |
|-------|-------------|
| **Username** | Minimum 3 characters |
| **Password** | Minimum 8 characters |
| **Confirm Password** | Must match the password |

### Tips

- Choose a secure username (not just `admin`)
- Use a strong password with letters, numbers, and special characters
- Save these credentials - they cannot be recovered later

:::caution[Important]
This account is the only way to access ReadyStackGo. Keep your credentials safe!
:::

---

## Step 2: Configure Organization

Here you define the identity of your ReadyStackGo instance.

### Input Fields

| Field | Description | Example |
|-------|-------------|---------|
| **Organization ID** | Technical identifier (lowercase letters, numbers, hyphens only) | `my-company` |
| **Organization Name** | Display name for your organization | `My Company Inc.` |

---

## Step 3: Set Up Environment

In this step, you can configure a Docker environment. An environment represents a Docker installation that ReadyStackGo should manage.

:::tip[Optional]
You can skip this step and add environments later. Click **"Skip for now"** to proceed.
:::

### Input Fields

| Field | Description | Default |
|-------|-------------|---------|
| **Environment ID** | Technical identifier | `local` |
| **Display Name** | Display name for the environment | `Local Docker` |
| **Docker Socket Path** | Path to the Docker socket | `unix:///var/run/docker.sock` |

---

## Step 4: Complete Setup

In the final step, you'll see a summary of your configuration:

- ✓ Admin account configured
- ✓ Organization details set
- ✓ Environment configured (if not skipped)

When you click **"Complete Setup"**:

1. Your configuration is saved
2. The admin account is activated
3. You are redirected to the login page

---

## After Setup

### Login

After completing the wizard, you'll be redirected to the login page. Log in with your admin account.

### Next Steps

1. **Add Environment** (if skipped) - Go to *Environments* and click *Add Environment*
2. **Configure Stack Sources** - Add Git repositories or local paths as stack sources
3. **Deploy First Stack** - See [First Deployment](/en/getting-started/first-deployment/)

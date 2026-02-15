---
title: Initial Setup
description: Complete the ReadyStackGo Setup Wizard and Onboarding
---

After [installation](/en/getting-started/installation/), the setup process consists of two phases:

1. **Setup Wizard** — Create your admin account (unauthenticated, time-limited)
2. **Onboarding** — Configure organization, environment, and stack sources (authenticated, guided)

---

## Phase 1: Setup Wizard

The setup wizard starts automatically on first access to the web interface. It has a single step: creating the admin account.

:::caution[Time Limit]
The setup wizard has a **5-minute timeout** for security. If the timer expires, restart the container to try again:
```bash
docker restart readystackgo
```
:::

### Create Admin Account

Enter your administrator credentials:

![Setup Wizard: Create Admin Account](/images/docs/wizard-01-admin-form.png)

| Field | Requirement |
|-------|-------------|
| **Username** | Minimum 3 characters |
| **Password** | Minimum 8 characters |
| **Confirm Password** | Must match the password |

The form validates your input before submission:

![Setup Wizard: Validation Error](/images/docs/wizard-02-validation-error.png)

### What Happens After Submission

When you click **"Continue"**:

1. The admin account is created
2. You are **automatically logged in** (no separate login step)
3. The wizard marks itself as complete
4. You are automatically redirected to the **Onboarding**

:::caution[Important]
This account is the only way to access ReadyStackGo. Keep your credentials safe — they cannot be recovered later!
:::

---

## Phase 2: Onboarding

After the wizard, the **guided onboarding** starts automatically. It walks you through the initial configuration of your ReadyStackGo instance in three steps.

:::note[Required]
The onboarding is **not skippable**. You will only be redirected to the application once at least the organization has been configured. ReadyStackGo checks on every login whether onboarding is complete.
:::

### Step 1: Create Organization

The organization is the **top-level identity** of your ReadyStackGo instance. All environments, stacks, and settings belong to it. This step is **required** — there is no "Skip" button.

![Onboarding: Create Organization](/images/docs/wizard-03-onboarding-start.png)

| Field | Requirement |
|-------|-------------|
| **Organization Name** | Minimum 2 characters |

Enter a name and click **"Continue"**.

:::tip[Tip]
You can change the organization name later in Settings at any time.
:::

---

### Step 2: Add Docker Environment

In this step, you connect ReadyStackGo to a Docker daemon. The fields are **pre-filled** with sensible defaults:

![Onboarding: Docker Environment](/images/docs/wizard-04-onboarding-env.png)

| Field | Default Value | Description |
|-------|--------------|-------------|
| **Environment Name** | `Local Docker` | A display name for this environment |
| **Docker Socket Path** | `unix:///var/run/docker.sock` | Path to the Docker daemon socket on the host |

You have two options:
- **"Continue"** — Creates the environment with the specified values
- **"Skip for now"** — Skips this step (you can add environments later in Settings)

:::tip[Docker Socket]
When running ReadyStackGo as a Docker container, the host's Docker socket must be mounted into the container (configured by default in `docker-compose.yml`).
:::

---

### Step 3: Select Stack Sources

Stack sources are repositories containing pre-configured stack definitions that you can deploy directly. ReadyStackGo shows you a **curated list** of available sources:

![Onboarding: Stack Sources](/images/docs/wizard-05-onboarding-sources.png)

- Recommended sources (**Featured**) are pre-selected automatically
- You can select and deselect sources as needed
- Click **"Add sources"** to add the selected sources
- Or click **"Skip for now"** to skip this step

:::tip[Stack Sources]
You can add, remove, or configure custom Git repositories as stack sources at any time under *Settings → Stack Sources*.
:::

---

### Step 4: Summary

After completing the steps, the onboarding shows a **summary** of all configured (and skipped) items:

![Onboarding: Summary](/images/docs/wizard-06-onboarding-complete.png)

Click **"Go to Dashboard"** to proceed to the dashboard.

---

## After Setup

After completing the onboarding, you are redirected to the **Dashboard**:

![Dashboard after Onboarding](/images/docs/wizard-07-dashboard.png)

If you skipped optional steps during onboarding (environment or stack sources), the dashboard shows a **hint** with recommendations for further configuration. This hint disappears automatically once all items are configured.

### Next Steps

1. **Add Environment** (if skipped) — Go to *Environments* and create a new environment
2. **Configure Stack Sources** (if skipped) — Under *Settings → Stack Sources*
3. **Deploy First Stack** — See [First Deployment](/en/getting-started/first-deployment/)

---

## Troubleshooting

### Wizard Does Not Start

If the wizard does not start automatically:

```bash
# Check container logs
docker logs readystackgo

# Restart the container
docker restart readystackgo
```

### Reset the Wizard

If you want to run the wizard again, you need to reset all data:

```bash
# Stop container and delete volumes
docker compose down -v

# Start container again
docker compose up -d
```

:::danger[Warning]
Deleting the volumes removes all configurations and deployment information!
:::

---
title: Initial Setup
description: Complete the ReadyStackGo Setup Wizard and Onboarding Checklist
---

After [installation](/en/getting-started/installation/), the setup process consists of two phases:

1. **Setup Wizard** — Create your admin account (unauthenticated, time-limited)
2. **Onboarding Checklist** — Configure organization, environments, and stack sources (authenticated, at your own pace)

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
4. You are redirected to the application

:::caution[Important]
This account is the only way to access ReadyStackGo. Keep your credentials safe — they cannot be recovered later!
:::

---

## Phase 2: Onboarding Checklist

After the wizard completes, an **onboarding checklist** appears on the dashboard to guide you through the remaining configuration. Unlike the wizard, there is no time limit — you can complete these steps at your own pace.

The checklist tracks the following items:

| Item | Required | Description |
|------|----------|-------------|
| **Admin account** | Yes | Always checked — you completed this in the wizard |
| **Organization** | Yes | Set up your organization identity |
| **Environment** | No | Connect ReadyStackGo to a Docker daemon |
| **Stack Sources** | No | Add Git repositories with stack definitions |
| **Container Registries** | No | Configure auth for private container registries |

### Setting Up Your Organization

The organization is the **first required step** after the wizard. Until an organization is configured, environment, stack sources, and registry items are disabled.

Click **"Configure"** next to "Set up your organization" to navigate to the organization settings page.

### Completing the Remaining Steps

Each checklist item has a **"Configure"** link that takes you directly to the relevant settings page. Items unlock progressively — the organization must be configured before the other items become available.

### Dismissing the Checklist

Once you've completed the steps you need (or if you prefer to configure things later), you can dismiss the checklist:

- Click the **✕** button in the top-right corner, or
- Click **"Dismiss checklist"** at the bottom

The checklist will not reappear after dismissal.

---

## After Setup

### Next Steps

1. **Configure Organization** — Required to unlock all features
2. **Add Environment** — Go to *Environments* and click *Add Environment*
3. **Configure Stack Sources** — Add Git repositories as stack sources
4. **Deploy First Stack** — See [First Deployment](/en/getting-started/first-deployment/)

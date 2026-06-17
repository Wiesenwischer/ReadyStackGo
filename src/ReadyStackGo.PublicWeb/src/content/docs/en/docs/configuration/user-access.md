---
title: User Access, Email & Single Sign-On
description: Invite users by email, verify email addresses, sign in by email or username, and connect external OIDC providers (single sign-on).
---

ReadyStackGo manages user access through **admin invitations**: there is no public
self-service signup — an administrator invites an email address, and the invited person
confirms their access via a link and sets a password. ReadyStackGo also supports **login by
email or username** and **single sign-on (SSO) via generic OIDC providers** (OpenID Connect).

## Overview

| Capability | Description |
|------------|-------------|
| Admin invitation | An administrator invites an email address with a role |
| Email verification | The invitation link proves ownership; your own address can be verified later |
| Login by email or username | Both identifiers work, backwards compatible |
| Single sign-on (OIDC) | Sign in through external identity providers (e.g. IdentityAccess, Keycloak) |

:::note[Prerequisite]
Invitations and verification emails require a configured **SMTP server**. Configure the email
settings first.
:::

---

## Step 1: Configure email (SMTP)

Open **Settings → Email (SMTP)**, enable sending, and fill in host, port, credentials and the
from address. Use **Send test** to send a test message and validate the configuration.

![SMTP settings](/images/docs/auth-03-smtp-settings.png)

:::tip[Password is preserved]
When saving again you can leave the password field empty — the stored password is then kept
unchanged. It is stored encrypted and never displayed again.
:::

---

## Step 2: Invite a user

Under **Settings → User Invitations** you invite a person by email. Choose the **role**
(Viewer, Operator, Organization Owner or System Administrator). For organization-scoped roles
also provide the **Organization ID**; System Administrators are not scoped to an organization.

![Invite a user](/images/docs/auth-05-invitations.png)

After sending, the invitation appears in the list with status **Pending** and an expiry date.
Pending invitations can be **revoked** at any time.

---

## Step 3: Accept the invitation

The invited person opens the link from the email (`/accept-invite?token=…`), sees their
address and sets a password. On acceptance the email address is considered **verified** (the
link is the ownership proof), the account is activated and the assigned role is granted.

![Accept invitation](/images/docs/auth-06-accept-invite.png)

---

## Login by email or username

On the sign-in page either the **email address** or the **username** can be used. When OIDC
providers are configured, "Sign in with …" buttons appear in addition.

![Sign-in page](/images/docs/auth-01-login.png)

### Verify your email address

If your own email address is not yet verified and SMTP is configured, a prompt banner appears
at the top. **Send verification email** sends a confirmation link.

![Email-not-verified banner](/images/docs/auth-07-verify-banner.png)

:::note[First administrator]
The administrator address created during setup is intentionally **not** auto-verified. The
administrator can still sign in with a password and verify the address later, once SMTP is set
up.
:::

---

## Single Sign-On (OIDC)

Under **Settings → Single Sign-On (OIDC)** you configure one or more generic OpenID Connect
providers. For each provider you set name, display name, authority (issuer URL), client ID,
client secret and scopes.

![OIDC settings](/images/docs/auth-04-oidc-settings.png)

Enabled providers appear as a button on the sign-in page. After a successful login at the
provider, ReadyStackGo issues its own session token.

:::caution[Invited or known identities only]
An OIDC login works only if a user with the returned email address already exists **or** a
pending invitation for that address exists. Unknown identities are rejected (no automatic
account creation).
:::

---

## Settings overview

All of the above are grouped under **Settings**.

![Settings overview](/images/docs/auth-02-settings.png)

---

## Roles

| Role | Scope | Purpose |
|------|-------|---------|
| Viewer | Organization | Read-only access |
| Operator | Organization | Manage deployments |
| Organization Owner | Organization | Full access to the organization |
| System Administrator | Global | Full system access |

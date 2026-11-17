# Security Architecture

This document describes the security architecture of ReadyStackGo.

## Topics

- [Initial Setup Security](Initial-Setup.md) - Security during initial setup
- Authentication (local password, single sign-on with OIDC, see [Identity Provider Templates](../Architecture/Identity-Provider-Templates.md))
- Authorization (Roles)
- JWT Tokens
- TLS
- Configuration Protection

---

## Authentication

- Local password (built-in sign-in in the wizard, invitations, profile)
- Single sign-on with OpenID Connect providers: WYSCH out of the box (pairing, PAR), any other provider through the
  template "Generic OIDC"; further templates (e.g. ams.Identity, Keycloak) from a directory
- The first administrator can be created through WYSCH in the setup wizard (no local password)
- Emergency access: `rsgo admin set-password <username>` inside the container

---

## Roles

- `admin`
- `operator`

Roles control access to endpoints.

---

## Tokens

JWT-based, with claims:

- `sub`
- `role`
- `exp`

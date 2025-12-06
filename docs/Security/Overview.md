# Security Architecture

This document describes the security architecture of ReadyStackGo.

## Topics

- [Initial Setup Security](Initial-Setup.md) - Security during initial setup
- Authentication (Local + OIDC)
- Authorization (Roles)
- JWT Tokens
- TLS
- Configuration Protection

---

## Authentication

- Local Admin (Wizard)
- Later OIDC (Keycloak, ams.identity, etc.)

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

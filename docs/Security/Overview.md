# Security Architecture

Dieses Dokument beschreibt die Sicherheitsarchitektur von ReadyStackGo.

## Themen

- Authentifizierung (Local + OIDC)
- Autorisierung (Rollen)
- JWT Tokens
- TLS
- Konfigurationsschutz

---

## Authentifizierung

- Lokaler Admin (Wizard)
- Später OIDC (Keycloak, ams.identity, etc.)

---

## Rollen

- `admin`
- `operator`

Rollen steuern Zugriff auf Endpunkte.

---

## Tokens

JWT-basiert, mit Claims:

- `sub`
- `role`
- `exp`


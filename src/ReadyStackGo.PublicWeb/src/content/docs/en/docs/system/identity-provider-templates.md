---
title: Identity Provider Templates
description: The format of identity provider templates, where ReadyStackGo loads them from and how operators and distributions add or replace them.
---

Every single sign-on provider is created from a **template**. A template describes an identity provider: its name and symbol, the provider address, how ReadyStackGo registers itself there and which claims carry username, display name and email. The guided setup under **Settings → Single Sign-On** reads everything else from the template.

## Built-in templates

| Id | Name | Provider address | Registration |
|----|------|------------------|--------------|
| `wysch` | WYSCH | `https://id.wysch.wiesenwischer.de/` (fixed) | `pairing`: connect and confirm at WYSCH, no typing |
| `generic-oidc` | Generic OIDC | entered by the user | `manual`: client ID and secret are entered |

`wysch` is also offered in the first step of the setup wizard.

## Template format

A template is a folder `<id>/` with `template.json` and an optional `icon.svg`:

```text
identity-provider-templates/
└── company-sso/
    ├── template.json
    └── icon.svg
```

```json
{
  "id": "company-sso",
  "name": "Company SSO",
  "description": "Sign in with your company account.",
  "setupDescription": "Sign in with your company account.",
  "order": 20,
  "provider": { "name": "company", "displayName": "Company SSO" },
  "authority": { "url": "https://login.example.com/realms/main" },
  "registration": { "kind": "manual" },
  "requirePar": false,
  "requireHttps": false,
  "scopes": "openid profile email",
  "claims": { "username": "preferred_username", "displayName": "name", "email": "email" },
  "helpUrl": "https://intranet.example.com/sso",
  "offerInSetup": false
}
```

| Field | Required | Meaning |
|-------|----------|---------|
| `id` | yes | Lowercase letters, digits and dashes (1–40), equal to the folder name |
| `name` | yes | Name of the tile and of the template |
| `description`, `setupDescription` | no | Text of the tile in the settings and in the setup wizard (the latter defaults to the first) |
| `order` | no | Sort order (smaller first) |
| `provider.name` | no | Suggested provider name, part of the redirect URI; defaults to the id |
| `provider.displayName` | no | Suggested display name |
| `authority.url` **or** `authority.input` | yes, exactly one | Fixed provider address, or `{ "hint", "example" }` for an address the user enters |
| `registration.kind` | yes | `manual` or `pairing` |
| `requirePar` | no | Always use pushed authorization requests (RFC 9126) |
| `requireHttps` | no | The installation must use HTTPS (http on localhost only) |
| `scopes` | no | Must contain `openid`; default `openid profile email` |
| `claims` | no | Claim names for username, display name and email |
| `helpUrl` | no | Link to further help |
| `offerInSetup` | no | Offer as a tile in the first wizard step (registration must be `pairing` and the address fixed) |

`icon.svg` is an SVG of at most 64 KB. It is served with a sandboxing content security policy and shown only as an image. Templates without icon show a key.

A provider keeps the settings of its template from the moment it is created: if a template changes or disappears later, existing providers keep working.

## Provide templates to a container

Mount a directory to `/app/identity-provider-templates`:

```yaml
services:
  readystackgo:
    volumes:
      - ./identity-provider-templates:/app/identity-provider-templates:ro
    environment:
      - IdentityProviderTemplates__Enabled=wysch,company-sso
```

- A template in the directory **replaces** a built-in template with the same id.
- `IdentityProviderTemplates__Enabled` limits the offered templates (comma-separated, empty offers all).
- `IdentityProviderTemplates__Path` changes the directory.
- Invalid templates are skipped with a warning in the log. Changes appear after at most 30 seconds.

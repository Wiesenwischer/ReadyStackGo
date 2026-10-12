---
title: Identity-Provider-Vorlagen
description: Das Format der Vorlagen für Identity Provider, woher ReadyStackGo sie lädt und wie Betreiber und Distributionen sie ergänzen oder ersetzen.
---

Jeder Single-Sign-On-Provider entsteht aus einer **Vorlage**. Eine Vorlage beschreibt einen Identity Provider: Name und Symbol, die Adresse des Anbieters, wie sich ReadyStackGo dort registriert und welche Claims Benutzername, Anzeigename und E-Mail liefern. Alles Weitere liest der Einrichtungslauf unter **Settings → Single Sign-On** aus der Vorlage.

## Eingebaute Vorlagen

| Id | Name | Adresse des Anbieters | Registrierung |
|----|------|-----------------------|---------------|
| `wysch` | WYSCH | `https://id.wysch.wiesenwischer.de/` (fest) | `pairing`: bei WYSCH verbinden und bestätigen, nichts abtippen |
| `generic-oidc` | Generic OIDC | vom Nutzer eingegeben | `manual`: Client-ID und Secret werden eingetragen |

`wysch` wird zusätzlich im ersten Schritt des Setup-Wizards angeboten.

## Format

Eine Vorlage ist ein Ordner `<id>/` mit `template.json` und optional `icon.svg`:

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

| Feld | Pflicht | Bedeutung |
|------|---------|-----------|
| `id` | ja | Kleinbuchstaben, Ziffern und Bindestriche (1–40), gleich dem Ordnernamen |
| `name` | ja | Name der Kachel und der Vorlage |
| `description`, `setupDescription` | nein | Text der Kachel in den Settings und im Setup-Wizard (Letzterer fällt auf den ersten zurück) |
| `order` | nein | Reihenfolge (kleiner zuerst) |
| `provider.name` | nein | Vorgeschlagener Provider-Name, Teil der Redirect-URI; Standard ist die Id |
| `provider.displayName` | nein | Vorgeschlagener Anzeigename |
| `authority.url` **oder** `authority.input` | ja, genau eins | Feste Adresse des Anbieters oder `{ "hint", "example" }` für eine Adresse, die der Nutzer eingibt |
| `registration.kind` | ja | `manual` oder `pairing` |
| `requirePar` | nein | Immer Pushed Authorization Requests (RFC 9126) nutzen |
| `requireHttps` | nein | Die Installation muss HTTPS nutzen (http nur auf localhost) |
| `scopes` | nein | Muss `openid` enthalten; Standard `openid profile email` |
| `claims` | nein | Claim-Namen für Benutzername, Anzeigename und E-Mail |
| `helpUrl` | nein | Link zu weiterer Hilfe |
| `offerInSetup` | nein | Als Kachel im ersten Wizard-Schritt anbieten (Registrierung muss `pairing` und die Adresse fest sein) |

`icon.svg` ist ein SVG mit höchstens 64 KB. Es wird mit einer abschottenden Content Security Policy ausgeliefert und nur als Bild angezeigt. Vorlagen ohne Symbol zeigen einen Schlüssel.

Ein Provider behält die Einstellungen seiner Vorlage ab dem Anlegen: Ändert sich eine Vorlage später oder verschwindet sie, arbeiten bestehende Provider weiter.

## Vorlagen einem Container mitgeben

Binden Sie ein Verzeichnis nach `/app/identity-provider-templates` ein:

```yaml
services:
  readystackgo:
    volumes:
      - ./identity-provider-templates:/app/identity-provider-templates:ro
    environment:
      - IdentityProviderTemplates__Enabled=wysch,company-sso
```

- Eine Vorlage im Verzeichnis **ersetzt** eine eingebaute mit derselben Id.
- `IdentityProviderTemplates__Enabled` schränkt die angebotenen Vorlagen ein (durch Kommas getrennt, leer bietet alle an).
- `IdentityProviderTemplates__Path` ändert das Verzeichnis.
- Ungültige Vorlagen werden mit einer Warnung im Log übersprungen. Änderungen erscheinen nach spätestens 30 Sekunden.

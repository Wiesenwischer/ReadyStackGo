---
title: Theme Packages
description: How color themes work in ReadyStackGo, how to build your own theme package and how to provide it to a container.
---

The web interface of ReadyStackGo is styled by **theme packages**. A theme package is pure CSS: a set of values for the design tokens of the interface, for light and dark mode. No code and no build step are needed, so anyone can create a theme and provide it to a running installation.

Users choose the theme under **Settings → Appearance**. The choice is saved in the browser. The mode is chosen separately: Light, Dark or System, which follows the operating system. The button in the header switches between light and dark as well.

## Built-in Themes

| Id | Name | Description |
|----|------|-------------|
| `turquoise` | Turquoise | Default. The colors of the ReadyStackGo wordmark. |
| `pastel-green` | Pastel Green | Soft pastel green with the same orange accent. |
| `classic` | Classic | The previous blue look of ReadyStackGo. |

New installations start with **Turquoise**. Installations that were set up with an earlier version keep their previous look after the update: they start with **Classic**. Every user can switch the theme under Settings → Appearance.

## Format of a Theme Package

A theme package is a folder whose name is the theme's id:

```
my-theme/
├── theme.json
└── theme.css
```

### theme.json

```json
{
  "id": "my-theme",
  "name": "My Theme",
  "description": "Short description shown on the theme card.",
  "order": 10
}
```

| Field | Description |
|-------|-------------|
| `id` | Unique id. Must match `^[a-z0-9][a-z0-9-]{0,39}$` (lowercase letters, digits and hyphens, at most 40 characters, not starting with a hyphen) and must be **identical to the folder name**. |
| `name` | Display name in Settings → Appearance. |
| `description` | Short description on the theme card. |
| `order` | Sort order in the selection (ascending). |

### theme.css

`theme.css` only sets CSS custom properties with the prefix `--rsgo-`. It contains two blocks, one for light and one for dark mode:

```css
/* Light mode */
[data-theme="my-theme"] {
  --rsgo-bg-page: #F3FBFB;
  --rsgo-primary-default: #1AD3D6;
  /* ... all other tokens ... */
}

/* Dark mode */
[data-theme="my-theme"][data-mode="dark"] {
  --rsgo-bg-page: #060B11;
  --rsgo-primary-default: #00CED1;
  /* ... all other tokens ... */
}
```

Use exactly these selectors. The dark block does not depend on a surrounding element, so the theme works on the `<html>` element as well as in the color orbs of the theme selection, which show each theme light and dark side by side. Both blocks must set **all** tokens:

- the semantic tokens, e.g. `--rsgo-bg-page`, `--rsgo-bg-surface`, `--rsgo-text-primary`, `--rsgo-text-brand`, `--rsgo-primary-default`, `--rsgo-accent-go`, `--rsgo-focus-ring`, `--rsgo-nav-*`, `--rsgo-status-*` and `--rsgo-logo-*`,
- the color scales `--rsgo-brand-25` … `--rsgo-brand-950` and `--rsgo-gray-25` … `--rsgo-gray-950`.

Optionally a theme can set `--rsgo-font-sans`, `--rsgo-font-display` and `--rsgo-radius-*`.

:::tip[Start from a template]
The easiest way is to copy the built-in Turquoise theme and change its values: [`themes/turquoise/theme.css`](https://github.com/Wiesenwischer/ReadyStackGo/blob/main/src/ReadyStackGo.WebUi/apps/rsgo-generic/public/themes/turquoise/theme.css). It lists every token for both modes.
:::

Make sure texts stay readable: text colors should reach a contrast of at least 4.5:1 against their backgrounds, borders and the focus ring at least 3:1.

## Providing a Theme to a Container

Besides the built-in themes, ReadyStackGo loads theme packages from a directory at startup. By default this is `/app/themes` in the container; mount your packages there:

```yaml
services:
  readystackgo:
    image: wiesenwischer/readystackgo:latest
    volumes:
      - ./themes:/app/themes:ro
    environment:
      - Themes__Default=my-theme
```

Each subfolder of the directory is one package. A package from the directory with the same id as a built-in theme replaces it. Invalid packages (missing files, invalid id, id different from the folder name) are skipped with a warning in the log. After adding or changing a package, restart the container; no new image is needed.

### Configuration

| Environment variable | Description | Default |
|----------------------|-------------|---------|
| `Themes__Path` | Directory with additional theme packages. May be missing. | `/app/themes` |
| `Themes__Enabled` | Comma-separated list of theme ids that are offered. Empty means all themes found. | (empty) |
| `Themes__Default` | Theme used when a browser has not chosen one yet (or its choice is no longer offered). Overrides the installation's own default. | empty: `turquoise` for new installations, `classic` for installations set up before the theme packages; if not offered, `turquoise`, then the first theme by `order` |

Example: offer only your own theme:

```bash
Themes__Enabled=my-theme
Themes__Default=my-theme
```

If only **one** theme is offered, Settings → Appearance hides the theme selection and only shows the mode switch.

## API

The web interface loads the themes via two endpoints. Both can be called without sign-in, because the login page is themed as well.

| Endpoint | Response |
|----------|----------|
| `GET /api/themes` | `{ "default": "<id>", "themes": [{ "id", "name", "description", "cssUrl" }] }` |
| `GET /api/themes/{id}/theme.css` | The `theme.css` of the package (`text/css`) |

## Security

A theme package is CSS that every user of the installation loads into the browser. Only use packages from sources you trust and check their content before mounting them. The operator of the installation is responsible for the packages in the theme directory.

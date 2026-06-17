---
title: Product Updates & Release Notes
description: Surface when a newer version of an installed product is available and show its release notes in the UI — including the manifest fields (releaseNotesUrl) and the CHANGELOG.md convention.
---

ReadyStackGo shows when a newer version of an installed **product deployment** is available in
the catalog and makes its **release notes** accessible directly in the UI — giving the
"upgrade or not" decision useful context.

## Overview

| Element | Description |
|---------|-------------|
| Update badge | On the product deployment detail page when a newer version exists |
| Release notes | Own `CHANGELOG.md` (rendered) **or** an external `releaseNotesUrl` (as a link) |
| Notification | A one-time notification after a source sync when an update appears |

---

## Declaring release notes in the manifest

Release notes are sourced per product version from two places — an **own `CHANGELOG.md`**
(preferred) and an **external URL** as a fallback.

### Option A: CHANGELOG.md next to the manifest (recommended)

Place a `CHANGELOG.md` **in the same directory** as the product manifest file. It is read
automatically during sync and rendered as formatted markdown in the UI.

```
my-product/
├── rsgo.yaml          # product manifest (productVersion: "1.1.0")
└── CHANGELOG.md       # picked up automatically as release notes
```

### Option B: releaseNotesUrl in the manifest

Alternatively (or in addition), provide a URL in the manifest metadata block, e.g. to a
GitHub release:

```yaml
metadata:
  name: "My Product"
  productId: "com.example.myproduct"
  productVersion: "1.1.0"
  releaseNotesUrl: "https://github.com/example/my-product/releases/tag/v1.1.0"
```

:::note[Precedence]
When a `CHANGELOG.md` is present it is **preferred** and rendered directly in the viewer. A
`releaseNotesUrl` is only shown as an **external link** — for security it is **not** fetched
server-side (SSRF protection).
:::

For the full manifest schema see [Manifest format](/en/reference/manifest-format/).

---

## Step by step: discover an update & read the release notes

### Step 1: Update badge on the deployment detail page

When a newer version of the same product exists in the catalog (determined by SemVer during
source sync), the product deployment detail page shows an **"Update available"** badge with the
target version. If the target version has release notes, a **"Release notes"** link appears too.

![Update badge on the product deployment detail page](/images/docs/product-release-notes-01-badge.png)

### Step 2: View the release notes

Clicking **"Release notes"** opens the viewer. An own `CHANGELOG.md` is shown as formatted
markdown; an external `releaseNotesUrl` is shown as a link.

![Release notes viewer with the rendered changelog](/images/docs/product-release-notes-02-viewer.png)

:::tip[Notification]
After each source sync, ReadyStackGo checks active product deployments for newer versions and
raises a notification **once** per deployment and target version (no spam on repeated syncs).
:::

---

## How update detection works

1. A **source sync** loads product versions from the configured sources (local, Git, OCI
   registry). Multiple versions of the same product can coexist.
2. For each active product deployment, RSGO compares the installed version with the latest
   available one (**strictly SemVer-newer**, no downgrade).
3. If a newer version exists, the badge appears and — once — a notification is raised.
4. The target version's release notes are loaded on demand.

---

## Security

- An own `CHANGELOG.md` from the configured sources is loaded server-side and rendered as
  **sanitized** markdown.
- External `releaseNotesUrl` values are **never** fetched server-side, only shown as a link
  (SSRF protection).

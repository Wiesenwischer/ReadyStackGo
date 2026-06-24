---
title: Maintenance Edge-Proxy
description: A surviving per-product reverse-proxy that serves a controlled page plus a machine-readable status during redeploy and maintenance
---

The **Maintenance Edge-Proxy** ("edge") is a ReadyStackGo-managed reverse-proxy container that runs **per product** in front of its public entry. It **survives product redeploys** and — driven by RSGO's authoritative deploy state and the maintenance flag — either proxies transparently to the upstream or serves a **controlled maintenance page** plus a **machine-readable status**.

The feature is **opt-in per product** (via the manifest) and **dormant by default**: without an `edge:` block nothing changes about the existing behaviour.

## Why this feature exists

During a redeploy or maintenance, RSGO removes the product containers — including the single public entry (e.g. the BFF/gateway). Clients and browsers then get **"connection refused"** instead of a meaningful page, and launchers/clients have no reliable way to tell *why* the service is down.

The edge closes that gap and cleanly separates two layers:

| Layer | Responsibility | Solved by |
|-------|----------------|-----------|
| **Control plane** | "Should the service be in maintenance?" — the toggle | [Maintenance Mode](/en/docs/monitoring/maintenance-mode/) (flag/observer) — already present |
| **Experience plane** | "What does the client see while the service is down?" | **Maintenance Edge-Proxy** (this page) |

Benefits:

- **No more connection-refused** during redeploy or maintenance — the edge stays reachable throughout.
- **Controlled maintenance page** instead of a browser error page, fully customizable.
- **Machine-readable status** (`GET /__status`) so a launcher/client can robustly tell "running", "maintenance" or "deploying".
- **Generic**: nothing product-specific — each product enables it via the manifest only.

---

## How it works (for developers)

RSGO runs the edge as a **separate container** (Caddy) managed in its lifecycle — it does **not** run inside the RSGO process (clean control/data-plane separation; an RSGO self-update causes no traffic blip).

### Routing — from authoritative state, not guessed

The edge does **not** health-guess. Its only inputs are RSGO's deploy state (`ProductDeployment.Status`) and the maintenance flag (`OperationMode`):

| Deploy state | Maintenance flag | Edge behaviour | `/__status` `state` |
|--------------|------------------|----------------|---------------------|
| `Running` | off | Transparent proxy to the upstream | `running` |
| `Running` | **on** | Maintenance page, **planned maintenance** | `maintenance` |
| `Deploying` / `Redeploying` / `Upgrading` | any | Maintenance page, **temporarily unavailable** | `deploying` |
| `Failed` / `Stopped` / other | off | Maintenance page, **temporarily unavailable** | `maintenance` |

The flag only changes the **wording** (planned maintenance vs. temporarily down); *whether* to proxy is decided purely by the deploy state.

:::note[Health endpoints always pass through]
`/hc` and `/liveness` are forwarded to the upstream **even during maintenance** (they never get the 503 page). If the upstream is stopped it answers accordingly (e.g. 502) — but the edge never blocks health checks with its own page.
:::

### Survival — the edge outlives the redeploy

When tearing down a stack, RSGO removes all containers with the matching stack label. The edge (and an optional product-contributed maintenance container) carry **survival labels** and are excluded:

| Label | Value | Meaning |
|-------|-------|---------|
| `rsgo.scope` | `edge` | Container belongs to the edge scope → excluded from stack teardown |
| `rsgo.redeploy` | `ignore` | Generic opt-out → excluded from stack teardown |
| `rsgo.role` | `maintenance-page` | Product-contributed maintenance container (see below) |

RSGO is also the **single writer** of the edge config: it computes the desired state and pushes it **atomically and connection-preservingly** via the Caddy admin API (`POST /load`) — without restarting the edge.

---

## Step by step: enabling the edge

### Step 1: Add the `edge:` block to the product manifest

Add the optional product-level `edge:` block (sibling of `metadata:` / `services:`). The upstream service must be attached to the shared external network so the edge can reach it by DNS alias:

```yaml
metadata:
  name: ams.project
  productVersion: "1.0.0"

networks:
  ams-project-edge-net:
    external: true            # shared network edge <-> upstream

services:
  web-bff:                    # public entry (BFF/gateway)
    image: ams/bff:1.0.0
    networks: [ams-project-edge-net]

edge:
  enabled: true
  publicHostname: project.customer.tld
  publicPort: 443
  upstream:
    service: web-bff          # = service/alias name on the network
    port: 8080
  network: ams-project-edge-net
  tls:
    mode: selfsigned
  maintenancePage:
    mode: default
    branding:
      productName: "ams.project"
      supportContact: support@customer.tld
      locales: [de, en]
```

### Step 2: Deploy the product

Deploy as usual. RSGO detects the `edge:` block, provisions the edge container, and switches it to proxy once the product is `Running`. In normal operation the edge passes through transparently.

### Step 3: Observe the behaviour

During a **redeploy** the edge stays up and serves the maintenance page (`state: deploying`) — no connection-refused. With the **maintenance flag** set it shows the page with "planned maintenance" and a `reason`:

![Default maintenance page of the edge-proxy](/images/docs/edge-01-maintenance-page.png)

The machine-readable status at `/__status` distinguishes the states unambiguously:

![/__status in maintenance](/images/docs/edge-02-status-maintenance.png)

![/__status in normal operation](/images/docs/edge-04-status-running.png)

---

## a) Customizing the maintenance page

There are three stages. The edge resolves them in the order **container → bundle → default**, falling back to the next stage.

### Default page (branding variables)

`maintenancePage.mode: default` serves the built-in, bilingual page. Customize it without any HTML via `branding`:

```yaml
maintenancePage:
  mode: default
  branding:
    productName: "ams.project"
    logoUrl: https://customer.tld/logo.svg
    supportContact: support@customer.tld
    locales: [de, en]
```

| Field | Description |
|-------|-------------|
| `productName` | Display name on the page / in the browser tab |
| `logoUrl` | Optional logo (absolute URL) |
| `supportContact` | Optional support address shown at the bottom |
| `locales` | Languages for the texts (e.g. `[de, en]`) |

### Your own HTML (bundle)

`maintenancePage.mode: bundle` serves your own HTML page inline. RSGO reads it **at deploy time** from the manifest repository (`bundlePath/index.html`):

```yaml
maintenancePage:
  mode: bundle
  bundlePath: ./maintenance/   # reads ./maintenance/index.html
```

:::caution[Currently: a single HTML page]
Bundle mode currently serves a single, inline `index.html`. External assets (separate CSS/image files) are not yet shipped — for full control with your own assets, use container mode.
:::

---

## b) Replacing it with your own container (full control)

For full customizability (own assets, dynamic content, your own web server) the product ships **its own maintenance container** that the edge proxies to during maintenance.

How:

1. Set `maintenancePage.mode: container` and specify service + port.
2. Label the maintenance service `rsgo.role: maintenance-page` **and** `rsgo.redeploy: ignore` (so it survives redeploys, like the edge).
3. Attach the service to the shared edge network so the edge can reach it by alias.

```yaml
networks:
  ams-project-edge-net:
    external: true

services:
  maintenance-web:
    image: ams/maintenance-page:1.0
    networks: [ams-project-edge-net]
    labels:
      rsgo.role: maintenance-page
      rsgo.redeploy: ignore          # survives the redeploy

edge:
  enabled: true
  publicHostname: project.customer.tld
  upstream: { service: web-bff, port: 8080 }
  network: ams-project-edge-net
  maintenancePage:
    mode: container
    container:
      service: maintenance-web       # = alias on the network
      port: 80
```

During maintenance the edge forwards catch-all requests to `maintenance-web:80`. The `GET /__status` contract and the `/hc` passthrough stay **unchanged** — only the visible page comes from your container.

---

## Manifest reference: the `edge:` block

| Field | Required | Default | Description |
|-------|----------|---------|-------------|
| `enabled` | yes | `false` | Master switch. `false`/absent → feature inert. |
| `publicHostname` | yes | — | Hostname the edge serves (certificate CN; SNI key). |
| `publicPort` | no | `443` | Public port the edge listens on. |
| `image` | no | pinned `caddy` | Edge image; pin a digest in production. |
| `upstream.service` | yes | — | Internal DNS alias of the public entry (e.g. the BFF). |
| `upstream.port` | no | `8080` | Upstream port. |
| `network` | yes | — | Shared external network connecting edge and upstream (`external: true`). |
| `tls.mode` | no | none | TLS termination: `selfsigned`, `custom` (`certRef`), `reuse` (RSGO cert), `letsencrypt`. |
| `tls.certRef` | for `custom` | — | Reference to the uploaded certificate. |
| `tls.letsencrypt.email` / `.dnsChallenge` | for `letsencrypt` | — | ACME settings. |
| `maintenancePage.mode` | no | `default` | `default`, `bundle`, or `container`. |
| `maintenancePage.bundlePath` | for `bundle` | — | Asset directory in the manifest repo (`index.html`). |
| `maintenancePage.container.service` / `.port` | for `container` | `80` | Service alias + port of the maintenance container. |
| `maintenancePage.branding.*` | no | — | `productName`, `logoUrl`, `supportContact`, `locales`. |

### TLS termination

When `tls.mode` is set, the edge **terminates** HTTPS on `publicPort` with a certificate that **RSGO manages** — the edge never runs ACME itself. Renewed certificates are reloaded without an edge restart.

| `tls.mode` | Certificate source |
|------------|--------------------|
| `selfsigned` | RSGO generates a per-hostname self-signed cert and auto-renews it. |
| `custom` | Operator-provided certificate (`certRef`). |
| `reuse` | RSGO's own endpoint certificate (single-host case). |
| `letsencrypt` | Uses RSGO's ACME-managed certificate. |

Full schema details: [RSGo Manifest Format](/en/docs/reference/manifest-format/).

---

## Status contract: `GET /__status`

The edge serves a **stable, versioned** JSON at `/__status` — identical across all branding modes so a client/launcher can parse it robustly:

```json
{
  "schema": 1,
  "state": "running",
  "reason": null,
  "until": null,
  "productVersion": "1.0.0"
}
```

| Field | Meaning |
|-------|---------|
| `schema` | Contract version. Consumers should branch on it. |
| `state` | `running` (proxying), `maintenance` (planned, `reason` set), or `deploying` (redeploy/upgrade in progress). |
| `reason` | Planned-maintenance reason (from the flag), else `null`. |
| `until` | Announced end (ISO-8601) when available, else `null`. |
| `productVersion` | Current product version when known. |

This lets a client reliably distinguish "planned maintenance" (flag) from "redeploy/temporarily down" (deploy state) — without guessing.

---

## Related topics

- [Maintenance Mode](/en/docs/monitoring/maintenance-mode/) — the toggle (control plane) that drives the edge.
- [Product Redeploy](/en/docs/deployments/product-redeploy/) — during which the edge holds the fort.
- [RSGo Manifest Format](/en/docs/reference/manifest-format/) — full manifest reference.

# Maintenance Edge-Proxy — Example Templates

Ready-to-use templates for customizing the maintenance experience served by the RSGO
[Maintenance Edge-Proxy](../../docs/Reference/Manifest-Schema.md#maintenance-edge-proxy).

There are two ways to customize what visitors see while a product is in maintenance or being
redeployed. Both serve the **same machine-readable status** at `GET /__status` — only the page differs.

| Variant | When to use | Folder |
|---------|-------------|--------|
| **Bundle** | A single, self-contained branded page. No own container/image to build. | [`bundle/`](./bundle/) |
| **Container** | Full control: own assets, own web server, dynamic content. | [`container/`](./container/) |

Both pages poll `/__status` (same origin behind the edge) to switch wording between **planned
maintenance** and **deploying**, and reload automatically once the product is `running` again.

---

## Variant A — Bundle (`maintenancePage.mode: bundle`)

1. Copy [`bundle/index.html`](./bundle/index.html) into a directory next to your product manifest,
   e.g. `./maintenance/index.html`.
2. Adjust the texts, colors, support address and (optionally) inline a logo as a `data:` URI.
3. Add the `edge:` block from [`bundle/manifest-snippet.yaml`](./bundle/manifest-snippet.yaml):

   ```yaml
   edge:
     # ...
     maintenancePage:
       mode: bundle
       bundlePath: ./maintenance/    # RSGO reads ./maintenance/index.html at deploy time
   ```

> Bundle mode serves a **single inline HTML file**. For multiple external assets
> (separate CSS/images), use the container variant.

---

## Variant B — Own container (`maintenancePage.mode: container`)

1. Build and push the image (from [`container/`](./container/)):

   ```bash
   cd container
   docker build -t your-registry/ams-maintenance-page:1.0 .
   docker push your-registry/ams-maintenance-page:1.0
   ```

2. Add the service + `edge:` block from
   [`container/manifest-snippet.yaml`](./container/manifest-snippet.yaml). The maintenance service
   **must**:
   - carry the labels `rsgo.role: maintenance-page` **and** `rsgo.redeploy: ignore`
     (so it survives product redeploys, like the edge), and
   - be attached to the shared `edge.network` (so the edge reaches it by its `container.service` alias).

   ```yaml
   services:
     maintenance-web:
       image: your-registry/ams-maintenance-page:1.0
       networks: [ams-project-edge-net]
       labels:
         rsgo.role: maintenance-page
         rsgo.redeploy: ignore
   edge:
     maintenancePage:
       mode: container
       container: { service: maintenance-web, port: 80 }
   ```

The container in this folder is `nginx:alpine` serving `html/` (page + `styles.css` + `logo.svg`
+ `status.js`). Replace those assets with your own — the web server and port are up to you.

---

## The status contract (both variants)

```json
{ "schema": 1, "state": "running|maintenance|deploying",
  "reason": "<optional>", "until": "<iso8601|null>", "productVersion": "<optional>" }
```

`status.js` (container) and the inline script (bundle) consume this to update the page live. See the
full feature documentation: **docs/Configuration → Maintenance Edge-Proxy** (DE/EN) and
[`docs/Reference/Manifest-Schema.md`](../../docs/Reference/Manifest-Schema.md#maintenance-edge-proxy).

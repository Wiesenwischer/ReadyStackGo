# Edge-Proxy Example Stacks

Three deployable demo products that exercise the [Maintenance Edge-Proxy](../../../docs/Reference/Manifest-Schema.md#maintenance-edge-proxy) end-to-end — one per maintenance-page branding mode. Each uses `traefik/whoami` as the upstream and listens on its own port, so all three can run at once.

| Product | Branding mode | Public port |
|---------|---------------|-------------|
| `edge-default` | `default` (built-in themeable page) | `9443` |
| `edge-bundle` | `bundle` (static `maintenance/index.html`) | `9444` |
| `edge-container` | `container` (own maintenance container) | `9445` |

## Prerequisites

For `edge-container`, build the example maintenance image once:

```bash
docker build -t rsgo-examples/edge-maintenance-page:1.0 examples/edge-maintenance/container
```

## Deploy

1. In RSGO, add a **LocalDirectory** stack source pointing at `/app/stacks` (the compose mount) and **sync** it — the three `edge-*` products appear in the catalog.
2. Deploy any of them. RSGO provisions the edge container `<deploymentName>-edge` and switches it to proxy once the product is `Running`.

## Test the behaviour

Use `--resolve` so the TLS SNI matches the self-signed cert (replace port per product):

```bash
# Running → transparent proxy to whoami
curl -k --resolve edge-default.localhost:9443:127.0.0.1 https://edge-default.localhost:9443/
curl -k --resolve edge-default.localhost:9443:127.0.0.1 https://edge-default.localhost:9443/__status
```

- **Redeploy** the product → the edge keeps serving the maintenance page (`/__status` → `deploying`); no connection-refused. After `Running` it proxies again.
- Set the **maintenance flag** (Enter Maintenance) → the page shows planned-maintenance wording and `/__status` → `maintenance` with a `reason`.
- `/hc` and `/liveness` are always passed through to the upstream.

> These manifests are examples for local testing. Adjust `publicHostname`, ports, the upstream
> and the maintenance assets for your own product. See the templates in
> [`examples/edge-maintenance/`](../../../examples/edge-maintenance/).

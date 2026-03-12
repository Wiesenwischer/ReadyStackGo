---
title: Custom Distributions
description: Run RSGO with your own corporate design — companies can build a fully branded deployment platform on top of RSGO.
---

**Custom Distributions** allow companies to use ReadyStackGo as the foundation for a fully branded deployment platform. Instead of running a generic tool, you get a tailored solution in your own corporate design — with your logo, your colors, and your individual look and feel.

:::tip[For Software Vendors and System Integrators]
Custom Distributions are ideal for companies that want to use RSGO as the foundation for their own product — without giving up the proven infrastructure and functionality of RSGO.
:::

---

## What is a Custom Distribution?

An RSGO Distribution is a customized version of ReadyStackGo that is:

- **Fully branded** — your own logo, colors, and product name
- **Technically identical** to RSGO — same core functionality, same APIs, same stability
- **Independently deployed** — as a standalone Docker container with its own image

The concept is similar to enterprise products built on open-source foundations: the technology comes from RSGO, the presentation and branding come from your company.

---

## Architecture

RSGO is built as a monorepo with clearly separated packages:

| Package | Description |
|---------|-------------|
| `@rsgo/core` | Shared hooks, API layer, state management — unchanged in every distribution |
| `@rsgo/ui-generic` | React/Tailwind reference implementation (the standard RSGO interface) |
| `@rsgo/ui-[distribution]` | Distribution-specific UI based on `@rsgo/core` — e.g. using ConsistentUI/Lit |

A distribution implements the `IBootstrapper` interface from `@rsgo/core` to hook into the RSGO startup lifecycle, and optionally `ISetupWizardDefinitionProvider` for a customized setup wizard.

---

## Why Build Your Own Distribution?

### For Software Vendors

As a software vendor or IT service provider, you can offer your customers a professional deployment platform that:

- **Carries your corporate design** — not ReadyStackGo's
- **Runs under your brand** — your own product name, your own logo
- **Is built on proven technology** — without having to develop your own infrastructure from scratch
- **Can be fully self-hosted** — customer data never leaves their own infrastructure

### For Enterprise Organizations

Companies with centralized IT can operate an internal deployment platform that:

- **Integrates with the CI/CD landscape**
- **Complies with corporate branding and UX guidelines**
- **Is centrally maintained** and builds on RSGO updates

---

## Customizing the Setup Wizard

Each distribution can define its own setup wizard. Through `ISetupWizardDefinitionProvider`, the steps and appearance of the initial setup assistant can be customized — for example with a company logo, custom texts, and distribution-specific configuration steps.

---

## Example: AMS Distribution

The **AMS Distribution** is a real-world example of a Custom Distribution:

- Based on RSGO Core (`@rsgo/core`)
- Uses [ConsistentUI](https://github.com/consistentui/web-components) as the design system (Lit Web Components)
- Provides a fully branded interface in the AMS corporate design
- Built and deployed as a standalone Docker image

---

## Creating a Distribution

A new distribution is based on the RSGO monorepo and implements the following interfaces:

### 1. `IBootstrapper`

```typescript
export class MyDistributionBootstrapper implements IBootstrapper {
  bootstrap(app: Application): void {
    // Distribution-specific initialization
    // e.g. theme configuration, registering custom components
  }
}
```

### 2. `ISetupWizardDefinitionProvider` (optional)

```typescript
export class MySetupWizardProvider implements ISetupWizardDefinitionProvider {
  getDefinition(): SetupWizardDefinition {
    return {
      steps: [
        // Customized wizard steps
      ],
      branding: {
        logoUrl: '/assets/my-logo.svg',
        productName: 'My Deployment Platform',
      },
    };
  }
}
```

### 3. Building the Docker Image

The distribution package is included in the Docker build. The resulting image contains RSGO Core, the distribution UI, and the ASP.NET backend — all in a single container.

```dockerfile
FROM rsgo-base AS distribution-build
# Distribution-specific build steps
COPY packages/ui-my-distribution ./packages/ui-my-distribution
RUN pnpm build
```

---

## Interested in Your Own Distribution?

If you want to build a Custom Distribution on top of RSGO, feel free to reach out via [GitHub Issues](https://github.com/Wiesenwischer/ReadyStackGo/issues) — we'll help you get started.

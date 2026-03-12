---
title: Custom Distributions
description: RSGO mit eigenem Corporate Design betreiben — Unternehmen können eine vollständig gebrandete Deployment-Plattform auf Basis von RSGO aufbauen.
---

**Custom Distributions** ermöglichen es Unternehmen, ReadyStackGo als Basis für eine vollständig gebrandete Deployment-Plattform zu nutzen. Statt ein generisches Tool zu betreiben, entsteht so eine maßgeschneiderte Lösung im eigenen Corporate Design — mit eigenem Logo, eigenen Farben und individuellem Erscheinungsbild.

:::tip[Für Softwareanbieter und Systemintegratoren]
Custom Distributions sind ideal für Unternehmen, die RSGO als Grundlage für ihr eigenes Produkt nutzen möchten — ohne dabei auf die bewährte Infrastruktur und Funktionalität von RSGO verzichten zu müssen.
:::

---

## Was ist eine Custom Distribution?

Eine RSGO Distribution ist eine angepasste Version von ReadyStackGo, die:

- **Vollständig gebrandet** ist — eigenes Logo, eigene Farben, eigener Produktname
- **Technisch identisch** mit RSGO ist — gleiche Kernfunktionalität, gleiche APIs, gleiche Stabilität
- **Unabhängig deployed** wird — als eigenständiger Docker-Container mit eigenem Image

Das Konzept ist vergleichbar mit Enterprise-Produkten, die auf Open-Source-Basis aufbauen: Die Technologie kommt von RSGO, die Präsentation und das Branding kommen vom Unternehmen.

---

## Architektur

RSGO ist intern als Monorepo mit klar getrennten Packages aufgebaut:

| Package | Beschreibung |
|---------|-------------|
| `@rsgo/core` | Gemeinsame Hooks, API-Layer, State Management — unverändert in jeder Distribution |
| `@rsgo/ui-generic` | React/Tailwind Referenz-Implementation (die Standard-RSGO-Oberfläche) |
| `@rsgo/ui-[distribution]` | Distributionsspezifische UI auf Basis von `@rsgo/core` — z.B. mit ConsistentUI/Lit |

Eine Distribution implementiert das `IBootstrapper`-Interface aus `@rsgo/core`, um sich in den RSGO-Start-Lifecycle einzuklinken, sowie optional `ISetupWizardDefinitionProvider` für einen angepassten Setup-Wizard.

---

## Warum eine eigene Distribution?

### Für Softwareanbieter

Als Softwareanbieter oder IT-Dienstleister können Sie Ihren Kunden eine professionelle Deployment-Plattform anbieten, die:

- **Ihr Unternehmensdesign** trägt — nicht das von ReadyStackGo
- **Unter Ihrer Marke** läuft — eigener Produktname, eigenes Logo
- **Auf bewährter Technologie** basiert — ohne eigene Infrastruktur von Grund auf entwickeln zu müssen
- **Vollständig Self-Hosted** betrieben werden kann — Kundendaten verlassen nie deren Infrastruktur

### Für große Unternehmen

Unternehmen mit zentraler IT können eine interne Deployment-Plattform betreiben, die:

- **In die CI/CD-Landschaft** integriert ist
- **Unternehmensrichtlinien** für Branding und UX einhält
- **Zentral gepflegt** wird und auf RSGO-Updates aufbaut

---

## Setup Wizard anpassen

Jede Distribution kann einen eigenen Setup Wizard definieren. Über `ISetupWizardDefinitionProvider` lassen sich die Schritte und das Erscheinungsbild des ersten Einrichtungsassistenten anpassen — beispielsweise mit Unternehmenslogo, angepassten Texten und distributionsspezifischen Konfigurationsschritten.

---

## Distribution erstellen

Eine neue Distribution basiert auf dem RSGO-Monorepo und implementiert folgende Interfaces:

### 1. `IBootstrapper`

```typescript
export class MyDistributionBootstrapper implements IBootstrapper {
  bootstrap(app: Application): void {
    // Distribution-spezifische Initialisierung
    // z.B. Theme-Konfiguration, Custom-Components registrieren
  }
}
```

### 2. `ISetupWizardDefinitionProvider` (optional)

```typescript
export class MySetupWizardProvider implements ISetupWizardDefinitionProvider {
  getDefinition(): SetupWizardDefinition {
    return {
      steps: [
        // Angepasste Wizard-Schritte
      ],
      branding: {
        logoUrl: '/assets/my-logo.svg',
        productName: 'My Deployment Platform',
      },
    };
  }
}
```

### 3. Docker-Image bauen

Das Distribution-Package wird beim Docker-Build eingebunden. Das resultierende Image enthält RSGO Core, die Distribution-UI und den ASP.NET-Backend — alles in einem Container.

```dockerfile
FROM rsgo-base AS distribution-build
# Distribution-spezifische Build-Steps
COPY packages/ui-my-distribution ./packages/ui-my-distribution
RUN pnpm build
```

---

## Interesse an einer eigenen Distribution?

Wenn Sie eine Custom Distribution auf Basis von RSGO aufbauen möchten, melden Sie sich gerne über [GitHub Issues](https://github.com/Wiesenwischer/ReadyStackGo/issues) — wir unterstützen Sie beim Einstieg.

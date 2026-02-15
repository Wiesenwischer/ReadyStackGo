# AMS UI Integration in ReadyStackGo

**Ziel:**  
Beschreiben, wie das bestehende ams UI Framework & Design System (inkl. Web Components) in die ReadyStackGo-ams Distribution integriert wird – auf Basis der zuvor definierten Frontend-Architektur:

- `@rsgo/core` → Hooks & ViewModel (Logik, API, SignalR)
- `@rsgo/ui-ams` → React UI, die das ams Design System (Web Components) nutzt
- `rsgo-ams` App → verwendet `@rsgo/ui-ams` + `@rsgo/core` als fertiges Frontend

Diese Datei kannst du z. B. als  
`/docs/AMS-UI-INTEGRATION.md` in dein ams-Repo oder ins RSGO-Repo legen.

---

## 1. Überblick

### 1.1 Architektur im Frontend

```text
/packages
  /core            # @rsgo/core
  /ui-ams          # @rsgo/ui-ams (React + ams Design System)
  /ams-design      # optional: Wrapper oder DS-spezifische Helper

/apps
  /rsgo-ams        # App-Host, nutzt @rsgo/core + @rsgo/ui-ams
```

- **@rsgo/core**
  - enthält:
    - DTO-Typen (Spiegel der C#-DTOs),
    - API-Clients (fetch/JSON),
    - Hooks als ViewModel (z. B. `useStacksPageViewModel()`).
  - kennt **kein** ams Design System.

- **ams UI Framework & Design System**
  - liefert Web Components (`<ams-button>`, `<ams-card>`, `<ams-input>`, `<ams-stack-card>` etc.).
  - wird im Browser über `customElements.define(... )` registriert.

- **@rsgo/ui-ams**
  - React UI-Paket:
    - bootstrapped das ams Design System,
    - nutzt kleine React-Wrapper für Web Components,
    - kombiniert diese mit `@rsgo/core` Hooks.

---

## 2. Einbindung des ams Design Systems

### 2.1 Registrierung der Web Components

Damit React `<ams-...>`-Tags nutzen kann, muss das Design System einmalig initialisiert werden.

```ts
// ui-ams/src/setupDesignSystem.ts

// Beispiel – je nach DS-Implementierung anpassen:
import "@ams/design-system/button";
import "@ams/design-system/card";
import "@ams/design-system/input";
import "@ams/design-system/stack-card";
// ... weitere Komponenten

// optional: Theme laden
import "@ams/design-system/theme/default.css";
```

Im App-Entry:

```ts
// ui-ams/src/main.tsx

import "./setupDesignSystem";

import React from "react";
import ReactDOM from "react-dom/client";
import { App } from "./App";

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
```

Damit sind alle `ams-*` Custom Elements im Browser verfügbar.

---

## 3. React-Wrapper für Web Components

React kann Custom Elements direkt rendern, aber:

- Props werden in der Regel als **Attribute** gesetzt,
- **Properties** und **Custom Events** brauchen Wrapper-Logik.

Deshalb erstellen wir **Wrapper-Komponenten**, die:

- in React typisch aussehen (Props, Events),
- intern Web Components nutzen (`<ams-button>` etc.),
- Properties und Events korrekt behandeln.

### 3.1 Einfacher Wrapper: `<ams-button>`

Wenn der Button primär über Attribute gesteuert wird:

```tsx
// ui-ams/src/components/AmsButton.tsx

import React from "react";

export type AmsButtonVariant = "primary" | "secondary" | "danger";

export interface AmsButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: AmsButtonVariant;
}

export const AmsButton: React.FC<AmsButtonProps> = ({
  variant = "primary",
  children,
  ...rest
}) => {
  return (
    <ams-button variant={variant} {...rest}>
      {children}
    </ams-button>
  );
};
```

Verwendung:

```tsx
<AmsButton variant="primary" onClick={handleClick}>
  Speichern
</AmsButton>
```

---

### 3.2 Komplexerer Wrapper: `<ams-stack-card>` mit Properties & Custom Events

Angenommen das Design System erwartet:

- Property `data` (Objekt),
- feuert `stackSelected` als `CustomEvent<{ id: string }>`.

```tsx
// ui-ams/src/components/AmsStackCard.tsx

import React from "react";

export type StackCardData = {
  id: string;
  name: string;
  version: string;
  status: string;
};

export interface AmsStackCardProps {
  data: StackCardData;
  onStackSelected?: (id: string) => void;
}

export const AmsStackCard: React.FC<AmsStackCardProps> = ({
  data,
  onStackSelected
}) => {
  const ref = React.useRef<any>(null);

  // data als Property setzen
  React.useEffect(() => {
    if (ref.current) {
      ref.current.data = data;
    }
  }, [data]);

  // Custom Event binden
  React.useEffect(() => {
    const el = ref.current;
    if (!el || !onStackSelected) return;

    const handler = (e: CustomEvent<{ id: string }>) => {
      onStackSelected(e.detail.id);
    };

    el.addEventListener("stackSelected", handler as EventListener);

    return () => {
      el.removeEventListener("stackSelected", handler as EventListener);
    };
  }, [onStackSelected]);

  return <ams-stack-card ref={ref}></ams-stack-card>;
};
```

Verwendung in einer Page:

```tsx
<AmsStackCard
  data={{
    id: s.id,
    name: s.name,
    version: s.version,
    status: s.status
  }}
  onStackSelected={(id) => vm.installStack(id)}
/>
```

---

## 4. Kombination mit `@rsgo/core` Hooks

### 4.1 Beispiel: `StacksPage` in `@rsgo/ui-ams`

```tsx
// ui-ams/src/pages/StacksPage.tsx

import React from "react";
import { useStacksPageViewModel } from "@rsgo/core";
import { AmsButton } from "../components/AmsButton";
import { AmsStackCard } from "../components/AmsStackCard";

export const StacksPage: React.FC = () => {
  const vm = useStacksPageViewModel();

  return (
    <div className="flex flex-col gap-4">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-primary-600">
          ams.project Stacks
        </h1>
        <AmsButton onClick={vm.refresh} disabled={vm.isLoading}>
          Neu laden
        </AmsButton>
      </header>

      {vm.error && (
        <div className="mb-4">
          <ams-alert variant="error">{vm.error}</ams-alert>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
        {vm.stacks.map((s) => (
          <AmsStackCard
            key={s.id}
            data={{
              id: s.id,
              name: s.name,
              version: s.version,
              status: s.status
            }}
            onStackSelected={(id) => {
              if (vm.canInstall && s.status === "available") {
                void vm.installStack(id);
              }
            }}
          />
        ))}
      </div>
    </div>
  );
};
```

- `useStacksPageViewModel()` kommt aus `@rsgo/core` und kapselt:
  - API-Aufrufe (`/api/stacks/viewmodel`),
  - Loading/Error-State,
  - `installStack(id)` → POST ans Backend.
- Die Page rendert ausschließlich:
  - Layout (Grid, Header),
  - Web Components via Wrapper (`AmsButton`, `AmsStackCard`).

**Ergebnis:**

- UI ist zu 100 % ams CI (Design System),
- Logik ist 100 % in Core + Backend gekapselt.

---

## 5. App-Host `rsgo-ams`

### 5.1 App-Komponente

```tsx
// apps/rsgo-ams/src/App.tsx

import React from "react";
import { BrowserRouter, Routes, Route } from "react-router-dom";
import { StacksPage } from "@rsgo/ui-ams"; // Barrel-Export aus ui-ams
// weitere Pages (Wizard, Environments, Deployments, ...)

export const App: React.FC = () => (
  <BrowserRouter>
    <Routes>
      <Route path="/stacks" element={<StacksPage />} />
      {/* weitere ams-spezifische Routen */}
    </Routes>
  </BrowserRouter>
);
```

### 5.2 Entry

```tsx
// apps/rsgo-ams/src/main.tsx

import "../../ui-ams/src/setupDesignSystem"; // oder eigenes Paket

import React from "react";
import ReactDOM from "react-dom/client";
import { App } from "./App";

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
```

---

## 6. Typisierung & Developer Experience

### 6.1 Typen für Web Components

Je nach Implementierung des Design Systems:

- **Stencil / Lit** können oft TypeScript-Typen generieren:
  - ggf. als `@types/ams-design-system` Package bereitstellen.
- Alternativ:
  - Typen manuell in den React-Wrappern definieren (wie in `AmsButtonProps`, `AmsStackCardProps` gezeigt).

### 6.2 Empfehlung

- Web Components möglichst **„präsentationsorientiert“** halten:
  - Props/Properties rein,
  - Events raus,
  - keine komplexe Business-Logik darin.
- Logik bleibt im C#-Backend und in den `@rsgo/core` Hooks.
- React-Wrapper sind dünne Adapter.

---

## 7. Vorteile dieses Integrationsansatzes

- ✅ **Wiederverwendung** des bestehenden ams Design Systems (Web Components).
- ✅ **Saubere Trennung**:
  - `@rsgo/core` → Logik & Datenfluss,
  - `@rsgo/ui-ams` → ams CI + Web Components.
- ✅ **Flexibilität**:
  - andere Distributionen (z. B. Generic UI) können reines React/Tailwind oder ein anderes Design System nutzen.
- ✅ **Testbarkeit**:
  - Hooks (`@rsgo/core`) separat testbar,
  - Wrapper & Pages können als reine UI getestet werden (Storybook, Jest, Playwright).

---

## 8. Zusammenfassung

- Das **ams UI Framework** (Web Components) wird im ams-Frontend (`@rsgo/ui-ams`) initialisiert.
- Für jede relevante Web Component gibt es einen **React-Wrapper**, der:
  - Props (inkl. komplexer Properties) an das Custom Element übergibt,
  - Custom Events in React-Callbacks übersetzt.
- Die Pages in `@rsgo/ui-ams` verwenden:
  - `@rsgo/core` Hooks als ViewModels,
  - die Wrapper-Komponenten aus dem ams Design System.
- Die App `rsgo-ams` setzt alles zusammen und bildet das UI der ams-Distribution.

Damit kannst du die ams-Edition von ReadyStackGo perfekt an das bestehende ams Design System andocken, ohne die saubere Trennung von Core/Logic und UI aufzugeben.

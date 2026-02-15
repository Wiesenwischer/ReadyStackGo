# ReadyStackGo Frontend Architektur

**Ziel:**  
Klares Muster, wie das Frontend von ReadyStackGo aufgebaut ist, so dass:

- **C# (.NET 9)** die komplette fachliche Logik hält (Domain + Application + „ViewModel-Backend“),
- **TypeScript/React** nur als dünne View-Schicht fungiert,
- und **verschiedene UIs** (Generic vs. ams) durch Austausch eines UI-Packages realisiert werden können.

Diese Datei kannst du z. B. als  
`/docs/RSGO-FRONTEND-ARCH.md` in dein GitHub-Repo legen.

---

## 1. High-Level Architektur

### 1.1 Ziele

- **Keine C#-Logik im Browser**, aber:
  - fachliche Entscheidungen und Zustände kommen **aus dem Backend**.
- **Frontend-Core** (TS/React) kapselt:
  - API-Zugriff,
  - State-Handling (inkl. Loading/Error),
  - SignalR-Subscriptions.
- **UI-Packages** sind:
  - reine Views („dumme Komponenten“),
  - austauschbar (Generic vs. ams),
  - nutzen dieselben Core-Hooks/ViewModels.

### 1.2 Struktur (Monorepo-Sicht)

Empfohlenes Setup (z. B. mit pnpm / yarn workspaces):

```text
/packages
  /core            # @rsgo/core   -> DTO-Typen, API-Clients, Hooks (ViewModel)
  /ui-generic      # @rsgo/ui-generic -> generische UI-Komponenten & Pages
  /ui-ams          # @rsgo/ui-ams      -> ams-spezifische UI-Komponenten & Pages
/apps
  /rsgo-generic    # Generic Host, nutzt core + ui-generic
  /rsgo-ams        # Ams Host, nutzt core + ui-ams
```

---

## 2. Verantwortlichkeiten

### 2.1 Backend (C#/.NET 9)

- **Domain & Application Layer**:
  - „Wahrheit“ über:
    - Organisationen, Environments,
    - StackSources, ContainerRegistries,
    - StackManifests, StackInstallations, Deployments,
    - Health/Zustand, Berechtigungen etc.
- **„ViewModel-Backend“**:
  - stellt pro Seite **spezialisierte DTOs** zur Verfügung, z. B.:
    - `StackListViewModelDto`,
    - `WizardViewModelDto`,
    - `DeploymentListViewModelDto`.
  - Aggregiert ggf. mehrere Domänenquellen für eine UI-Ansicht.
- **HTTP-API & SignalR**:
  - `/api/...` → liefert DTOs, nimmt Kommandos entgegen.
  - `/hubs/...` → pusht Events (Deployments, Health-Updates, etc.).

> Wichtig: Die „Intelligenz“ sitzt im C#-Service.  
> Das Frontend bildet diese nur ab.

---

### 2.2 Frontend-Core (`@rsgo/core`)

**Ziel:** dünner ViewModel-Layer in TypeScript.

- Definiert:
  - **DTO-Typen** (Spiegel der C#-DTOs),
  - **API-Client-Funktionen** (Fetch/JSON),
  - **React Hooks** als „ViewModels“.

#### 2.2.1 Beispiel: DTO-Typen & API-Funktionen

```ts
// @rsgo/core/src/api/stacks.ts

export type StackListItemDto = {
  id: string;
  name: string;
  version: string;
  status: string;       // "installed" | "available" | "updating" ...
  description?: string;
};

export type StackListViewModelDto = {
  stacks: StackListItemDto[];
  canInstall: boolean;
  canUpgrade: boolean;
};

export async function fetchStackListViewModel(): Promise<StackListViewModelDto> {
  const res = await fetch("/api/stacks/viewmodel");
  if (!res.ok) {
    throw new Error(`Failed to load stacks: ${res.status}`);
  }
  return res.json();
}

export async function installStack(id: string): Promise<void> {
  const res = await fetch(`/api/stacks/${encodeURIComponent(id)}/install`, {
    method: "POST"
  });
  if (!res.ok && res.status !== 202) {
    throw new Error(`Failed to start install: ${res.status}`);
  }
}
```

#### 2.2.2 Beispiel: Hook als ViewModel

```ts
// @rsgo/core/src/hooks/useStacksPageViewModel.ts

import { useEffect, useState } from "react";
import {
  fetchStackListViewModel,
  installStack,
  type StackListItemDto,
  type StackListViewModelDto
} from "../api/stacks";

export function useStacksPageViewModel() {
  const [stacks, setStacks] = useState<StackListItemDto[]>([]);
  const [canInstall, setCanInstall] = useState(false);
  const [canUpgrade, setCanUpgrade] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    setIsLoading(true);
    setError(null);
    try {
      const vm: StackListViewModelDto = await fetchStackListViewModel();
      setStacks(vm.stacks);
      setCanInstall(vm.canInstall);
      setCanUpgrade(vm.canUpgrade);
    } catch (e: any) {
      setError(e.message ?? "Unknown error");
    } finally {
      setIsLoading(false);
    }
  }

  async function install(id: string) {
    setIsLoading(true);
    try {
      await installStack(id);
      await refresh();
    } catch (e: any) {
      setError(e.message ?? "Install failed");
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
  }, []);

  return {
    stacks,
    canInstall,
    canUpgrade,
    isLoading,
    error,
    refresh,
    installStack: install
  };
}
```

> **Wichtig:**  
> Im Core-Hook findet keine fachliche Entscheidung statt („darf der User das?“ etc.).  
> Die Logik dazu kommt bereits aus dem C#-ViewModel-Endpoint (`CanInstall`, `CanUpgrade`, `Status` etc.).

---

### 2.3 UI-Pakete (`@rsgo/ui-generic`, `@rsgo/ui-ams`)

**Ziel:** reine Views, kein Business-Code.

Beide Pakete:

- importieren **die gleichen Hooks** aus `@rsgo/core`,
- unterscheiden sich nur in:
  - Styling (Tailwind/Design-System),
  - Struktur (Tabellen vs. Karten),
  - Texten/Branding.

#### 2.3.1 Generic UI – Beispiel

```tsx
// @rsgo/ui-generic/src/pages/StacksPage.tsx

import React from "react";
import { useStacksPageViewModel } from "@rsgo/core";

export const StacksPage: React.FC = () => {
  const vm = useStacksPageViewModel();

  if (vm.isLoading && vm.stacks.length === 0) {
    return <div>Stacks werden geladen…</div>;
  }

  if (vm.error) {
    return (
      <div>
        <p>Fehler: {vm.error}</p>
        <button onClick={vm.refresh}>Erneut versuchen</button>
      </div>
    );
  }

  return (
    <div>
      <h1>Stacks</h1>
      <button onClick={vm.refresh} disabled={vm.isLoading}>
        Aktualisieren
      </button>

      <ul>
        {vm.stacks.map(s => (
          <li key={s.id}>
            <strong>{s.name}</strong> ({s.version}) – {s.status}
            {vm.canInstall && s.status === "available" && (
              <button onClick={() => vm.installStack(s.id)} disabled={vm.isLoading}>
                Installieren
              </button>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
};
```

#### 2.3.2 Ams UI – Beispiel

```tsx
// @rsgo/ui-ams/src/pages/StacksPage.tsx

import React from "react";
import { useStacksPageViewModel } from "@rsgo/core";

export const StacksPage: React.FC = () => {
  const vm = useStacksPageViewModel();

  return (
    <div className="flex flex-col gap-4">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-primary-600">
          ams.project Stacks
        </h1>
        <button
          className="btn btn-outline"
          onClick={vm.refresh}
          disabled={vm.isLoading}
        >
          Neu laden
        </button>
      </header>

      {vm.error && (
        <div className="alert alert-error">
          <span>{vm.error}</span>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
        {vm.stacks.map(s => (
          <div key={s.id} className="card border rounded-lg p-4 shadow-sm">
            <div className="flex justify-between items-start">
              <div>
                <h2 className="font-semibold">{s.name}</h2>
                <p className="text-xs text-gray-500">Version {s.version}</p>
              </div>
              <span className="badge badge-outline">{s.status}</span>
            </div>

            {vm.canInstall && s.status === "available" && (
              <button
                className="btn btn-primary mt-3 w-full"
                onClick={() => vm.installStack(s.id)}
                disabled={vm.isLoading}
              >
                Installieren
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
};
```

> UI-Paket unterscheiden sich nur in Darstellung – **nicht** in Logik.  
> Die gesamte Programmlogik bleibt im C#-Backend und im Core-Hook.

---

## 3. Realtime / SignalR

### 3.1 Backend: SignalR-Hubs

Beispiel: `DeploymentHub` in C#:

```csharp
public class DeploymentHub : Hub
{
    // serverseitige Methoden, um Clients über Updates zu informieren
    public async Task NotifyDeploymentUpdated(DeploymentUpdatedDto dto)
    {
        await Clients.All.SendAsync("DeploymentUpdated", dto);
    }
}
```

Domain-/Application-Services rufen `NotifyDeploymentUpdated` auf, wenn sich ein Deployment ändert.

### 3.2 Frontend-Core: SignalR-Integration als Hook

```ts
// @rsgo/core/src/realtime/useDeploymentEvents.ts

import { useEffect } from "react";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { useDeploymentStore } from "./useDeploymentStore";

export function useDeploymentEvents() {
  const { updateDeployment } = useDeploymentStore();

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/deployments")
      .withAutomaticReconnect()
      .build();

    connection.on("DeploymentUpdated", payload => {
      updateDeployment(payload);
    });

    async function start() {
      try {
        await connection.start();
      } catch (err) {
        console.error("SignalR connection failed", err);
      }
    }

    void start();

    return () => {
      void connection.stop();
    };
  }, [updateDeployment]);
}
```

### 3.3 Verwendung im ViewModel-Hook

```ts
// @rsgo/core/src/hooks/useDeploymentsPageViewModel.ts

import { useDeploymentStore } from "../realtime/useDeploymentStore";
import { useDeploymentEvents } from "../realtime/useDeploymentEvents";

export function useDeploymentsPageViewModel() {
  const { deployments, isLoading, error, refresh } = useDeploymentStore();

  // aktiviert Live-Updates
  useDeploymentEvents();

  return {
    deployments,
    isLoading,
    error,
    refresh
  };
}
```

UI (Generic oder Ams) subscribed einfach auf `useDeploymentsPageViewModel()` und rendert Live-Änderungen.

---

## 4. Apps (`/apps/rsgo-generic` & `/apps/rsgo-ams`)

### 4.1 Generic App

```tsx
// /apps/rsgo-generic/src/App.tsx

import React from "react";
import { BrowserRouter, Routes, Route } from "react-router-dom";
import { StacksPage } from "@rsgo/ui-generic";
// ... andere Pages

export const App: React.FC = () => (
  <BrowserRouter>
    <Routes>
      <Route path="/stacks" element={<StacksPage />} />
      {/* weitere Routen */}
    </Routes>
  </BrowserRouter>
);
```

### 4.2 Ams App

```tsx
// /apps/rsgo-ams/src/App.tsx

import React from "react";
import { BrowserRouter, Routes, Route } from "react-router-dom";
import { StacksPage } from "@rsgo/ui-ams";
// ... ams-spezifische Pages

export const App: React.FC = () => (
  <BrowserRouter>
    <Routes>
      <Route path="/stacks" element={<StacksPage />} />
      {/* ams-spezifische Routen */}
    </Routes>
  </BrowserRouter>
);
```

Beide:

- verwenden dasselbe Backend (unterschiedliche Hosts/Images),
- denselben Frontend-Core (`@rsgo/core`),
- unterschiedliche UI-Packages.

---

## 5. Vorteile dieses Ansatzes

- ✅ **C# bleibt der „Single Source of Truth“** für Logik & Berechtigungen.
- ✅ **React/TS bleibt schlank** – hauptsächlich Darstellung, State, API/Wireup.
- ✅ **Unterschiedliche UIs (Generic vs. ams)** sind leicht realisierbar:
  - UI-Pakete tauschen, Core bleibt gleich.
- ✅ **Wiederverwendung**:
  - Core-Hooks können von beliebigen Distributionen genutzt werden.
- ✅ **Testbarkeit**:
  - C#-Logik in Unit-/Integration-Tests,
  - TS-Hooks in einfachen Frontend-Tests,
  - UI-Komponenten sind „dumme“ Presentational Components.

---

## 6. Zusammenfassung

- **Backend (C#)**:
  - liefert _ViewModel-DTOs_ über REST & SignalR.
- **Frontend-Core (`@rsgo/core`)**:
  - realisiert API-Clients, DTO-Typen und Hooks als dünne ViewModels.
- **UI-Pakete (`@rsgo/ui-generic`, `@rsgo/ui-ams`)**:
  - konsumieren diese Hooks und definieren nur Layout & Styling.
- **Apps**:
  - binden die passenden UI-Packages ein (Generic vs. ams) und bauen das finale SPA für das jeweilige Docker-Image.

Damit ist die Trennung zwischen **Logik**, **ViewModel** und **UI** klar und du kannst ReadyStackGo langfristig sowohl als OSS-Core als auch als firmenspezifische Distribution pflegen.


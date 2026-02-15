# ReadyStackGo Distribution Setup  
**Core auf GitHub (OSS) & ams-Distribution in Azure DevOps (privat)**

Dieses Dokument beschreibt, wie du:

- **ReadyStackGo Core** als Open-Source-Projekt auf GitHub betreibst,
- und darauf basierend eine **ams-spezifische Distribution** in Azure DevOps baust,
- mit **zwei getrennten Docker-Images**, ohne dass der Core irgendetwas von `ams.project` wissen muss.

Die Datei ist so geschrieben, dass du sie direkt als  
`/docs/RSGO-DISTRIBUTION-SETUP.md` in dein ReadyStackGo-Repo legen kannst (oder im ams-Repo spiegeln).

---

## 1. Zielbild

### 1.1 High-Level

Wir wollen:

- **Ein zentrales, generisches Produkt**:  
  **ReadyStackGo Core** – Open Source auf GitHub.
- **Eine angepasste, firmenspezifische Distribution**:  
  **ReadyStackGo ams** – private Portierung in Azure DevOps.

Beide nutzen **denselben Core**, aber:

- **UI & Wizard & Default-Konfiguration** unterscheiden sich,
- sie werden als **separate Docker-Images** bereitgestellt.

### 1.2 Produkte

- **Generic / OSS Image** (für dich & Community):
  - z. B. `ghcr.io/wiesenwischer/readystackgo:0.5.0`
  - Generisches UI, generischer Setup-Wizard.
  - Voll konfigurierbar (StackSources, Registries, Environments, …).

- **ams Distribution** (nur für deine Firma / Kunden):
  - z. B. `registry.company.local/rsgo-ams:0.5.0-ams.1`
  - Firmeneigenes Branding (Corporate Identity).
  - ams-spezifischer Wizard:
    - Registry-Token,
    - ams.erp DB-Verbindung,
    - Auswahl von ams-spezifischen Stacks.
  - Vordefinierte StackSource & ContainerRegistry für `amssolution/*`.

**Wichtig:**  
Der **Core weiß nichts von `ams.project`** – alle ams-spezifischen Dinge liegen in der **privaten Distribution**.

---

## 2. Repository-Struktur

### 2.1 GitHub Repo – `ReadyStackGo` (öffentlich)

Enthält alle generischen, wiederverwendbaren Teile:

```text
/ReadyStackGo (GitHub)
  /src
    /ReadyStackGo.Core              # Domain, Application, Infrastructure
    /ReadyStackGo.WebHost.Generic   # Generischer ASP.NET-WebHost
    /ReadyStackGo.Ui.Generic        # Generische React/Tailwind UI
  /docs
    RSGO-DISTRIBUTION-SETUP.md
    ...
```

**ReadyStackGo.Core**:

- Domänenmodell:
  - Organizations, Environments, StackSources, ContainerRegistries, StackManifests, StackInstallations, Deployments, Health, …
- Services:
  - StackSource-Verwaltung (Git, FileSystem, OCI, Import),
  - Container Registry Registry,
  - Deployment Engine (Docker API),
  - Health Monitoring, Logging, etc.
- Erweiterungspunkte:
  - z. B. `ISetupWizardDefinitionProvider`, `IBootstrapper`, `IStackSource`-Implementierungen.

**ReadyStackGo.WebHost.Generic**:

- ASP.NET Core Host für den generischen Modus.
- Registriert:
  - generische Wizard-Definition,
  - keine produktspezifischen Defaults (oder nur minimal).

**ReadyStackGo.Ui.Generic**:

- Generische, neutrale UI (Tailwind, React, o. ä.).
- Voller Zugriff auf alle Konfigurationsbereiche:
  - StackSources, Registries, Orgs, Envs, Stacks, Deployments.

---

### 2.2 Azure DevOps Repo – `ReadyStackGo.Ams` (privat)

Enthält nur ams-spezifische Dinge:

```text
/ReadyStackGo.Ams (Azure DevOps)
  /src
    /ReadyStackGo.WebHost.Ams     # ASP.NET-Host für die ams-Distribution
    /ReadyStackGo.Ui.Ams          # ams-spezifische UI (Branding & Wizard)
  /pipelines
    rsgo-ams-ci.yml
    rsgo-ams-cd.yml
```

**ReadyStackGo.WebHost.Ams**:

- Referenziert `ReadyStackGo.Core` als NuGet-Paket (siehe Abschnitt 3).
- Registriert:
  - `AmsProjectSetupWizardDefinitionProvider`,
  - `AmsBootstrapper` (Seed von StackSources & ContainerRegistries),
  - ggf. ams-spezifische API-Endpunkte (nur falls nötig).

**ReadyStackGo.Ui.Ams**:

- ams-spezifisches Branding:
  - Logos, Farben, Typographie.
- ams-spezifischer Setup Wizard:
  - Schritt 1: Registry-Token für `docker.io/amssolution/*`,
  - Schritt 2: ams.erp DB-Verbindung,
  - Schritt 3: Auswahl ams Stacks (IdentityAccess, Infrastruktur, ams.project Core, Monitoring).
- Evtl. eigene Übersichtsseiten für ams.project.

---

## 3. Kopplung: Wie kommt Ams an den Core?

### 3.1 Empfehlung: Core als NuGet-Package

**Ziel:**

- ReadyStackGo.Core wird in GitHub gebaut und als Paket veröffentlicht.
- ReadyStackGo.Ams verwendet diese Pakete als Dependencies.

#### 3.1.1 Build & Package im GitHub-Repo

CI in GitHub (z. B. GitHub Actions):

1. Code bauen & testen.
2. Version bestimmen (z. B. via GitVersion).
3. `ReadyStackGo.Core` (und evtl. weitere libs) als NuGet-Pakete erstellen.
4. Pakete in einen Feed pushen, z. B.:
   - GitHub Packages,
   - oder direkt Azure Artifacts (öffentlich oder privat).

Beispiel-Package-Namen:

- `ReadyStackGo.Core`
- optional: `ReadyStackGo.Infrastructure`, `ReadyStackGo.Domain`, etc.

#### 3.1.2 Nutzung in Azure DevOps

Im `ReadyStackGo.Ams`-Repo:

- NuGet-Feed (GitHub Packages oder Azure Artifacts) als externe Quelle einbinden.
- In `ReadyStackGo.WebHost.Ams.csproj` (vereinfacht):

```xml
<ItemGroup>
  <PackageReference Include="ReadyStackGo.Core" Version="0.5.0" />
</ItemGroup>
```

Damit:

- ist **klar versioniert**, auf welcher Core-Version die ams-Distribution läuft,
- kannst du Updates bewusst steuern (0.5.0 → 0.6.0).

---

## 4. Wizard & Programmlogik: Erweiterung ohne ams im Core

Der Core definiert nur **Abstraktionen**, die Ams-Distribution implementiert.

### 4.1 Wizard Definition (im Core)

```csharp
public interface ISetupWizardDefinitionProvider
{
    SetupWizardDefinition GetDefinition();
}

public sealed class SetupWizardDefinition
{
    public string Id { get; init; }                    // z.B. "generic", "ams-project"
    public IReadOnlyList<WizardStepDefinition> Steps { get; init; }
}

public sealed class WizardStepDefinition
{
    public string Id { get; init; }                    // "registry-token", "db-connection", ...
    public string Title { get; init; }
    public string Description { get; init; }
    public string ComponentType { get; init; }         // UI-Komponententyp, z.B. "RegistryTokenStep"
}
```

- Core selbst:
  - stellt ein API zur Verfügung, z. B. `GET /api/wizard/definition`.
  - kennt nur **Strukturen**, keine konkreten Flows.

### 4.2 Generische Implementierung (im GitHub-Host)

In `ReadyStackGo.WebHost.Generic`:

```csharp
public sealed class GenericSetupWizardDefinitionProvider : ISetupWizardDefinitionProvider
{
    public SetupWizardDefinition GetDefinition()
        => new()
        {
            Id = "generic",
            Steps =
            [
                new()
                {
                    Id = "registry",
                    Title = "Registry konfigurieren",
                    Description = "Legen Sie Containerregistries fest …",
                    ComponentType = "GenericRegistrySetupStep"
                },
                new()
                {
                    Id = "stackSources",
                    Title = "Stack Sources",
                    Description = "Fügen Sie Stack-Quellen hinzu …",
                    ComponentType = "GenericStackSourceSetupStep"
                }
            ]
        };
}
```

### 4.3 Ams-spezifische Implementierung (im Ams-Host)

In `ReadyStackGo.WebHost.Ams`:

```csharp
public sealed class AmsProjectSetupWizardDefinitionProvider : ISetupWizardDefinitionProvider
{
    public SetupWizardDefinition GetDefinition()
        => new()
        {
            Id = "ams-project",
            Steps =
            [
                new()
                {
                    Id = "registry-token",
                    Title = "Registry-Zugriff einrichten",
                    Description = "Bitte geben Sie den bereitgestellten API-Token ein.",
                    ComponentType = "AmsRegistryTokenStep"
                },
                new()
                {
                    Id = "db-connection",
                    Title = "Datenbank-Verbindung",
                    Description = "Verbindung zur ams.erp-Datenbank konfigurieren.",
                    ComponentType = "AmsDbConnectionStep"
                },
                new()
                {
                    Id = "initial-stacks",
                    Title = "ams.project Komponenten auswählen",
                    Description = "Wählen Sie, welche ams.project Teile installiert werden sollen.",
                    ComponentType = "AmsStackSelectionStep"
                }
            ]
        };
}
```

- WebHost.Ams registriert diese Implementierung im DI-Container.
- Die UI (Ui.Ams) kennt die zugehörigen Komponenten-Namen (`AmsRegistryTokenStep` etc.) und rendert sie.

**Ergebnis:**

- Der **Core** weiß nur: „Es gibt einen Wizard mit Steps“.
- Die **Konkrete Ausgestaltung** ist 100 % ams-spezifisch im Ams-Repo.

---

## 5. Vorkonfiguration im Ams-Host (Bootstrapper)

Ams-spezifische Defaults (StackSource/Registry) liegen **nur** im Ams-Host.

### 5.1 Beispiel-Bootstrapper

```csharp
public sealed class AmsBootstrapper
{
    private readonly IStackSourceRepository _stackSources;
    private readonly IContainerRegistryRepository _registries;

    public AmsBootstrapper(
        IStackSourceRepository stackSources,
        IContainerRegistryRepository registries)
    {
        _stackSources = stackSources;
        _registries = registries;
    }

    public async Task InitializeAsync()
    {
        // 1) OCI Stack Source für amssolution
        await _stackSources.EnsureExistsAsync(new StackSource
        {
            Id = "oci-amssolution",
            Type = StackSourceType.Oci,
            // Diese Details sind nur hier bekannt:
            // RegistryHost = "index.docker.io",
            // Repository  = "amssolution/rsgo-stacks",
            // TagPattern  = "*"
        });

        // 2) Container Registry Config für amssolution/*
        await _registries.EnsureExistsAsync(new ContainerRegistryConfig
        {
            Id = "amssolution-dockerhub",
            Host = "index.docker.io",
            Pattern = "amssolution/*",
            Username = "amssolutionci", // Fix, Token kommt im Wizard
            Password = null
        });
    }
}
```

Dieser Bootstrapper:

- wird beim ersten Start des Ams-Hosts ausgeführt,
- legt Standard-Einträge an, falls sie noch nicht existieren.

Der Core kennt:

- nur `IStackSourceRepository`, `IContainerRegistryRepository`,
- **nicht** die speziellen Werte (Host, Repository, Patterns).

---

## 6. Docker-Images & Deployments

### 6.1 Generic Image (GitHub)

Dockerfile (vereinfacht):

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app
COPY ./publish/generic/ .    # Build-Artefakte von ReadyStackGo.WebHost.Generic + Ui.Generic

ENTRYPOINT ["dotnet", "ReadyStackGo.WebHost.Generic.dll"]
```

CI-Schritte (GitHub Actions):

1. Build + Test.
2. Publish (z. B. `dotnet publish -c Release -o ./publish/generic`).
3. Docker-Image bauen.
4. Push zu `ghcr.io/wiesenwischer/readystackgo:<version>`.

### 6.2 Ams Image (Azure DevOps)

Dockerfile (vereinfacht):

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app
COPY ./publish/ams/ .        # Build-Artefakte von ReadyStackGo.WebHost.Ams + Ui.Ams

ENTRYPOINT ["dotnet", "ReadyStackGo.WebHost.Ams.dll"]
```

CI-Schritte (Azure DevOps):

1. NuGet-Pakete aus GitHub/Azure Artifacts holen (ReadyStackGo.Core).
2. Build + Test Ams-Host + Ams-UI.
3. Publish (z. B. `dotnet publish -c Release -o ./publish/ams`).
4. Docker-Image bauen.
5. Push in eure interne Registry:
   - `registry.company.local/rsgo-ams:<version>`.

---

## 7. Versionierung

Empfehlung:

- **Core-Version**:  
  - z. B. `0.5.0`, `0.6.0`, gesteuert via GitVersion oder Release-Tags.
- **Ams-Distribution-Version**:
  - z. B. `0.5.0-ams.1`, `0.5.0-ams.2`, `0.6.0-ams.1`.

Du kannst z. B.:

- in `ReadyStackGo.Ams` im CI:
  - die Core-Version aus den NuGet-Paketen auslesen,
  - daraus eine zusammengesetzte Version für das Docker-Image bilden.

---

## 8. Zusammenfassung

- **ReadyStackGo.Core** lebt als Open-Source Projekt auf GitHub.
- **ReadyStackGo.Ams** ist eine private Distribution in Azure DevOps:
  - sie referenziert den Core als NuGet-Paket,
  - bringt eigene UI, Wizard-Definition und Bootstrapper mit,
  - und erzeugt ein eigenes `rsgo-ams`-Docker-Image.
- Der Core:

  - kennt **keine** `ams.project`-Details,
  - stellt nur generische Bausteine (StackSources, Registries, Wizard-Interfaces) bereit.

- Die Ams-Distribution:

  - nutzt diese Bausteine,
  - setzt konkrete Werte (StackSource `amssolution/rsgo-stacks`, Registry `amssolution/*`),
  - liefert ein fertiges, firmengebrandetes Produkt für Kunden.

Dieses Setup hält:

- **Wiederverwendbarkeit & Klarheit** im Core,
- **Flexibilität** für weitere zukünftige Distributionen,
- und **saubere Trennung** zwischen Open Source und proprietärer Anpassung.


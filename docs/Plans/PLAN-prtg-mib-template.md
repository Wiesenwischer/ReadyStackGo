<!-- GitHub Epic: #401 (Variant 1) -->
# Phase: PRTG Integration — Variant 1 (MIB + Device-Template Export)

## Ziel

RSGO bietet einen Ein-Klick-Export für PRTG: ein importierbares Device-Template (`.template`-Datei) plus die bestehende MIB-Datei. Ein PRTG-Admin importiert beides einmalig in seinen PRTG-Server, danach reicht klassische SNMP-Auto-Discovery, um RSGO-Instanzen mit allen relevanten Sensoren zu erfassen — **ohne dass RSGO PRTG-Credentials kennt**.

Diese Variante ist der niedrigschwellige Einstieg in die PRTG-Integration: kein Outbound-Call von RSGO, keine Credential-Surface, kompatibel mit jeder PRTG-Lizenz (auch Freeware und PRTG Hosted) und mit allen abgeschotteten PRTG-Setups.

## Analyse

### Bestehende Architektur

- **SNMP-Agent** ([SnmpAgent.cs](../../src/ReadyStackGo.Infrastructure/Snmp/SnmpAgent.cs)) und der OID-Tree aus v0.64 ([PLAN-snmp-agent.md](PLAN-snmp-agent.md)) liefern alle Daten, die ein PRTG-Sensor pollen kann — Produkt-/Stack-/Service-Tabellen plus `rsgoSystem*`-Scalars.
- **MIB-Datei** (`READYSTACKGO-MIB.txt`) liegt bereits als Embedded Resource vor und wird unter `/api/snmp/mib` ausgeliefert.
- **SNMP-Settings-Page** ([SnmpSettingsPage.tsx](../../src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/Snmp/SnmpSettingsPage.tsx)) ist der natürliche Ort für den Template-Download-Button: ein Admin, der gerade SNMP konfiguriert, will auch das PRTG-Setup vorbereiten.
- **Endpoint-Pattern**: `GET /api/snmp/mib` (Download) ist die direkte Vorlage für `GET /api/snmp/prtg-template`.

### PRTG-Device-Template-Format

PRTG-Device-Templates sind XML-Dateien, die ein "Device + Sensoren"-Bundle beschreiben. Beim Import zeigt PRTG dem Admin ein Wizard mit den enthaltenen Sensor-Definitionen; nach Auswahl der RSGO-IP werden die Sensoren angelegt.

Template-Struktur (vereinfacht):

```xml
<devicetemplate id="readystackgo" name="ReadyStackGo Deployment">
  <create>
    <data>
      <kind>snmpcustomtable</kind>
      <name>RSGO: Product Deployments</name>
      <interval>60</interval>
      <baseoid>1.3.6.1.4.1.99999.1.3.1</baseoid>
      <columns>
        <column id="6" channel="Status" unit="Custom" lookup="rsgo.productstatus" />
        <column id="9" channel="Total Stacks" unit="Count" />
        <column id="10" channel="Running Stacks" unit="Count" />
        <column id="11" channel="Failed Stacks" unit="Count" />
      </columns>
      <namecolumn>4</namecolumn>
    </data>
    <data>
      <kind>snmpcustomtable</kind>
      <name>RSGO: Stacks</name>
      <baseoid>1.3.6.1.4.1.99999.1.4.1</baseoid>
      ...
    </data>
    <data>
      <kind>snmpcustomstring</kind>
      <name>RSGO: System Version</name>
      <oid>1.3.6.1.4.1.99999.1.1.1.0</oid>
    </data>
  </create>
</devicetemplate>
```

Plus PRTG-Lookup-Dateien (`rsgo.productstatus.ovl`, `rsgo.stackstatus.ovl`) für das Enum-Mapping (Integer → Statustext + Farbe Grün/Gelb/Rot). Lookups werden in PRTG's `Lookups/Custom/`-Ordner importiert.

### Dynamische Generierung vs. statisches Template

Zwei Wege:

1. **Statisches Template als Embedded Resource** — eine Datei, gepflegt zusammen mit der MIB. Einfach, aber die Sensor-OIDs sind hart kodiert auf RootOid `99999`. Nach IANA-PEN-Migration muss das Template neu released werden.
2. **Dynamisch generiertes Template** — Endpoint baut das XML zur Request-Zeit aus dem aktuellen `Snmp:RootOid` aus den Settings. Erlaubt Customers mit eigener PEN, sofort ein passendes Template zu bekommen.

**Gewählt: Dynamisch.** Mehraufwand ist klein (XML-Template mit Platzhaltern + String-Replace im Endpoint), aber der Wert ist groß: das gleiche RSGO-Image funktioniert für `99999`-Default und für Customers mit eigener PEN, ohne dass wir das Template versionieren müssen.

### Lookups (Enum-Mapping)

PRTG Lookups mappen Integer-Werte auf Text + Sensor-Status. Pro Enum brauchen wir eine Lookup-Datei:

- `rsgo.productstatus.ovl` — `1=Running:OK`, `2=PartiallyRunning:Warning`, `3=Failed:Error`, ...
- `rsgo.stackstatus.ovl` — analog
- `rsgo.healthstatus.ovl` — `1=Healthy:OK`, `2=Unhealthy:Error`, `3=Starting:Warning`
- `rsgo.environmenttype.ovl` — `1=LocalDocker`, `2=SshTunnel`, ...

Lookups werden auch dynamisch erzeugt (aus den Domain-Enums) und im Download-Bundle mitgeliefert.

### Download-Format

Eine einzelne ZIP-Datei `readystackgo-prtg-bundle.zip` mit:

```
readystackgo-prtg-bundle/
├── README.txt                          (Installations-Anweisung)
├── devicetemplates/
│   └── readystackgo.template           (XML, dynamisch erzeugt)
├── snmplibs/
│   └── READYSTACKGO-MIB.txt            (für MIB-Browser-Support)
└── lookups/custom/
    ├── rsgo.productstatus.ovl
    ├── rsgo.stackstatus.ovl
    ├── rsgo.healthstatus.ovl
    └── rsgo.environmenttype.ovl
```

Der Pfad in der ZIP entspricht der Ziel-Struktur unter `C:\Program Files (x86)\PRTG Network Monitor\` — der Admin entpackt das ZIP direkt im PRTG-Programmverzeichnis (Standard-Vorgehen für PRTG-Device-Templates).

### Betroffene Bounded Contexts

- **Domain** — keine neuen Entities. Die Status-Enums (ProductDeploymentStatus, StackStatus, HealthStatus, EnvironmentType) bleiben Single Source of Truth für die Lookup-Generierung.
- **Application** — neuer Query `GetPrtgBundleQuery` der das Template-XML und die Lookups aus dem aktuellen Snapshot der `SnmpSettings.RootOid` und den Enum-Definitionen generiert. Nutzt ZIP-Streaming.
- **Infrastructure** — Template-Skeleton und README als Embedded Resources mit `{{rootOid}}`-Platzhaltern; `PrtgBundleBuilder` interpoliert sie zur Request-Zeit.
- **API** — neuer Endpoint `GET /api/snmp/prtg-bundle` (Permission `Snmp:Read`, liefert `application/zip` mit ZIP-Stream).
- **WebUI (rsgo-generic)** — neuer Bereich "PRTG Integration" auf der SNMP-Settings-Page mit Download-Button und Kurz-Anleitung. Optional zusätzlicher Tab "PRTG" in der OID-Reference-Page für Schritt-für-Schritt-Setup.

## AMS UI Counterpart

> RSGO has two UI distributions with different design systems:
> - **rsgo-generic**: React + Tailwind CSS (reference implementation, `packages/ui-generic`)
> - **AMS UI**: ConsistentUI/Lit web components (separate repo `ReadyStackGo.Ams`)
>
> Shared logic lives in `@rsgo/core` (hooks, API calls, state). Pages/layouts must be reimplemented per distribution.

**Benötigt AMS UI eine Entsprechung?**

- [x] **Ja** — die PRTG-Integrations-Section auf der SNMP-Settings-Page muss auch in der AMS-Distribution erscheinen. Logik (`useDownloadPrtgBundle`) wandert in `@rsgo/core`, UI-Section wird in ConsistentUI reimplementiert.

→ AMS-Counterpart-Plan: `C:\proj\ReadyStackGo.Ams\docs\Plans\PLAN-prtg-mib-template.md`

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: PRTG Template Skeleton + Embedded Resources** — Template-XML mit `{{rootOid}}`-Platzhaltern, README-Text, vier Lookup-Skeletons. Alle als Embedded Resources unter `src/ReadyStackGo.Application/Snmp/Prtg/Resources/`.
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Snmp/Prtg/Resources/readystackgo.template`
    - `src/ReadyStackGo.Application/Snmp/Prtg/Resources/README.txt`
    - `src/ReadyStackGo.Application/Snmp/Prtg/Resources/lookups/*.ovl`
  - Abhängig von: -

- [ ] **Feature 2: PrtgBundleBuilder + GetPrtgBundleQuery** — Service liest Embedded Resources, interpoliert RootOid und Enum-Werte, baut In-Memory-ZIP. CQRS-Query in `@Application`.
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Snmp/Prtg/PrtgBundleBuilder.cs`
    - `src/ReadyStackGo.Application/Snmp/Prtg/GetPrtgBundleQuery.cs`
    - `src/ReadyStackGo.Application/Snmp/Prtg/PrtgBundleResult.cs` (byte[]-Container)
  - Pattern-Vorlage: bestehende MIB-Download-Query
  - Abhängig von: Feature 1

- [ ] **Feature 3: REST-Endpoint** — `GET /api/snmp/prtg-bundle` mit `[RequirePermission("Snmp","Read")]` + `RbacPreProcessor`, Response `application/zip`, Dateiname `readystackgo-prtg-bundle-{version}.zip`.
  - Betroffene Dateien:
    - `src/ReadyStackGo.Api/Endpoints/Snmp/GetPrtgBundleEndpoint.cs`
  - Abhängig von: Feature 2

- [ ] **Feature 4: Core-Hook + API-Client** — `useDownloadPrtgBundle()` in `@rsgo/core` triggert den Download (Browser-Save-Dialog). API-Funktion `downloadPrtgBundle()` in `packages/core/src/api/snmp.ts`.
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/core/src/api/snmp.ts`
    - `src/ReadyStackGo.WebUi/packages/core/src/hooks/usePrtgBundle.ts`
  - Abhängig von: Feature 3

- [ ] **Feature 5: WebUI-Section "PRTG Integration"** — neue Section auf der SNMP-Settings-Page mit Download-Button und Setup-Anleitung (3 Schritte: ZIP herunterladen, in PRTG-Programmverzeichnis entpacken, in PRTG Auto-Discovery starten). Permission-gated (`Snmp:Read`).
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/Snmp/PrtgIntegrationSection.tsx`
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/Snmp/SnmpSettingsPage.tsx` (Section einhängen)
  - Abhängig von: Feature 4

- [ ] **Feature 6: Public Website Doc** — Reference-Page "PRTG Integration" unter DE/EN mit Screenshots des Import-Wizards, Beispiel-Sensor-Konfigurationen und Verlinkung auf die SNMP-Reference.
  - Betroffene Dateien:
    - `public-website/src/content/docs/reference/prtg-integration.md` (DE)
    - `public-website/src/content/docs/en/reference/prtg-integration.md` (EN)

- [ ] **Dokumentation & Wiki** — kurzer Eintrag in der internen Docs unter "Monitoring Integrations", Roadmap-Update.

- [ ] **Phase abschließen** — alle Tests grün, dotnet build 0 errors / 0 warnings, manueller End-to-End-Test (PRTG-Trial-Instanz lokal, Bundle importieren, Discovery laufen lassen, Sensoren prüfen), PR gegen main.

## Test-Strategie

- **Unit Tests**:
  - `PrtgBundleBuilder` — RootOid-Platzhalter-Substitution (Default + custom PEN), ZIP-Struktur (erwartete Einträge vorhanden), Lookups enthalten alle Enum-Werte.
  - Template-XML — Schema-Validierung gegen PRTG-XSD (falls verfügbar; sonst structural assertions).
  - `GetPrtgBundleQuery` — Permission-Check (RBAC-PreProcessor blockt ohne `Snmp:Read`).

- **Integration Tests**:
  - Endpoint-Test: GET `/api/snmp/prtg-bundle` mit/ohne Permission, Response-Header (`Content-Type: application/zip`, `Content-Disposition: attachment`).
  - ZIP-Inhalt entpacken und Struktur asserten.

- **E2E Tests** (Playwright):
  - PRTG-Section auf SnmpSettingsPage sichtbar wenn Permission vorhanden.
  - Download-Button löst Browser-Download aus (Playwright `download` event).

- **Manueller PRTG-Smoke-Test** (Acceptance):
  - PRTG-Trial-Container lokal starten, Bundle importieren, Auto-Discovery gegen RSGO laufen lassen, mindestens drei Sensoren sehen mit Werten aus dem laufenden RSGO.

## Offene Punkte

- [ ] PRTG-Template-Format-Quellen (offizielle Doku-Links) im README sammeln, falls PRTG keine XSD bereitstellt.
- [ ] Klären ob das Bundle auch für **PRTG Hosted** (PaaS-Variante) funktioniert — dort fehlt der lokale Dateisystemzugriff, dann braucht es eventuell zusätzlich ein "Sensor-Library"-Import-Format.

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Template-Generierung | Statisch / Dynamisch zur Request-Zeit | **Dynamisch** | Customer-spezifische RootOids (eigene PEN) ohne Re-Release des Images. Aufwand minimal (String-Replace). |
| Download-Form | Einzeldatei `.template` / ZIP-Bundle | **ZIP-Bundle** | Lookups + MIB müssen mitgeliefert werden. Standard-Verteilformat für PRTG-Templates. |
| UI-Ort | Eigene Seite / Section auf SnmpSettingsPage | **Section auf SnmpSettingsPage** | Konsistent mit MIB-Download (gleicher Workflow), keine neue Navigation. |
| Permission | `Snmp:Read` / neue `Prtg:Read` | **`Snmp:Read`** | Bundle ist Read-only, enthält keine Secrets, gleicher Scope wie MIB-Download. |
| IANA-PEN-Abhängigkeit | blockt v0.66 / Wir liefern mit Placeholder | **Liefern mit Placeholder** | Variante 1 ist read-only und am stärksten von Re-Release entkoppelt — Customer kann nach PEN-Migration einfach erneut downloaden. |

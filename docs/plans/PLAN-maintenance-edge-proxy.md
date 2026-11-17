# PLAN: Generischer Maintenance-Edge-Proxy (R2)

> **Umsetzungsplan.** Dieses Dokument ist so geschrieben, dass eine Claude-Code-Session in diesem Repo
> (`C:\proj\ReadyStackGo`) es **eigenständig, ohne Rückfragen** umsetzen kann. Alle offenen Designfragen sind
> unten als **GESPERRTE ENTSCHEIDUNGEN** vorab geklärt. Beginne mit **Phase 0 (Verifikation am Code)**, setze
> dann Phase 1→5 um und liefere jede Phase mergebar + per Default inert.

---

## 1. Mission

RSGO ist der dauerhaft laufende Multi-Produkt-Orchestrator. Bei **Wartung** oder **Redeploy** eines Produkts
löscht RSGO heute alle Produkt-Container (`DeploymentEngine.RemoveStackAsync` entfernt alles mit Label
`rsgo.stack == <stackVersion>`) → der einzige öffentliche Eingang des Produkts (z. B. die YARP-BFF von
ams.project) ist weg, und Clients/Browser bekommen **Connection-Refused** statt einer Wartungsseite.

**Ziel:** RSGO bekommt eine **generische Fähigkeit**, pro Produkt einen **verwalteten, separaten Reverse-Proxy-
Container („Edge")** zu betreiben, der **den Redeploy überlebt** und — gesteuert durch RSGOs **autoritativen
Deploy-Zustand** + das Maintenance-Flag — entweder transparent zum Upstream durchproxt oder eine **kontrollierte
Wartungsseite** plus **maschinenlesbaren Status** ausliefert.

Das ist **kein** ams.project-Spezifikum: Es ist eine generische RSGO-Fähigkeit für **jedes** Produkt, per Manifest
opt-in und per Default aus.

**Zwei Ebenen (nicht vermischen):**
- **Kontroll-Ebene** („kann der Operator umschalten?") ist **bereits gelöst** durch den vorhandenen
  Maintenance-Setter (`PUT /api/.../operation-mode` → `SqlExtendedPropertySetter`/`WebhookSetter`). **Nicht Teil
  dieses Plans** — nur Eingabequelle.
- **Erlebnis-Ebene** („was sieht Browser/Client, während das Produkt unten ist?") = **dieser Plan**.

---

## 2. GESPERRTE ENTSCHEIDUNGEN (keine Rückfragen nötig)

1. **Edge = separater Container**, von RSGO im Lifecycle verwaltet — **nicht** im RSGO-Prozess. Begründung:
   Control-/Data-Plane-Trennung; RSGO-Self-Update darf keinen Traffic-Blip erzeugen.
2. **Proxy-Technologie = Caddy.** Grund: native **Admin-API** (`POST http://localhost:2019/load`) für **atomares,
   verbindungs-erhaltendes** Config-Reload — exakt unser Config-Push-Modell. (Traefik wäre Alternative; Caddy ist
   gesetzt.) Image: offizielles `caddy:<pin>` (Digest-gepinnt).
3. **Eine Edge-Instanz pro Produkt** (kein geteilter Ingress). Passt zu RSGOs Produkt-Isolation; isolierter
   Blast-Radius; ermöglicht per-Produkt-Branding.
4. **Survival:** Edge (und ein optionaler produkt-beigesteuerter Wartungs-Container) laufen in einem
   **„System/Edge-Scope" außerhalb der Produkt-Stack-Identität** → `RemoveStackAsync` muss sie **ausschließen**.
   Umsetzung als **generisches Survival-Primitiv** (Label `rsgo.scope: edge` bzw. `rsgo.redeploy: ignore`), das
   `RemoveStackAsync` respektiert.
5. **Routing-Eingaben (kein Health-Raten):** (a) RSGO-Deploy-Zustand `ProductDeployment.Status` (`Running` →
   proxy; `Redeploying`/`Deploying`/`Failed`/gestoppt → maintenance); (b) Maintenance-Flag entscheidet nur über
   die **Wortwahl** (geplante Wartung vs. „vorübergehend nicht erreichbar"). `/hc`, `/liveness` immer durchlassen.
6. **RSGO ist einziger Schreiber der Edge-Config.** Es hält pro Edge ein kleines Dokument
   (`mode: proxy|maintenance`, `upstream`, `tls`, `branding-ref`, `statusJson`) und pusht via Caddy-Admin-API.
7. **TLS:** Edge **terminiert** TLS (`SslMode=Termination`). RSGOs vorhandene TLS-Mechanik
   (`TlsConfig`/`TlsConfigService`/`TlsService`, Modi SelfSigned/Custom/LetsEncrypt, Cert-Store, ACME/Renewal)
   wird von „RSGOs eigenem Endpoint" auf **per-Produkt-Edge verallgemeinert**. RSGO verwaltet die Certs und
   **injiziert Cert+Key als Dateien** in den Edge-Container (Volume-Mount); Caddy nutzt `tls <cert> <key>`. **Keine
   zweite ACME-Instanz in Caddy.** Den *einen* RSGO-Cert direkt nur im Single-Host-Sonderfall.
8. **Optionaler Host-Level-SNI-Router** (Phase 4): reines **L4-SNI-Passthrough** (`SslMode=Passthrough`),
   terminiert nicht; ein Public-:443 für viele Produkt-Hostnamen. Default aus.
9. **Branding (3-stufig, Edge löst in dieser Reihenfolge auf):** Default (RSGO-Standardseite, themebar via Vars) →
   Manifest-Asset-Bundle (HTML/CSS/Logo) → produkt-beigesteuerter **survivor-scoped** Container
   (`rsgo.role: maintenance-page`).
10. **Maschinenlesbarer Status** wird **immer** im selben stabilen Format ausgeliefert, unabhängig von der
    visuellen Stufe (Client/Launcher-Vertrag hängt nicht am Branding).
11. **Backward-compatible & dormant:** Alles per Manifest opt-in; ohne `edge:`-Block ändert sich **nichts** am
    heutigen Verhalten. Keine Breaking-Changes an bestehenden Manifesten/Deploy-Pfaden.

---

## 3. Verifizierter Ist-Stand (Filemap) + Phase 0

> **Phase 0 — zuerst ausführen:** Lies die folgenden Dateien und bestätige die Annahmen am echten Code, bevor du
> Code schreibst. Korrigiere die Task-Details, falls Signaturen abweichen. (Die Pfade sind aus einer früheren
> Exploration; verifiziere sie.)

**Deploy-Zustand & Orchestrierung:**
- `src/ReadyStackGo.Domain/Deployment/ProductDeployments/ProductDeployment.cs` — Status-State-Machine
  (`Deploying, Running, PartiallyRunning, Upgrading, Redeploying, Failed, Removed, Superseded`). **Finde, wo
  Status-Übergänge passieren/persistiert werden** — dort hängt der Edge-Reconciler an (Event/Hook/Polling).
- `src/ReadyStackGo.Application/UseCases/Deployments/DeployProduct/DeployProductHandler.cs`
- `src/ReadyStackGo.Application/UseCases/Deployments/RedeployProduct/RedeployProductHandler.cs`
- `src/ReadyStackGo.Infrastructure/Services/Deployment/DeploymentEngine.cs` — `RemoveStackAsync`
  (löscht alles mit `rsgo.stack == <stackVersion>`), `StartContainerAsync`, `ManagementNetwork` (`rsgo-net`).

**Container-/Netz-Primitive:**
- `src/ReadyStackGo.Infrastructure.Docker/DockerService.cs` — `ListContainersAsync`, `RemoveContainerAsync`,
  Container-Create, `EnsureNetworkAsync` (externe Netze: einmal erzeugt, nie gelöscht, teilbar),
  `StopStackContainersAsync`/`StartStackContainersAsync` (respektieren `rsgo.maintenance: ignore`).
- `src/ReadyStackGo.Infrastructure.Docker/SelfUpdateService.cs` — **Referenz-Pattern** für „RSGO erzeugt/startet
  einen Container und überwacht ihn" (CreateContainerParameters, Netze anhängen, Monitoring). Als Vorlage nutzen.

**Maintenance (bereits vorhanden — nur Eingabe):**
- `src/ReadyStackGo.Application/UseCases/Deployments/ChangeProductOperationMode/ChangeProductOperationModeHandler.cs`
- `src/ReadyStackGo.Infrastructure/Services/Health/SqlExtendedPropertySetter.cs`,
  `.../WebhookSetter.cs`, `src/ReadyStackGo.Domain/Deployment/Observers/IMaintenanceSetter.cs`,
  Observer (pollt `ams-MaintenanceMode` alle 30 s). **Finde den Observer-Service** — der Edge-Reconciler nutzt
  dieselbe Flag-Quelle.
- `src/ReadyStackGo.Infrastructure.DataAccess/Migrations/*AddMaintenanceSetterConfig*.cs` — Migrationsmuster.

**TLS (vorhanden — verallgemeinern):**
- `src/ReadyStackGo.Infrastructure/Configuration/TlsConfig.cs` — `TlsConfig`, `TlsMode`
  (`SelfSigned/Custom/LetsEncrypt`), `ReverseProxyConfig` mit `SslMode` (`Termination/Passthrough/ReEncryption`).
- `src/ReadyStackGo.Infrastructure/Tls/TlsService.cs`, `src/ReadyStackGo.Infrastructure/Services/TlsConfigService.cs`
  (`UploadPfxCertificateAsync`, `UploadPemCertificateAsync`, Cert-Store `/app/config/tls/`).
- `src/ReadyStackGo.Api/Endpoints/System/*Tls*`, `src/ReadyStackGo.Api/Program.cs`
  (`ConfigureReverseProxyAsync`, `BootstrapTlsCertificateAsync`).

**Manifest-Modell (erweitern):**
- `src/ReadyStackGo.Domain/StackManagement/Manifests/RsgoService.cs`,
  `.../RsgoProductMetadata.cs` — **keine** TLS/Hostname/Ingress-Felder heute → hier kommen die neuen Felder rein.
- `docs/Reference/Manifest-Schema.md`, `docs/Configuration/Manifest-Specification.md` — Schema-Doku mitziehen.

**Labels heute:** `rsgo.stack`, `rsgo.maintenance: ignore` (nur Stop/Start-Ausnahme, **nicht** Redeploy).

---

## 4. Manifest-Erweiterung (neu, optional, abwärtskompatibel)

Neuer optionaler **Produkt-Block** `edge:` (im Produkt-Manifest, nicht pro Service). Beispiel-Schema (final in
Code + `docs/Reference/Manifest-Schema.md` dokumentieren):

```yaml
edge:
  enabled: true                     # default false → Feature komplett inert
  publicHostname: project.kunde.tld # Hostname, den der Edge bedient (Cert-CN)
  publicPort: 443
  upstream:
    service: web-bff                # interner Service-Name (BFF) im Produkt-Netz
    port: 8080
  network: ams-project-edge-net     # gemeinsames externes Netz Edge<->Upstream (external:true)
  tls:
    mode: reuse | selfsigned | custom | letsencrypt   # 'reuse' = RSGO-Cert (Single-Host); sonst per-Produkt
    certRef: <name>                 # bei custom: Verweis auf hochgeladenen Cert
    letsencrypt: { email: ..., dnsChallenge: ... }
  maintenancePage:
    mode: default | bundle | container
    bundlePath: ./maintenance/      # bei bundle: Asset-Verzeichnis im Manifest-Repo
    container:                      # bei container: produkt-beigesteuerter survivor-Container
      service: maintenance-web      # Service mit Label rsgo.role: maintenance-page
    branding:                       # bei default: themebare Variablen
      productName: "ams.project"
      logoUrl: ...
      supportContact: ...
      locales: [de, en]
```

Neue Labels:
- `rsgo.scope: edge` (bzw. `rsgo.redeploy: ignore`) → **Survival-Primitiv**, von `RemoveStackAsync` ausgeschlossen.
- `rsgo.role: maintenance-page` → produkt-beigesteuerter, survivor-scoped Wartungs-Container.

---

## 5. Phasen

> Jede Phase: eigenständig mergebar, per Default inert, mit Tests + Akzeptanzkriterien. Reihenfolge einhalten.

### Phase 1 — Managed Edge-Container-Lifecycle + Survival + State-driven Routing (MVP, schließt die Lücke)

**Ziel:** Pro Produkt mit `edge.enabled: true` betreibt RSGO einen Caddy-Edge, der den Redeploy überlebt und je
nach Deploy-Zustand/Flag durchproxt oder eine (Default-)Wartungsseite + Status-JSON liefert.

**Tasks:**
1. **Manifest-Parsing:** `edge:`-Block + neue Labels in `RsgoProductMetadata`/Domain-Modell + Schema-Doku.
2. **Survival-Primitiv:** `RemoveStackAsync` (und alle Teardown-Pfade) so anpassen, dass Container mit
   `rsgo.scope: edge`/`rsgo.redeploy: ignore` **nicht** entfernt werden. Generisch + getestet.
3. **Edge-Provisionierung:** Beim Produkt-Deploy (wenn `edge.enabled`) einen Caddy-Container erzeugen/starten
   (Vorlage `SelfUpdateService` für CreateContainerParameters), an das **gemeinsame externe Netz** + `rsgo-net`
   hängen, mit `rsgo.scope: edge` labeln, Caddy-Admin-API erreichbar machen. Idempotent (existiert er, reuse).
4. **EdgeConfigReconciler:** Service, der auf `ProductDeployment.Status`-Übergänge **und** die Maintenance-Flag-
   Quelle (Observer) reagiert, den Soll-Zustand berechnet (`proxy` vs `maintenance`) und via
   `POST /load` an die Caddy-Admin-API pusht. Caddy-Config-Template: in `proxy` → `reverse_proxy <upstream>`; in
   `maintenance` → 503 + statische Default-Seite + `GET /__status` (JSON). `/hc`,`/liveness` immer durchlassen.
5. **Status-JSON-Format festlegen** (stabil, versioniert) — siehe Phase 5; in Phase 1 schon Default ausliefern.

**Akzeptanz:** Produkt mit `edge.enabled` deployen → Edge läuft. Produkt redeployen → Edge bleibt **stehen** und
serviert durchgehend die Wartungsseite (kein Connection-Refused); nach `Running` proxt er wieder. Maintenance-Flag
setzen → Wortwahl wechselt auf „geplante Wartung". Produkt **ohne** `edge`-Block → unverändertes Verhalten.

### Phase 2 — TLS-Terminierung am Edge (TLS-Mechanik verallgemeinern)

**Tasks:**
1. TLS-Modell von „RSGOs eigenem Endpoint" auf **per-Produkt-Edge** verallgemeinern: per-Produkt Cert
   verwalten/erzeugen (`selfsigned`/`custom`/`letsencrypt`), im Cert-Store ablegen.
2. Cert+Key als Dateien in den Edge-Container **injizieren** (Volume-Mount); Caddy-Config `tls <cert> <key>`.
3. `tls.mode: reuse` → RSGO-Cert verwenden (nur wenn Edge-Hostname == RSGO-Hostname).
4. API/Endpoint zum Cert-Upload pro Produkt (analog `/api/system/tls`, aber produkt-scoped) — falls nötig.

**Akzeptanz:** Edge terminiert HTTPS auf `publicHostname:publicPort` mit gültigem Cert; Renewal greift; Reload des
Certs ohne Edge-Neustart (Caddy-Admin-API).

### Phase 3 — Branding-Vertrag (Default → Bundle → Custom-Container)

**Tasks:**
1. `maintenancePage.mode: bundle` → Asset-Verzeichnis in den Edge mounten/servieren.
2. `maintenancePage.mode: container` → produkt-beigesteuerten Container mit `rsgo.role: maintenance-page` als
   **survivor-scoped** deployen (gleiches Survival-Primitiv wie Edge); Edge routet bei `maintenance` dorthin.
3. Default-Branding über Variablen (productName/logo/contact/i18n).
4. **Resolutionsreihenfolge** (container → bundle → default) implementieren + dokumentieren.

**Akzeptanz:** Alle drei Modi funktionieren; der Custom-Container überlebt den Produkt-Redeploy; Status-JSON bleibt
in allen Modi identisch.

### Phase 4 — Optionaler Host-Level-SNI-Passthrough-Router (Default aus)

**Tasks:** Optionaler geteilter L4-Router (`SslMode=Passthrough`), der nach SNI-Hostname an den richtigen
Produkt-Edge weiterleitet, ohne zu terminieren. Konfigurierbar; Default aus.

**Akzeptanz:** Ein Public-:443 bedient mehrere Produkt-Hostnamen; jeder Edge behält seinen eigenen Cert; ohne
Aktivierung unverändert.

### Phase 5 — Maschinenlesbarer Status-Vertrag (stabil, versioniert)

**Tasks:** Edge liefert unter einem stabilen Pfad (z. B. `GET /__status`) ein versioniertes JSON, in **allen**
Branding-Modi identisch. Felder (Vorschlag, final festzurren):
```json
{ "schema": 1, "state": "running|maintenance|deploying",
  "reason": "<optional>", "until": "<iso8601|null>", "productVersion": "<optional>" }
```
`reason`/`until` aus `ams-MaintenanceReason`/`ams-MaintenanceAnnouncedUntil` (falls erreichbar), sonst null.

**Akzeptanz:** Der Status ist von einem Client/Launcher robust parsebar, ändert sich nicht zwischen den
Branding-Stufen, und unterscheidet „geplante Wartung" (Flag) von „Redeploy/temporär weg" (nur Deploy-Zustand).

---

### Phase 6 — Follow-ups & Hardening (offen)

> Erfasst **nach** der Umsetzung von Phasen 1–5 (Branch `feature/maintenance-edge-proxy`). Enthält teils
> zurückgestellte Tasks aus früheren Phasen, teils neu erkannte Lücken. Reihenfolge nach Produktrelevanz.

**As-Built-Hinweise zu Phasen 1–5** (Umsetzung weicht im *nicht-bindenden* Detail vom Wortlaut ab, erfüllt
aber die bindenden Constraints — hier festgehalten, damit der Plan den Ist-Stand widerspiegelt):

- **TLS-Cert-Injektion (§2.7):** inline `load_pem` über die **Caddy-Admin-API** statt Datei-/Volume-Mount +
  Caddyfile `tls <cert> <key>`. Grund: `/app/config` ist ein **compose-präfixiertes Named Volume**
  (Host-Pfad-/Volume-Namen-Introspektion fragil); inline erlaubt zudem den geforderten **Cert-Reload ohne
  Edge-Neustart**. Bindend erfüllt: Edge terminiert, RSGO verwaltet den Cert, **keine** zweite ACME-Instanz,
  verbindungserhaltender Reload.
- **Edge-Image (§2.2):** Default **Tag-Pin** `caddy:2.8.4`, Digest über `edge.image` überschreibbar (Digest
  offline nicht verifizierbar).
- **SNI-Router (§2.8):** braucht das Caddy-`layer4`-Modul (nicht im offiziellen Image) → Image über
  `Edge:SniRouter:Image`; Control-Plane unit-getestet, Live-L4-Test umgebungsabhängig ausgelassen.
- **Sonstige Plan-0-Korrekturen:** `edge:`-Block an `RsgoManifest` (Geschwister von `maintenance:`);
  Reconciler = Polling-`BackgroundService` (§1.4 erlaubt „Event/Hook/Polling").

**Tasks:**

1. **Edge-Teardown bei Produkt-Entfernung** (neu — in §1–5 nicht adressiert): Beim **vollständigen Remove**
   eines Produkts den zugehörigen Edge (und survivor-scoped Maintenance-Container) deterministisch entfernen.
   Das Survival-Primitiv gilt bewusst nur für **Redeploy**, nicht für **Remove** — sonst verwaiste Container.
   **Akzeptanz:** Remove entfernt den Edge; Redeploy lässt ihn stehen.
2. **SNI-Router ↔ Edge-Port-Koordination** (Detail zu Phase 4): Bei aktivem Router ist der Router der alleinige
   öffentliche `:443`-Eingang → Edges binden ihren `publicPort` dann **nicht** an den Host (nur intern, vom
   Router per SNI erreicht). Ohne Router unverändert. **Akzeptanz:** kein Host-Port-Konflikt bei aktivem Router.
3. **Bundle = Asset-Verzeichnis** (Vervollständigung Phase 3, Task 1): statt nur inline-`index.html` ein
   vollständiges Asset-Verzeichnis (CSS/Logo/…) ausliefern (Caddy `file_server` bzw. gleichwertig). **Akzeptanz:**
   mehrteilige Bundles laden vollständig.
4. **Per-Produkt-Cert-Upload-Endpoint** (Phase 2, Task 4 „falls nötig"): produkt-scoped Cert-Upload analog
   `/api/system/tls`. **Akzeptanz:** `custom`-Cert per API hochladbar und vom Edge genutzt.
5. **`until` aus Maintenance-Quelle** (Vervollständigung Phase 5): generischer (nicht ams-spezifischer)
   Mechanismus, um eine angekündigte „until"-Zeit in den Status zu heben, ohne §7 zu verletzen (Observer bleibt
   reine Eingabe). Bis dahin `until: null`. **Akzeptanz:** `until` wird gesetzt, wenn die Quelle es liefert.
6. **Per-Produkt-Let's-Encrypt-Ausstellung** (Vervollständigung Phase 2 / §2.7): `tls.mode: letsencrypt`
   **wiederverwendet** derzeit nur den RSGO-Cert (bzw. selfsigned-Fallback) — also den von §2.7 genannten
   **Single-Host-Sonderfall**. Offen ist die in §2.7 geforderte **Verallgemeinerung**: RSGO stellt über **seine
   eigene ACME-Mechanik** (nicht in Caddy!) einen per-Produkt-LE-Cert für `publicHostname` aus, legt ihn im
   Cert-Store ab und injiziert/erneuert ihn am Edge. **Akzeptanz:** ein Produkt mit eigenem Public-Hostname
   erhält einen gültigen, öffentlich vertrauenswürdigen LE-Cert; Renewal greift.

**Weiterhin Non-Goal:** eine **zweite ACME-Instanz *in Caddy*** (der Edge führt selbst kein ACME aus). Das ist
**nicht** dasselbe wie Task 6 — dort läuft ACME weiterhin in **RSGO**, der Edge bekommt nur den fertigen Cert
injiziert.

---

## 6. Tests

- **Unit:** EdgeConfigReconciler (Zustands-→-Config-Mapping inkl. `/hc`-Durchlass, Wortwahl aus Flag),
  Manifest-Parsing des `edge:`-Blocks, Survival-Filter in `RemoveStackAsync`.
- **Integration (Docker):** Edge-Provisionierung idempotent; **Redeploy-Survival** (Edge + Custom-Container
  überleben `RemoveStackAsync`); Caddy-Admin-`POST /load` schaltet `proxy`↔`maintenance` ohne Verbindungsabriss;
  TLS-Terminierung mit selfsigned Cert; Branding-Resolution alle drei Modi.
- **Backward-Compat:** Bestehende Produkte ohne `edge:`-Block deployen/redeployen/entfernen sich **bit-identisch**
  wie heute (keine neuen Container, kein geändertes Teardown).

---

## 7. Constraints / Non-Goals

- **Kein** Blue/Green / Zero-Downtime-App-Update in diesem Plan (Edge ist später der Cutover-Punkt, aber nicht hier).
- **Kein** Eingriff in den Maintenance-Setter/Observer (nur als Eingabe konsumieren).
- **Kein** Proxy-Eigenbau in C# — Caddy ist die Data-Plane.
- **Keine** Breaking-Changes an Manifest/Deploy-Pfaden; alles opt-in + dormant.
- Generisch halten: nichts ams.project-Spezifisches im RSGO-Code; das konsumierende Produkt konfiguriert nur per Manifest.

## 8. Definition of Done

Ein beliebiges Produkt kann per `edge:`-Block einen überlebenden, TLS-terminierenden, brandbaren Maintenance-Edge
aktivieren; während Wartung **und** Redeploy serviert er durchgehend eine kontrollierte Seite + stabiles
Status-JSON; ohne den Block bleibt RSGO unverändert; alle Tests grün; `docs/Reference/Manifest-Schema.md` +
`docs/Configuration/Manifest-Specification.md` aktualisiert.

---

## Anhang — Konsumenten-Kontext (warum)

Die Gegenseite (ams.project) ist umgesetzt/geplant in SPEC-088 (product-maintenance-mode) und PLAN-113/114: das
Maintenance-Flag = SQL-Extended-Property `ams-MaintenanceMode` auf der externen Persistence-DB; Admin-Portal +
RSGO-Setter setzen es; der Desktop-Client/Launcher prüft den Edge-Status. Dieser RSGO-Plan liefert die
**Erlebnis-Ebene** (überlebender Front-Door) generisch. Die **Kontroll-Ebene** ist bereits über den RSGO-Setter
gelöst.

# Service-Optionen wie in Docker Compose

## Ziel

Wer ein RSGO-Manifest schreibt, wir oder andere, schreibt die Docker-Optionen eines Service so, wie er sie aus Docker
Compose kennt, und RSGO gibt sie vollständig an Docker weiter. Für die nächste Docker-Option muss RSGO nicht mehr
geändert werden (Marcus, 18.11.2026). Und nichts geht mehr still verloren: Ein Key, den RSGO nicht kennt oder nicht
unterstützt, ist ein Fehler, der Key und Service nennt.

Anlass: Am 18.11.2026 liefen auf zwei Release-Management-Hosts die Platten voll, weil der `logging`-Block aus den alten
Compose-Dateien RSGO nie erreicht hat. #516 hat dafür einmalig `logging` nachgezogen. Jetzt braucht EventStore
`ulimits: nofile: 65536`, und RSGO würde auch das still verwerfen.

## Ausgangslage

Stand `main` am 18.11.2026, im Code nachgelesen:

- **Unbekannte Keys verschwinden still.** Beide YAML-Leser nutzen `IgnoreUnmatchedProperties()`
  (`src/ReadyStackGo.Infrastructure/Parsing/RsgoManifestParser.cs:36`,
  `src/ReadyStackGo.Infrastructure.Docker/DockerComposeParser.cs:30`). `RsgoService` kennt 17 Keys. Alles andere fällt
  weg: `ulimits`, `sysctls`, `cap_add` usw., aber auch Tippfehler und die Compose-Schreibweise bekannter Keys
  (`depends_on`, `container_name`, `healthcheck`). Dasselbe gilt auf jeder anderen Ebene des Manifests.
- **Sechs Keys werden gelesen, kommen aber nie im Container an:** `command`, `entrypoint`, `user`, `workingDir`,
  `restart` und Service-`labels`. `ServiceTemplate` trägt sie noch
  (`LocalDirectoryProductSourceProvider.cs:556`), `DeploymentStep` hat keine Felder dafür
  (`src/ReadyStackGo.Application/UseCases/Deployments/DeploymentDtos.cs:51`). `DeploymentEngine.cs:907` setzt `restart`
  fest auf `unless-stopped` (Init-Container: `no`), `DeploymentEngine.cs:914` setzt nur die festen `rsgo.*`-Labels.
  Folgen heute:
  - `rsgo.maintenance: ignore` an `eventstore`, `eventstore-db` und `cachedata` (SPE-Project,
    `stacks/ams.project/Infrastructure/infrastructure.yaml`) wirkt nicht. Die Container werden in der Wartung gestoppt.
  - `rsgo.redeploy: ignore` und `rsgo.role: maintenance-page` im Beispiel `stacks/examples/edge/edge-container.yml`
    wirken nicht (`src/ReadyStackGo.Application/Services/Edge/EdgeConstants.cs:24`, `:30`).
  - `command: chown -R 10001:0 /data /log /secrets` im Init-Container `mssql-sandbox-init` (SPE-Project,
    `stacks/tooling/mssql-sandbox.yaml`) läuft nie. busybox startet seine Standard-`CMD`, endet mit 0, und das Init gilt
    als erfolgreich.
  - `command` als Liste scheitert schon beim Lesen, weil `RsgoService.Command` ein `string?` ist
    (`src/ReadyStackGo.Domain/StackManagement/Manifests/RsgoService.cs:72`).
- **Katalog-Produkte werden nicht validiert.** Lokale, Git- und OCI-Quellen lesen Manifeste nur
  (`LocalDirectoryProductSourceProvider.cs:184`, `:212`). `ValidateAsync` läuft nur beim Ad-hoc-Deploy
  (`DeploymentService.ParseComposeAsync` und `DeployComposeAsync`). Die `logging`-Prüfung aus #516 greift bei Produkten
  also nicht.
- **Der Precheck** (`IDeploymentPrecheckRule`, `PrecheckItem` mit `ServiceName`, `PrecheckResult.CanDeploy`) läuft vor
  dem Deploy in der Oberfläche (`DeployProduct.tsx`, `DeployStack.tsx`, die Precheck-Seiten) und im Hook-Deploy
  (`DeployViaHookCommand.cs:216`). Vor Upgrade, Redeploy und Retry läuft er nicht.
- **`DockerComposeParser` und `DockerComposeToRsgoConverter` ruft kein Produktionspfad auf**, nur
  `DockerComposeParserTests`. Der Deploy aus YAML in der Oberfläche (`/api/environments/{id}/deployments`) liest
  RSGO-Manifeste.
- **Scan der bestehenden Manifeste** (536 Dateien in SPE-Project `stacks/`, RSGO `stacks/` und
  `rsgo-ams-project-preview`, 18.11.2026): kein unbekannter Key. Von den heute wirkungslosen Keys sind gesetzt:
  `restart: unless-stopped` (1467-mal, entspricht dem heutigen Standard), `command: redis-server` (30-mal, entspricht
  der Standard-`CMD` von `redis:alpine`), der `chown` oben, und als Labels `rsgo.maintenance: ignore` (90-mal),
  `rsgo.redeploy: ignore` und `rsgo.role: maintenance-page` (je einmal).

## Was dazugehört

- **Service-Optionen nach der Compose Specification.** RSGO nimmt die Keys der Tabelle unter „Vorgaben“ an und bildet
  sie einmal vollständig auf die Docker Engine API ab (Docker.DotNet 3.125.15, `CreateContainerParameters`, `Config`,
  `HostConfig`). Die Werte schreiben sich wie in Compose, in Kurz- und Langform.
- **Eine Abbildung für alle Wege.** Natives RSGO-Manifest, Produkte aus allen Quellen, der Deploy aus YAML und der
  interne Compose-Parser samt Konverter nutzen dieselbe Abbildung. Der Konverter übernimmt die Keys ins RSGO-Manifest.
- **Die sechs wirkungslosen Keys werden wirksam:** `command`, `entrypoint`, `user`, `workingDir`, `restart` und
  Service-`labels`, mit den Vorrangregeln unter „Vorgaben“ (Marcus, 18.11.2026).
- **Unbekannte Keys sind Fehler**, auf jeder Ebene des Manifests: oberste Ebene, `metadata`, Variablen, Stacks,
  Services, `healthCheck`, `logging`, Volumes, Netzwerke, `maintenance` und `edge` (Marcus, 18.11.2026).
- **Nicht unterstützte Compose-Keys sind Fehler** mit einer kurzen Begründung (Liste unter „Vorgaben“).
- **Ungültige Werte sind Fehler**, wie bei `logging` seit #516.
- **Sichtbar und blockierend auf jedem Weg.** Die Meldungen erscheinen bei der Manifest-Validierung, im Precheck (neue
  Regel, ein Eintrag je Fehler mit Service), in der Antwort der Hooks und beim Quellen-Sync. Kein Weg legt Container aus
  einem Manifest mit Fehler an: Deploy, Upgrade, Redeploy, Retry und Hooks brechen ab, **bevor** ein Container entfernt
  oder angelegt wird.
- **Dokumentation** wie bei #516: `docs/Reference/Manifest-Schema.md` und die Website
  `src/ReadyStackGo.PublicWeb/src/content/docs/{de,en}/reference/manifest-format.md`.

## Was ausdrücklich nicht dazugehört

- **Ein roher Docker-API-Block als Notausgang** (etwa `x-docker: hostConfig: …`). Entschieden: nein (Marcus,
  18.11.2026). Docker.DotNet 3.125 kann nur Felder senden, die sein Modell kennt, und die deckt die Compose-Abbildung
  praktisch ab. Ein Passthrough wäre eine zweite, ungeprüfte Schreibweise.
- **Die Langform von `ports`, `volumes` und `networks`** (Mappings mit `target`, `aliases` usw.). Sie bleibt ein Fehler
  mit dem Hinweis auf die Kurzform, statt wie heute an einer Ausnahme beim Lesen zu scheitern.
- **Eine Markierung im Katalog** für Produkte mit Fehlern im Manifest. Das wäre eine neue Oberfläche mit Entwurf, also
  ein eigenes Vorhaben.
- **Der Edge-Container.** `EdgeProvisioner` setzt seine Labels und Sysctls weiter selbst.
- **Ein Compose-Import in der Oberfläche.** Den gibt es nicht. Parser und Konverter bleiben intern.

## Vorgaben

### Schreibweise der Keys

- **camelCase bleibt die kanonische Schreibweise**, wie bei `containerName` und `healthCheck`. Die Compose-Schreibweise
  wird zusätzlich angenommen: `cap_add` für `capAdd`, `stop_grace_period` für `stopGracePeriod`, auch für die
  bestehenden Keys (`container_name`, `depends_on`, `working_dir`, `healthcheck` mit `start_period`). So lassen sich
  Compose-Blöcke direkt übernehmen (Vorschlag vom 18.11.2026).
- Stehen beide Schreibweisen desselben Keys in einem Block, ist das ein Fehler.
- `x-*`-Keys sind wie in Compose auf jeder Ebene erlaubt und werden nicht gemeldet.
- Frei belegbare Maps werden nicht auf unbekannte Keys geprüft: `environment`, `labels`, `sysctls`, `ulimits` (Namen),
  `storageOpt`, `logging.options`, `driverOpts`, `headers` eines Observers.

### Abgebildete Keys

Wertformen wie in der Compose Specification. **Bytewerte**: Zahl (Bytes) oder Zahl mit `b`, `k`/`kb`, `m`/`mb`,
`g`/`gb`, z. B. `512m`, `1gb`. **Dauern**: `us`, `ms`, `s`, `m`, `h`, auch zusammengesetzt wie `1m30s`.

| Key (RSGO) | Compose | Docker Engine API | Wertform |
|------------|---------|-------------------|----------|
| `ulimits` | `ulimits` | `HostConfig.Ulimits` | `nofile: 65536` (soft = hard) oder `nofile: { soft: 20000, hard: 40000 }` |
| `sysctls` | `sysctls` | `HostConfig.Sysctls` | Map oder Liste `name=wert` |
| `capAdd`, `capDrop` | `cap_add`, `cap_drop` | `HostConfig.CapAdd`, `CapDrop` | Liste |
| `privileged` | `privileged` | `HostConfig.Privileged` | bool |
| `securityOpt` | `security_opt` | `HostConfig.SecurityOpt` | Liste |
| `readOnly` | `read_only` | `HostConfig.ReadonlyRootfs` | bool |
| `usernsMode` | `userns_mode` | `HostConfig.UsernsMode` | String |
| `groupAdd` | `group_add` | `HostConfig.GroupAdd` | Liste |
| `init` | `init` | `HostConfig.Init` | bool |
| `ipc`, `pid`, `uts` | `ipc`, `pid`, `uts` | `HostConfig.IpcMode`, `PidMode`, `UTSMode` | String |
| `cgroup` | `cgroup` | `HostConfig.CgroupnsMode` | `host` oder `private` |
| `cgroupParent` | `cgroup_parent` | `HostConfig.CgroupParent` | String |
| `runtime` | `runtime` | `HostConfig.Runtime` | String |
| `isolation` | `isolation` | `HostConfig.Isolation` | String |
| `shmSize` | `shm_size` | `HostConfig.ShmSize` | Bytewert |
| `memLimit` | `mem_limit` | `HostConfig.Memory` | Bytewert |
| `memReservation` | `mem_reservation` | `HostConfig.MemoryReservation` | Bytewert |
| `memswapLimit` | `memswap_limit` | `HostConfig.MemorySwap` | Bytewert, `-1` = unbegrenzt |
| `memSwappiness` | `mem_swappiness` | `HostConfig.MemorySwappiness` | 0 bis 100 |
| `oomKillDisable` | `oom_kill_disable` | `HostConfig.OomKillDisable` | bool |
| `oomScoreAdj` | `oom_score_adj` | `HostConfig.OomScoreAdj` | -1000 bis 1000 |
| `pidsLimit` | `pids_limit` | `HostConfig.PidsLimit` | Ganzzahl, `-1` = unbegrenzt |
| `cpus` | `cpus` | `HostConfig.NanoCPUs` | Zahl größer 0, z. B. `0.5` |
| `cpuShares` | `cpu_shares` | `HostConfig.CPUShares` | Ganzzahl |
| `cpuPeriod`, `cpuQuota` | `cpu_period`, `cpu_quota` | `HostConfig.CPUPeriod`, `CPUQuota` | Ganzzahl (µs) oder Dauer |
| `cpuRtPeriod`, `cpuRtRuntime` | `cpu_rt_period`, `cpu_rt_runtime` | `HostConfig.CPURealtimePeriod`, `CPURealtimeRuntime` | Ganzzahl (µs) oder Dauer |
| `cpuset` | `cpuset` | `HostConfig.CpusetCpus` | z. B. `0-3`, `0,1` |
| `cpuCount`, `cpuPercent` | `cpu_count`, `cpu_percent` | `HostConfig.CPUCount`, `CPUPercent` | Ganzzahl (Windows) |
| `blkioConfig` | `blkio_config` | `HostConfig.BlkioWeight`, `BlkioWeightDevice`, `BlkioDevice{Read,Write}{Bps,IOps}` | wie Compose |
| `deploy.resources` | `deploy.resources` | `limits.cpus` → `NanoCPUs`, `limits.memory` → `Memory`, `limits.pids` → `PidsLimit`, `reservations.memory` → `MemoryReservation`, `reservations.devices` → `DeviceRequests` | wie Compose |
| `devices` | `devices` | `HostConfig.Devices` | `HOST:CONTAINER[:rwm]` |
| `deviceCgroupRules` | `device_cgroup_rules` | `HostConfig.DeviceCgroupRules` | Liste |
| `gpus` | `gpus` | `HostConfig.DeviceRequests` | `all` oder Liste wie Compose |
| `tmpfs` | `tmpfs` | `HostConfig.Tmpfs` | String oder Liste `pfad[:optionen]` |
| `storageOpt` | `storage_opt` | `HostConfig.StorageOpt` | Map |
| `extraHosts` | `extra_hosts` | `HostConfig.ExtraHosts` | Liste `host:ip` oder `host=ip`, oder Map; IPv6 wie Compose |
| `dns` | `dns` | `HostConfig.DNS` | String oder Liste |
| `dnsSearch` | `dns_search` | `HostConfig.DNSSearch` | String oder Liste |
| `dnsOpt` | `dns_opt` | `HostConfig.DNSOptions` | Liste |
| `hostname`, `domainname` | `hostname`, `domainname` | `Config.Hostname`, `Domainname` | String |
| `macAddress` | `mac_address` | `Config.MacAddress` | String |
| `expose` | `expose` | `Config.ExposedPorts` | Liste wie `80`, `8000-8010/udp` |
| `command` | `command` | `Config.Cmd` | String oder Liste |
| `entrypoint` | `entrypoint` | `Config.Entrypoint` | String oder Liste |
| `user` | `user` | `Config.User` | String |
| `workingDir` | `working_dir` | `Config.WorkingDir` | String |
| `stopSignal` | `stop_signal` | `Config.StopSignal` | z. B. `SIGTERM` |
| `stopGracePeriod` | `stop_grace_period` | `Config.StopTimeout` | Dauer |
| `tty` | `tty` | `Config.Tty` | bool |
| `stdinOpen` | `stdin_open` | `Config.OpenStdin` | bool |
| `restart` | `restart` | `HostConfig.RestartPolicy` | `no`, `always`, `on-failure[:n]`, `unless-stopped` |
| `labels` | `labels` | `Config.Labels` | Map oder Liste `KEY=VALUE` |
| `environment` | `environment` | `Config.Env` | Map oder Liste `KEY=VALUE` |

Weitere Regeln zu den Werten:

- **`command` und `entrypoint` als String** werden wie in Compose ohne Shell in Wörter zerlegt, Anführungszeichen
  zählen. Eine Liste geht unverändert an Docker.
- **`${VAR}`** darf in jedem Wert stehen und wird eingesetzt wie heute in `environment`, `ports` und `volumes`. Ein
  Wert mit `${VAR}` wird nach dem Einsetzen geprüft, spätestens im Precheck und vor dem Anlegen des Containers.
- **`deploy.resources` und ein direkter Key** für dasselbe Feld (etwa `limits.memory` und `memLimit`) mit
  verschiedenen Werten sind ein Fehler.
- **Unbekannte Unterkeys** in strukturierten Werten (`ulimits.nofile.soft`, `blkioConfig`, `deploy.resources`,
  `healthCheck`) sind Fehler wie jeder andere unbekannte Key.

### Nicht unterstützt

Jeder dieser Keys ist ein Fehler, der Key, Service und Begründung nennt:

- `build`: RSGO deployt nur fertige Images.
- `network_mode`, `links`, `external_links`: RSGO verwaltet die Netze und hängt jeden Container an `rsgo-net`. Andere
  Services sind über ihren Namen als Netz-Alias erreichbar.
- `volumes_from`: RSGO benennt die Container selbst.
- `secrets`, `configs`, `env_file`, `label_file`: Dateien aus dem Projekt gibt es beim Deploy nicht. Dafür gibt es
  Variablen.
- `extends`, `profiles`, `scale`, `develop`, `attach`, `pull_policy`, `post_start`, `pre_stop`, `provider`, `models`,
  `use_api_socket`, `credential_spec`, `annotations`, `platform`, sowie unter `deploy` alles außer `resources.limits`
  (`cpus`, `memory`, `pids`) und `resources.reservations` (`memory`, `devices`).
- `healthCheck.disable` und `healthCheck.start_interval`: Docker.DotNet 3.125 kennt kein `StartInterval`.
- `devices` mit CDI-Namen (`vendor/class=name`).
- Langform von `ports`, `volumes` und `networks`.

### Vorrang zwischen Manifest und RSGO

- **RSGO verwaltet weiter:** Containername (mit Stack-Präfix), Image (mit Digest aus dem OCI-Lockfile), Ports, Volumes,
  Netzwerke samt `rsgo-net` und Netz-Alias, den Docker-Health-Check und das Standard-Log-Limit aus #516.
- **Kein stiller Vorrang.** Wo ein Manifest-Wert mit einem RSGO-Feld kollidiert, ist das ein Fehler, statt dass eine
  Seite still gewinnt.
- **Labels:** Manifest-Labels kommen auf den Container. RSGO setzt `rsgo.stack`, `rsgo.context`, `rsgo.lifecycle`,
  `rsgo.stack.name`, `rsgo.product`, `rsgo.scope` und `rsgo.edge.*` selbst. Setzt ein Manifest eines davon, ist das ein
  Fehler. Aus dem Manifest erlaubt sind `rsgo.maintenance`, `rsgo.redeploy` und `rsgo.role` mit ihren dokumentierten
  Werten. Jedes andere `rsgo.*`-Label ist ein Fehler, damit ein Tippfehler wie `rsgo.maintenace` auffällt.
- **`restart`:** Für Services gilt der Wert aus dem Manifest, ohne Angabe `unless-stopped` wie heute. Init-Container
  laufen immer mit `no`. Ein anderer Wert an einem Init-Container ist ein Fehler.
- **`hostname`:** ohne Angabe der Docker-Standard wie heute. Der Netz-Alias bleibt der Servicename.

### Meldungen

- Texte englisch (bis zur Lokalisierung vor 1.0, `CLAUDE.md`), mit dem Pfad im Manifest, zum Beispiel
  `Stack 'infrastructure' > Service 'eventstore': unknown key 'ulimit'`,
  `… Service 'api': key 'build' is not supported: RSGO deploys pre-built images only`,
  `… Service 'eventstore': invalid value 'abc' for 'ulimits.nofile': expected a number or soft/hard`.
- Im Precheck ist jeder Fehler ein eigener Eintrag mit Schweregrad `Error` und `ServiceName`. `CanDeploy` ist dann
  `false`.
- Der Quellen-Sync lädt ein Produkt mit Fehlern trotzdem, damit es nicht still aus dem Katalog verschwindet, und
  schreibt die Fehler ins Log und in das Sync-Ergebnis (siehe „Offene Fragen“).

### Tests

Nach `CLAUDE.md`, also nicht nur der Normalfall. Besonders: leere und fehlende Werte, `null`, Kurz- und Langform, beide
Schreibweisen, Grenzwerte (`oomScoreAdj` -1000 und 1001, `memSwappiness` 101, `cpus: 0`), `${VAR}` vor und nach dem
Einsetzen, reservierte Labels, Init-Container mit `restart`.

## Oberfläche und Bilder

Keine neue Oberfläche, kein Entwurf nötig. Die Meldungen erscheinen in den bestehenden Precheck-Ansichten
(`ProductPrecheckPage.tsx`, `StackPrecheckPage.tsx`, die Deploy-Seiten) als Fehler mit Servicenamen, außerdem in
API-Antworten und im Log.

## Abnahme

- **EventStore:** Ein Test führt einen Manifest-Text mit `ulimits: nofile: 65536` durch die ganze Kette (Parser,
  `ServiceTemplate`, `DeploymentStep`, `CreateContainerRequest`) bis zu den Create-Parametern und findet
  `HostConfig.Ulimits` mit `nofile`, Soft und Hard 65536. Dasselbe für die Langform mit `soft`/`hard`. Der Test kürzt
  keine Schicht ab, denn genau dazwischen gingen die Keys bisher verloren.
- **Jeder Key der Tabelle** hat einen Test vom Manifest bis zu den Create-Parametern, in Kurz- und Langform, und in
  beiden Schreibweisen.
- **Vollständigkeit:** Ein Test kennt alle Service-Keys der Compose Specification und wird rot, wenn einer davon weder
  abgebildet noch als nicht unterstützt geführt ist.
- **Unbekannter Key:** `ulimit` an einem Service, ein unbekannter Key in `healthCheck`, auf oberster Ebene, in
  `metadata` und in einer Variablen ergeben je einen Fehler mit Pfad. `x-foo` ergibt keinen.
- **Nicht unterstützt:** `build`, `network_mode`, `secrets` ergeben Fehler mit Begründung.
- **Ungültige Werte:** `ulimits: nofile: abc`, `memLimit: 10x`, `cpus: -1`, `stopGracePeriod: soon`,
  `restart: sometimes` ergeben Fehler, mit `${VAR}` erst nach dem Einsetzen.
- **Blockiert auf jedem Weg:** Der Precheck eines Produkts mit einem solchen Fehler zeigt ihn als `Error` mit Service,
  `CanDeploy` ist `false`. Der Hook-Deploy liefert ihn in der Antwort. Upgrade, Redeploy und Retry brechen ab, ohne
  einen Container entfernt zu haben.
- **Die sechs Keys wirken:** `command` (String und Liste), `entrypoint`, `user`, `workingDir`, `restart` und `labels`
  kommen in den Create-Parametern an. Ein Container mit `rsgo.maintenance: ignore` aus dem Manifest wird von
  `MaintenanceContainerFilter` ausgenommen. Ein reserviertes Label und `restart` an einem Init-Container ergeben Fehler.
- **Echter Docker:** Ein Integrationstest (wie `tests/ReadyStackGo.IntegrationTests/DockerServiceIntegrationTests.cs`)
  legt einen Container aus einem Manifest mit `ulimits: nofile: 65536` an und liest `HostConfig.Ulimits` per Inspect
  zurück.
- **Bestehende Manifeste:** Alle Manifeste unter `stacks/` in diesem Repo bestehen die neue Prüfung (Test). Vor dem
  Release laufen SPE-Project `stacks/` und `rsgo-ams-project-preview` ohne Fehler durch; das Ergebnis steht im
  Umsetzungs-PR.
- **Dokumentation:** `Manifest-Schema.md` und die Website (de/en) enthalten die Tabelle mit Compose-Namen und Wertform,
  das EventStore-Beispiel, die nicht unterstützten Keys mit Begründung und die Vorrangregeln.
- **Patchnotes** nennen die Verhaltensänderung: Keys, die bisher wirkungslos waren, wirken jetzt (etwa
  `rsgo.maintenance: ignore` an EventStore, `command` im Init-Container), und unbekannte Keys sind jetzt Fehler.

## Material

- #516 und sein Commit `26313ba2`: `logging` als Vorbild für Abbildung, Prüfung und Doku, mit den Tests
  `tests/ReadyStackGo.UnitTests/Docker/ContainerLogConfigTests.cs`, `Manifests/DockerComposeParserTests.cs` und
  `Manifests/RsgoManifestParserTests.cs`.
- Compose Specification, Abschnitt Services (`05-services.md` in `compose-spec/compose-spec`) und das JSON-Schema
  `compose-spec.json` für die vollständige Liste der Service-Keys.
- Docker.DotNet 3.125.15: `CreateContainerParameters`, `Config`, `HostConfig`, `Ulimit`, `DeviceMapping`,
  `DeviceRequest`, `HealthConfig` (ohne `StartInterval`).
- Heutiger Weg eines Service: `RsgoManifestParser` → `LocalDirectoryProductSourceProvider.ConvertToServiceTemplate`
  (`:556`) → `StackVariableResolver` → `DeploymentService.CreateDeploymentPlanFromStructuredData` (`:1121`) →
  `DeploymentEngine.StartContainerAsync` (`:914`) → `DockerService.CreateAndStartContainerAsync` (`:311`). Daneben
  `RsgoManifestParser.BuildDeploymentPlanAsync` (`:614`) für den Deploy aus YAML.
- Der Precheck: `src/ReadyStackGo.Application/UseCases/Deployments/Precheck/` und
  `src/ReadyStackGo.Infrastructure/Services/Deployment/Precheck/` mit den bestehenden Regeln als Vorbild.
- SPE-Project `stacks/ams.project/Infrastructure/infrastructure.yaml` (EventStore) und `stacks/tooling/mssql-sandbox.yaml`
  als echte Fälle.

## Offene Fragen

- **`logging` in Compose-Form.** Soll `logging` neben `maxSize`/`maxFile` auch `driver` und `options` annehmen?
  Vorschlag: ja, `driver` und `options` gehen unverändert an `HostConfig.LogConfig`. Für `json-file` und `local` füllt
  das Standard-Limit aus #516 fehlendes `max-size`/`max-file` auf. Beide Formen im selben Block sind ein Fehler.
- **`depends_on` in Langform** (`db: { condition: service_healthy }`). Vorschlag: annehmen. `service_started` entspricht
  dem heutigen Verhalten. `service_completed_successfully` nur, wenn der Ziel-Service `lifecycle: init` ist, denn das
  macht RSGO schon. `service_healthy` und `restart: true` sind Fehler, solange die Engine nicht nachweislich auf die
  Gesundheit wartet. Der Plan prüft das am Code.
- **Hinweis bei sicherheitsrelevanten Optionen** (`privileged`, `pid: host`, `capAdd: [SYS_ADMIN]`, `devices`).
  Vorschlag: keiner. Wer Manifeste deployen darf, darf schon heute Container mit Zugriff auf den Docker-Host anlegen,
  etwa über ein Volume auf `/var/run/docker.sock`.
- **Interner Compose-Parser und Konverter.** Sie laufen in keinem Produktionspfad. Vorschlag: auf die gemeinsame
  Abbildung umstellen, wie besprochen, statt sie zu entfernen. Das kostet wenig, weil die Abbildung geteilt ist.
- **Fehler beim Quellen-Sync.** Vorschlag: Das Produkt wird geladen, die Fehler hängen an seinem Stack, der Sync
  schreibt sie ins Log und in `SyncResult.Warnings`. Die Oberfläche zeigt Sync-Warnungen heute nicht an
  (`useStackSourceStore.ts`). Sichtbar und blockierend werden sie im Precheck.

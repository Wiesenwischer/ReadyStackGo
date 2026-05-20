<!-- GitHub Epic: #383 -->
# Phase: SNMP Completion (v0.65)

## Ziel

v0.64 hat SNMP als read-only-MVP mit v2c-Polling und einer read-only Settings-Section ausgeliefert. v0.65 macht das Feature komplett: vollständige SNMPv3-Responses, DB-Persistenz, editierbares UI, SNMP-Traps, MIB-CI-Validierung. Nach dem Merge dieser Phase erfüllt RSGO die ursprünglichen Planning-Entscheidungen aus PLAN-snmp-agent.md ohne Einschränkungen.

## Features

- [x] **Feature 1: Domain entities** — `SnmpSettings` (Singleton-Aggregate für Enable/Port/ListenAddress/RootOid/Community/TrapReceivers) und `SnmpV3User` (Aggregate für USM-User; Passphrase via `CredentialEncryptionService` verschlüsselt). Beide mit IRepository.

- [x] **Feature 2: EF migration** — `SnmpSettingsConfiguration` + `SnmpV3UserConfiguration` + Migration `AddSnmpSettingsAndV3Users`.

- [x] **Feature 3: Read settings + users from DB** — `SnmpAgent` liest aus `SnmpRuntimeSettingsProvider` über `IServiceScopeFactory`. Re-Read auf `SnmpSettingsChanged` Domain-Event ohne Container-Restart über `ISnmpAgentReloader`-Adapter.

- [ ] **Feature 4: Full SNMPv3 response construction** — *Wandert in eine Folgephase (eigene Session).* USM-Decoding der incoming v3 PDUs steht; Response-Construction (Engine-ID + Time-Window + PrivacyProvider re-encrypt) braucht die SnmpEngine-Framework-Integration und wird separat gezogen.

- [x] **Feature 5: CRUD endpoints** — `GET/PUT /api/snmp/settings`, `GET/POST/DELETE /api/snmp/v3-users[/{id}]`. Permission `Settings:Read` / `Settings:Manage`. Settings-Update löst `SnmpSettingsChanged` Domain-Event aus, Notification-Handler ruft Agent.ReloadAsync auf.

- [x] **Feature 6: Editable WebUI** — `/settings/snmp` wird read/write: Enable-Toggle, Port, Community, TrapReceivers Inputs; v3-User-Liste mit Add-Dialog (Name + Auth-Protokoll + Passphrase + Priv-Protokoll + Passphrase) und Delete-Action. OID-Reference-Tree und MIB-Download bleiben.

- [ ] **Feature 7: SNMP Traps** — *Wandert in eine Folgephase (eigene Session).* `TrapReceivers`-Feld ist im UI und in der DB vorbereitet; Trap-Emission über `ISnmpTrapEmitter` Application-Service kommt separat.

- [x] **Feature 8: smilint CI** — `.github/workflows/mib-lint.yml` installiert `smitools` und validiert `READYSTACKGO-MIB.txt` auf PRs + Pushes.

- [x] **Documentation update** — Public Website DE/EN: read-only-Hinweis raus, neuer Hinweis auf Editing in WebUI.

- [ ] **Phase close** — alle Tests grün, integration → main PR, Milestone v0.65 schließen. (After follow-up sessions for Features 4 + 7.)

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| v3-Response-Pfad | Manueller ResponseMessage-v3-ctor / SnmpEngine-Framework | **Manueller v3-ctor** | Wir haben die UDP-Listener-Schleife bereits selbst implementiert; den SnmpEngine-Aufsatz parallel betreiben erzeugt zwei Wege. Manueller ctor ist mehr Code, aber konsistent. |
| Passphrase-Storage | Plain in DB / mit CredentialEncryptionService verschlüsselt | **Encrypted** | RSGO verschlüsselt schon Docker-Registry-Credentials mit demselben Service — gleiches Muster. Reduziert Audit-Risiko. |
| Settings-Change-Propagation | Container-Restart erfordern / Hot-Reload via Event | **Hot-Reload via Domain-Event** | Container-Restart ist für SNMP-Settings übertrieben; passt auch nicht zur Aktivierung über Settings-UI. |
| Trap-Versions | v1-Traps / v2-Traps (Inform) | **v2-Traps (snmpV2-trap PDU)** | v1-Traps sind deprecated. v2-Inform ist optional, fügt Reliability hinzu, ist aber out-of-scope. |
| smilint im Build-Image | Eigener Lint-Job / im Build-Step | **Eigener Lint-Job** | Reduziert Build-Image-Größe; Lint-Failure blockt PR-Merge über Branch-Protection. |

# ReadyStackGo – Database Migration & Permissions Specification

## 1. Ziele & Überblick

Dieses Dokument beschreibt, wie **ReadyStackGo (RSGO)** mit Datenbank-Migrationen umgeht – insbesondere für **ams.project** – und wie dabei unterschiedliche Datenbank-Benutzer (Least Privilege) eingesetzt werden.

Ziele:

- Sauberes, reproduzierbares **Migrationsmodell** für Stacks (insb. ams.project).
- Klare Trennung zwischen:
  - **App-User** (eingeschränkte Rechte, Laufzeitbetrieb),
  - **Migration-User** (Schema-/Migrationsrechte),
  - optional **Provisioning-/Admin-User** (DB-/Login-Anlage).
- Orchestrierung durch RSGO:
  - Migrationen werden als **Jobs (Migrations-Container)** ausgeführt.
  - RSGO wertet Exit-Codes aus und aktualisiert Status/Health.
- Integration mit dem RSGO-Health- und Deployment-Modell:
  - z. B. `OperationMode = Migrating`, `MigrationStatus`.

Dieses Dokument ist als Implementierungsvorlage gedacht (z. B. für Claude), um die Migrationen und DB-Rechte in RSGO umzusetzen.

---

## 2. Rollenmodell in der Datenbank

Für jede fachliche Datenbank (z. B. `ams_project`, `ams_identity` usw.) werden drei Ebenen von Benutzern unterschieden:

### 2.1 App-User

**Zweck:** Laufzeitbenutzer für alle regulären Services/Container.

- Typische Berechtigungen:
  - `SELECT`, `INSERT`, `UPDATE`, `DELETE`
  - `EXEC` auf Stored Procedures/Funktionen
- **Keine** DDL-Rechte:
  - kein `CREATE TABLE`, `ALTER TABLE`, `DROP TABLE`, etc.
- Wird **nur** in den App-Containern verwendet (z. B. `ams-api`, `ams-bff`, Worker, …).

### 2.2 Migration-User

**Zweck:** Ausführung von Schema-/Daten-Migrationen.

- Darf:
  - Tabellen/Views/Procs/Funktionen anlegen/ändern/löschen,
  - Indizes anlegen/ändern/löschen,
  - ggf. DB-spezifische Migrations-Operationen.
- Idealerweise auf eine konkrete DB beschränkt (z. B. nur `ams_project`).
- Wird **nur** von Migrations-Containern verwendet.

### 2.3 Provisioning-/Admin-User (optional)

**Zweck:** Einmaliges initiales Setup / „Provisioning“.

- Darf:
  - Datenbanken anlegen
  - Logins/Users anlegen
  - Rechte vergeben
- Wird **nur** für:
  - erstes Setup,
  - seltene Spezialfälle eingesetzt.
- Sollte nicht dauerhaft aktiv bleiben; entweder:
  - nach Provisioning vom Kunden gesperrt,
  - oder nur als hochsensibles Secret in RSGO gespeichert.

---

## 3. Modellierung im RSGO-Manifest

Das RSGO-Manifest beschreibt, welche Parameter für einen Stack erforderlich sind und wie sie in Services/Migrationen verwendet werden.

### 3.1 Beispiel: ams.project Stack

#### 3.1.1 Parameter

```yaml
parameters:
  - key: PROJECT_DB_APP
    description: "App-ConnectionString zur ams.project Datenbank (nur Lese/Schreibrechte)"
    required: true
    secret: true

  - key: PROJECT_DB_MIGRATION
    description: "Migration-ConnectionString zur ams.project Datenbank (Schema-Änderungen)"
    required: false    # Im Expertenmodus Pflicht, sonst optional
    secret: true

  - key: PROJECT_DB_PROVISIONING
    description: "Optionaler Provisioning-ConnectionString (DB/User anlegen)"
    required: false
    secret: true

  - key: PROJECT_DB_APP_USERNAME
    description: "Login-/Benutzername des App-Users (wird ggf. durch Migration angelegt)"
    required: false
    secret: false
```

> Hinweis: Die genauen Namen (`PROJECT_DB_*`) sind Beispiel-Namen; sie sollten zur Naming-Convention der jeweiligen Domain/DB passen.

#### 3.1.2 Services

Die regulären Services nutzen nur den **App-ConnectionString**:

```yaml
services:
  - name: ams-api
    image: registry.example.com/ams.api:0.5.0
    env:
      - name: ConnectionStrings__Main
        valueFrom: param:PROJECT_DB_APP

  - name: ams-worker
    image: registry.example.com/ams.worker:0.5.0
    env:
      - name: ConnectionStrings__Main
        valueFrom: param:PROJECT_DB_APP
```

#### 3.1.3 Migrationen

Migrationen nutzen den **Migration-ConnectionString** (oder im Provisioning-Szenario den Provisioning-String):

```yaml
migrations:
  - name: ams-project-db-migration
    image: registry.example.com/ams.migrations:0.5.0
    runPolicy: before-services        # wird vor dem Start der Services ausgeführt
    env:
      - name: PROJECT_DB_MIGRATION
        valueFrom: param:PROJECT_DB_MIGRATION
      - name: PROJECT_DB_PROVISIONING
        valueFrom: param:PROJECT_DB_PROVISIONING
      - name: PROJECT_DB_APP_USERNAME
        valueFrom: param:PROJECT_DB_APP_USERNAME
      - name: RSGO_TARGET_VERSION
        valueFrom: system:StackTargetVersion
      - name: RSGO_ORG_ID
        valueFrom: system:OrgId
      - name: RSGO_ENV_ID
        valueFrom: system:EnvironmentId
```

---

## 4. Migrations-Container – Contract

Migrations werden als **one-shot Jobs** in dedizierten Containern ausgeführt.

### 4.1 Verhalten

- Startet, führt Migrationen aus, beendet sich.
- **Keine** langlaufenden Hintergrundprozesse.
- **Idempotent**:
  - Mehrfachausführung soll keine Schäden erzeugen.
  - Bereits angewendete Migrationen werden übersprungen (z. B. Flyway, EF-Migrations-History).

### 4.2 Eingangsparameter (Env Vars)

Der Container erhält über Env Vars alle notwendigen Informationen:

Minimal:

```text
PROJECT_DB_MIGRATION=...
RSGO_TARGET_VERSION=0.5.0
RSGO_ORG_ID=...
RSGO_ENV_ID=...
RSGO_MIGRATION_RUN_ID=...    # optional, GUID von RSGO
```

Provisioning-Modus (optional):

```text
PROJECT_DB_PROVISIONING=...        # höher privilegierter ConnectionString
PROJECT_DB_APP=...                 # optional, falls App-User damit getestet werden soll
PROJECT_DB_APP_USERNAME=ams_app    # Name, den der Migration-Job für App-User verwendet
APP_DB_PASSWORD=...                # Passwort für anzulegenden App-User (aus Secret)
```

### 4.3 Output / Ergebnis

- **Exit-Code**:
  - `0` → Migration erfolgreich
  - `!= 0` → Migration fehlgeschlagen
- Optional: Ergebnisschreiben in JSON-Datei (z. B. in Volume `/rsgo/migration-result.json`):

```json
{
  "status": "Succeeded",
  "fromVersion": "0.4.2",
  "toVersion": "0.5.0",
  "appliedMigrations": [
    "V0_5_0__Create_Table_X",
    "V0_5_0__Add_Index_Y"
  ],
  "durationSeconds": 37
}
```

RSGO kann:

- den Exit-Code **immer** auswerten (Pflicht),
- den JSON-Output **optional** lesen, um History/Details in der UI zu zeigen.

---

## 5. Provisioning-Modus: Migration legt App-User an

Optionaler „Komfortmodus“:  
Die Migration erstellt (bei genügend Rechten) den App-User direkt in der Datenbank.

### 5.1 Ablauf im Provisioning-Modus

1. Im RSGO-Wizard/Deployment für eine neue Installation wählt der Admin:

   - Modus:
     - „Benutzer durch Datenbank-Admin verwaltet“
     - „Benutzer durch ReadyStackGo/Migration anlegen lassen“ (**Provisioning-Modus**)
   - gibt an:
     - `PROJECT_DB_PROVISIONING` (hoch privilegierter ConnectionString)
     - `PROJECT_DB_APP_USERNAME` (z. B. `ams_app`)
     - ggf. App-Passwort (oder lässt es generieren)

2. RSGO startet den Migrationscontainer mit:
   - `PROJECT_DB_PROVISIONING` gesetzt
   - `PROJECT_DB_APP_USERNAME` gesetzt
   - ggf. `APP_DB_PASSWORD` per Env-Var

3. Migrationslogik:

   - Falls DB noch nicht existiert → anlegen.
   - Falls App-Login/User noch nicht existiert:
     - Login anlegen (z. B. `CREATE LOGIN [ams_app] WITH PASSWORD = @AppUserPassword;`).
     - User für die DB anlegen (`CREATE USER [ams_app] FOR LOGIN [ams_app];`).
     - Rollen zuweisen (z. B. `db_datareader`, `db_datawriter`, ggf. eigene Rolle für `EXEC`).
   - Schema-Migrationen ausführen.

4. Ergebnis:
   - DB existiert.
   - App-User existiert.
   - Migrations-Historie ist aktuell.

5. RSGO vermerkt in der Konfiguration:
   - `PROJECT_DB_APP` → ConnectionString mit App-User.
   - `PROJECT_DB_MIGRATION` → ConnectionString mit Migration-/Provisioning-User (je nach Setup).

### 5.2 Idempotenz

Das Provisioning-Skript muss so geschrieben sein, dass es gefahrlos erneut laufen kann.

Beispiel (SQL Server, Pseudocode):

```sql
-- Login + User nur anlegen, wenn sie noch nicht existieren
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @AppUserName)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @AppUserName)
    BEGIN
        DECLARE @CreateLoginSql nvarchar(max);
        SET @CreateLoginSql = N'CREATE LOGIN [' + @AppUserName + N'] WITH PASSWORD = @AppUserPassword;';
        EXEC sp_executesql @CreateLoginSql, N'@AppUserPassword nvarchar(256)', @AppUserPassword=@AppUserPassword;
    END;

    DECLARE @CreateUserSql nvarchar(max);
    SET @CreateUserSql = N'CREATE USER [' + @AppUserName + N'] FOR LOGIN [' + @AppUserName + N'];';
    EXEC(@CreateUserSql);

    EXEC sp_addrolemember N'db_datareader', @AppUserName;
    EXEC sp_addrolemember N'db_datawriter', @AppUserName;
    -- ggf. eigene Rolle für EXEC-Rechte
END;
```

Passwörter werden **nicht** hardcodiert, sondern als Env-Var an den Migrationscontainer übergeben.

---

## 6. Orchestrierung durch RSGO

### 6.1 Deployment-/Upgrade-Flow

Beim Ausrollen/Upgraden eines Stacks (z. B. `ams-project`):

1. **Status setzen** (in SQLite):

   - `DeploymentStatus = Upgrading`
   - `MigrationStatus = Running`
   - `OperationMode = Migrating`
   - ggf. `TargetVersion` setzen.

2. **Services stoppen** (falls `runPolicy` dies vorsieht):

   - betroffene Services des Stacks stoppen (z. B. Worker/APIs),
   - je nach Policy.

3. **Migrations-Container starten**:

   - über Docker API (`restartPolicy = "no"`):
     - Image: `ams.project.migrations:<TargetVersion>`
     - Env: DB-ConnectionStrings, RSGO-IDs etc.
   - Container-ID in RSGO speichern.

4. **Migrationslauf überwachen**:

   - regelmäßig Container-Status prüfen:
     - `running` → Migration läuft
     - `exited` → Migration beendet
   - Exit-Code auslesen.

5. **Ergebnis verarbeiten**:

   - Exit-Code `0`:
     - `MigrationStatus = Succeeded`
     - `CurrentVersion = TargetVersion`
     - `OperationMode = Normal` (bzw. Übergang „Services starten“)
   - Exit-Code `!= 0`:
     - `MigrationStatus = Failed`
     - `OperationMode = Failed`
     - `DeploymentStatus = Failed`
     - UI: Hinweis „Migration fehlgeschlagen – Logs prüfen“.

6. **Services starten** (bei Erfolg):

   - Services des Stacks mit APP-ConnectionStrings starten.
   - `DeploymentStatus = Idle`
   - Health-Checks neu bewerten.

### 6.2 Retry

- UI-Option: „Migration erneut ausführen“:
  - startet denselben Migrations-Container erneut (neue `RSGO_MIGRATION_RUN_ID`).
  - Idempotenz der Migrationslogik vorausgesetzt.

---

## 7. Integration mit Health & OperationMode

Das Migration-Feature integriert sich in das Health-/OperationMode-Modell von RSGO:

- Während Migration:
  - `OperationMode = Migrating`
  - Health-Engine setzt:
    - `overall = Degraded`
    - UI zeigt: „Migration läuft (von X auf Y)“
- Nach erfolgreicher Migration:
  - `OperationMode = Normal`
  - `overall` wird aus Bus/Infra/Self neu berechnet.
- Nach fehlgeschlagener Migration:
  - `OperationMode = Failed`
  - `MigrationStatus = Failed`
  - `overall = Unhealthy`
  - UI zeigt:
    - „Migration fehlgeschlagen – manuelle Intervention nötig.“

So wird klar zwischen **geplanten Einschränkungen** (Migration/Wartung) und **ungeplanten Ausfällen** unterschieden.

---

## 8. Sicherheit & RBAC

### 8.1 Sichtbarkeit der ConnectionStrings

Für DB-Parameter (insb. MIGRATION/PROVISIONING) gilt:

- Nur **SystemOwner** / **OrgAdmin**:
  - dürfen Werte für MIGRATION/PROVISIONING setzen/ändern,
  - dürfen überhaupt sehen, ob diese gesetzt sind (Anzeige nur als `*****`).
- **Operator**:
  - darf Stacks starten/stoppen,
  - darf Migrationen erneut anstoßen (falls erlaubt),
  - darf Status/Logs sehen,
  - darf **keine** ConnectionStrings einsehen oder ändern.

### 8.2 Speicherung von Credentials

- Alle DB-ConnectionStrings werden in SQLite als **Secrets** behandelt:
  - Verschlüsselung mit ASP.NET Data Protection oder separatem Key,
  - keine Klartextablage.
- Logs:
  - Migrationslogs und RSGO-Logs dürfen keine kompletten ConnectionStrings oder Passwörter enthalten.
  - Fehlertexte sollten „gesäubert“ werden (z. B. Offending ConnectionStrings maskieren).

### 8.3 .env-Import

Wenn `.env`-Dateien verwendet werden:

```env
PROJECT_DB_APP=...
PROJECT_DB_MIGRATION=...
PROJECT_DB_PROVISIONING=...
PROJECT_DB_APP_USERNAME=ams_app
```

- RSGO:
  - liest die Werte,
  - speichert sie verschlüsselt,
  - zeigt für Secrets nur Maskierung (`******`) an.

---

## 9. Modi für unterschiedliche Sicherheitsanforderungen

RSGO sollte zwei grobe Betriebsmodi pro Datenbank/Stack unterstützen:

### 9.1 Modus A – „Self-Managed by RSGO“ (Provisioning durch Migration)

- Für kleinere Kunden / einfache Setups.
- RSGO/Migrations-Container:
  - legen Datenbank (falls nötig) an,
  - legen App-User an,
  - weisen Rollen zu.
- Kunde muss nur einen Provisioning-/Admin-ConnectionString bereitstellen.
- Nach Abschluss wird empfohlen:
  - Provisioning-Rechte zurückzufahren,
  - im Normalbetrieb nur Migration-/App-User weiterzunutzen.

### 9.2 Modus B – „DBA-Managed“

- Für Kunden mit strengen Richtlinien.
- DBAs legen:
  - Datenbank,
  - Logins,
  - Users,
  - Rechte
selbst an.
- RSGO erhält:
  - App-ConnectionString,
  - optional Migration-ConnectionString,
- RSGO/Migrations-Container ändern keine Logins/Users, sondern nur Schema/Daten.

Im Wizard kann das als Option abgebildet werden:

- „Benutzer & DB durch Datenbank-Admin verwaltet“  
- „Benutzer & DB durch ReadyStackGo/Migration anlegen lassen“

---

## 10. Zusammenfassung

- Migrationen werden in RSGO als **Jobs in dedizierten Migrations-Containern** ausgeführt.
- Es gibt mindestens zwei DB-Benutzer:
  - **App-User** (Least Privilege, nur für Runtime),
  - **Migration-User** (Schemaanpassungen).
- Optional kann ein **Provisioning-/Admin-User** genutzt werden, damit die Migration:
  - Datenbank,
  - App-User,
  - Rechte
selbst anlegen kann.
- RSGO:
  - orchestriert Start/Überwachung/Auswertung der Migrations-Container,
  - setzt `DeploymentStatus`, `MigrationStatus`, `OperationMode`,
  - integriert Migrationen sauber in das Health-Modell (`Degraded (Migrating)`).
- RBAC stellt sicher, dass:
  - nur berechtigte Rollen (SystemOwner/OrgAdmin) DB-Credentials verwalten,
  - Operatoren nur mit Status/Health/Start/Stop arbeiten.
- Diese Spezifikation ist ausreichend, damit ein Implementierer (z. B. Claude) das
  - Manifest,
  - die Migrations-Container-Integration
  - und die RSGO-Orchestrierung
konsistent umsetzen kann.

# ReadyStackGo – Database Migration & Permissions Specification

## 1. Goals & Overview

This document describes how **ReadyStackGo (RSGO)** handles database migrations – particularly for **ams.project** – and how different database users (Least Privilege) are used in the process.

Goals:

- Clean, reproducible **migration model** for stacks (especially ams.project).
- Clear separation between:
  - **App-User** (restricted rights, runtime operation),
  - **Migration-User** (schema/migration rights),
  - optionally **Provisioning-/Admin-User** (DB/login creation).
- Orchestration by RSGO:
  - Migrations are executed as **Jobs (Migration Containers)**.
  - RSGO evaluates exit codes and updates status/health.
- Integration with the RSGO health and deployment model:
  - e.g., `OperationMode = Migrating`, `MigrationStatus`.

This document is intended as an implementation template (e.g., for Claude) to implement migrations and DB permissions in RSGO.

---

## 2. Role Model in the Database

For each business database (e.g., `ams_project`, `ams_identity`, etc.), three levels of users are distinguished:

### 2.1 App-User

**Purpose:** Runtime user for all regular services/containers.

- Typical permissions:
  - `SELECT`, `INSERT`, `UPDATE`, `DELETE`
  - `EXEC` on Stored Procedures/Functions
- **No** DDL rights:
  - no `CREATE TABLE`, `ALTER TABLE`, `DROP TABLE`, etc.
- Used **only** in app containers (e.g., `ams-api`, `ams-bff`, Worker, …).

### 2.2 Migration-User

**Purpose:** Execution of schema/data migrations.

- May:
  - create/modify/delete tables/views/procs/functions,
  - create/modify/delete indexes,
  - perform DB-specific migration operations if needed.
- Ideally restricted to a specific DB (e.g., only `ams_project`).
- Used **only** by migration containers.

### 2.3 Provisioning-/Admin-User (optional)

**Purpose:** One-time initial setup / "provisioning".

- May:
  - create databases
  - create logins/users
  - grant permissions
- Used **only** for:
  - initial setup,
  - rare special cases.
- Should not remain permanently active; either:
  - locked by the customer after provisioning,
  - or stored only as a highly sensitive secret in RSGO.

---

## 3. Modeling in the RSGO Manifest

The RSGO manifest describes which parameters are required for a stack and how they are used in services/migrations.

### 3.1 Example: ams.project Stack

#### 3.1.1 Parameters

```yaml
parameters:
  - key: PROJECT_DB_APP
    description: "App connection string to the ams.project database (read/write rights only)"
    required: true
    secret: true

  - key: PROJECT_DB_MIGRATION
    description: "Migration connection string to the ams.project database (schema changes)"
    required: false    # Required in expert mode, otherwise optional
    secret: true

  - key: PROJECT_DB_PROVISIONING
    description: "Optional provisioning connection string (DB/user creation)"
    required: false
    secret: true

  - key: PROJECT_DB_APP_USERNAME
    description: "Login/username of the App-User (may be created by migration)"
    required: false
    secret: false
```

> Note: The exact names (`PROJECT_DB_*`) are example names; they should match the naming convention of the respective domain/DB.

#### 3.1.2 Services

Regular services use only the **App connection string**:

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

#### 3.1.3 Migrations

Migrations use the **Migration connection string** (or the provisioning string in provisioning scenarios):

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

## 4. Migration Container – Contract

Migrations are executed as **one-shot jobs** in dedicated containers.

### 4.1 Behavior

- Starts, executes migrations, terminates.
- **No** long-running background processes.
- **Idempotent**:
  - Multiple executions should not cause damage.
  - Already applied migrations are skipped (e.g., Flyway, EF Migrations History).

### 4.2 Input Parameters (Env Vars)

The container receives all necessary information via env vars:

Minimal:

```text
PROJECT_DB_MIGRATION=...
RSGO_TARGET_VERSION=0.5.0
RSGO_ORG_ID=...
RSGO_ENV_ID=...
RSGO_MIGRATION_RUN_ID=...    # optional, GUID from RSGO
```

Provisioning mode (optional):

```text
PROJECT_DB_PROVISIONING=...        # higher privileged connection string
PROJECT_DB_APP=...                 # optional, if App-User should be tested with it
PROJECT_DB_APP_USERNAME=ams_app    # Name the migration job uses for App-User
APP_DB_PASSWORD=...                # Password for App-User to be created (from Secret)
```

### 4.3 Output / Result

- **Exit-Code**:
  - `0` → Migration successful
  - `!= 0` → Migration failed
- Optional: Write result to JSON file (e.g., in volume `/rsgo/migration-result.json`):

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

RSGO can:

- **always** evaluate the exit code (mandatory),
- **optionally** read the JSON output to show history/details in the UI.

---

## 5. Provisioning Mode: Migration Creates App-User

Optional "convenience mode":
The migration creates (with sufficient rights) the App-User directly in the database.

### 5.1 Process in Provisioning Mode

1. In the RSGO wizard/deployment for a new installation, the admin selects:

   - Mode:
     - "Users managed by Database Admin"
     - "Users created by ReadyStackGo/Migration" (**Provisioning Mode**)
   - specifies:
     - `PROJECT_DB_PROVISIONING` (highly privileged connection string)
     - `PROJECT_DB_APP_USERNAME` (e.g., `ams_app`)
     - optionally App password (or lets it be generated)

2. RSGO starts the migration container with:
   - `PROJECT_DB_PROVISIONING` set
   - `PROJECT_DB_APP_USERNAME` set
   - optionally `APP_DB_PASSWORD` via env var

3. Migration logic:

   - If DB doesn't exist yet → create it.
   - If App-Login/User doesn't exist yet:
     - Create login (e.g., `CREATE LOGIN [ams_app] WITH PASSWORD = @AppUserPassword;`).
     - Create user for the DB (`CREATE USER [ams_app] FOR LOGIN [ams_app];`).
     - Assign roles (e.g., `db_datareader`, `db_datawriter`, possibly custom role for `EXEC`).
   - Execute schema migrations.

4. Result:
   - DB exists.
   - App-User exists.
   - Migration history is up to date.

5. RSGO notes in the configuration:
   - `PROJECT_DB_APP` → Connection string with App-User.
   - `PROJECT_DB_MIGRATION` → Connection string with Migration-/Provisioning-User (depending on setup).

### 5.2 Idempotency

The provisioning script must be written so that it can safely run again.

Example (SQL Server, pseudocode):

```sql
-- Create login + user only if they don't already exist
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
    -- possibly custom role for EXEC rights
END;
```

Passwords are **not** hardcoded, but passed to the migration container as env var.

---

## 6. Orchestration by RSGO

### 6.1 Deployment/Upgrade Flow

When deploying/upgrading a stack (e.g., `ams-project`):

1. **Set status** (in SQLite):

   - `DeploymentStatus = Upgrading`
   - `MigrationStatus = Running`
   - `OperationMode = Migrating`
   - set `TargetVersion` if applicable.

2. **Stop services** (if `runPolicy` requires it):

   - stop affected services of the stack (e.g., Worker/APIs),
   - depending on policy.

3. **Start migration container**:

   - via Docker API (`restartPolicy = "no"`):
     - Image: `ams.project.migrations:<TargetVersion>`
     - Env: DB connection strings, RSGO IDs etc.
   - Store container ID in RSGO.

4. **Monitor migration run**:

   - periodically check container status:
     - `running` → Migration is running
     - `exited` → Migration finished
   - read exit code.

5. **Process result**:

   - Exit-Code `0`:
     - `MigrationStatus = Succeeded`
     - `CurrentVersion = TargetVersion`
     - `OperationMode = Normal` (or transition to "start services")
   - Exit-Code `!= 0`:
     - `MigrationStatus = Failed`
     - `OperationMode = Failed`
     - `DeploymentStatus = Failed`
     - UI: Note "Migration failed – check logs".

6. **Start services** (on success):

   - Start stack services with APP connection strings.
   - `DeploymentStatus = Idle`
   - Re-evaluate health checks.

### 6.2 Retry

- UI option: "Retry migration":
  - restarts the same migration container (new `RSGO_MIGRATION_RUN_ID`).
  - assumes idempotency of the migration logic.

---

## 7. Integration with Health & OperationMode

The migration feature integrates into the Health/OperationMode model of RSGO:

- During migration:
  - `OperationMode = Migrating`
  - Health engine sets:
    - `overall = Degraded`
    - UI shows: "Migration running (from X to Y)"
- After successful migration:
  - `OperationMode = Normal`
  - `overall` is recalculated from Bus/Infra/Self.
- After failed migration:
  - `OperationMode = Failed`
  - `MigrationStatus = Failed`
  - `overall = Unhealthy`
  - UI shows:
    - "Migration failed – manual intervention required."

This clearly distinguishes between **planned restrictions** (migration/maintenance) and **unplanned outages**.

---

## 8. Security & RBAC

### 8.1 Visibility of Connection Strings

For DB parameters (especially MIGRATION/PROVISIONING):

- Only **SystemOwner** / **OrgAdmin**:
  - may set/change values for MIGRATION/PROVISIONING,
  - may even see whether these are set (displayed only as `*****`).
- **Operator**:
  - may start/stop stacks,
  - may trigger migrations again (if allowed),
  - may see status/logs,
  - may **not** view or change connection strings.

### 8.2 Storage of Credentials

- All DB connection strings are treated as **Secrets** in SQLite:
  - Encryption with ASP.NET Data Protection or separate key,
  - no plaintext storage.
- Logs:
  - Migration logs and RSGO logs must not contain complete connection strings or passwords.
  - Error texts should be "sanitized" (e.g., mask offending connection strings).

### 8.3 .env Import

When `.env` files are used:

```env
PROJECT_DB_APP=...
PROJECT_DB_MIGRATION=...
PROJECT_DB_PROVISIONING=...
PROJECT_DB_APP_USERNAME=ams_app
```

- RSGO:
  - reads the values,
  - stores them encrypted,
  - shows only masking (`******`) for secrets.

---

## 9. Modes for Different Security Requirements

RSGO should support two broad operating modes per database/stack:

### 9.1 Mode A – "Self-Managed by RSGO" (Provisioning via Migration)

- For smaller customers / simple setups.
- RSGO/Migration containers:
  - create database (if needed),
  - create App-User,
  - assign roles.
- Customer only needs to provide a provisioning/admin connection string.
- After completion, it is recommended to:
  - reduce provisioning rights,
  - use only Migration-/App-User for normal operation.

### 9.2 Mode B – "DBA-Managed"

- For customers with strict policies.
- DBAs create:
  - Database,
  - Logins,
  - Users,
  - Permissions
themselves.
- RSGO receives:
  - App connection string,
  - optionally Migration connection string,
- RSGO/Migration containers do not change logins/users, only schema/data.

In the wizard, this can be presented as an option:

- "Users & DB managed by Database Admin"
- "Users & DB created by ReadyStackGo/Migration"

---

## 10. Summary

- Migrations in RSGO are executed as **jobs in dedicated migration containers**.
- There are at least two DB users:
  - **App-User** (Least Privilege, runtime only),
  - **Migration-User** (schema changes).
- Optionally, a **Provisioning-/Admin-User** can be used so that the migration can:
  - create database,
  - create App-User,
  - assign permissions
itself.
- RSGO:
  - orchestrates start/monitoring/evaluation of migration containers,
  - sets `DeploymentStatus`, `MigrationStatus`, `OperationMode`,
  - cleanly integrates migrations into the health model (`Degraded (Migrating)`).
- RBAC ensures that:
  - only authorized roles (SystemOwner/OrgAdmin) manage DB credentials,
  - operators only work with status/health/start/stop.
- This specification is sufficient for an implementer (e.g., Claude) to consistently implement:
  - the manifest,
  - the migration container integration,
  - and the RSGO orchestration.

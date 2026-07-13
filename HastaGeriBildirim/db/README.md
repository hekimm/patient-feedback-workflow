# HGB Oracle database modules

Use the numbered files to install the database. They run in order, and each
module can be run separately. Top-level `install-*.sql` files are optional
shortcuts for local automation and CI.

## Requirements

- Oracle Database 12c or newer, connected to the target PDB/service
- an `AL32UTF8` database character set
- SQL*Plus or SQLcl
- a dedicated `PATIENT_APP` schema

On Windows, set the client encoding before running files containing Turkish and
Arabic reference text:

```powershell
$env:NLS_LANG = '.AL32UTF8'
```

Run commands from the repository root because the paths below are relative to it.
Each SQL file also sets both `WHENEVER SQLERROR` and `WHENEVER OSERROR`, so a
missing include or SQL failure returns a non-zero exit.

## Module map

| Order | Module | Run as | Environment | Owns |
| --- | --- | --- | --- | --- |
| A1 | `admin/001-create-application-user.sql` | DBA | all | create/unlock `PATIENT_APP` and explicitly set its password |
| A2 | `admin/002-grant-application-privileges.sql` | DBA | all | install-time table/identity-sequence/view grants and configurable tablespace quota |
| 000 | `000-preflight.sql` | `PATIENT_APP` | all | user, Oracle version, PDB, charset, privileges, quota checks |
| 001-006 | `schema/001-*.sql` through `006-*.sql` | `PATIENT_APP` | all | the 46 current-state application tables |
| 007 | `schema/007-indexes.sql` | `PATIENT_APP` | all | lookup, queue, uniqueness, and foreign-key support indexes |
| 008 | `schema/008-views.sql` | `PATIENT_APP` | all | dashboard and recovery views |
| 009 | `schema/009-foreign-keys.sql` | `PATIENT_APP` | all | named and validated application foreign keys |
| 010-012 | `data/001-*.sql` through `003-*.sql` | `PATIENT_APP` | all | environment-neutral roles, channels, policies, consent, and default survey |
| B1 | `bootstrap/001-create-admin.sql` | `PATIENT_APP` | production | first real `SYS_ADMIN`, using a bcrypt hash |
| D1-D3 | `demo/001-*.sql` through `003-*.sql` | `PATIENT_APP` | local/demo only | synthetic institution, accounts, patient, and localhost setting |
| V1-V2 | `verify/001-*.sql`, `002-*.sql` | `PATIENT_APP` | all | strict schema/column/index/FK/view and reference-data contracts |
| V3 | `verify/003-demo.sql` | `PATIENT_APP` | local/demo only | strict demo profile |
| V4 | `verify/004-production.sql` | `PATIENT_APP` | production | active admin, no demo users, no unsafe DB URL |
| A3 | `admin/003-revoke-install-privileges.sql` | DBA | production | revoke install-only DDL grants |

The numbered modules define the first database version in this repository.
There is no earlier schema version or upgrade path.

## Fresh installation, module by module

First connect as the DBA so the DBA password is prompted instead of appearing
in the process list. Run both admin modules, then leave that session. Quota
accepts `UNLIMITED` or a positive size such as `500M` or `2G`.

```text
sqlplus -L system@//host:1521/<pdb-service>

@HastaGeriBildirim/db/admin/001-create-application-user.sql "<app-password>"
@HastaGeriBildirim/db/admin/002-grant-application-privileges.sql <app-tablespace> <quota>
EXIT
```

Then connect once as `PATIENT_APP` and run each module separately. A successful
step can be rerun safely; if a step fails, fix the cause and resume from that
step.

```text
sqlplus -L patient_app@//host:1521/<pdb-service>

@HastaGeriBildirim/db/000-preflight.sql
@HastaGeriBildirim/db/schema/001-foundation.sql
@HastaGeriBildirim/db/schema/002-access-control.sql
@HastaGeriBildirim/db/schema/003-survey-design.sql
@HastaGeriBildirim/db/schema/004-delivery.sql
@HastaGeriBildirim/db/schema/005-feedback-recovery.sql
@HastaGeriBildirim/db/schema/006-operations.sql
@HastaGeriBildirim/db/schema/007-indexes.sql
@HastaGeriBildirim/db/schema/008-views.sql
@HastaGeriBildirim/db/schema/009-foreign-keys.sql
@HastaGeriBildirim/db/data/001-access-reference.sql
@HastaGeriBildirim/db/data/002-channel-reference.sql
@HastaGeriBildirim/db/data/003-survey-reference.sql
@HastaGeriBildirim/db/verify/001-schema-contract.sql
@HastaGeriBildirim/db/verify/002-reference-data.sql
```

This path does not create demo users, a demo hospital, a demo patient, or a
localhost `PUBLIC_BASE_URL`.

### Production admin bootstrap

Generate a bcrypt hash without placing the plaintext password in a SQL file or
shell argument:

```powershell
dotnet run --project .\HastaGeriBildirim\HastaGeriBildirim.csproj -- --generate-password-hash
```

Inside the existing `PATIENT_APP` SQL*Plus session, pass that hash to the
separate bootstrap module:

```text
@HastaGeriBildirim/db/bootstrap/001-create-admin.sql admin "<bcrypt-hash>" "System Administrator" "admin@example.org"
@HastaGeriBildirim/db/verify/004-production.sql
EXIT
```

The bootstrap module can be run again. It replaces the selected account's hash
and restores its active `SYS_ADMIN` mapping.

After verification, reconnect as the DBA and remove install-only DDL privileges:

```text
sqlplus -L system@//host:1521/<pdb-service>

@HastaGeriBildirim/db/admin/003-revoke-install-privileges.sql
EXIT
```

Before a future migration, run admin step A2 again; after the migration, run A3.

### Local/demo profile

After core modules 000-012, run the three demo modules and the strict demo
verifier:

```text
@HastaGeriBildirim/db/demo/001-demo-organization.sql
@HastaGeriBildirim/db/demo/002-demo-users.sql
@HastaGeriBildirim/db/demo/003-demo-settings.sql
@HastaGeriBildirim/db/verify/003-demo.sql
```

These files are synthetic and must never be run in Production.

## Optional manifests

The manifests provide a shorter command for local setup and CI. The numbered
modules can still be run separately:

- `install-production.sql`: fresh current-state schema, reference data, and core verification
- `install-demo.sql`: production manifest plus local demo modules and demo verification

From the `db` directory, these can be invoked as
`@install-production.sql` or `@install-demo.sql`.

## Rerunning after a failure

Oracle DDL commits automatically, so the full installation is not one
transaction. After fixing a failed step, continue from that module:

- table modules create a table only when its name is absent;
- index and foreign-key modules create named objects only when absent;
- views are replaced;
- reference/demo inserts use stable keys and guarded inserts;
- successful schema/index/view/foreign-key/data/demo modules write codes to
  `HGB_SCHEMA_VERSION`;
- strict verification checks all 46 tables, 364 columns, 36 named indexes, 57
  foreign keys, view columns, module versions, and required reference rows.

Rerunning by object name does not repair an existing object with the wrong
shape. Verification fails in that case. After the first release, schema
changes should be delivered as reviewed, forward-only versioned
migrations; do not edit production data by hand to make verification pass.

`check-view-columns.sql` shows view and setup-table shapes. It is not an
acceptance test.

# Hasta Geri Bildirim production runbook

## Target and release boundary

The target is on-prem Windows/IIS with an Oracle PDB using `AL32UTF8`.
Docker is only for local demo/test. A production release includes three
separate operations: database modules, environment/secret configuration, and
IIS deployment.

For manual installation, use the module order in
[`db/README.md`](../db/README.md). The `install-production.sql` manifest is
available for optional automation.

## Required environment configuration

Set values outside the repository through IIS configuration, machine/user
environment, or an approved secret store. The repository's `.env.example` is
an inventory only and is not loaded automatically.

Required application values:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__OracleDb`
- `PublicBaseUrl` using the externally reachable HTTPS origin
- `HGB_PII_ENCRYPTION_KEY`
- `HGB_TOKEN_HASH_SALT`
- `HGB_WEBHOOK_API_KEY`
- `HGB_WEBHOOK_HMAC_SECRET`
- `PROBEL_SMS_BASE_URL`
- either `PROBEL_SMS_API_KEY` or `PROBEL_SMS_BEARER_TOKEN`
- `WHATSAPP_BASE_URL`
- `WHATSAPP_BEARER_TOKEN`
- `WHATSAPP_VERIFY_TOKEN`
- `WHATSAPP_APP_SECRET`
- `PROBEL_BI_BASE_URL`
- `PROBEL_BI_BEARER_TOKEN`

The following four values must each contain at least 32 characters:
`HGB_PII_ENCRYPTION_KEY`, `HGB_TOKEN_HASH_SALT`,
`HGB_WEBHOOK_HMAC_SECRET`, and `WHATSAPP_APP_SECRET`.

Treat encryption keys and token salts as persistent data keys. Replacing them
without an approved rotation/migration plan can make existing encrypted data or
tokens unusable. Production startup rejects missing, placeholder, demo, or
unsafe values.

## Fresh Oracle installation

Before starting:

1. Back up the target schema/database.
2. Confirm the connection targets the intended PDB, not `CDB$ROOT`.
3. Set `NLS_LANG=.AL32UTF8` in the SQL*Plus/SQLcl process.
4. Select the approved application tablespace and quota policy.
5. Keep the application pool stopped until all verification passes.

Connect as the DBA so its password is prompted rather than included in the
process list. Run the two admin modules separately. The quota argument accepts
`UNLIMITED` or a positive value such as `500M` or `2G`.

```text
sqlplus -L system@//host:1521/<pdb-service>

@HastaGeriBildirim/db/admin/001-create-application-user.sql "<app-password>"
@HastaGeriBildirim/db/admin/002-grant-application-privileges.sql <app-tablespace> <quota>
EXIT
```

Connect as `PATIENT_APP` (SQL*Plus prompts for its password), then run the
current-state modules in this exact order:

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

Do not run anything under `db/demo/` or `install-demo.sql` in Production.
Fresh production modules do not load a synthetic institution or a localhost
DB URL.

### Bootstrap the first administrator

The UI requires an existing `SYS_ADMIN` to manage users. Generate the initial
bcrypt hash through the application helper:

```powershell
dotnet run --project .\HastaGeriBildirim\HastaGeriBildirim.csproj -- --generate-password-hash
```

Run the bootstrap and production checks as `PATIENT_APP`:

```text
@HastaGeriBildirim/db/bootstrap/001-create-admin.sql admin "<bcrypt-hash>" "System Administrator" "admin@example.org"
@HastaGeriBildirim/db/verify/004-production.sql
EXIT
```

Store the plaintext password only in the approved credential channel. The SQL
script receives and stores only its bcrypt hash.

Finally, remove install-only DDL permissions as the DBA:

```text
sqlplus -L system@//host:1521/<pdb-service>

@HastaGeriBildirim/db/admin/003-revoke-install-privileges.sql
EXIT
```

For a later database release, run admin step 002 before the reviewed database
changes and run step 003 after verification.

Oracle DDL commits automatically, so the full installation cannot be rolled
back as one transaction. Use a backup restore or a reviewed forward fix.

## IIS deployment

Publish:

```powershell
dotnet publish .\HastaGeriBildirim\HastaGeriBildirim.csproj -c Release -o C:\inetpub\hgb
```

Configure the IIS application pool:

- No Managed Code
- 64-bit enabled
- identity with Oracle network access
- HTTPS binding only
- required environment variables available to the worker process

Use the generated `web.config`, recycle the pool, and check in order:

1. `GET /health/live`
2. `GET /health/ready`
3. the login page using the bootstrap administrator
4. a generated survey link uses the approved public HTTPS host

## Rollback and recovery

Before every release, back up both the Oracle schema and the existing IIS
publish directory.

- If only application binaries changed, restore the previous IIS publish
  directory.
- If database modules changed and cannot be forward-fixed safely, stop the app,
  restore the approved Oracle backup, restore the matching IIS build, and rerun
  readiness checks.
- Never run demo seeds to repair a production login or reference-data problem.

## Operational checks

- readiness: `GET /health/ready`
- liveness: `GET /health/live`
- HGB webhook headers: `X-HGB-API-Key`, `X-HGB-Timestamp`,
  `X-HGB-Signature`
- WhatsApp signature: `X-Hub-Signature-256`
- database acceptance: `db/verify/001-schema-contract.sql`,
  `002-reference-data.sql`, and `004-production.sql`

Production secrets are not stored in source. PII phone/email values use
encrypted fields and lookup hashes; personal-data and DSR operations are
audited.

# Hasta Geri Bildirim Production Runbook

## Target

On-prem Windows/IIS + Oracle. Docker is only for local demo/test.

## Required Environment Variables

Set these outside the repository through IIS environment variables, machine/user environment, or a secret store:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__OracleDb`
- `PublicBaseUrl`
- `HGB_PII_ENCRYPTION_KEY`
- `HGB_TOKEN_HASH_SALT`
- `HGB_WEBHOOK_API_KEY`
- `HGB_WEBHOOK_HMAC_SECRET`
- `PROBEL_SMS_BASE_URL`
- `PROBEL_SMS_API_KEY` or `PROBEL_SMS_BEARER_TOKEN`
- `WHATSAPP_BASE_URL`
- `WHATSAPP_BEARER_TOKEN`
- `WHATSAPP_VERIFY_TOKEN`
- `WHATSAPP_APP_SECRET`
- `PROBEL_BI_BASE_URL`
- `PROBEL_BI_BEARER_TOKEN`

Production fails fast when required secrets, placeholder values, demo keys, or hardening tables are missing.

## Oracle Install Order

1. Create/unlock the application schema with least privilege.
2. Run `db/install-production.sql` as `patient_app`.
3. Do not run `db/install-demo.sql`, `db/demo-seed.sql` or `db/set-demo-password-hashes.sql` in Production.
4. Confirm `/health/ready` returns `ready`.

## IIS Deployment

1. Publish:

   ```powershell
   dotnet publish .\HastaGeriBildirim\HastaGeriBildirim.csproj -c Release -o C:\inetpub\hgb
   ```

2. Configure the IIS app pool:

   - No Managed Code
   - 64-bit enabled
   - Identity with Oracle network access
   - HTTPS binding only

3. Copy/set `web.config` from the project publish output.
4. Set environment variables for the app pool.
5. Recycle the app pool and check:

   - `/health/live`
   - `/health/ready`
   - login page

## Backup And Restore

- Back up Oracle schema before each migration.
- Back up IIS publish folder before each release.
- Rollback path:
  - Restore previous IIS publish folder.
  - Restore Oracle backup if a schema migration was applied and cannot be forward-fixed.

## Operational Checks

- Readiness: `GET /health/ready`
- Liveness: `GET /health/live`
- Webhook HMAC headers for HGB APIs:
  - `X-HGB-API-Key`
  - `X-HGB-Timestamp`
  - `X-HGB-Signature`
- WhatsApp POST signature:
  - `X-Hub-Signature-256`

## Security Notes

- No production secret is stored in source.
- PII phone/email fields are encrypted and lookup hashes are used.
- Personal-data export and DSR operations write audit logs with hashed IP.
- Security headers and production error page are enabled by default.

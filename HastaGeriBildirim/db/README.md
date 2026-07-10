# HGB Oracle Scripts

Run these scripts with SQLPlus or SQLcl.

## Fresh Install

1. As `SYSTEM` or an admin user:

   ```sql
   @setup-oracle-permissions.sql "StrongAppPasswordHere"
   ```

2. As `patient_app`:

   ```sql
   @install-production.sql
   ```

For local/demo environments only, use `@install-demo.sql` instead of
`@install-production.sql`. Demo install creates `admin.demo`, `kalite.demo` and
`birim.demo` users.

## Script Roles

- `install-production.sql`: production entry point; schema, FKs, hardening and verification.
- `install-demo.sql`: local/demo entry point; production install plus demo users and smoke checks.
- `hgb-oracle-install.sql`: base schema, views, indexes and reference data.
- `referential-integrity.sql`: foreign key constraints.
- `production-hardening.sql`: security/hardening migration objects and indexes.
- `verify-database.sql`: strict post-install verification.
- `smoke-ui.sql`: demo-only UI smoke data checks.
- `check-view-columns.sql`: diagnostic helper for view/table shape issues.

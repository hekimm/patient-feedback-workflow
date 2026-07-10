-- Run as patient_app after setup-oracle-permissions.sql has created/unlocked the user.
-- Installs schema, constraints, hardening tables, demo users and local smoke checks.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK

SET DEFINE OFF;
SET VERIFY OFF;

PROMPT HGB demo install started.

@@hgb-oracle-install.sql
@@referential-integrity.sql
@@production-hardening.sql
@@demo-seed.sql
@@verify-database.sql
@@smoke-ui.sql

PROMPT HGB demo install completed.

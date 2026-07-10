-- Run as patient_app after setup-oracle-permissions.sql has created/unlocked the user.
-- Demo users/data are intentionally not installed here.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK

SET DEFINE OFF;
SET VERIFY OFF;

PROMPT HGB production install started.

@@hgb-oracle-install.sql
@@referential-integrity.sql
@@production-hardening.sql
@@verify-database.sql

PROMPT HGB production install completed.

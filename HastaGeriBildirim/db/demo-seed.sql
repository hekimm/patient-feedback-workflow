-- Development/demo only. Do not run in Production.
-- Run after hgb-oracle-install.sql and production-hardening.sql for local UI testing.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK

SET DEFINE OFF;
SET VERIFY OFF;

@@set-demo-password-hashes.sql

PROMPT HGB demo seed tamamlandı.

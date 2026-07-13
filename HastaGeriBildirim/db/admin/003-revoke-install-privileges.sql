-- Run after production installation. Grant the DDL privileges again before a schema upgrade.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
WHENEVER OSERROR EXIT FAILURE

REVOKE CREATE TABLE FROM patient_app;
REVOKE CREATE SEQUENCE FROM patient_app;
REVOKE CREATE VIEW FROM patient_app;

PROMPT Admin step 003 - install-only DDL privileges revoked from PATIENT_APP.

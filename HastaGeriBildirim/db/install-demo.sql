-- Local setup only. This script loads mock data and must not be used in production.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
WHENEVER OSERROR EXIT FAILURE

SET DEFINE OFF;
SET VERIFY OFF;

PROMPT HGB demo manifest started.

@@install-production.sql
@@demo/001-demo-organization.sql
@@demo/002-demo-users.sql
@@demo/003-demo-settings.sql
@@verify/003-demo.sql

PROMPT HGB demo manifest completed.

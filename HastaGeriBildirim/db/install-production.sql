-- Run as PATIENT_APP after the admin scripts. Demo data is not loaded.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
WHENEVER OSERROR EXIT FAILURE

SET DEFINE OFF;
SET VERIFY OFF;

PROMPT HGB fresh schema manifest started.

@@000-preflight.sql
@@schema/001-foundation.sql
@@schema/002-access-control.sql
@@schema/003-survey-design.sql
@@schema/004-delivery.sql
@@schema/005-feedback-recovery.sql
@@schema/006-operations.sql
@@schema/007-indexes.sql
@@schema/008-views.sql
@@schema/009-foreign-keys.sql
@@data/001-access-reference.sql
@@data/002-channel-reference.sql
@@data/003-survey-reference.sql
@@verify/001-schema-contract.sql
@@verify/002-reference-data.sql

PROMPT HGB fresh schema manifest completed.

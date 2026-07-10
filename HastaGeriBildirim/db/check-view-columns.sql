-- Run as patient_app after install-production.sql or install-demo.sql.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK

SET DEFINE OFF;
SET VERIFY OFF;
SET PAGESIZE 200;
SET LINESIZE 220;

COLUMN object_name FORMAT A34;
COLUMN column_name FORMAT A34;
COLUMN data_type FORMAT A24;
COLUMN nullable FORMAT A8;

PROMPT HGB dashboard/recovery view columns:
SELECT table_name AS object_name, column_id, column_name, data_type, nullable
FROM user_tab_columns
WHERE table_name IN ('HGB_V_FEEDBACK_DASHBOARD', 'HGB_V_OPEN_RECOVERY_CASES')
ORDER BY table_name, column_id;

PROMPT HGB setup table columns used by UI:
SELECT table_name AS object_name, column_id, column_name, data_type, nullable
FROM user_tab_columns
WHERE table_name IN ('HGB_SURVEY_TEMPLATES', 'HGB_CHANNELS', 'HGB_USERS')
ORDER BY table_name, column_id;

PROMPT HGB dashboard sample row:
SELECT *
FROM HGB_V_FEEDBACK_DASHBOARD
WHERE ROWNUM <= 1;

PROMPT HGB open recovery sample row:
SELECT *
FROM HGB_V_OPEN_RECOVERY_CASES
WHERE ROWNUM <= 1;

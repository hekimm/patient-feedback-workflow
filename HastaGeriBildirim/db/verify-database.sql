-- Run as patient_app after install-production.sql or install-demo.sql.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK

SET DEFINE OFF;
SET VERIFY OFF;
SET FEEDBACK ON;
SET PAGESIZE 200;
SET LINESIZE 220;

PROMPT HGB database verification started.

COLUMN table_count FORMAT 999;
COLUMN view_count FORMAT 999;
COLUMN fk_count FORMAT 999;
COLUMN demo_user_count FORMAT 999;
COLUMN version_code FORMAT A34;
COLUMN description FORMAT A70;
COLUMN missing_table FORMAT A40;
COLUMN missing_view FORMAT A40;
COLUMN missing_version FORMAT A40;
COLUMN invalid_object FORMAT A40;
COLUMN object_type FORMAT A20;

SELECT COUNT(*) AS table_count
FROM user_tables
WHERE table_name LIKE 'HGB_%';

SELECT COUNT(*) AS view_count
FROM user_views
WHERE view_name LIKE 'HGB_%';

SELECT COUNT(*) AS fk_count
FROM user_constraints
WHERE constraint_type = 'R'
  AND constraint_name LIKE 'FK_HGB_%';

PROMPT Missing required tables:
WITH expected_tables (table_name) AS (
    SELECT 'HGB_ALERTS' FROM dual UNION ALL
    SELECT 'HGB_APP_SETTINGS' FROM dual UNION ALL
    SELECT 'HGB_AUDIT_LOGS' FROM dual UNION ALL
    SELECT 'HGB_BI_EXPORT_QUEUE' FROM dual UNION ALL
    SELECT 'HGB_BRANCHES' FROM dual UNION ALL
    SELECT 'HGB_BRANCHING_RULES' FROM dual UNION ALL
    SELECT 'HGB_CASE_ESCALATIONS' FROM dual UNION ALL
    SELECT 'HGB_CHANNEL_TEMPLATES' FROM dual UNION ALL
    SELECT 'HGB_CHANNELS' FROM dual UNION ALL
    SELECT 'HGB_CLINICAL_EVENTS' FROM dual UNION ALL
    SELECT 'HGB_CONSENT_RECORDS' FROM dual UNION ALL
    SELECT 'HGB_CONSENT_TEXTS' FROM dual UNION ALL
    SELECT 'HGB_DATA_SUBJECT_REQUESTS' FROM dual UNION ALL
    SELECT 'HGB_DELIVERY_ATTEMPTS' FROM dual UNION ALL
    SELECT 'HGB_DEPARTMENTS' FROM dual UNION ALL
    SELECT 'HGB_DOCTORS' FROM dual UNION ALL
    SELECT 'HGB_EVENT_QUEUE' FROM dual UNION ALL
    SELECT 'HGB_HOSPITALS' FROM dual UNION ALL
    SELECT 'HGB_INTEGRATION_LOGS' FROM dual UNION ALL
    SELECT 'HGB_INTEGRATION_SYSTEMS' FROM dual UNION ALL
    SELECT 'HGB_KPI_TARGETS' FROM dual UNION ALL
    SELECT 'HGB_PATIENTS' FROM dual UNION ALL
    SELECT 'HGB_PERMISSIONS' FROM dual UNION ALL
    SELECT 'HGB_RECOVERY_ACTIONS' FROM dual UNION ALL
    SELECT 'HGB_REPORT_EXPORTS' FROM dual UNION ALL
    SELECT 'HGB_RESPONSE_THEME_MATCHES' FROM dual UNION ALL
    SELECT 'HGB_RETENTION_POLICIES' FROM dual UNION ALL
    SELECT 'HGB_ROLE_PERMISSIONS' FROM dual UNION ALL
    SELECT 'HGB_ROLES' FROM dual UNION ALL
    SELECT 'HGB_SCHEMA_VERSION' FROM dual UNION ALL
    SELECT 'HGB_SENTIMENT_RESULTS' FROM dual UNION ALL
    SELECT 'HGB_SERVICE_RECOVERY_CASES' FROM dual UNION ALL
    SELECT 'HGB_SERVICES' FROM dual UNION ALL
    SELECT 'HGB_SURVEY_ANSWERS' FROM dual UNION ALL
    SELECT 'HGB_SURVEY_INVITATIONS' FROM dual UNION ALL
    SELECT 'HGB_SURVEY_OPTIONS' FROM dual UNION ALL
    SELECT 'HGB_SURVEY_QUESTIONS' FROM dual UNION ALL
    SELECT 'HGB_SURVEY_RESPONSES' FROM dual UNION ALL
    SELECT 'HGB_SURVEY_TEMPLATE_VERSIONS' FROM dual UNION ALL
    SELECT 'HGB_SURVEY_TEMPLATES' FROM dual UNION ALL
    SELECT 'HGB_THEME_CATEGORIES' FROM dual UNION ALL
    SELECT 'HGB_TRIGGER_RULES' FROM dual UNION ALL
    SELECT 'HGB_USER_ROLES' FROM dual UNION ALL
    SELECT 'HGB_USER_SCOPES' FROM dual UNION ALL
    SELECT 'HGB_USERS' FROM dual UNION ALL
    SELECT 'HGB_WEBHOOK_REPLAY' FROM dual
)
SELECT table_name AS missing_table
FROM expected_tables
MINUS
SELECT table_name
FROM user_tables
ORDER BY missing_table;

PROMPT Missing required views:
WITH expected_views (view_name) AS (
    SELECT 'HGB_V_FEEDBACK_DASHBOARD' FROM dual UNION ALL
    SELECT 'HGB_V_OPEN_RECOVERY_CASES' FROM dual
)
SELECT view_name AS missing_view
FROM expected_views
MINUS
SELECT view_name
FROM user_views
ORDER BY missing_view;

PROMPT Missing required schema versions:
WITH expected_versions (version_code) AS (
    SELECT '2026-07-base-schema' FROM dual UNION ALL
    SELECT '2026-07-referential-integrity' FROM dual UNION ALL
    SELECT '2026-07-production-hardening' FROM dual
)
SELECT version_code AS missing_version
FROM expected_versions
MINUS
SELECT version_code
FROM hgb_schema_version
ORDER BY missing_version;

PROMPT Invalid HGB objects:
SELECT object_name AS invalid_object, object_type
FROM user_objects
WHERE object_name LIKE 'HGB_%'
  AND object_type IN ('VIEW', 'PACKAGE', 'PACKAGE BODY', 'PROCEDURE', 'FUNCTION', 'TRIGGER')
  AND status <> 'VALID'
ORDER BY object_type, object_name;

PROMPT Applied schema versions:
SELECT version_code, description, applied_at
FROM hgb_schema_version
ORDER BY applied_at, version_code;

PROMPT Demo user count (informational; expected 0 in Production, 3 in local demo):
SELECT COUNT(*) AS demo_user_count
FROM hgb_users
WHERE username IN ('admin.demo', 'kalite.demo', 'birim.demo')
  AND status = 'ACTIVE';

DECLARE
    v_missing_tables NUMBER;
    v_missing_views NUMBER;
    v_missing_versions NUMBER;
    v_invalid_objects NUMBER;
    v_fk_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_missing_tables
    FROM (
        WITH expected_tables (table_name) AS (
            SELECT 'HGB_ALERTS' FROM dual UNION ALL
            SELECT 'HGB_APP_SETTINGS' FROM dual UNION ALL
            SELECT 'HGB_AUDIT_LOGS' FROM dual UNION ALL
            SELECT 'HGB_BI_EXPORT_QUEUE' FROM dual UNION ALL
            SELECT 'HGB_BRANCHES' FROM dual UNION ALL
            SELECT 'HGB_BRANCHING_RULES' FROM dual UNION ALL
            SELECT 'HGB_CASE_ESCALATIONS' FROM dual UNION ALL
            SELECT 'HGB_CHANNEL_TEMPLATES' FROM dual UNION ALL
            SELECT 'HGB_CHANNELS' FROM dual UNION ALL
            SELECT 'HGB_CLINICAL_EVENTS' FROM dual UNION ALL
            SELECT 'HGB_CONSENT_RECORDS' FROM dual UNION ALL
            SELECT 'HGB_CONSENT_TEXTS' FROM dual UNION ALL
            SELECT 'HGB_DATA_SUBJECT_REQUESTS' FROM dual UNION ALL
            SELECT 'HGB_DELIVERY_ATTEMPTS' FROM dual UNION ALL
            SELECT 'HGB_DEPARTMENTS' FROM dual UNION ALL
            SELECT 'HGB_DOCTORS' FROM dual UNION ALL
            SELECT 'HGB_EVENT_QUEUE' FROM dual UNION ALL
            SELECT 'HGB_HOSPITALS' FROM dual UNION ALL
            SELECT 'HGB_INTEGRATION_LOGS' FROM dual UNION ALL
            SELECT 'HGB_INTEGRATION_SYSTEMS' FROM dual UNION ALL
            SELECT 'HGB_KPI_TARGETS' FROM dual UNION ALL
            SELECT 'HGB_PATIENTS' FROM dual UNION ALL
            SELECT 'HGB_PERMISSIONS' FROM dual UNION ALL
            SELECT 'HGB_RECOVERY_ACTIONS' FROM dual UNION ALL
            SELECT 'HGB_REPORT_EXPORTS' FROM dual UNION ALL
            SELECT 'HGB_RESPONSE_THEME_MATCHES' FROM dual UNION ALL
            SELECT 'HGB_RETENTION_POLICIES' FROM dual UNION ALL
            SELECT 'HGB_ROLE_PERMISSIONS' FROM dual UNION ALL
            SELECT 'HGB_ROLES' FROM dual UNION ALL
            SELECT 'HGB_SCHEMA_VERSION' FROM dual UNION ALL
            SELECT 'HGB_SENTIMENT_RESULTS' FROM dual UNION ALL
            SELECT 'HGB_SERVICE_RECOVERY_CASES' FROM dual UNION ALL
            SELECT 'HGB_SERVICES' FROM dual UNION ALL
            SELECT 'HGB_SURVEY_ANSWERS' FROM dual UNION ALL
            SELECT 'HGB_SURVEY_INVITATIONS' FROM dual UNION ALL
            SELECT 'HGB_SURVEY_OPTIONS' FROM dual UNION ALL
            SELECT 'HGB_SURVEY_QUESTIONS' FROM dual UNION ALL
            SELECT 'HGB_SURVEY_RESPONSES' FROM dual UNION ALL
            SELECT 'HGB_SURVEY_TEMPLATE_VERSIONS' FROM dual UNION ALL
            SELECT 'HGB_SURVEY_TEMPLATES' FROM dual UNION ALL
            SELECT 'HGB_THEME_CATEGORIES' FROM dual UNION ALL
            SELECT 'HGB_TRIGGER_RULES' FROM dual UNION ALL
            SELECT 'HGB_USER_ROLES' FROM dual UNION ALL
            SELECT 'HGB_USER_SCOPES' FROM dual UNION ALL
            SELECT 'HGB_USERS' FROM dual UNION ALL
            SELECT 'HGB_WEBHOOK_REPLAY' FROM dual
        )
        SELECT table_name FROM expected_tables
        MINUS
        SELECT table_name FROM user_tables
    );

    SELECT COUNT(*) INTO v_missing_views
    FROM (
        WITH expected_views (view_name) AS (
            SELECT 'HGB_V_FEEDBACK_DASHBOARD' FROM dual UNION ALL
            SELECT 'HGB_V_OPEN_RECOVERY_CASES' FROM dual
        )
        SELECT view_name FROM expected_views
        MINUS
        SELECT view_name FROM user_views
    );

    SELECT COUNT(*) INTO v_missing_versions
    FROM (
        WITH expected_versions (version_code) AS (
            SELECT '2026-07-base-schema' FROM dual UNION ALL
            SELECT '2026-07-referential-integrity' FROM dual UNION ALL
            SELECT '2026-07-production-hardening' FROM dual
        )
        SELECT version_code FROM expected_versions
        MINUS
        SELECT version_code FROM hgb_schema_version
    );

    SELECT COUNT(*) INTO v_invalid_objects
    FROM user_objects
    WHERE object_name LIKE 'HGB_%'
      AND object_type IN ('VIEW', 'PACKAGE', 'PACKAGE BODY', 'PROCEDURE', 'FUNCTION', 'TRIGGER')
      AND status <> 'VALID';

    SELECT COUNT(*) INTO v_fk_count
    FROM user_constraints
    WHERE constraint_type = 'R'
      AND constraint_name LIKE 'FK_HGB_%';

    IF v_missing_tables > 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'HGB verification failed: missing required tables.');
    END IF;

    IF v_missing_views > 0 THEN
        RAISE_APPLICATION_ERROR(-20002, 'HGB verification failed: missing required views.');
    END IF;

    IF v_missing_versions > 0 THEN
        RAISE_APPLICATION_ERROR(-20003, 'HGB verification failed: missing required schema versions.');
    END IF;

    IF v_invalid_objects > 0 THEN
        RAISE_APPLICATION_ERROR(-20004, 'HGB verification failed: invalid HGB database objects.');
    END IF;

    IF v_fk_count < 51 THEN
        RAISE_APPLICATION_ERROR(-20005, 'HGB verification failed: referential-integrity.sql was not fully applied.');
    END IF;
END;
/

PROMPT HGB database verification completed.

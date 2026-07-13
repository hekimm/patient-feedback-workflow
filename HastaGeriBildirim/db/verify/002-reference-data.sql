WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
WHENEVER OSERROR EXIT FAILURE

SET DEFINE OFF;
SET SERVEROUTPUT ON;
SET VERIFY OFF;

PROMPT Reference-data verification started.

DECLARE
    v_missing NUMBER;

    PROCEDURE assert_none_missing(p_label VARCHAR2, p_missing NUMBER) IS
    BEGIN
        IF p_missing > 0 THEN
            RAISE_APPLICATION_ERROR(
                -20120,
                'Missing or invalid reference data: ' || p_label ||
                ' (' || p_missing || ' contract rows)');
        END IF;
    END;
BEGIN
    SELECT COUNT(*) INTO v_missing
    FROM (
        SELECT column_value role_code
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'SYS_ADMIN',
            'QUALITY_MANAGER',
            'UNIT_MANAGER'))
        MINUS
        SELECT role_code
        FROM HGB_ROLES
    );
    assert_none_missing('roles', v_missing);

    SELECT COUNT(*) INTO v_missing
    FROM (
        SELECT column_value permission_name
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'DASHBOARD_VIEW',
            'SURVEY_ADMIN',
            'RECOVERY_MANAGE',
            'COMPLIANCE_MANAGE'))
        MINUS
        SELECT permission_name
        FROM HGB_PERMISSIONS
    );
    assert_none_missing('permissions', v_missing);

    SELECT COUNT(*) INTO v_missing
    FROM (
        SELECT 'SYS_ADMIN' role_code, 'DASHBOARD_VIEW' permission_name FROM dual
        UNION ALL SELECT 'SYS_ADMIN', 'SURVEY_ADMIN' FROM dual
        UNION ALL SELECT 'SYS_ADMIN', 'RECOVERY_MANAGE' FROM dual
        UNION ALL SELECT 'SYS_ADMIN', 'COMPLIANCE_MANAGE' FROM dual
        UNION ALL SELECT 'QUALITY_MANAGER', 'DASHBOARD_VIEW' FROM dual
        UNION ALL SELECT 'QUALITY_MANAGER', 'RECOVERY_MANAGE' FROM dual
        UNION ALL SELECT 'UNIT_MANAGER', 'DASHBOARD_VIEW' FROM dual
        UNION ALL SELECT 'UNIT_MANAGER', 'RECOVERY_MANAGE' FROM dual
        MINUS
        SELECT r.role_code, p.permission_name
        FROM HGB_ROLE_PERMISSIONS rp
        JOIN HGB_ROLES r ON r.role_id = rp.role_id
        JOIN HGB_PERMISSIONS p ON p.permission_id = rp.permission_id
    );
    assert_none_missing('role-permission mappings', v_missing);

    SELECT COUNT(*) INTO v_missing
    FROM (
        SELECT column_value channel_code
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'SMS',
            'WHATSAPP',
            'QR',
            'PORTAL',
            'KIOSK'))
        MINUS
        SELECT channel_code
        FROM HGB_CHANNELS
    );
    assert_none_missing('channels', v_missing);

    SELECT COUNT(*) INTO v_missing
    FROM (
        SELECT column_value system_code
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'PROBEL_HBYS',
            'PROBEL_LBYS_SMS',
            'WHATSAPP_BUSINESS',
            'PROBEL_BI'))
        MINUS
        SELECT system_code
        FROM HGB_INTEGRATION_SYSTEMS
    );
    assert_none_missing('integration systems', v_missing);

    SELECT COUNT(*) INTO v_missing
    FROM HGB_INTEGRATION_SYSTEMS
    WHERE system_code = 'PROBEL_BI'
      AND auth_type = 'BEARER';
    assert_none_missing('PROBEL_BI bearer contract', CASE WHEN v_missing = 1 THEN 0 ELSE 1 END);

    SELECT COUNT(*) INTO v_missing
    FROM HGB_APP_SETTINGS
    WHERE setting_key = 'DEFAULT_TOKEN_TTL_HOURS'
      AND REGEXP_LIKE(setting_value, '^[1-9][0-9]*$');
    assert_none_missing(
        'positive integer DEFAULT_TOKEN_TTL_HOURS',
        CASE WHEN v_missing = 1 THEN 0 ELSE 1 END);

    SELECT COUNT(*) INTO v_missing
    FROM HGB_APP_SETTINGS
    WHERE setting_key = 'DEFAULT_LOW_SCORE_THRESHOLD'
      AND REGEXP_LIKE(setting_value, '^[0-9]+([.,][0-9]+)?$');
    assert_none_missing(
        'numeric DEFAULT_LOW_SCORE_THRESHOLD',
        CASE WHEN v_missing = 1 THEN 0 ELSE 1 END);

    SELECT COUNT(*) INTO v_missing
    FROM (
        SELECT column_value data_category
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'SURVEY_RESPONSE',
            'AUDIT_LOG'))
        MINUS
        SELECT data_category
        FROM HGB_RETENTION_POLICIES
        WHERE retention_days > 0
          AND action_after_retention IN ('ANONYMIZE', 'DELETE')
    );
    assert_none_missing('retention policies', v_missing);

    SELECT COUNT(*) INTO v_missing
    FROM (
        SELECT column_value theme_code
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'WAIT_TIME',
            'STAFF_ATTITUDE',
            'CLEANLINESS',
            'COMMUNICATION',
            'TECHNICAL'))
        MINUS
        SELECT theme_code
        FROM HGB_THEME_CATEGORIES
    );
    assert_none_missing('theme categories', v_missing);

    SELECT COUNT(*) INTO v_missing
    FROM (
        SELECT column_value language_code
        FROM TABLE(SYS.ODCIVARCHAR2LIST('tr', 'en', 'ar'))
        MINUS
        SELECT language_code
        FROM HGB_CONSENT_TEXTS
        WHERE status = 'ACTIVE'
    );
    assert_none_missing('active consent languages', v_missing);

    SELECT COUNT(*) INTO v_missing
    FROM (
        SELECT column_value question_code
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'Q_OVERALL',
            'Q_NPS',
            'Q_NEG_DETAIL'))
        MINUS
        SELECT q.question_code
        FROM HGB_SURVEY_QUESTIONS q
        JOIN HGB_SURVEY_TEMPLATE_VERSIONS v
          ON v.template_version_id = q.template_version_id
        JOIN HGB_SURVEY_TEMPLATES t
          ON t.survey_template_id = v.survey_template_id
        WHERE t.template_code = 'PATIENT_SATISFACTION_DEFAULT'
          AND v.version_no = 1
          AND v.status = 'PUBLISHED'
    );
    assert_none_missing('default survey v1 questions', v_missing);

    SELECT COUNT(*) INTO v_missing
    FROM (
        SELECT column_value event_type
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'DISCHARGE',
            'OUTPATIENT_COMPLETED'))
        MINUS
        SELECT event_type
        FROM HGB_TRIGGER_RULES
    );
    assert_none_missing('trigger rules', v_missing);

    SELECT COUNT(*) INTO v_missing
    FROM (
        SELECT column_value kpi_code
        FROM TABLE(SYS.ODCIVARCHAR2LIST(
            'NPS',
            'CSAT',
            'RESPONSE_RATE'))
        MINUS
        SELECT kpi_code
        FROM HGB_KPI_TARGETS
        WHERE department_id IS NULL
          AND target_value IS NOT NULL
    );
    assert_none_missing('global KPI targets', v_missing);

    SELECT COUNT(*) INTO v_missing
    FROM HGB_CHANNEL_TEMPLATES ct
    JOIN HGB_CHANNELS c ON c.channel_id = ct.channel_id
    WHERE c.channel_code IN ('SMS', 'WHATSAPP', 'QR', 'PORTAL', 'KIOSK')
      AND ct.template_code = 'SURVEY_INVITE'
      AND ct.language_code IN ('tr', 'en', 'ar')
      AND ct.is_active = 1;
    assert_none_missing(
        '15 channel/language templates',
        CASE WHEN v_missing = 15 THEN 0 ELSE ABS(15 - v_missing) END);

    DBMS_OUTPUT.PUT_LINE('reference-data contract ok');
END;
/

PROMPT Reference-data verification completed.

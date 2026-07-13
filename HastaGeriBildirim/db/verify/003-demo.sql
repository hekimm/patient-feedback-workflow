-- Checks mock data only. Do not use this for production approval.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
WHENEVER OSERROR EXIT FAILURE

SET DEFINE OFF;
SET SERVEROUTPUT ON;
SET VERIFY OFF;
SET PAGESIZE 100;
SET LINESIZE 180;

PROMPT Demo-profile verification started.

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*)
    INTO v_count
    FROM (
        SELECT 'admin.demo' username, 'SYS_ADMIN' role_code FROM dual
        UNION ALL SELECT 'kalite.demo', 'QUALITY_MANAGER' FROM dual
        UNION ALL SELECT 'birim.demo', 'UNIT_MANAGER' FROM dual
        MINUS
        SELECT u.username, r.role_code
        FROM HGB_USERS u
        JOIN HGB_USER_ROLES ur ON ur.user_id = u.user_id
        JOIN HGB_ROLES r ON r.role_id = ur.role_id
        WHERE u.status = 'ACTIVE'
    );

    IF v_count > 0 THEN
        RAISE_APPLICATION_ERROR(
            -20130,
            'Demo verification failed: demo user/role mappings are incomplete.');
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM HGB_PATIENTS
    WHERE external_patient_ref = 'P-DEMO-001'
      AND is_deleted = 0;

    IF v_count <> 1 THEN
        RAISE_APPLICATION_ERROR(
            -20131,
            'Demo verification failed: synthetic patient is missing.');
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM HGB_HOSPITALS
    WHERE hospital_name = 'Probel Demo Hastanesi';

    IF v_count <> 1 THEN
        RAISE_APPLICATION_ERROR(
            -20132,
            'Demo verification failed: synthetic organization is missing.');
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM HGB_APP_SETTINGS
    WHERE setting_key = 'PUBLIC_BASE_URL'
      AND setting_value = 'http://localhost:5080';

    IF v_count <> 1 THEN
        RAISE_APPLICATION_ERROR(
            -20133,
            'Demo verification failed: local PUBLIC_BASE_URL is missing.');
    END IF;

    DBMS_OUTPUT.PUT_LINE('demo profile contract ok');
END;
/

COLUMN username FORMAT A24;
COLUMN role_code FORMAT A24;

SELECT u.username, r.role_code
FROM HGB_USERS u
JOIN HGB_USER_ROLES ur ON ur.user_id = u.user_id
JOIN HGB_ROLES r ON r.role_id = ur.role_id
WHERE u.username IN ('admin.demo', 'kalite.demo', 'birim.demo')
ORDER BY u.username;

PROMPT Demo-profile verification completed.

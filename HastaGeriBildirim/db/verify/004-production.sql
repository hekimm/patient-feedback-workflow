-- Run after the schema, environment settings and admin account are ready.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
WHENEVER OSERROR EXIT FAILURE

SET DEFINE OFF;
SET SERVEROUTPUT ON;
SET VERIFY OFF;

PROMPT HGB production database verification started.

DECLARE
    v_admin_count NUMBER;
    v_demo_user_count NUMBER;
    v_unsafe_url_count NUMBER;
BEGIN
    SELECT COUNT(*)
    INTO v_admin_count
    FROM HGB_USERS u
    JOIN HGB_USER_ROLES ur ON ur.user_id = u.user_id
    JOIN HGB_ROLES r ON r.role_id = ur.role_id
    WHERE r.role_code = 'SYS_ADMIN'
      AND u.status = 'ACTIVE';

    SELECT COUNT(*)
    INTO v_demo_user_count
    FROM HGB_USERS
    WHERE username IN ('admin.demo', 'kalite.demo', 'birim.demo');

    SELECT COUNT(*)
    INTO v_unsafe_url_count
    FROM HGB_APP_SETTINGS
    WHERE setting_key = 'PUBLIC_BASE_URL'
       AND (
           setting_value IS NULL
           OR NOT REGEXP_LIKE(
               TRIM(setting_value),
               '^https://[^[:space:]]+$',
               'i')
           OR REGEXP_LIKE(
               LOWER(setting_value),
               'localhost|127\.0\.0\.1|example\.(com|org)|change[-_]?me')
      );

    IF v_admin_count = 0 THEN
        RAISE_APPLICATION_ERROR(
            -20040,
            'Production verification failed: no active SYS_ADMIN role mapping exists.');
    END IF;

    IF v_demo_user_count > 0 THEN
        RAISE_APPLICATION_ERROR(
            -20041,
            'Production verification failed: local demo users exist.');
    END IF;

    IF v_unsafe_url_count > 0 THEN
        RAISE_APPLICATION_ERROR(
            -20042,
            'Production verification failed: PUBLIC_BASE_URL must use HTTPS and cannot be local or placeholder.');
    END IF;

    DBMS_OUTPUT.PUT_LINE(
        'production verification ok: active SYS_ADMIN count=' || v_admin_count);
END;
/

PROMPT HGB production database verification completed.

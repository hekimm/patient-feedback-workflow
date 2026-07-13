-- Run as PATIENT_APP after the reference data scripts.
-- Pass a bcrypt hash, not a plain-text password:
--   @001-create-admin.sql admin "$2a$..." "System Administrator" "admin@example.org"

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
WHENEVER OSERROR EXIT FAILURE

SET VERIFY OFF;
SET DEFINE ON;

DEFINE HGB_ADMIN_USERNAME = '&1'
DEFINE HGB_ADMIN_PASSWORD_HASH = '&2'
DEFINE HGB_ADMIN_FULL_NAME = '&3'
DEFINE HGB_ADMIN_EMAIL = '&4'

SET SERVEROUTPUT ON;

DECLARE
    v_username HGB_USERS.USERNAME%TYPE := LOWER(TRIM(q'~&HGB_ADMIN_USERNAME~'));
    v_password_hash HGB_USERS.PASSWORD_HASH%TYPE := q'~&HGB_ADMIN_PASSWORD_HASH~';
    v_full_name HGB_USERS.FULL_NAME%TYPE := TRIM(q'~&HGB_ADMIN_FULL_NAME~');
    v_email HGB_USERS.EMAIL%TYPE := LOWER(TRIM(q'~&HGB_ADMIN_EMAIL~'));
    v_role_id HGB_ROLES.ROLE_ID%TYPE;
    v_user_id HGB_USERS.USER_ID%TYPE;
BEGIN
    IF NOT REGEXP_LIKE(v_username, '^[a-z0-9._-]{3,100}$') THEN
        RAISE_APPLICATION_ERROR(
            -20030,
            'Admin username must be 3-100 characters: a-z, 0-9, dot, underscore or dash.');
    END IF;

    IF LENGTH(v_password_hash) <> 60
       OR SUBSTR(v_password_hash, 1, 4) NOT IN ('$2a$', '$2b$', '$2y$') THEN
        RAISE_APPLICATION_ERROR(
            -20031,
            'Argument 2 must be a 60-character bcrypt hash, not a plaintext password.');
    END IF;

    IF v_full_name IS NULL THEN
        RAISE_APPLICATION_ERROR(-20032, 'Admin full name cannot be empty.');
    END IF;

    SELECT role_id
    INTO v_role_id
    FROM HGB_ROLES
    WHERE role_code = 'SYS_ADMIN';

    MERGE INTO HGB_USERS t
    USING (
        SELECT v_username username,
               v_password_hash password_hash,
               v_full_name full_name,
               NULLIF(v_email, '') email,
               v_role_id primary_role_id
        FROM dual
    ) s
    ON (t.username = s.username)
    WHEN MATCHED THEN
        UPDATE SET
            t.password_hash = s.password_hash,
            t.full_name = s.full_name,
            t.email = s.email,
            t.primary_role_id = s.primary_role_id,
            t.status = 'ACTIVE',
            t.updated_at = SYSTIMESTAMP
    WHEN NOT MATCHED THEN
        INSERT (
            username,
            password_hash,
            full_name,
            email,
            primary_role_id,
            status)
        VALUES (
            s.username,
            s.password_hash,
            s.full_name,
            s.email,
            s.primary_role_id,
            'ACTIVE');

    SELECT user_id
    INTO v_user_id
    FROM HGB_USERS
    WHERE username = v_username;

    MERGE INTO HGB_USER_ROLES t
    USING (
        SELECT v_user_id user_id, v_role_id role_id
        FROM dual
    ) s
    ON (t.user_id = s.user_id AND t.role_id = s.role_id)
    WHEN NOT MATCHED THEN
        INSERT (user_id, role_id)
        VALUES (s.user_id, s.role_id);

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('SYS_ADMIN bootstrap completed for ' || v_username);
END;
/

UNDEFINE HGB_ADMIN_USERNAME
UNDEFINE HGB_ADMIN_PASSWORD_HASH
UNDEFINE HGB_ADMIN_FULL_NAME
UNDEFINE HGB_ADMIN_EMAIL

-- Run as PATIENT_APP, not SYS or SYSTEM.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
WHENEVER OSERROR EXIT FAILURE

SET DEFINE OFF;
SET SERVEROUTPUT ON;
SET VERIFY OFF;

PROMPT Step 000 - database preflight started.

DECLARE
    v_default_tablespace USER_USERS.DEFAULT_TABLESPACE%TYPE;
    v_character_set NLS_DATABASE_PARAMETERS.VALUE%TYPE;
    v_container_name VARCHAR2(128);
    v_has_quota NUMBER;
    v_missing_privileges VARCHAR2(4000);
BEGIN
    IF UPPER(USER) <> 'PATIENT_APP' THEN
        RAISE_APPLICATION_ERROR(
            -20010,
            'Connect as PATIENT_APP; current user is ' || USER || '.');
    END IF;

    IF DBMS_DB_VERSION.VERSION < 12 THEN
        RAISE_APPLICATION_ERROR(
            -20011,
            'Oracle 12c or newer is required because the schema uses identity columns.');
    END IF;

    v_container_name := SYS_CONTEXT('USERENV', 'CON_NAME');
    IF v_container_name = 'CDB$ROOT' THEN
        RAISE_APPLICATION_ERROR(
            -20014,
            'Connect to the target PDB/service, not to CDB$ROOT.');
    END IF;

    SELECT value
    INTO v_character_set
    FROM nls_database_parameters
    WHERE parameter = 'NLS_CHARACTERSET';

    IF v_character_set <> 'AL32UTF8' THEN
        RAISE_APPLICATION_ERROR(
            -20015,
            'AL32UTF8 database character set is required; found ' ||
            v_character_set || '.');
    END IF;

    SELECT LISTAGG(required_privilege, ', ')
               WITHIN GROUP (ORDER BY required_privilege)
    INTO v_missing_privileges
    FROM (
        SELECT 'CREATE TABLE' required_privilege FROM dual
        UNION ALL
        SELECT 'CREATE SEQUENCE' FROM dual
        UNION ALL
        SELECT 'CREATE VIEW' FROM dual
    ) required
    WHERE NOT EXISTS (
        SELECT 1
        FROM session_privs granted
        WHERE granted.privilege = required.required_privilege
    );

    IF v_missing_privileges IS NOT NULL THEN
        RAISE_APPLICATION_ERROR(
            -20012,
            'Missing schema privileges: ' || v_missing_privileges);
    END IF;

    SELECT default_tablespace
    INTO v_default_tablespace
    FROM user_users;

    SELECT COUNT(*)
    INTO v_has_quota
    FROM dual
    WHERE EXISTS (
        SELECT 1
        FROM session_privs
        WHERE privilege = 'UNLIMITED TABLESPACE'
    )
       OR EXISTS (
        SELECT 1
        FROM user_ts_quotas
        WHERE tablespace_name = v_default_tablespace
          AND (max_bytes = -1 OR max_bytes > bytes)
    );

    IF v_has_quota = 0 THEN
        RAISE_APPLICATION_ERROR(
            -20013,
            'No writable quota is available on default tablespace ' ||
            v_default_tablespace || '.');
    END IF;

    DBMS_OUTPUT.PUT_LINE(
        'preflight ok: user=' || USER ||
        ', oracle=' || DBMS_DB_VERSION.VERSION || '.' || DBMS_DB_VERSION.RELEASE ||
        ', container=' || NVL(v_container_name, 'non-CDB') ||
        ', charset=' || v_character_set ||
        ', tablespace=' || v_default_tablespace);
END;
/

PROMPT Step 000 - database preflight completed.

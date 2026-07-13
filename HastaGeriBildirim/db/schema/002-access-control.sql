-- Run as the application schema owner. This script can be run again safely.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
WHENEVER OSERROR EXIT FAILURE

SET DEFINE OFF;
SET SERVEROUTPUT ON;
SET VERIFY OFF;

PROMPT Step 002 - access-control tables started.

DECLARE
    PROCEDURE create_table_if_missing(p_table_name VARCHAR2, p_sql CLOB) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count
        FROM user_tables
        WHERE table_name = UPPER(p_table_name);

        IF v_count = 0 THEN
            EXECUTE IMMEDIATE p_sql;
            DBMS_OUTPUT.PUT_LINE('created table ' || p_table_name);
        END IF;
    END;
BEGIN
    create_table_if_missing('HGB_ROLES', q'[
        CREATE TABLE HGB_ROLES (
            ROLE_ID NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            ROLE_CODE VARCHAR2(50) UNIQUE NOT NULL,
            ROLE_NAME NVARCHAR2(150) NOT NULL,
            DESCRIPTION NVARCHAR2(500),
            CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP
        )]');

    create_table_if_missing('HGB_PERMISSIONS', q'[
        CREATE TABLE HGB_PERMISSIONS (
            PERMISSION_ID NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            PERMISSION_NAME VARCHAR2(100) UNIQUE NOT NULL,
            MODULE_NAME VARCHAR2(100) NOT NULL,
            CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP
        )]');

    create_table_if_missing('HGB_ROLE_PERMISSIONS', q'[
        CREATE TABLE HGB_ROLE_PERMISSIONS (
            ROLE_ID NUMBER NOT NULL,
            PERMISSION_ID NUMBER NOT NULL,
            PRIMARY KEY (ROLE_ID, PERMISSION_ID)
        )]');

    create_table_if_missing('HGB_USERS', q'[
        CREATE TABLE HGB_USERS (
            USER_ID NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            USERNAME VARCHAR2(100) UNIQUE NOT NULL,
            PASSWORD_HASH VARCHAR2(255) NOT NULL,
            FULL_NAME NVARCHAR2(200) NOT NULL,
            EMAIL VARCHAR2(200),
            PRIMARY_ROLE_ID NUMBER,
            STATUS VARCHAR2(20) DEFAULT 'ACTIVE',
            CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
            UPDATED_AT TIMESTAMP
        )]');

    create_table_if_missing('HGB_USER_ROLES', q'[
        CREATE TABLE HGB_USER_ROLES (
            USER_ID NUMBER NOT NULL,
            ROLE_ID NUMBER NOT NULL,
            PRIMARY KEY (USER_ID, ROLE_ID)
        )]');

    create_table_if_missing('HGB_USER_SCOPES', q'[
        CREATE TABLE HGB_USER_SCOPES (
            USER_SCOPE_ID NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            USER_ID NUMBER NOT NULL,
            SCOPE_TYPE VARCHAR2(30) NOT NULL,
            SCOPE_ID NUMBER NOT NULL,
            IS_ACTIVE NUMBER(1) DEFAULT 1,
            CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP
        )]');

END;
/

MERGE INTO HGB_SCHEMA_VERSION t
USING (
    SELECT '2026-07-schema-02-access' VERSION_CODE, 'Roles, users, permissions and scopes' DESCRIPTION
    FROM dual
) s
ON (t.VERSION_CODE = s.VERSION_CODE)
WHEN NOT MATCHED THEN
    INSERT (VERSION_CODE, DESCRIPTION)
    VALUES (s.VERSION_CODE, s.DESCRIPTION);

COMMIT;

PROMPT Step 002 - access-control tables completed.

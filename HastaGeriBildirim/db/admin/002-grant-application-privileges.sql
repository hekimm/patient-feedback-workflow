-- Run as SYSTEM or an account that can grant privileges to PATIENT_APP.
-- Usage: @002-grant-application-privileges.sql USERS 500M
-- Quota can be UNLIMITED or a positive K/M/G/T value.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
WHENEVER OSERROR EXIT FAILURE

SET VERIFY OFF;
SET DEFINE ON;

DEFINE HGB_TABLESPACE = '&1'
DEFINE HGB_QUOTA = '&2'

SET SERVEROUTPUT ON;

GRANT CREATE SESSION TO patient_app;
GRANT CREATE TABLE TO patient_app;
GRANT CREATE SEQUENCE TO patient_app;
GRANT CREATE VIEW TO patient_app;

DECLARE
    v_tablespace VARCHAR2(128);
    v_quota VARCHAR2(30);
BEGIN
    v_tablespace := DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER('&HGB_TABLESPACE'));
    v_quota := UPPER(TRIM('&HGB_QUOTA'));

    IF NOT REGEXP_LIKE(v_quota, '^(UNLIMITED|[1-9][0-9]*(K|M|G|T))$') THEN
        RAISE_APPLICATION_ERROR(
            -20021,
            'Quota must be UNLIMITED or a positive K/M/G/T size, for example 500M.');
    END IF;

    EXECUTE IMMEDIATE
        'ALTER USER patient_app DEFAULT TABLESPACE ' || v_tablespace;
    EXECUTE IMMEDIATE
        'ALTER USER patient_app QUOTA ' || v_quota || ' ON ' || v_tablespace;

    DBMS_OUTPUT.PUT_LINE(
        'granted install privileges and ' || v_quota ||
        ' quota on ' || v_tablespace);
END;
/

SELECT username, account_status, default_tablespace
FROM dba_users
WHERE username = 'PATIENT_APP';

PROMPT Admin step 002 - PATIENT_APP privileges are ready.

UNDEFINE HGB_TABLESPACE
UNDEFINE HGB_QUOTA

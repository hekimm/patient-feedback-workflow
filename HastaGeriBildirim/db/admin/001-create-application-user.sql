-- Run as SYSTEM or an account that can manage Oracle users.
-- Usage: @001-create-application-user.sql "StrongPasswordHere"

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
WHENEVER OSERROR EXIT FAILURE

SET VERIFY OFF;
SET DEFINE ON;

DEFINE PATIENT_APP_PASSWORD = '&1'

SET SERVEROUTPUT ON;

DECLARE
    v_count NUMBER;
BEGIN
    IF LENGTH('&PATIENT_APP_PASSWORD') = 0 THEN
        RAISE_APPLICATION_ERROR(-20020, 'The PATIENT_APP password cannot be empty.');
    END IF;

    SELECT COUNT(*)
    INTO v_count
    FROM dba_users
    WHERE username = 'PATIENT_APP';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE
            'CREATE USER patient_app IDENTIFIED BY "&PATIENT_APP_PASSWORD"';
        DBMS_OUTPUT.PUT_LINE('created user PATIENT_APP');
    ELSE
        EXECUTE IMMEDIATE
            'ALTER USER patient_app IDENTIFIED BY "&PATIENT_APP_PASSWORD" ACCOUNT UNLOCK';
        DBMS_OUTPUT.PUT_LINE('updated and unlocked user PATIENT_APP');
    END IF;
END;
/

PROMPT Admin step 001 - PATIENT_APP user is available.

UNDEFINE PATIENT_APP_PASSWORD

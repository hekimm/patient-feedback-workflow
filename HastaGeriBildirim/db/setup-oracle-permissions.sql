-- Run as SYSTEM/SYSDBA. Pass the application user password as argument:
--   sqlplus system/<password>@//host:1521/FREEPDB1 @setup-oracle-permissions.sql "StrongPasswordHere"
-- Creates patient_app when missing, otherwise resets the password and unlocks it.

DEFINE PATIENT_APP_PASSWORD = '&1'

WHENEVER SQLERROR EXIT FAILURE ROLLBACK

SET VERIFY OFF

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM dba_users WHERE username = 'PATIENT_APP';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE USER patient_app IDENTIFIED BY "&PATIENT_APP_PASSWORD"';
    ELSE
        EXECUTE IMMEDIATE 'ALTER USER patient_app IDENTIFIED BY "&PATIENT_APP_PASSWORD" ACCOUNT UNLOCK';
    END IF;
END;
/

GRANT CREATE SESSION TO patient_app;
GRANT CONNECT TO patient_app;
GRANT RESOURCE TO patient_app;

GRANT CREATE TABLE TO patient_app;
GRANT CREATE SEQUENCE TO patient_app;
GRANT CREATE VIEW TO patient_app;
GRANT CREATE PROCEDURE TO patient_app;
GRANT CREATE TRIGGER TO patient_app;
GRANT CREATE SYNONYM TO patient_app;

ALTER USER patient_app QUOTA UNLIMITED ON USERS;

COMMIT;

SELECT username, account_status, default_tablespace
FROM dba_users
WHERE username = 'PATIENT_APP';

PROMPT patient_app setup completed.

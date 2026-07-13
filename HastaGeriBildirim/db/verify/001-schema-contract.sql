WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
WHENEVER OSERROR EXIT FAILURE

SET DEFINE OFF;
SET SERVEROUTPUT ON;
SET VERIFY OFF;
SET FEEDBACK ON;
SET PAGESIZE 200;
SET LINESIZE 220;

PROMPT Core schema verification started.

COLUMN table_count FORMAT 999;
COLUMN view_count FORMAT 999;
COLUMN index_count FORMAT 999;
COLUMN fk_count FORMAT 999;

SELECT COUNT(*) AS table_count
FROM user_tables
WHERE table_name LIKE 'HGB\_%' ESCAPE '\';

SELECT COUNT(*) AS view_count
FROM user_views
WHERE view_name LIKE 'HGB\_%' ESCAPE '\';

SELECT COUNT(*) AS index_count
FROM user_indexes
WHERE index_name LIKE 'IX\_HGB\_%' ESCAPE '\'
   OR index_name LIKE 'UX\_HGB\_%' ESCAPE '\';

SELECT COUNT(*) AS fk_count
FROM user_constraints
WHERE constraint_name LIKE 'FK\_HGB\_%' ESCAPE '\'
  AND constraint_type = 'R'
  AND status = 'ENABLED'
  AND validated = 'VALIDATED';

DECLARE
    v_nullable USER_TAB_COLUMNS.NULLABLE%TYPE;

    PROCEDURE fail(p_message VARCHAR2) IS
    BEGIN
        RAISE_APPLICATION_ERROR(-20100, p_message);
    END;

    PROCEDURE check_table(p_table VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count
        FROM user_tables
        WHERE table_name = p_table;

        IF v_count <> 1 THEN
            fail('Missing required table: ' || p_table);
        END IF;
    END;

    PROCEDURE check_primary_key(p_table VARCHAR2, p_columns VARCHAR2) IS
        v_constraint_count NUMBER;
        v_columns VARCHAR2(4000);
    BEGIN
        SELECT COUNT(DISTINCT constraint_name),
               LISTAGG(column_name, ',') WITHIN GROUP (ORDER BY position)
        INTO v_constraint_count, v_columns
        FROM (
            SELECT c.constraint_name, cc.column_name, cc.position
            FROM user_constraints c
            JOIN user_cons_columns cc
              ON cc.constraint_name = c.constraint_name
            WHERE c.table_name = p_table
              AND c.constraint_type = 'P'
              AND c.status = 'ENABLED'
              AND c.validated = 'VALIDATED'
        );

        IF v_constraint_count <> 1 OR v_columns <> p_columns THEN
            fail('Primary-key contract mismatch: ' || p_table);
        END IF;
    END;

    PROCEDURE check_unique_constraint(p_table VARCHAR2, p_columns VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*)
        INTO v_count
        FROM (
            SELECT c.constraint_name,
                   LISTAGG(cc.column_name, ',')
                       WITHIN GROUP (ORDER BY cc.position) columns_csv
            FROM user_constraints c
            JOIN user_cons_columns cc
              ON cc.constraint_name = c.constraint_name
            WHERE c.table_name = p_table
              AND c.constraint_type = 'U'
              AND c.status = 'ENABLED'
              AND c.validated = 'VALIDATED'
            GROUP BY c.constraint_name
        )
        WHERE columns_csv = p_columns;

        IF v_count <> 1 THEN
            fail('Unique-constraint contract mismatch: ' || p_table || '(' || p_columns || ')');
        END IF;
    END;

    PROCEDURE check_identity(p_table VARCHAR2, p_column VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count
        FROM user_tab_identity_cols
        WHERE table_name = p_table
          AND column_name = p_column;

        IF v_count <> 1 THEN
            fail('Identity-column contract mismatch: ' || p_table || '.' || p_column);
        END IF;
    END;

    PROCEDURE check_column(p_table VARCHAR2, p_column VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count
        FROM user_tab_columns
        WHERE table_name = p_table
          AND column_name = p_column;

        IF v_count <> 1 THEN
            fail('Missing required column: ' || p_table || '.' || p_column);
        END IF;
    END;

    PROCEDURE check_view(p_view VARCHAR2) IS
        v_status USER_OBJECTS.STATUS%TYPE;
    BEGIN
        SELECT status INTO v_status
        FROM user_objects
        WHERE object_name = p_view
          AND object_type = 'VIEW';

        IF v_status <> 'VALID' THEN
            fail('Invalid required view: ' || p_view);
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            fail('Missing required view: ' || p_view);
    END;

    PROCEDURE check_view_columns(p_view VARCHAR2, p_columns VARCHAR2) IS
        v_columns VARCHAR2(4000);
    BEGIN
        SELECT LISTAGG(column_name, ',') WITHIN GROUP (ORDER BY column_id)
        INTO v_columns
        FROM user_tab_columns
        WHERE table_name = p_view;

        IF v_columns IS NULL OR v_columns <> p_columns THEN
            fail('View-column contract mismatch: ' || p_view);
        END IF;
    END;

    PROCEDURE check_index(
        p_index VARCHAR2,
        p_table VARCHAR2,
        p_columns VARCHAR2,
        p_uniqueness VARCHAR2) IS
        v_table USER_INDEXES.TABLE_NAME%TYPE;
        v_uniqueness USER_INDEXES.UNIQUENESS%TYPE;
        v_status USER_INDEXES.STATUS%TYPE;
        v_columns VARCHAR2(4000);
    BEGIN
        SELECT table_name, uniqueness, status
        INTO v_table, v_uniqueness, v_status
        FROM user_indexes
        WHERE index_name = p_index;

        SELECT LISTAGG(column_name, ',') WITHIN GROUP (ORDER BY column_position)
        INTO v_columns
        FROM user_ind_columns
        WHERE index_name = p_index;

        IF v_table <> p_table
           OR v_columns <> p_columns
           OR v_uniqueness <> p_uniqueness
           OR v_status <> 'VALID' THEN
            fail('Index contract mismatch: ' || p_index);
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            fail('Missing required index: ' || p_index);
    END;

    PROCEDURE check_fk(
        p_constraint VARCHAR2,
        p_table VARCHAR2,
        p_column VARCHAR2,
        p_parent_table VARCHAR2,
        p_parent_column VARCHAR2,
        p_delete_rule VARCHAR2) IS
        v_table USER_CONSTRAINTS.TABLE_NAME%TYPE;
        v_column USER_CONS_COLUMNS.COLUMN_NAME%TYPE;
        v_parent_table USER_CONSTRAINTS.TABLE_NAME%TYPE;
        v_parent_column USER_CONS_COLUMNS.COLUMN_NAME%TYPE;
        v_delete_rule USER_CONSTRAINTS.DELETE_RULE%TYPE;
        v_status USER_CONSTRAINTS.STATUS%TYPE;
        v_validated USER_CONSTRAINTS.VALIDATED%TYPE;
    BEGIN
        SELECT child.table_name,
               child_col.column_name,
               parent.table_name,
               parent_col.column_name,
               child.delete_rule,
               child.status,
               child.validated
        INTO v_table,
             v_column,
             v_parent_table,
             v_parent_column,
             v_delete_rule,
             v_status,
             v_validated
        FROM user_constraints child
        JOIN user_cons_columns child_col
          ON child_col.constraint_name = child.constraint_name
        JOIN user_constraints parent
          ON parent.constraint_name = child.r_constraint_name
        JOIN user_cons_columns parent_col
          ON parent_col.constraint_name = parent.constraint_name
         AND parent_col.position = child_col.position
        WHERE child.constraint_name = p_constraint
          AND child.constraint_type = 'R';

        IF v_table <> p_table
           OR v_column <> p_column
           OR v_parent_table <> p_parent_table
           OR v_parent_column <> p_parent_column
           OR v_delete_rule <> p_delete_rule
           OR v_status <> 'ENABLED'
           OR v_validated <> 'VALIDATED' THEN
            fail('Foreign-key contract mismatch: ' || p_constraint);
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            fail('Missing required foreign key: ' || p_constraint);
    END;

    PROCEDURE check_version(p_version VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count
        FROM HGB_SCHEMA_VERSION
        WHERE version_code = p_version;

        IF v_count <> 1 THEN
            fail('Missing required module version: ' || p_version);
        END IF;
    END;
BEGIN
    check_table('HGB_HOSPITALS');
    check_table('HGB_BRANCHES');
    check_table('HGB_DEPARTMENTS');
    check_table('HGB_DOCTORS');
    check_table('HGB_SERVICES');
    check_table('HGB_PATIENTS');
    check_table('HGB_SCHEMA_VERSION');
    check_table('HGB_ROLES');
    check_table('HGB_PERMISSIONS');
    check_table('HGB_ROLE_PERMISSIONS');
    check_table('HGB_USERS');
    check_table('HGB_USER_ROLES');
    check_table('HGB_USER_SCOPES');
    check_table('HGB_SURVEY_TEMPLATES');
    check_table('HGB_SURVEY_TEMPLATE_VERSIONS');
    check_table('HGB_SURVEY_QUESTIONS');
    check_table('HGB_SURVEY_OPTIONS');
    check_table('HGB_BRANCHING_RULES');
    check_table('HGB_CHANNELS');
    check_table('HGB_CHANNEL_TEMPLATES');
    check_table('HGB_TRIGGER_RULES');
    check_table('HGB_CLINICAL_EVENTS');
    check_table('HGB_EVENT_QUEUE');
    check_table('HGB_SURVEY_INVITATIONS');
    check_table('HGB_DELIVERY_ATTEMPTS');
    check_table('HGB_CONSENT_TEXTS');
    check_table('HGB_CONSENT_RECORDS');
    check_table('HGB_SURVEY_RESPONSES');
    check_table('HGB_SURVEY_ANSWERS');
    check_table('HGB_ALERTS');
    check_table('HGB_SERVICE_RECOVERY_CASES');
    check_table('HGB_RECOVERY_ACTIONS');
    check_table('HGB_CASE_ESCALATIONS');
    check_table('HGB_AUDIT_LOGS');
    check_table('HGB_WEBHOOK_REPLAY');
    check_table('HGB_INTEGRATION_SYSTEMS');
    check_table('HGB_INTEGRATION_LOGS');
    check_table('HGB_APP_SETTINGS');
    check_table('HGB_KPI_TARGETS');
    check_table('HGB_REPORT_EXPORTS');
    check_table('HGB_RETENTION_POLICIES');
    check_table('HGB_BI_EXPORT_QUEUE');
    check_table('HGB_SENTIMENT_RESULTS');
    check_table('HGB_THEME_CATEGORIES');
    check_table('HGB_RESPONSE_THEME_MATCHES');
    check_table('HGB_DATA_SUBJECT_REQUESTS');

    check_primary_key('HGB_HOSPITALS', 'HOSPITAL_ID');
    check_primary_key('HGB_BRANCHES', 'BRANCH_ID');
    check_primary_key('HGB_DEPARTMENTS', 'DEPARTMENT_ID');
    check_primary_key('HGB_DOCTORS', 'DOCTOR_ID');
    check_primary_key('HGB_SERVICES', 'SERVICE_ID');
    check_primary_key('HGB_PATIENTS', 'PATIENT_ID');
    check_primary_key('HGB_SCHEMA_VERSION', 'VERSION_ID');
    check_primary_key('HGB_ROLES', 'ROLE_ID');
    check_primary_key('HGB_PERMISSIONS', 'PERMISSION_ID');
    check_primary_key('HGB_ROLE_PERMISSIONS', 'ROLE_ID,PERMISSION_ID');
    check_primary_key('HGB_USERS', 'USER_ID');
    check_primary_key('HGB_USER_ROLES', 'USER_ID,ROLE_ID');
    check_primary_key('HGB_USER_SCOPES', 'USER_SCOPE_ID');
    check_primary_key('HGB_SURVEY_TEMPLATES', 'SURVEY_TEMPLATE_ID');
    check_primary_key('HGB_SURVEY_TEMPLATE_VERSIONS', 'TEMPLATE_VERSION_ID');
    check_primary_key('HGB_SURVEY_QUESTIONS', 'QUESTION_ID');
    check_primary_key('HGB_SURVEY_OPTIONS', 'OPTION_ID');
    check_primary_key('HGB_BRANCHING_RULES', 'BRANCHING_RULE_ID');
    check_primary_key('HGB_CHANNELS', 'CHANNEL_ID');
    check_primary_key('HGB_CHANNEL_TEMPLATES', 'CHANNEL_TEMPLATE_ID');
    check_primary_key('HGB_TRIGGER_RULES', 'TRIGGER_RULE_ID');
    check_primary_key('HGB_CLINICAL_EVENTS', 'CLINICAL_EVENT_ID');
    check_primary_key('HGB_EVENT_QUEUE', 'QUEUE_ID');
    check_primary_key('HGB_SURVEY_INVITATIONS', 'INVITATION_ID');
    check_primary_key('HGB_DELIVERY_ATTEMPTS', 'DELIVERY_ATTEMPT_ID');
    check_primary_key('HGB_CONSENT_TEXTS', 'CONSENT_TEXT_ID');
    check_primary_key('HGB_CONSENT_RECORDS', 'CONSENT_RECORD_ID');
    check_primary_key('HGB_SURVEY_RESPONSES', 'RESPONSE_ID');
    check_primary_key('HGB_SURVEY_ANSWERS', 'ANSWER_ID');
    check_primary_key('HGB_ALERTS', 'ALERT_ID');
    check_primary_key('HGB_SERVICE_RECOVERY_CASES', 'RECOVERY_CASE_ID');
    check_primary_key('HGB_RECOVERY_ACTIONS', 'RECOVERY_ACTION_ID');
    check_primary_key('HGB_CASE_ESCALATIONS', 'CASE_ESCALATION_ID');
    check_primary_key('HGB_AUDIT_LOGS', 'AUDIT_LOG_ID');
    check_primary_key('HGB_WEBHOOK_REPLAY', 'WEBHOOK_REPLAY_ID');
    check_primary_key('HGB_INTEGRATION_SYSTEMS', 'INTEGRATION_SYSTEM_ID');
    check_primary_key('HGB_INTEGRATION_LOGS', 'INTEGRATION_LOG_ID');
    check_primary_key('HGB_APP_SETTINGS', 'SETTING_ID');
    check_primary_key('HGB_KPI_TARGETS', 'KPI_TARGET_ID');
    check_primary_key('HGB_REPORT_EXPORTS', 'REPORT_EXPORT_ID');
    check_primary_key('HGB_RETENTION_POLICIES', 'RETENTION_POLICY_ID');
    check_primary_key('HGB_BI_EXPORT_QUEUE', 'BI_EXPORT_ID');
    check_primary_key('HGB_SENTIMENT_RESULTS', 'SENTIMENT_RESULT_ID');
    check_primary_key('HGB_THEME_CATEGORIES', 'THEME_CATEGORY_ID');
    check_primary_key('HGB_RESPONSE_THEME_MATCHES', 'RESPONSE_THEME_MATCH_ID');
    check_primary_key('HGB_DATA_SUBJECT_REQUESTS', 'DSR_ID');

    check_identity('HGB_HOSPITALS', 'HOSPITAL_ID');
    check_identity('HGB_BRANCHES', 'BRANCH_ID');
    check_identity('HGB_DEPARTMENTS', 'DEPARTMENT_ID');
    check_identity('HGB_DOCTORS', 'DOCTOR_ID');
    check_identity('HGB_SERVICES', 'SERVICE_ID');
    check_identity('HGB_PATIENTS', 'PATIENT_ID');
    check_identity('HGB_SCHEMA_VERSION', 'VERSION_ID');
    check_identity('HGB_ROLES', 'ROLE_ID');
    check_identity('HGB_PERMISSIONS', 'PERMISSION_ID');
    check_identity('HGB_USERS', 'USER_ID');
    check_identity('HGB_USER_SCOPES', 'USER_SCOPE_ID');
    check_identity('HGB_SURVEY_TEMPLATES', 'SURVEY_TEMPLATE_ID');
    check_identity('HGB_SURVEY_TEMPLATE_VERSIONS', 'TEMPLATE_VERSION_ID');
    check_identity('HGB_SURVEY_QUESTIONS', 'QUESTION_ID');
    check_identity('HGB_SURVEY_OPTIONS', 'OPTION_ID');
    check_identity('HGB_BRANCHING_RULES', 'BRANCHING_RULE_ID');
    check_identity('HGB_CHANNELS', 'CHANNEL_ID');
    check_identity('HGB_CHANNEL_TEMPLATES', 'CHANNEL_TEMPLATE_ID');
    check_identity('HGB_TRIGGER_RULES', 'TRIGGER_RULE_ID');
    check_identity('HGB_CLINICAL_EVENTS', 'CLINICAL_EVENT_ID');
    check_identity('HGB_EVENT_QUEUE', 'QUEUE_ID');
    check_identity('HGB_SURVEY_INVITATIONS', 'INVITATION_ID');
    check_identity('HGB_DELIVERY_ATTEMPTS', 'DELIVERY_ATTEMPT_ID');
    check_identity('HGB_CONSENT_TEXTS', 'CONSENT_TEXT_ID');
    check_identity('HGB_CONSENT_RECORDS', 'CONSENT_RECORD_ID');
    check_identity('HGB_SURVEY_RESPONSES', 'RESPONSE_ID');
    check_identity('HGB_SURVEY_ANSWERS', 'ANSWER_ID');
    check_identity('HGB_ALERTS', 'ALERT_ID');
    check_identity('HGB_SERVICE_RECOVERY_CASES', 'RECOVERY_CASE_ID');
    check_identity('HGB_RECOVERY_ACTIONS', 'RECOVERY_ACTION_ID');
    check_identity('HGB_CASE_ESCALATIONS', 'CASE_ESCALATION_ID');
    check_identity('HGB_AUDIT_LOGS', 'AUDIT_LOG_ID');
    check_identity('HGB_WEBHOOK_REPLAY', 'WEBHOOK_REPLAY_ID');
    check_identity('HGB_INTEGRATION_SYSTEMS', 'INTEGRATION_SYSTEM_ID');
    check_identity('HGB_INTEGRATION_LOGS', 'INTEGRATION_LOG_ID');
    check_identity('HGB_APP_SETTINGS', 'SETTING_ID');
    check_identity('HGB_KPI_TARGETS', 'KPI_TARGET_ID');
    check_identity('HGB_REPORT_EXPORTS', 'REPORT_EXPORT_ID');
    check_identity('HGB_RETENTION_POLICIES', 'RETENTION_POLICY_ID');
    check_identity('HGB_BI_EXPORT_QUEUE', 'BI_EXPORT_ID');
    check_identity('HGB_SENTIMENT_RESULTS', 'SENTIMENT_RESULT_ID');
    check_identity('HGB_THEME_CATEGORIES', 'THEME_CATEGORY_ID');
    check_identity('HGB_RESPONSE_THEME_MATCHES', 'RESPONSE_THEME_MATCH_ID');
    check_identity('HGB_DATA_SUBJECT_REQUESTS', 'DSR_ID');

    check_unique_constraint('HGB_ROLES', 'ROLE_CODE');
    check_unique_constraint('HGB_PERMISSIONS', 'PERMISSION_NAME');
    check_unique_constraint('HGB_USERS', 'USERNAME');
    check_unique_constraint('HGB_PATIENTS', 'EXTERNAL_PATIENT_REF');
    check_unique_constraint('HGB_SURVEY_TEMPLATES', 'TEMPLATE_CODE');
    check_unique_constraint('HGB_CHANNELS', 'CHANNEL_CODE');
    check_unique_constraint('HGB_SCHEMA_VERSION', 'VERSION_CODE');
    check_unique_constraint('HGB_INTEGRATION_SYSTEMS', 'SYSTEM_CODE');
    check_unique_constraint('HGB_APP_SETTINGS', 'SETTING_KEY');
    check_unique_constraint('HGB_RETENTION_POLICIES', 'DATA_CATEGORY');
    check_unique_constraint('HGB_THEME_CATEGORIES', 'THEME_CODE');

    check_column('HGB_HOSPITALS', 'HOSPITAL_ID');
    check_column('HGB_HOSPITALS', 'HOSPITAL_NAME');
    check_column('HGB_HOSPITALS', 'STATUS');
    check_column('HGB_HOSPITALS', 'CREATED_AT');
    check_column('HGB_BRANCHES', 'BRANCH_ID');
    check_column('HGB_BRANCHES', 'HOSPITAL_ID');
    check_column('HGB_BRANCHES', 'BRANCH_NAME');
    check_column('HGB_BRANCHES', 'STATUS');
    check_column('HGB_BRANCHES', 'CREATED_AT');
    check_column('HGB_DEPARTMENTS', 'DEPARTMENT_ID');
    check_column('HGB_DEPARTMENTS', 'BRANCH_ID');
    check_column('HGB_DEPARTMENTS', 'DEPARTMENT_NAME');
    check_column('HGB_DEPARTMENTS', 'STATUS');
    check_column('HGB_DEPARTMENTS', 'CREATED_AT');
    check_column('HGB_DOCTORS', 'DOCTOR_ID');
    check_column('HGB_DOCTORS', 'FULL_NAME');
    check_column('HGB_DOCTORS', 'STATUS');
    check_column('HGB_DOCTORS', 'CREATED_AT');
    check_column('HGB_SERVICES', 'SERVICE_ID');
    check_column('HGB_SERVICES', 'SERVICE_NAME');
    check_column('HGB_SERVICES', 'STATUS');
    check_column('HGB_SERVICES', 'CREATED_AT');
    check_column('HGB_PATIENTS', 'PATIENT_ID');
    check_column('HGB_PATIENTS', 'EXTERNAL_PATIENT_REF');
    check_column('HGB_PATIENTS', 'PSEUDONYM_CODE');
    check_column('HGB_PATIENTS', 'FULL_NAME');
    check_column('HGB_PATIENTS', 'PHONE');
    check_column('HGB_PATIENTS', 'PHONE_ENC');
    check_column('HGB_PATIENTS', 'PHONE_HASH');
    check_column('HGB_PATIENTS', 'EMAIL');
    check_column('HGB_PATIENTS', 'EMAIL_ENC');
    check_column('HGB_PATIENTS', 'EMAIL_HASH');
    check_column('HGB_PATIENTS', 'PREFERRED_LANGUAGE');
    check_column('HGB_PATIENTS', 'ALLOW_CONTACT');
    check_column('HGB_PATIENTS', 'IS_DELETED');
    check_column('HGB_PATIENTS', 'CREATED_AT');
    check_column('HGB_PATIENTS', 'UPDATED_AT');
    check_column('HGB_SCHEMA_VERSION', 'VERSION_ID');
    check_column('HGB_SCHEMA_VERSION', 'VERSION_CODE');
    check_column('HGB_SCHEMA_VERSION', 'DESCRIPTION');
    check_column('HGB_SCHEMA_VERSION', 'APPLIED_AT');
    check_column('HGB_ROLES', 'ROLE_ID');
    check_column('HGB_ROLES', 'ROLE_CODE');
    check_column('HGB_ROLES', 'ROLE_NAME');
    check_column('HGB_ROLES', 'DESCRIPTION');
    check_column('HGB_ROLES', 'CREATED_AT');
    check_column('HGB_PERMISSIONS', 'PERMISSION_ID');
    check_column('HGB_PERMISSIONS', 'PERMISSION_NAME');
    check_column('HGB_PERMISSIONS', 'MODULE_NAME');
    check_column('HGB_PERMISSIONS', 'CREATED_AT');
    check_column('HGB_ROLE_PERMISSIONS', 'ROLE_ID');
    check_column('HGB_ROLE_PERMISSIONS', 'PERMISSION_ID');
    check_column('HGB_USERS', 'USER_ID');
    check_column('HGB_USERS', 'USERNAME');
    check_column('HGB_USERS', 'PASSWORD_HASH');
    check_column('HGB_USERS', 'FULL_NAME');
    check_column('HGB_USERS', 'EMAIL');
    check_column('HGB_USERS', 'PRIMARY_ROLE_ID');
    check_column('HGB_USERS', 'STATUS');
    check_column('HGB_USERS', 'CREATED_AT');
    check_column('HGB_USERS', 'UPDATED_AT');
    check_column('HGB_USER_ROLES', 'USER_ID');
    check_column('HGB_USER_ROLES', 'ROLE_ID');
    check_column('HGB_USER_SCOPES', 'USER_SCOPE_ID');
    check_column('HGB_USER_SCOPES', 'USER_ID');
    check_column('HGB_USER_SCOPES', 'SCOPE_TYPE');
    check_column('HGB_USER_SCOPES', 'SCOPE_ID');
    check_column('HGB_USER_SCOPES', 'IS_ACTIVE');
    check_column('HGB_USER_SCOPES', 'CREATED_AT');
    check_column('HGB_SURVEY_TEMPLATES', 'SURVEY_TEMPLATE_ID');
    check_column('HGB_SURVEY_TEMPLATES', 'TEMPLATE_CODE');
    check_column('HGB_SURVEY_TEMPLATES', 'TEMPLATE_NAME');
    check_column('HGB_SURVEY_TEMPLATES', 'DESCRIPTION');
    check_column('HGB_SURVEY_TEMPLATES', 'STATUS');
    check_column('HGB_SURVEY_TEMPLATES', 'CREATED_BY_USER_ID');
    check_column('HGB_SURVEY_TEMPLATES', 'CREATED_AT');
    check_column('HGB_SURVEY_TEMPLATE_VERSIONS', 'TEMPLATE_VERSION_ID');
    check_column('HGB_SURVEY_TEMPLATE_VERSIONS', 'SURVEY_TEMPLATE_ID');
    check_column('HGB_SURVEY_TEMPLATE_VERSIONS', 'VERSION_NO');
    check_column('HGB_SURVEY_TEMPLATE_VERSIONS', 'VERSION_LABEL');
    check_column('HGB_SURVEY_TEMPLATE_VERSIONS', 'STATUS');
    check_column('HGB_SURVEY_TEMPLATE_VERSIONS', 'PUBLISHED_AT');
    check_column('HGB_SURVEY_TEMPLATE_VERSIONS', 'CREATED_AT');
    check_column('HGB_SURVEY_QUESTIONS', 'QUESTION_ID');
    check_column('HGB_SURVEY_QUESTIONS', 'TEMPLATE_VERSION_ID');
    check_column('HGB_SURVEY_QUESTIONS', 'QUESTION_CODE');
    check_column('HGB_SURVEY_QUESTIONS', 'QUESTION_ORDER');
    check_column('HGB_SURVEY_QUESTIONS', 'QUESTION_TYPE');
    check_column('HGB_SURVEY_QUESTIONS', 'METRIC_TYPE');
    check_column('HGB_SURVEY_QUESTIONS', 'QUESTION_TEXT_TR');
    check_column('HGB_SURVEY_QUESTIONS', 'QUESTION_TEXT_EN');
    check_column('HGB_SURVEY_QUESTIONS', 'QUESTION_TEXT_AR');
    check_column('HGB_SURVEY_QUESTIONS', 'HELP_TEXT');
    check_column('HGB_SURVEY_QUESTIONS', 'IS_REQUIRED');
    check_column('HGB_SURVEY_QUESTIONS', 'IS_INITIAL_QUESTION');
    check_column('HGB_SURVEY_QUESTIONS', 'MIN_VALUE');
    check_column('HGB_SURVEY_QUESTIONS', 'MAX_VALUE');
    check_column('HGB_SURVEY_QUESTIONS', 'CREATED_AT');
    check_column('HGB_SURVEY_OPTIONS', 'OPTION_ID');
    check_column('HGB_SURVEY_OPTIONS', 'QUESTION_ID');
    check_column('HGB_SURVEY_OPTIONS', 'OPTION_ORDER');
    check_column('HGB_SURVEY_OPTIONS', 'OPTION_VALUE');
    check_column('HGB_SURVEY_OPTIONS', 'OPTION_TEXT_TR');
    check_column('HGB_SURVEY_OPTIONS', 'OPTION_TEXT_EN');
    check_column('HGB_SURVEY_OPTIONS', 'OPTION_TEXT_AR');
    check_column('HGB_SURVEY_OPTIONS', 'NUMERIC_VALUE');
    check_column('HGB_BRANCHING_RULES', 'BRANCHING_RULE_ID');
    check_column('HGB_BRANCHING_RULES', 'SOURCE_QUESTION_ID');
    check_column('HGB_BRANCHING_RULES', 'OPERATOR_CODE');
    check_column('HGB_BRANCHING_RULES', 'COMPARE_NUMERIC_VALUE');
    check_column('HGB_BRANCHING_RULES', 'COMPARE_OPTION_ID');
    check_column('HGB_BRANCHING_RULES', 'TARGET_QUESTION_ID');
    check_column('HGB_BRANCHING_RULES', 'RULE_ORDER');
    check_column('HGB_BRANCHING_RULES', 'IS_ACTIVE');
    check_column('HGB_CHANNELS', 'CHANNEL_ID');
    check_column('HGB_CHANNELS', 'CHANNEL_CODE');
    check_column('HGB_CHANNELS', 'CHANNEL_NAME');
    check_column('HGB_CHANNELS', 'IS_ENABLED');
    check_column('HGB_CHANNELS', 'CREATED_AT');
    check_column('HGB_CHANNEL_TEMPLATES', 'CHANNEL_TEMPLATE_ID');
    check_column('HGB_CHANNEL_TEMPLATES', 'CHANNEL_ID');
    check_column('HGB_CHANNEL_TEMPLATES', 'TEMPLATE_CODE');
    check_column('HGB_CHANNEL_TEMPLATES', 'LANGUAGE_CODE');
    check_column('HGB_CHANNEL_TEMPLATES', 'BODY_TEMPLATE');
    check_column('HGB_CHANNEL_TEMPLATES', 'IS_ACTIVE');
    check_column('HGB_CHANNEL_TEMPLATES', 'CREATED_AT');
    check_column('HGB_TRIGGER_RULES', 'TRIGGER_RULE_ID');
    check_column('HGB_TRIGGER_RULES', 'EVENT_TYPE');
    check_column('HGB_TRIGGER_RULES', 'SURVEY_TEMPLATE_ID');
    check_column('HGB_TRIGGER_RULES', 'PRIMARY_CHANNEL_ID');
    check_column('HGB_TRIGGER_RULES', 'FALLBACK_CHANNEL_ID');
    check_column('HGB_TRIGGER_RULES', 'IS_ENABLED');
    check_column('HGB_TRIGGER_RULES', 'DELAY_MINUTES');
    check_column('HGB_TRIGGER_RULES', 'LOW_SCORE_THRESHOLD');
    check_column('HGB_TRIGGER_RULES', 'FREQUENCY_CAP_DAYS');
    check_column('HGB_TRIGGER_RULES', 'FREQUENCY_CAP_COUNT');
    check_column('HGB_TRIGGER_RULES', 'REMINDER_ENABLED');
    check_column('HGB_TRIGGER_RULES', 'REMINDER_COUNT');
    check_column('HGB_TRIGGER_RULES', 'REMINDER_INTERVAL_MINUTES');
    check_column('HGB_TRIGGER_RULES', 'SERVICE_RECOVERY_SLA_HOURS');
    check_column('HGB_TRIGGER_RULES', 'CREATED_AT');
    check_column('HGB_TRIGGER_RULES', 'UPDATED_AT');
    check_column('HGB_CLINICAL_EVENTS', 'CLINICAL_EVENT_ID');
    check_column('HGB_CLINICAL_EVENTS', 'EXTERNAL_EVENT_REF');
    check_column('HGB_CLINICAL_EVENTS', 'SOURCE_SYSTEM');
    check_column('HGB_CLINICAL_EVENTS', 'EVENT_TYPE');
    check_column('HGB_CLINICAL_EVENTS', 'PATIENT_ID');
    check_column('HGB_CLINICAL_EVENTS', 'HOSPITAL_ID');
    check_column('HGB_CLINICAL_EVENTS', 'BRANCH_ID');
    check_column('HGB_CLINICAL_EVENTS', 'DEPARTMENT_ID');
    check_column('HGB_CLINICAL_EVENTS', 'DOCTOR_ID');
    check_column('HGB_CLINICAL_EVENTS', 'SERVICE_ID');
    check_column('HGB_CLINICAL_EVENTS', 'EVENT_TIME');
    check_column('HGB_CLINICAL_EVENTS', 'IS_SENSITIVE');
    check_column('HGB_CLINICAL_EVENTS', 'SENSITIVITY_REASON');
    check_column('HGB_CLINICAL_EVENTS', 'STATUS');
    check_column('HGB_CLINICAL_EVENTS', 'PROCESSED_AT');
    check_column('HGB_CLINICAL_EVENTS', 'CREATED_AT');
    check_column('HGB_EVENT_QUEUE', 'QUEUE_ID');
    check_column('HGB_EVENT_QUEUE', 'CLINICAL_EVENT_ID');
    check_column('HGB_EVENT_QUEUE', 'QUEUE_STATUS');
    check_column('HGB_EVENT_QUEUE', 'SCHEDULED_AT');
    check_column('HGB_EVENT_QUEUE', 'RETRY_COUNT');
    check_column('HGB_EVENT_QUEUE', 'STARTED_AT');
    check_column('HGB_EVENT_QUEUE', 'COMPLETED_AT');
    check_column('HGB_EVENT_QUEUE', 'LAST_ERROR_MESSAGE');
    check_column('HGB_EVENT_QUEUE', 'CREATED_AT');
    check_column('HGB_SURVEY_INVITATIONS', 'INVITATION_ID');
    check_column('HGB_SURVEY_INVITATIONS', 'CLINICAL_EVENT_ID');
    check_column('HGB_SURVEY_INVITATIONS', 'PATIENT_ID');
    check_column('HGB_SURVEY_INVITATIONS', 'TEMPLATE_VERSION_ID');
    check_column('HGB_SURVEY_INVITATIONS', 'TRIGGER_RULE_ID');
    check_column('HGB_SURVEY_INVITATIONS', 'SELECTED_CHANNEL_ID');
    check_column('HGB_SURVEY_INVITATIONS', 'TOKEN_HASH');
    check_column('HGB_SURVEY_INVITATIONS', 'TOKEN_EXPIRES_AT');
    check_column('HGB_SURVEY_INVITATIONS', 'TOKEN_USED_AT');
    check_column('HGB_SURVEY_INVITATIONS', 'INVITATION_STATUS');
    check_column('HGB_SURVEY_INVITATIONS', 'SENT_AT');
    check_column('HGB_SURVEY_INVITATIONS', 'COMPLETED_AT');
    check_column('HGB_SURVEY_INVITATIONS', 'CREATED_AT');
    check_column('HGB_DELIVERY_ATTEMPTS', 'DELIVERY_ATTEMPT_ID');
    check_column('HGB_DELIVERY_ATTEMPTS', 'INVITATION_ID');
    check_column('HGB_DELIVERY_ATTEMPTS', 'CHANNEL_ID');
    check_column('HGB_DELIVERY_ATTEMPTS', 'ATTEMPT_NO');
    check_column('HGB_DELIVERY_ATTEMPTS', 'DELIVERY_STATUS');
    check_column('HGB_DELIVERY_ATTEMPTS', 'SENT_AT');
    check_column('HGB_DELIVERY_ATTEMPTS', 'FAILED_AT');
    check_column('HGB_DELIVERY_ATTEMPTS', 'ERROR_MESSAGE');
    check_column('HGB_DELIVERY_ATTEMPTS', 'CREATED_AT');
    check_column('HGB_CONSENT_TEXTS', 'CONSENT_TEXT_ID');
    check_column('HGB_CONSENT_TEXTS', 'LANGUAGE_CODE');
    check_column('HGB_CONSENT_TEXTS', 'VERSION_NO');
    check_column('HGB_CONSENT_TEXTS', 'BODY');
    check_column('HGB_CONSENT_TEXTS', 'STATUS');
    check_column('HGB_CONSENT_TEXTS', 'CREATED_AT');
    check_column('HGB_CONSENT_RECORDS', 'CONSENT_RECORD_ID');
    check_column('HGB_CONSENT_RECORDS', 'INVITATION_ID');
    check_column('HGB_CONSENT_RECORDS', 'PATIENT_ID');
    check_column('HGB_CONSENT_RECORDS', 'CONSENT_TEXT_ID');
    check_column('HGB_CONSENT_RECORDS', 'CONSENT_STATUS');
    check_column('HGB_CONSENT_RECORDS', 'CONSENT_SCOPE');
    check_column('HGB_CONSENT_RECORDS', 'ANONYMOUS_SELECTED');
    check_column('HGB_CONSENT_RECORDS', 'IP_HASH');
    check_column('HGB_CONSENT_RECORDS', 'GIVEN_AT');
    check_column('HGB_SURVEY_RESPONSES', 'RESPONSE_ID');
    check_column('HGB_SURVEY_RESPONSES', 'INVITATION_ID');
    check_column('HGB_SURVEY_RESPONSES', 'CLINICAL_EVENT_ID');
    check_column('HGB_SURVEY_RESPONSES', 'PATIENT_ID');
    check_column('HGB_SURVEY_RESPONSES', 'TEMPLATE_VERSION_ID');
    check_column('HGB_SURVEY_RESPONSES', 'HOSPITAL_ID');
    check_column('HGB_SURVEY_RESPONSES', 'BRANCH_ID');
    check_column('HGB_SURVEY_RESPONSES', 'DEPARTMENT_ID');
    check_column('HGB_SURVEY_RESPONSES', 'DOCTOR_ID');
    check_column('HGB_SURVEY_RESPONSES', 'SERVICE_ID');
    check_column('HGB_SURVEY_RESPONSES', 'CONSENT_RECORD_ID');
    check_column('HGB_SURVEY_RESPONSES', 'IS_ANONYMOUS');
    check_column('HGB_SURVEY_RESPONSES', 'OVERALL_SCORE');
    check_column('HGB_SURVEY_RESPONSES', 'NPS_SCORE');
    check_column('HGB_SURVEY_RESPONSES', 'CSAT_SCORE');
    check_column('HGB_SURVEY_RESPONSES', 'IS_NEGATIVE');
    check_column('HGB_SURVEY_RESPONSES', 'SENTIMENT_LABEL');
    check_column('HGB_SURVEY_RESPONSES', 'SENTIMENT_SCORE');
    check_column('HGB_SURVEY_RESPONSES', 'RESPONSE_STATUS');
    check_column('HGB_SURVEY_RESPONSES', 'SUBMITTED_AT');
    check_column('HGB_SURVEY_RESPONSES', 'CREATED_AT');
    check_column('HGB_SURVEY_ANSWERS', 'ANSWER_ID');
    check_column('HGB_SURVEY_ANSWERS', 'RESPONSE_ID');
    check_column('HGB_SURVEY_ANSWERS', 'QUESTION_ID');
    check_column('HGB_SURVEY_ANSWERS', 'OPTION_ID');
    check_column('HGB_SURVEY_ANSWERS', 'NUMERIC_VALUE');
    check_column('HGB_SURVEY_ANSWERS', 'TEXT_VALUE');
    check_column('HGB_SURVEY_ANSWERS', 'ANSWERED_AT');
    check_column('HGB_SURVEY_ANSWERS', 'CREATED_AT');
    check_column('HGB_ALERTS', 'ALERT_ID');
    check_column('HGB_ALERTS', 'ALERT_TYPE');
    check_column('HGB_ALERTS', 'SEVERITY');
    check_column('HGB_ALERTS', 'ALERT_STATUS');
    check_column('HGB_ALERTS', 'RESPONSE_ID');
    check_column('HGB_ALERTS', 'CLINICAL_EVENT_ID');
    check_column('HGB_ALERTS', 'MESSAGE');
    check_column('HGB_ALERTS', 'TARGET_USER_ID');
    check_column('HGB_ALERTS', 'ACKNOWLEDGED_AT');
    check_column('HGB_ALERTS', 'CLOSED_AT');
    check_column('HGB_ALERTS', 'CREATED_AT');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'RECOVERY_CASE_ID');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'ALERT_ID');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'RESPONSE_ID');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'DEPARTMENT_ID');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'CASE_STATUS');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'PRIORITY');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'ASSIGNED_USER_ID');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'SLA_DUE_AT');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'OPENED_AT');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'CLOSED_AT');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'CLOSURE_NOTE');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'ESCALATION_LEVEL');
    check_column('HGB_SERVICE_RECOVERY_CASES', 'CREATED_AT');
    check_column('HGB_RECOVERY_ACTIONS', 'RECOVERY_ACTION_ID');
    check_column('HGB_RECOVERY_ACTIONS', 'RECOVERY_CASE_ID');
    check_column('HGB_RECOVERY_ACTIONS', 'ACTION_TYPE');
    check_column('HGB_RECOVERY_ACTIONS', 'ACTION_BY_USER_ID');
    check_column('HGB_RECOVERY_ACTIONS', 'ACTION_NOTE');
    check_column('HGB_RECOVERY_ACTIONS', 'ACTION_AT');
    check_column('HGB_CASE_ESCALATIONS', 'CASE_ESCALATION_ID');
    check_column('HGB_CASE_ESCALATIONS', 'RECOVERY_CASE_ID');
    check_column('HGB_CASE_ESCALATIONS', 'ESCALATION_REASON');
    check_column('HGB_CASE_ESCALATIONS', 'CREATED_AT');
    check_column('HGB_AUDIT_LOGS', 'AUDIT_LOG_ID');
    check_column('HGB_AUDIT_LOGS', 'ENTITY_NAME');
    check_column('HGB_AUDIT_LOGS', 'ENTITY_ID');
    check_column('HGB_AUDIT_LOGS', 'ACTION_CODE');
    check_column('HGB_AUDIT_LOGS', 'ACTOR_USER_ID');
    check_column('HGB_AUDIT_LOGS', 'PATIENT_ID');
    check_column('HGB_AUDIT_LOGS', 'CHANGE_DESCRIPTION');
    check_column('HGB_AUDIT_LOGS', 'IP_HASH');
    check_column('HGB_AUDIT_LOGS', 'CREATED_AT');
    check_column('HGB_WEBHOOK_REPLAY', 'WEBHOOK_REPLAY_ID');
    check_column('HGB_WEBHOOK_REPLAY', 'SIGNATURE_HASH');
    check_column('HGB_WEBHOOK_REPLAY', 'SOURCE_SYSTEM');
    check_column('HGB_WEBHOOK_REPLAY', 'RECEIVED_AT');
    check_column('HGB_WEBHOOK_REPLAY', 'EXPIRES_AT');
    check_column('HGB_INTEGRATION_SYSTEMS', 'INTEGRATION_SYSTEM_ID');
    check_column('HGB_INTEGRATION_SYSTEMS', 'SYSTEM_CODE');
    check_column('HGB_INTEGRATION_SYSTEMS', 'SYSTEM_NAME');
    check_column('HGB_INTEGRATION_SYSTEMS', 'BASE_URL');
    check_column('HGB_INTEGRATION_SYSTEMS', 'IS_ENABLED');
    check_column('HGB_INTEGRATION_SYSTEMS', 'AUTH_TYPE');
    check_column('HGB_INTEGRATION_SYSTEMS', 'CREATED_AT');
    check_column('HGB_INTEGRATION_LOGS', 'INTEGRATION_LOG_ID');
    check_column('HGB_INTEGRATION_LOGS', 'INTEGRATION_SYSTEM_ID');
    check_column('HGB_INTEGRATION_LOGS', 'DIRECTION');
    check_column('HGB_INTEGRATION_LOGS', 'OPERATION_NAME');
    check_column('HGB_INTEGRATION_LOGS', 'REQUEST_PAYLOAD');
    check_column('HGB_INTEGRATION_LOGS', 'RESPONSE_PAYLOAD');
    check_column('HGB_INTEGRATION_LOGS', 'HTTP_STATUS_CODE');
    check_column('HGB_INTEGRATION_LOGS', 'SUCCESS_FLAG');
    check_column('HGB_INTEGRATION_LOGS', 'ERROR_MESSAGE');
    check_column('HGB_INTEGRATION_LOGS', 'PROVIDER_MESSAGE_ID');
    check_column('HGB_INTEGRATION_LOGS', 'CORRELATION_ID');
    check_column('HGB_INTEGRATION_LOGS', 'RETRY_COUNT');
    check_column('HGB_INTEGRATION_LOGS', 'NEXT_RETRY_AT');
    check_column('HGB_INTEGRATION_LOGS', 'CREATED_AT');
    check_column('HGB_APP_SETTINGS', 'SETTING_ID');
    check_column('HGB_APP_SETTINGS', 'SETTING_KEY');
    check_column('HGB_APP_SETTINGS', 'SETTING_VALUE');
    check_column('HGB_APP_SETTINGS', 'DESCRIPTION');
    check_column('HGB_APP_SETTINGS', 'UPDATED_BY_USER_ID');
    check_column('HGB_APP_SETTINGS', 'UPDATED_AT');
    check_column('HGB_KPI_TARGETS', 'KPI_TARGET_ID');
    check_column('HGB_KPI_TARGETS', 'KPI_CODE');
    check_column('HGB_KPI_TARGETS', 'DEPARTMENT_ID');
    check_column('HGB_KPI_TARGETS', 'TARGET_PERIOD');
    check_column('HGB_KPI_TARGETS', 'TARGET_VALUE');
    check_column('HGB_KPI_TARGETS', 'VALID_FROM');
    check_column('HGB_KPI_TARGETS', 'VALID_TO');
    check_column('HGB_KPI_TARGETS', 'CREATED_BY_USER_ID');
    check_column('HGB_KPI_TARGETS', 'CREATED_AT');
    check_column('HGB_REPORT_EXPORTS', 'REPORT_EXPORT_ID');
    check_column('HGB_REPORT_EXPORTS', 'REQUESTED_BY_USER_ID');
    check_column('HGB_REPORT_EXPORTS', 'REPORT_TYPE');
    check_column('HGB_REPORT_EXPORTS', 'EXPORT_FORMAT');
    check_column('HGB_REPORT_EXPORTS', 'FILTER_JSON');
    check_column('HGB_REPORT_EXPORTS', 'EXPORT_STATUS');
    check_column('HGB_REPORT_EXPORTS', 'ERROR_MESSAGE');
    check_column('HGB_REPORT_EXPORTS', 'REQUESTED_AT');
    check_column('HGB_REPORT_EXPORTS', 'COMPLETED_AT');
    check_column('HGB_RETENTION_POLICIES', 'RETENTION_POLICY_ID');
    check_column('HGB_RETENTION_POLICIES', 'DATA_CATEGORY');
    check_column('HGB_RETENTION_POLICIES', 'RETENTION_DAYS');
    check_column('HGB_RETENTION_POLICIES', 'ACTION_AFTER_RETENTION');
    check_column('HGB_RETENTION_POLICIES', 'IS_ACTIVE');
    check_column('HGB_BI_EXPORT_QUEUE', 'BI_EXPORT_ID');
    check_column('HGB_BI_EXPORT_QUEUE', 'RESPONSE_ID');
    check_column('HGB_BI_EXPORT_QUEUE', 'EXPORT_STATUS');
    check_column('HGB_BI_EXPORT_QUEUE', 'RETRY_COUNT');
    check_column('HGB_BI_EXPORT_QUEUE', 'NEXT_RETRY_AT');
    check_column('HGB_BI_EXPORT_QUEUE', 'LAST_ERROR_MESSAGE');
    check_column('HGB_BI_EXPORT_QUEUE', 'CREATED_AT');
    check_column('HGB_BI_EXPORT_QUEUE', 'EXPORTED_AT');
    check_column('HGB_SENTIMENT_RESULTS', 'SENTIMENT_RESULT_ID');
    check_column('HGB_SENTIMENT_RESULTS', 'RESPONSE_ID');
    check_column('HGB_SENTIMENT_RESULTS', 'SOURCE_ANSWER_ID');
    check_column('HGB_SENTIMENT_RESULTS', 'MODEL_NAME');
    check_column('HGB_SENTIMENT_RESULTS', 'SENTIMENT_LABEL');
    check_column('HGB_SENTIMENT_RESULTS', 'SENTIMENT_SCORE');
    check_column('HGB_SENTIMENT_RESULTS', 'CREATED_AT');
    check_column('HGB_THEME_CATEGORIES', 'THEME_CATEGORY_ID');
    check_column('HGB_THEME_CATEGORIES', 'THEME_CODE');
    check_column('HGB_THEME_CATEGORIES', 'THEME_NAME');
    check_column('HGB_THEME_CATEGORIES', 'IS_ACTIVE');
    check_column('HGB_RESPONSE_THEME_MATCHES', 'RESPONSE_THEME_MATCH_ID');
    check_column('HGB_RESPONSE_THEME_MATCHES', 'RESPONSE_ID');
    check_column('HGB_RESPONSE_THEME_MATCHES', 'THEME_CATEGORY_ID');
    check_column('HGB_RESPONSE_THEME_MATCHES', 'CONFIDENCE_SCORE');
    check_column('HGB_RESPONSE_THEME_MATCHES', 'CREATED_AT');
    check_column('HGB_DATA_SUBJECT_REQUESTS', 'DSR_ID');
    check_column('HGB_DATA_SUBJECT_REQUESTS', 'PATIENT_ID');
    check_column('HGB_DATA_SUBJECT_REQUESTS', 'REQUEST_TYPE');
    check_column('HGB_DATA_SUBJECT_REQUESTS', 'REQUEST_STATUS');
    check_column('HGB_DATA_SUBJECT_REQUESTS', 'REQUESTED_AT');
    check_column('HGB_DATA_SUBJECT_REQUESTS', 'COMPLETED_AT');
    check_column('HGB_DATA_SUBJECT_REQUESTS', 'REQUESTED_BY_NOTE');
    check_column('HGB_DATA_SUBJECT_REQUESTS', 'HANDLED_BY_USER_ID');
    check_column('HGB_DATA_SUBJECT_REQUESTS', 'RESOLUTION_NOTE');

    check_view('HGB_V_FEEDBACK_DASHBOARD');
    check_view('HGB_V_OPEN_RECOVERY_CASES');
    check_view_columns(
        'HGB_V_FEEDBACK_DASHBOARD',
        'REPORT_DATE,BRANCH_ID,DEPARTMENT_ID,DEPARTMENT_NAME,DOCTOR_ID,DOCTOR_NAME,TOTAL_RESPONSES,AVG_OVERALL_SCORE,AVG_CSAT_SCORE,NPS_VALUE,NEGATIVE_COUNT,NEGATIVE_RATE');
    check_view_columns(
        'HGB_V_OPEN_RECOVERY_CASES',
        'RECOVERY_CASE_ID,RESPONSE_ID,CASE_STATUS,PRIORITY,DEPARTMENT_NAME,OVERALL_SCORE,ASSIGNED_USER_NAME,OPENED_AT,SLA_DUE_AT,IS_SLA_BREACHED');

    check_index('UX_HGB_INV_TOKEN', 'HGB_SURVEY_INVITATIONS', 'TOKEN_HASH', 'UNIQUE');
    check_index('IX_HGB_EVENTS_STATUS', 'HGB_CLINICAL_EVENTS', 'STATUS,EVENT_TIME', 'NONUNIQUE');
    check_index('IX_HGB_QUEUE_STATUS', 'HGB_EVENT_QUEUE', 'QUEUE_STATUS,SCHEDULED_AT', 'NONUNIQUE');
    check_index('IX_HGB_RESP_SUBMITTED', 'HGB_SURVEY_RESPONSES', 'RESPONSE_STATUS,SUBMITTED_AT', 'NONUNIQUE');
    check_index('UX_HGB_USER_SCOPES_ACTIVE', 'HGB_USER_SCOPES', 'USER_ID,SCOPE_TYPE,SCOPE_ID,IS_ACTIVE', 'UNIQUE');
    check_index('UX_HGB_WEBHOOK_REPLAY', 'HGB_WEBHOOK_REPLAY', 'SIGNATURE_HASH,SOURCE_SYSTEM', 'UNIQUE');
    check_index('IX_HGB_WEBHOOK_REPLAY_AT', 'HGB_WEBHOOK_REPLAY', 'RECEIVED_AT', 'NONUNIQUE');
    check_index('IX_HGB_USER_SCOPES_USER', 'HGB_USER_SCOPES', 'USER_ID,IS_ACTIVE', 'NONUNIQUE');
    check_index('UX_HGB_CLINICAL_EVENT_EXT', 'HGB_CLINICAL_EVENTS', 'SOURCE_SYSTEM,EXTERNAL_EVENT_REF', 'UNIQUE');
    check_index('IX_HGB_BI_EXPORT_RETRY', 'HGB_BI_EXPORT_QUEUE', 'EXPORT_STATUS,NEXT_RETRY_AT', 'NONUNIQUE');
    check_index('UX_HGB_RESP_INVITATION', 'HGB_SURVEY_RESPONSES', 'INVITATION_ID', 'UNIQUE');
    check_index('UX_HGB_ANSWER_QUESTION', 'HGB_SURVEY_ANSWERS', 'RESPONSE_ID,QUESTION_ID', 'UNIQUE');
    check_index('UX_HGB_TRIGGER_EVENT', 'HGB_TRIGGER_RULES', 'EVENT_TYPE', 'UNIQUE');
    check_index('UX_HGB_DELIVERY_ATTEMPT', 'HGB_DELIVERY_ATTEMPTS', 'INVITATION_ID,ATTEMPT_NO', 'UNIQUE');
    check_index('UX_HGB_TEMPLATE_VERSION', 'HGB_SURVEY_TEMPLATE_VERSIONS', 'SURVEY_TEMPLATE_ID,VERSION_NO', 'UNIQUE');
    check_index('UX_HGB_QUESTION_CODE', 'HGB_SURVEY_QUESTIONS', 'TEMPLATE_VERSION_ID,QUESTION_CODE', 'UNIQUE');
    check_index('UX_HGB_QUESTION_ORDER', 'HGB_SURVEY_QUESTIONS', 'TEMPLATE_VERSION_ID,QUESTION_ORDER', 'UNIQUE');
    check_index('UX_HGB_OPTION_VALUE', 'HGB_SURVEY_OPTIONS', 'QUESTION_ID,OPTION_VALUE', 'UNIQUE');
    check_index('UX_HGB_OPTION_ORDER', 'HGB_SURVEY_OPTIONS', 'QUESTION_ID,OPTION_ORDER', 'UNIQUE');
    check_index('UX_HGB_BRANCH_RULE_ORDER', 'HGB_BRANCHING_RULES', 'SOURCE_QUESTION_ID,RULE_ORDER', 'UNIQUE');
    check_index('UX_HGB_CHANNEL_TEMPLATE', 'HGB_CHANNEL_TEMPLATES', 'CHANNEL_ID,TEMPLATE_CODE,LANGUAGE_CODE', 'UNIQUE');
    check_index('UX_HGB_RESPONSE_THEME', 'HGB_RESPONSE_THEME_MATCHES', 'RESPONSE_ID,THEME_CATEGORY_ID', 'UNIQUE');
    check_index('IX_HGB_QUEUE_EVENT', 'HGB_EVENT_QUEUE', 'CLINICAL_EVENT_ID', 'NONUNIQUE');
    check_index('IX_HGB_INV_PATIENT_CREATED', 'HGB_SURVEY_INVITATIONS', 'PATIENT_ID,CREATED_AT', 'NONUNIQUE');
    check_index('IX_HGB_INV_STATUS_CREATED', 'HGB_SURVEY_INVITATIONS', 'INVITATION_STATUS,CREATED_AT', 'NONUNIQUE');
    check_index('IX_HGB_INV_EVENT', 'HGB_SURVEY_INVITATIONS', 'CLINICAL_EVENT_ID', 'NONUNIQUE');
    check_index('IX_HGB_INV_TRIGGER', 'HGB_SURVEY_INVITATIONS', 'TRIGGER_RULE_ID', 'NONUNIQUE');
    check_index('IX_HGB_ATTEMPT_INV_STATUS', 'HGB_DELIVERY_ATTEMPTS', 'INVITATION_ID,DELIVERY_STATUS,CREATED_AT', 'NONUNIQUE');
    check_index('IX_HGB_RESP_PATIENT_DATE', 'HGB_SURVEY_RESPONSES', 'PATIENT_ID,SUBMITTED_AT', 'NONUNIQUE');
    check_index('IX_HGB_RECOVERY_STATUS_SLA', 'HGB_SERVICE_RECOVERY_CASES', 'CASE_STATUS,SLA_DUE_AT', 'NONUNIQUE');
    check_index('IX_HGB_RECOVERY_RESPONSE', 'HGB_SERVICE_RECOVERY_CASES', 'RESPONSE_ID', 'NONUNIQUE');
    check_index('IX_HGB_RECOVERY_ACTION_CASE', 'HGB_RECOVERY_ACTIONS', 'RECOVERY_CASE_ID,ACTION_AT', 'NONUNIQUE');
    check_index('IX_HGB_INTLOG_CREATED', 'HGB_INTEGRATION_LOGS', 'CREATED_AT', 'NONUNIQUE');
    check_index('IX_HGB_INTLOG_SYSTEM_DATE', 'HGB_INTEGRATION_LOGS', 'INTEGRATION_SYSTEM_ID,CREATED_AT', 'NONUNIQUE');
    check_index('IX_HGB_SENTIMENT_RESPONSE', 'HGB_SENTIMENT_RESULTS', 'RESPONSE_ID', 'NONUNIQUE');
    check_index('IX_HGB_DSR_STATUS_DATE', 'HGB_DATA_SUBJECT_REQUESTS', 'REQUEST_STATUS,REQUESTED_AT', 'NONUNIQUE');

    check_fk('FK_HGB_BRANCH_HOSPITAL', 'HGB_BRANCHES', 'HOSPITAL_ID', 'HGB_HOSPITALS', 'HOSPITAL_ID', 'NO ACTION');
    check_fk('FK_HGB_DEPT_BRANCH', 'HGB_DEPARTMENTS', 'BRANCH_ID', 'HGB_BRANCHES', 'BRANCH_ID', 'NO ACTION');
    check_fk('FK_HGB_USER_PRIMARY_ROLE', 'HGB_USERS', 'PRIMARY_ROLE_ID', 'HGB_ROLES', 'ROLE_ID', 'NO ACTION');
    check_fk('FK_HGB_USERROLE_USER', 'HGB_USER_ROLES', 'USER_ID', 'HGB_USERS', 'USER_ID', 'NO ACTION');
    check_fk('FK_HGB_USERROLE_ROLE', 'HGB_USER_ROLES', 'ROLE_ID', 'HGB_ROLES', 'ROLE_ID', 'NO ACTION');
    check_fk('FK_HGB_ROLEPERM_ROLE', 'HGB_ROLE_PERMISSIONS', 'ROLE_ID', 'HGB_ROLES', 'ROLE_ID', 'NO ACTION');
    check_fk('FK_HGB_ROLEPERM_PERM', 'HGB_ROLE_PERMISSIONS', 'PERMISSION_ID', 'HGB_PERMISSIONS', 'PERMISSION_ID', 'NO ACTION');
    check_fk('FK_HGB_USERSCOPE_USER', 'HGB_USER_SCOPES', 'USER_ID', 'HGB_USERS', 'USER_ID', 'NO ACTION');
    check_fk('FK_HGB_TMPLVER_TEMPLATE', 'HGB_SURVEY_TEMPLATE_VERSIONS', 'SURVEY_TEMPLATE_ID', 'HGB_SURVEY_TEMPLATES', 'SURVEY_TEMPLATE_ID', 'NO ACTION');
    check_fk('FK_HGB_QUESTION_VERSION', 'HGB_SURVEY_QUESTIONS', 'TEMPLATE_VERSION_ID', 'HGB_SURVEY_TEMPLATE_VERSIONS', 'TEMPLATE_VERSION_ID', 'NO ACTION');
    check_fk('FK_HGB_OPTION_QUESTION', 'HGB_SURVEY_OPTIONS', 'QUESTION_ID', 'HGB_SURVEY_QUESTIONS', 'QUESTION_ID', 'CASCADE');
    check_fk('FK_HGB_BRANCHRULE_SOURCE', 'HGB_BRANCHING_RULES', 'SOURCE_QUESTION_ID', 'HGB_SURVEY_QUESTIONS', 'QUESTION_ID', 'CASCADE');
    check_fk('FK_HGB_BRANCHRULE_TARGET', 'HGB_BRANCHING_RULES', 'TARGET_QUESTION_ID', 'HGB_SURVEY_QUESTIONS', 'QUESTION_ID', 'CASCADE');
    check_fk('FK_HGB_CHANTMPL_CHANNEL', 'HGB_CHANNEL_TEMPLATES', 'CHANNEL_ID', 'HGB_CHANNELS', 'CHANNEL_ID', 'NO ACTION');
    check_fk('FK_HGB_TRIGGER_TEMPLATE', 'HGB_TRIGGER_RULES', 'SURVEY_TEMPLATE_ID', 'HGB_SURVEY_TEMPLATES', 'SURVEY_TEMPLATE_ID', 'NO ACTION');
    check_fk('FK_HGB_TRIGGER_PRIMCHAN', 'HGB_TRIGGER_RULES', 'PRIMARY_CHANNEL_ID', 'HGB_CHANNELS', 'CHANNEL_ID', 'NO ACTION');
    check_fk('FK_HGB_TRIGGER_FALLCHAN', 'HGB_TRIGGER_RULES', 'FALLBACK_CHANNEL_ID', 'HGB_CHANNELS', 'CHANNEL_ID', 'NO ACTION');
    check_fk('FK_HGB_EVENT_PATIENT', 'HGB_CLINICAL_EVENTS', 'PATIENT_ID', 'HGB_PATIENTS', 'PATIENT_ID', 'NO ACTION');
    check_fk('FK_HGB_QUEUE_EVENT', 'HGB_EVENT_QUEUE', 'CLINICAL_EVENT_ID', 'HGB_CLINICAL_EVENTS', 'CLINICAL_EVENT_ID', 'NO ACTION');
    check_fk('FK_HGB_INV_EVENT', 'HGB_SURVEY_INVITATIONS', 'CLINICAL_EVENT_ID', 'HGB_CLINICAL_EVENTS', 'CLINICAL_EVENT_ID', 'NO ACTION');
    check_fk('FK_HGB_INV_PATIENT', 'HGB_SURVEY_INVITATIONS', 'PATIENT_ID', 'HGB_PATIENTS', 'PATIENT_ID', 'NO ACTION');
    check_fk('FK_HGB_INV_VERSION', 'HGB_SURVEY_INVITATIONS', 'TEMPLATE_VERSION_ID', 'HGB_SURVEY_TEMPLATE_VERSIONS', 'TEMPLATE_VERSION_ID', 'NO ACTION');
    check_fk('FK_HGB_INV_TRIGGER', 'HGB_SURVEY_INVITATIONS', 'TRIGGER_RULE_ID', 'HGB_TRIGGER_RULES', 'TRIGGER_RULE_ID', 'NO ACTION');
    check_fk('FK_HGB_INV_CHANNEL', 'HGB_SURVEY_INVITATIONS', 'SELECTED_CHANNEL_ID', 'HGB_CHANNELS', 'CHANNEL_ID', 'NO ACTION');
    check_fk('FK_HGB_ATTEMPT_INV', 'HGB_DELIVERY_ATTEMPTS', 'INVITATION_ID', 'HGB_SURVEY_INVITATIONS', 'INVITATION_ID', 'NO ACTION');
    check_fk('FK_HGB_ATTEMPT_CHANNEL', 'HGB_DELIVERY_ATTEMPTS', 'CHANNEL_ID', 'HGB_CHANNELS', 'CHANNEL_ID', 'NO ACTION');
    check_fk('FK_HGB_CONSENT_INV', 'HGB_CONSENT_RECORDS', 'INVITATION_ID', 'HGB_SURVEY_INVITATIONS', 'INVITATION_ID', 'NO ACTION');
    check_fk('FK_HGB_CONSENT_PATIENT', 'HGB_CONSENT_RECORDS', 'PATIENT_ID', 'HGB_PATIENTS', 'PATIENT_ID', 'NO ACTION');
    check_fk('FK_HGB_CONSENT_TEXT', 'HGB_CONSENT_RECORDS', 'CONSENT_TEXT_ID', 'HGB_CONSENT_TEXTS', 'CONSENT_TEXT_ID', 'NO ACTION');
    check_fk('FK_HGB_RESP_INV', 'HGB_SURVEY_RESPONSES', 'INVITATION_ID', 'HGB_SURVEY_INVITATIONS', 'INVITATION_ID', 'NO ACTION');
    check_fk('FK_HGB_RESP_EVENT', 'HGB_SURVEY_RESPONSES', 'CLINICAL_EVENT_ID', 'HGB_CLINICAL_EVENTS', 'CLINICAL_EVENT_ID', 'NO ACTION');
    check_fk('FK_HGB_RESP_VERSION', 'HGB_SURVEY_RESPONSES', 'TEMPLATE_VERSION_ID', 'HGB_SURVEY_TEMPLATE_VERSIONS', 'TEMPLATE_VERSION_ID', 'NO ACTION');
    check_fk('FK_HGB_RESP_CONSENT', 'HGB_SURVEY_RESPONSES', 'CONSENT_RECORD_ID', 'HGB_CONSENT_RECORDS', 'CONSENT_RECORD_ID', 'NO ACTION');
    check_fk('FK_HGB_ANSWER_RESPONSE', 'HGB_SURVEY_ANSWERS', 'RESPONSE_ID', 'HGB_SURVEY_RESPONSES', 'RESPONSE_ID', 'NO ACTION');
    check_fk('FK_HGB_ALERT_RESPONSE', 'HGB_ALERTS', 'RESPONSE_ID', 'HGB_SURVEY_RESPONSES', 'RESPONSE_ID', 'NO ACTION');
    check_fk('FK_HGB_ALERT_EVENT', 'HGB_ALERTS', 'CLINICAL_EVENT_ID', 'HGB_CLINICAL_EVENTS', 'CLINICAL_EVENT_ID', 'NO ACTION');
    check_fk('FK_HGB_ALERT_TARGET_USER', 'HGB_ALERTS', 'TARGET_USER_ID', 'HGB_USERS', 'USER_ID', 'NO ACTION');
    check_fk('FK_HGB_CASE_ALERT', 'HGB_SERVICE_RECOVERY_CASES', 'ALERT_ID', 'HGB_ALERTS', 'ALERT_ID', 'NO ACTION');
    check_fk('FK_HGB_CASE_RESPONSE', 'HGB_SERVICE_RECOVERY_CASES', 'RESPONSE_ID', 'HGB_SURVEY_RESPONSES', 'RESPONSE_ID', 'NO ACTION');
    check_fk('FK_HGB_CASE_ASSIGNEE', 'HGB_SERVICE_RECOVERY_CASES', 'ASSIGNED_USER_ID', 'HGB_USERS', 'USER_ID', 'NO ACTION');
    check_fk('FK_HGB_ACTION_CASE', 'HGB_RECOVERY_ACTIONS', 'RECOVERY_CASE_ID', 'HGB_SERVICE_RECOVERY_CASES', 'RECOVERY_CASE_ID', 'NO ACTION');
    check_fk('FK_HGB_ACTION_USER', 'HGB_RECOVERY_ACTIONS', 'ACTION_BY_USER_ID', 'HGB_USERS', 'USER_ID', 'NO ACTION');
    check_fk('FK_HGB_ESCALATION_CASE', 'HGB_CASE_ESCALATIONS', 'RECOVERY_CASE_ID', 'HGB_SERVICE_RECOVERY_CASES', 'RECOVERY_CASE_ID', 'NO ACTION');
    check_fk('FK_HGB_SENTIMENT_RESPONSE', 'HGB_SENTIMENT_RESULTS', 'RESPONSE_ID', 'HGB_SURVEY_RESPONSES', 'RESPONSE_ID', 'NO ACTION');
    check_fk('FK_HGB_SENTIMENT_ANSWER', 'HGB_SENTIMENT_RESULTS', 'SOURCE_ANSWER_ID', 'HGB_SURVEY_ANSWERS', 'ANSWER_ID', 'SET NULL');
    check_fk('FK_HGB_THEMEMATCH_RESPONSE', 'HGB_RESPONSE_THEME_MATCHES', 'RESPONSE_ID', 'HGB_SURVEY_RESPONSES', 'RESPONSE_ID', 'NO ACTION');
    check_fk('FK_HGB_THEMEMATCH_THEME', 'HGB_RESPONSE_THEME_MATCHES', 'THEME_CATEGORY_ID', 'HGB_THEME_CATEGORIES', 'THEME_CATEGORY_ID', 'NO ACTION');
    check_fk('FK_HGB_BIEXPORT_RESPONSE', 'HGB_BI_EXPORT_QUEUE', 'RESPONSE_ID', 'HGB_SURVEY_RESPONSES', 'RESPONSE_ID', 'NO ACTION');
    check_fk('FK_HGB_DSR_PATIENT', 'HGB_DATA_SUBJECT_REQUESTS', 'PATIENT_ID', 'HGB_PATIENTS', 'PATIENT_ID', 'NO ACTION');
    check_fk('FK_HGB_DSR_HANDLER', 'HGB_DATA_SUBJECT_REQUESTS', 'HANDLED_BY_USER_ID', 'HGB_USERS', 'USER_ID', 'NO ACTION');
    check_fk('FK_HGB_KPI_DEPARTMENT', 'HGB_KPI_TARGETS', 'DEPARTMENT_ID', 'HGB_DEPARTMENTS', 'DEPARTMENT_ID', 'NO ACTION');
    check_fk('FK_HGB_BRANCHRULE_OPTION', 'HGB_BRANCHING_RULES', 'COMPARE_OPTION_ID', 'HGB_SURVEY_OPTIONS', 'OPTION_ID', 'CASCADE');
    check_fk('FK_HGB_INTLOG_SYSTEM', 'HGB_INTEGRATION_LOGS', 'INTEGRATION_SYSTEM_ID', 'HGB_INTEGRATION_SYSTEMS', 'INTEGRATION_SYSTEM_ID', 'SET NULL');
    check_fk('FK_HGB_TEMPLATE_CREATOR', 'HGB_SURVEY_TEMPLATES', 'CREATED_BY_USER_ID', 'HGB_USERS', 'USER_ID', 'SET NULL');
    check_fk('FK_HGB_SETTING_UPDATER', 'HGB_APP_SETTINGS', 'UPDATED_BY_USER_ID', 'HGB_USERS', 'USER_ID', 'SET NULL');
    check_fk('FK_HGB_KPI_CREATOR', 'HGB_KPI_TARGETS', 'CREATED_BY_USER_ID', 'HGB_USERS', 'USER_ID', 'SET NULL');
    check_fk('FK_HGB_REPORT_REQUESTER', 'HGB_REPORT_EXPORTS', 'REQUESTED_BY_USER_ID', 'HGB_USERS', 'USER_ID', 'SET NULL');

    check_version('2026-07-schema-01-foundation');
    check_version('2026-07-schema-02-access');
    check_version('2026-07-schema-03-survey');
    check_version('2026-07-schema-04-delivery');
    check_version('2026-07-schema-05-feedback');
    check_version('2026-07-schema-06-operations');
    check_version('2026-07-schema-07-indexes');
    check_version('2026-07-schema-08-views');
    check_version('2026-07-schema-09-foreign-keys');
    check_version('2026-07-data-01-access');
    check_version('2026-07-data-02-channels');
    check_version('2026-07-data-03-survey');
    SELECT nullable INTO v_nullable
    FROM user_tab_columns
    WHERE table_name = 'HGB_WEBHOOK_REPLAY'
      AND column_name = 'EXPIRES_AT';

    IF v_nullable <> 'N' THEN
        fail('Column contract mismatch: HGB_WEBHOOK_REPLAY.EXPIRES_AT must be NOT NULL');
    END IF;

    DBMS_OUTPUT.PUT_LINE(
        'core schema contract ok: 46 tables, 364 columns, ' ||
        '36 indexes, 57 foreign keys');
END;
/

PROMPT Core schema verification completed.

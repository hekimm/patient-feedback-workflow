-- Run as patient_app after install-demo.sql.

WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK

SET DEFINE OFF;
SET VERIFY OFF;
SET PAGESIZE 100;
SET LINESIZE 180;

COLUMN username FORMAT A24;
COLUMN full_name FORMAT A32;
COLUMN role_code FORMAT A24;
COLUMN channel_code FORMAT A16;
COLUMN template_code FORMAT A34;

PROMPT Demo users:
SELECT u.username, u.full_name, r.role_code
FROM hgb_users u
LEFT JOIN hgb_roles r ON r.role_id = u.primary_role_id
WHERE u.username IN ('admin.demo', 'kalite.demo', 'birim.demo')
ORDER BY u.username;

PROMPT Enabled channels:
SELECT channel_code, channel_name
FROM hgb_channels
WHERE is_enabled = 1
ORDER BY channel_code;

PROMPT Published survey templates:
SELECT st.template_code, st.template_name, v.version_no, v.status
FROM hgb_survey_templates st
JOIN hgb_survey_template_versions v ON v.survey_template_id = st.survey_template_id
WHERE v.status = 'PUBLISHED'
ORDER BY st.template_code, v.version_no;

PROMPT Dashboard rows:
SELECT COUNT(*) AS dashboard_row_count
FROM hgb_v_feedback_dashboard;

PROMPT Open recovery rows:
SELECT COUNT(*) AS open_recovery_row_count
FROM hgb_v_open_recovery_cases;

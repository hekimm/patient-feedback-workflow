# Traceability Checklist

This checklist maps the requirement analysis document to source-level evidence. Production acceptance still requires deployment-specific endpoint credentials and live integration sign-off.

## Functional Requirements

| Requirement | Status | Evidence |
| --- | --- | --- |
| FR-101 clinical event ingestion | Implemented | `POST /api/hbys/events`, `ClinicalEventIngestionService` |
| FR-102 event-specific delay | Implemented | `TriggerRule.DelayMinutes`, `SurveyDispatchService.ProcessPendingEventsAsync` |
| FR-103 sensitive case exclusion | Implemented | `ClinicalEvent.IsSensitiveCase` skip logic |
| FR-104 frequency cap | Implemented | `DispatchRepository.CountRecentInvitationsAsync` |
| FR-105 reminders | Implemented | `SurveyDispatchService.ProcessRemindersAsync` |
| FR-106 trigger-rule UI | Implemented | `TriggerRulesController`, `Views/TriggerRules` |
| FR-201 SMS single-use secure link | Implemented | `TokenService`, `ProbelSmsGatewayClient`, `SurveyDispatchService` |
| FR-202 WhatsApp invite/chat survey | Implemented | `WhatsAppSurveyClient`, `WhatsAppChatSurveyService` |
| FR-203 QR physical access | Implemented | QR invitation detail and kiosk/QR flows |
| FR-204 portal/mobile access | Implemented | `POST /api/survey-invitations` token API |
| FR-205 kiosk/tablet mode | Implemented | `KioskController`, `Views/Kiosk` |
| FR-206 channel fallback | Implemented | primary/fallback dispatch logic |
| FR-301 single initial question | Implemented | `SurveyFlowService.GetFirstQuestionAsync` |
| FR-302 branching | Implemented | `SurveyFlowService.GetNextQuestionIdAsync`, `HGB_BRANCHING_RULES` |
| FR-303 question types | Implemented | NPS/CSAT/CES, five-point rating, multiple choice, free text views/models |
| FR-304 optional free text | Implemented | `SurveyController.SubmitAnswer` free-text skip handling |
| FR-305 clinical context tagging | Implemented | response hospital/branch/department/doctor/service fields |
| FR-306 no-code survey builder | Implemented | `SurveyTemplatesController`, `SurveyTemplateRepository` |
| FR-307 multilingual surveys | Implemented | TR/EN/AR text fields, `SurveyTexts`, invitation link carries patient preferred language |
| FR-308 survey length limit | Implemented | `SurveyTemplatesController` max 10 question guard |
| FR-401 token-only patient access | Implemented | `SurveyController`, token validation |
| FR-402 mobile-first responsive UI | Implemented | `wwwroot/css/survey.css` |
| FR-403 anonymous response | Implemented | consent anonymous option and response anonymization |
| FR-404 progress indicator | Implemented | `SurveyFlowService.GetProgressAsync`, `Views/Survey/Question.cshtml` |
| FR-405 low-literacy scale | Implemented | five-point numeric rating UI support |
| FR-501 low-score alert | Implemented | `SurveyFlowService.CreateRecoveryCaseAsync` alert creation |
| FR-502 responsible unit routing | Implemented | service recovery case department fields and screens |
| FR-503 recovery workflow | Implemented | assign/action/close/escalate in `ServiceRecoveryController` |
| FR-504 sentiment analysis | Implemented | `LocalLexiconSentimentAnalyzer`, `SentimentService` |
| FR-505 theme categorization | Implemented | `HGB_THEME_CATEGORIES`, sentiment/theme persistence |
| FR-506 SLA escalation | Implemented | `MaintenanceService.EscalateOverdueCasesAsync` |
| FR-601 dashboard filters | Implemented | dashboard repository filter/scope parameters |
| FR-602 NPS/CSAT/CES metrics | Implemented | scoring/finalization and dashboard metric queries |
| FR-603 trend reports | Implemented | `DashboardRepository.GetTrendAsync` |
| FR-604 PDF/Excel export | Implemented | `ReportExportService` |
| FR-605 role-based dashboard views | Implemented | `RoleAuthorizeAttribute`, scoped dashboard calls |
| FR-606 KPI targets | Implemented | `KpiRepository`, `DashboardService.GetKpiComparisonsAsync` |
| FR-607 BI export | Implemented | `IBiExportClient`, `ProbelBiExportClient`, BI queue/view |
| FR-701 consent before feedback | Implemented | consent gate before survey start |
| FR-702 KVKK disclosure | Implemented | consent texts and records |
| FR-703 pseudonymized reporting | Implemented | PII encryption/hash and anonymous reporting fields |
| FR-704 retention/anonymization | Implemented | `MaintenanceService.ApplyRetentionPoliciesAsync` |
| FR-705 data subject requests | Implemented | `ComplianceController`, DSR export/anonymize/scrub |
| FR-706 personal-data access audit | Implemented | audit logs with hashed IP and DSR/export entries |
| FR-801 admin management | Implemented | templates, channels, triggers, users, roles and compliance screens |
| FR-802 user/role/permission management | Implemented | users/roles repositories and screens |
| FR-803 integration logging | Implemented | `HGB_INTEGRATION_LOGS`, provider response/correlation fields |
| FR-804 administrative audit | Implemented | audit service and admin action logging |

## Non-Functional Requirements

| Requirement | Status | Evidence |
| --- | --- | --- |
| NFR-101 survey page load target | Ready for acceptance | CDN-free local assets, mobile-first UI; measure in target network |
| NFR-102 low-score alert latency | Ready for acceptance | alert/recovery created during survey finalization; measure in target Oracle |
| NFR-103 concurrency | Ready for acceptance | stateless MVC + Oracle persistence; load test required in target environment |
| NFR-201 completion | Implemented | short survey, branching, skip handling |
| NFR-202 usability | Implemented | mobile-first patient flow and admin workflows |
| NFR-203 limited required steps | Implemented | max question guard and optional free text |
| NFR-301 WCAG 2.1 AA target | Implemented | focus/touch/contrast hardening; axe validation recommended |
| NFR-302 screen reader support | Implemented | semantic HTML and aria progress labels |
| NFR-303 contrast | Implemented | survey CSS contrast pass target |
| NFR-304 touch targets | Implemented | 48px minimum patient controls |
| NFR-401 availability/readiness | Implemented | `/health/live`, `/health/ready` |
| NFR-402 resilience | Implemented | background job retry/error isolation |
| NFR-501 data in transit | Deployment requirement | enforce HTTPS/TLS at IIS/load balancer |
| NFR-502 data at rest | Implemented | AES-GCM PII encryption and hashed lookup fields |
| NFR-503 single-use token | Implemented | token hash, expiry, used timestamp |
| NFR-504 KVKK compliance | Implemented | consent, retention, DSR, audit, anonymization |
| NFR-601 integration resilience | Implemented | timeout/retry/backoff, HMAC signatures, replay protection |
| NFR-602 browser support | Implemented | no external CDN, standards-based HTML/CSS/JS |
| NFR-603 database | Implemented | idempotent Oracle install/hardening scripts |
| NFR-801 deployment environment | Implemented | on-prem IIS `web.config`, runbook; cloud-compatible source |

## Explicitly Out Of Scope

- Automatic patient-facing response generation.
- External benchmark integration with other institutions.

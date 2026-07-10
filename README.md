# Patient Feedback Workflow

An ASP.NET Core MVC project I built during my Software Engineering internship at Probel HBYS. It explores a patient-feedback workflow from clinical-event ingestion and survey delivery to service recovery and aggregate reporting.

> **Internship and data notice**
>
> This repository documents an internship project, not a live deployment. Every application record in the demo is entirely mock and synthetic, including patient, staff-user, institution, identifier, clinical-event, survey, score, and operational data. None of it is derived from or intended to represent real people, customers, hospitals, Probel HBYS environments, integrations, workflows, or production activity. No real patient data or production secrets are included.

## What I Worked On

I used this project to learn how a small product feature crosses several system boundaries. Sending a survey also means deciding who is eligible, when delivery should happen, how consent is recorded, which failures are visible, and how a low score becomes follow-up work.

The implemented scope includes:

- signed clinical-event ingestion with replay protection;
- trigger rules, delayed dispatch, reminders, exclusions, and frequency limits;
- configurable SMS and WhatsApp adapters, plus QR, kiosk, portal, and mobile entry points;
- a token-based, mobile-first survey flow with branching, consent, and an anonymous-response option;
- NPS, CSAT, and CES scoring with deterministic local sentiment and theme classification;
- alerts, service-recovery cases, dashboards, KPI tracking, and PDF/Excel exports;
- PII encryption and lookup hashing, role and scope checks, audit logs, retention, and data-subject request workflows.

## Workflow

```text
Clinical event
  -> eligibility and trigger evaluation
  -> queued invitation
  -> SMS / WhatsApp / QR / portal
  -> tokenized survey and consent
  -> scoring and alerts
  -> service recovery, dashboard, and export
```

## Implementation Choices

- MVC controllers delegate workflow decisions to application services, while repositories keep Oracle access separate.
- Dapper keeps the SQL and stored workflow state visible when debugging an invitation or delivery attempt.
- Hosted services process background delivery, maintenance, and migration work.
- Sentiment analysis uses a small local Turkish lexicon. Its behavior is deterministic and inspectable, but deliberately limited.
- SMS, WhatsApp, and BI integrations are configurable HTTP adapter boundaries; they are not presented as validated live integrations.
- Production configuration and schema checks fail early when required secrets, hardening objects, or safe settings are missing.

## Technology

| Area | Choice |
| --- | --- |
| Application | .NET 8, ASP.NET Core MVC, Razor |
| Data access | Oracle Managed Data Access and Dapper |
| Background work | ASP.NET Core hosted services |
| Integration boundaries | Configurable HTTP clients for SMS, WhatsApp, and BI |
| Documents | QuestPDF for PDF, ClosedXML for Excel, QRCoder for QR codes |
| Security controls | BCrypt, HMAC signatures, replay checks, encrypted PII, and hashed tokens |
| Tests | xUnit |
| Deployment design | Windows/IIS with Oracle |
| Local database | Oracle Free in Docker for demo use only |

## Repository Map

- `HastaGeriBildirim/Controllers` contains MVC and API entry points.
- `HastaGeriBildirim/Services` contains survey, dispatch, security, maintenance, reporting, and integration workflows.
- `HastaGeriBildirim/Repositories` contains Oracle persistence implemented with Dapper.
- `HastaGeriBildirim/db` contains separate database install, hardening, verification, and demo scripts.
- `HastaGeriBildirim/docs` contains deployment notes and requirement traceability.
- `HastaGeriBildirim.Tests` covers tokens, PII cryptography, local sentiment, configuration validation, and report export.

## Run the Local Demo

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Windows PowerShell 5.1 or later

From the repository root:

```powershell
.\setup-local-db.ps1
.\run-hgb-ui.ps1
```

The setup script creates an Oracle Free container, installs the schema, and loads synthetic demo records. The application is then available at:

- `http://localhost:5080` for the login screen;
- `http://localhost:5080/health/live` for the liveness check;
- `http://localhost:5080/health/ready` for configuration and Oracle readiness.

The local-only accounts are:

- `admin.demo / Admin123!`
- `kalite.demo / Kalite123!`
- `birim.demo / Birim123!`

To rebuild the local database:

```powershell
.\setup-local-db.ps1 -Reset
```

`run-hgb-ui.ps1` uses `ConnectionStrings__OracleDb` when it is already set. Otherwise, it reads the default local `patient-oracle` container configuration. If you use custom `-Port`, `-ContainerName`, or `-AppPassword` values, set the connection string printed by the setup script before starting the application.

## Use an Existing Oracle Database

SQL*Plus or SQLcl is required for this path.

1. Create or unlock the application user as `SYSTEM`:

   ```text
   sqlplus system/<password>@//host:1521/<service> @HastaGeriBildirim/db/setup-oracle-permissions.sql "<app-password>"
   ```

2. Install the schema as `patient_app`:

   ```sql
   @HastaGeriBildirim/db/install-production.sql
   ```

   Use `install-demo.sql` instead only in a local environment that needs synthetic users and records. The `install-production.sql` name describes the non-demo install path; it is not, by itself, a production-readiness guarantee.

3. Set the connection string and start the application:

   ```powershell
   $env:ConnectionStrings__OracleDb = 'User Id=patient_app;Password=<app-password>;Data Source=host:1521/<service>;'
   .\run-hgb-ui.ps1
   ```

## Build and Test

```powershell
dotnet restore HastaGeriBildirim.sln
dotnet build HastaGeriBildirim.sln
dotnet test HastaGeriBildirim.sln
```

Stop the local UI before rebuilding in the same Windows workspace because the running process locks `HastaGeriBildirim.exe`.

## Current Boundaries

- This is an internship implementation, not official documentation for a deployed Probel HBYS system.
- Anonymous responses are supported as an option; not every survey response is anonymous.
- The local sentiment analyzer is an explainable fallback, not a clinical model.
- The project does not generate automated patient-facing replies or provide external institution benchmarking.
- A real deployment would still require approved integration contracts, environment-specific security review, monitoring, load testing, and operational acceptance.

## Related Documentation

- [Database scripts](HastaGeriBildirim/db/README.md)
- [Deployment runbook](HastaGeriBildirim/docs/production-runbook.md)
- [Report export template](HastaGeriBildirim/docs/report-export-template.md)
- [Requirement traceability](HastaGeriBildirim/docs/traceability-checklist.md)
- [Security policy](SECURITY.md)

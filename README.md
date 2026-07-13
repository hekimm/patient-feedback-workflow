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
- Sentiment analysis uses a small local Turkish lexicon. It only matches defined terms, so results are deterministic and easy to inspect.
- SMS, WhatsApp, and BI integrations use configurable HTTP clients. They have not been tested against live provider systems.
- Production configuration and schema checks fail early when required secrets, schema objects, or safe settings are missing.

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
- `HastaGeriBildirim/db` contains ordered admin, schema, reference-data, bootstrap, demo, and verification modules.
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
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__OracleDb = 'User Id=patient_app;Password=OraclePass_12345;Data Source=127.0.0.1:1521/FREEPDB1;Connection Timeout=5;'
dotnet run --project .\HastaGeriBildirim\HastaGeriBildirim.csproj -- --urls http://localhost:5080
```

The setup script starts a version-pinned Oracle Free container, waits for its
SQL health check, and runs the same schema/demo manifests used by manual setup.
The application is then available at:

- `http://localhost:5080` for the login screen;
- `http://localhost:5080/health/live` for the liveness check;
- `http://localhost:5080/health/ready` for configuration and Oracle readiness.

The local-only accounts are:

- `admin.demo / Admin123!`
- `kalite.demo / Kalite123!`
- `birim.demo / Birim123!`

To delete and rebuild the local container and all of its data:

```powershell
.\setup-local-db.ps1 -Reset
```

If you use custom `-Port`, `-AppPassword`, or `-OracleImage` values, use the
connection string printed by the setup script before starting the application.

## Use an Existing Oracle Database

SQL*Plus or SQLcl is required. The database setup uses separate modules so each
step can be reviewed, rerun, and resumed after a failure.

1. Set the client encoding and run the two DBA modules for the application
   schema and approved tablespace:

   ```powershell
   $env:NLS_LANG = '.AL32UTF8'
   ```

2. As `patient_app`, run numbered schema, index, view, foreign-key,
   reference-data, and verification modules in the documented order.

3. For Production, generate a bcrypt hash with
   `--generate-password-hash`, run the separate first-admin bootstrap,
   execute the Production verifier, and revoke install-only DDL privileges.

The [database module guide](HastaGeriBildirim/db/README.md) lists the module
order, dependencies, rerun steps, demo restrictions, and optional manifests.
Do not run `db/demo/` files in Production.

For local/development use with an existing Oracle database, set the connection
string and start the Development runner:

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = 'Development'
   $env:ConnectionStrings__OracleDb = 'User Id=patient_app;Password=<app-password>;Data Source=host:1521/<service>;'
   dotnet run --project .\HastaGeriBildirim\HastaGeriBildirim.csproj -- --urls http://localhost:5080
   ```

For Production, follow the
[IIS production runbook](HastaGeriBildirim/docs/production-runbook.md).

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
- The local sentiment analyzer is a small rule-based fallback, not a clinical model.
- The project does not generate automated patient-facing replies or provide external institution benchmarking.
- A real deployment would still require approved integration contracts, environment-specific security review, monitoring, load testing, and operational acceptance.

## Related Documentation

- [Database scripts](HastaGeriBildirim/db/README.md)
- [Deployment runbook](HastaGeriBildirim/docs/production-runbook.md)
- [Report export template](HastaGeriBildirim/docs/report-export-template.md)
- [Requirement traceability](HastaGeriBildirim/docs/traceability-checklist.md)
- [Security policy](SECURITY.md)

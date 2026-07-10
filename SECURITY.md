# Security Policy

## Supported Version

The `main` branch is the supported development line until a formal release tag is created.

## Secret Handling

Do not commit real credentials, tokens, connection strings, private keys or production endpoint secrets.

Use environment variables or a managed secret store for:

- Oracle connection string
- PII encryption key
- token hash salt
- webhook API key and HMAC secret
- SMS, WhatsApp and BI provider credentials

`appsettings.Production.json` and `.env.example` intentionally contain empty values.

## Demo Data

`HastaGeriBildirim/db/demo-seed.sql` and `set-demo-password-hashes.sql` are for local/demo use only. Production startup rejects active demo users.

All data in this repository and its local demo environment is mock data. It does not represent real people, patients, institutions, or production records.

## Reporting Issues

Report suspected vulnerabilities privately to the repository owner. Do not open a public issue containing secrets, patient data or exploit details.

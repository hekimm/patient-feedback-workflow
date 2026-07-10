using System.Data;
using Dapper;
using HastaGeriBildirim.Data;
using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Services;

namespace HastaGeriBildirim.Repositories;

public class ClinicalEventRepository
{
    private readonly OracleConnectionFactory _connectionFactory;
    private readonly IPiiCryptoService _piiCryptoService;

    public ClinicalEventRepository(
        OracleConnectionFactory connectionFactory,
        IPiiCryptoService piiCryptoService)
    {
        _connectionFactory = connectionFactory;
        _piiCryptoService = piiCryptoService;
    }

    public async Task<List<ClinicalEvent>> GetRecentEventsAsync(int limit = 50)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                CLINICAL_EVENT_ID as EventId,
                EXTERNAL_EVENT_REF as ExternalEventId,
                EVENT_TYPE as EventType,
                PATIENT_ID as PatientId,
                HOSPITAL_ID as HospitalId,
                BRANCH_ID as BranchId,
                DEPARTMENT_ID as DepartmentId,
                DOCTOR_ID as DoctorId,
                SERVICE_ID as ServiceId,
                EVENT_TIME as EventDate,
                IS_SENSITIVE as IsSensitiveCase,
                0 as IsFrequencyCapped,
                STATUS as Status,
                CREATED_AT as CreatedAt
            FROM HGB_CLINICAL_EVENTS
            ORDER BY CREATED_AT DESC
            FETCH FIRST :Limit ROWS ONLY";

        var results = await connection.QueryAsync<ClinicalEvent>(sql, new { Limit = limit });
        return results.ToList();
    }

    public async Task<ClinicalEvent?> GetEventByIdAsync(int clinicalEventId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                CLINICAL_EVENT_ID as EventId,
                EXTERNAL_EVENT_REF as ExternalEventId,
                EVENT_TYPE as EventType,
                PATIENT_ID as PatientId,
                HOSPITAL_ID as HospitalId,
                BRANCH_ID as BranchId,
                DEPARTMENT_ID as DepartmentId,
                DOCTOR_ID as DoctorId,
                SERVICE_ID as ServiceId,
                EVENT_TIME as EventDate,
                IS_SENSITIVE as IsSensitiveCase,
                0 as IsFrequencyCapped,
                STATUS as Status,
                CREATED_AT as CreatedAt
            FROM HGB_CLINICAL_EVENTS
            WHERE CLINICAL_EVENT_ID = :ClinicalEventId";

        return await connection.QueryFirstOrDefaultAsync<ClinicalEvent>(sql, new { ClinicalEventId = clinicalEventId });
    }

    public async Task<int?> GetEventIdByExternalRefAsync(string? externalEventRef, string sourceSystem)
    {
        if (string.IsNullOrWhiteSpace(externalEventRef))
            return null;

        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<int?>(@"
            SELECT CLINICAL_EVENT_ID
            FROM HGB_CLINICAL_EVENTS
            WHERE EXTERNAL_EVENT_REF = :ExternalEventRef
              AND SOURCE_SYSTEM = :SourceSystem
            FETCH FIRST 1 ROWS ONLY",
            new { ExternalEventRef = externalEventRef, SourceSystem = sourceSystem });
    }

    public async Task<int> CreateEventAsync(ClinicalEvent clinicalEvent, string sourceSystem)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO HGB_CLINICAL_EVENTS
            (EXTERNAL_EVENT_REF, SOURCE_SYSTEM, EVENT_TYPE, PATIENT_ID,
             HOSPITAL_ID, BRANCH_ID, DEPARTMENT_ID, DOCTOR_ID, SERVICE_ID,
             EVENT_TIME, IS_SENSITIVE, SENSITIVITY_REASON, STATUS)
            VALUES
            (:ExternalEventRef, :SourceSystem, :EventType, :PatientId,
             :HospitalId, :BranchId, :DepartmentId, :DoctorId, :ServiceId,
             :EventTime, :IsSensitive, :SensitivityReason, 'RECEIVED')
            RETURNING CLINICAL_EVENT_ID INTO :EventId";

        var parameters = new DynamicParameters();
        parameters.Add("ExternalEventRef", clinicalEvent.ExternalEventId);
        parameters.Add("SourceSystem", sourceSystem);
        parameters.Add("EventType", clinicalEvent.EventType);
        parameters.Add("PatientId", clinicalEvent.PatientId);
        parameters.Add("HospitalId", clinicalEvent.HospitalId);
        parameters.Add("BranchId", clinicalEvent.BranchId);
        parameters.Add("DepartmentId", clinicalEvent.DepartmentId);
        parameters.Add("DoctorId", clinicalEvent.DoctorId);
        parameters.Add("ServiceId", clinicalEvent.ServiceId);
        parameters.Add("EventTime", clinicalEvent.EventDate);
        parameters.Add("IsSensitive", clinicalEvent.IsSensitiveCase ? 1 : 0);
        parameters.Add("SensitivityReason", clinicalEvent.IsSensitiveCase ? "Kullanıcı tarafından hassas işaretlendi" : null);
        parameters.Add("EventId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<int>("EventId");
    }

    public class PatientUpsert
    {
        public int? PatientId { get; set; }
        public string? ExternalPatientRef { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string PreferredLanguage { get; set; } = "tr";
        public bool AllowContact { get; set; } = true;
    }

    public async Task<int?> UpsertPatientAsync(PatientUpsert patient)
    {
        if (patient.PatientId.HasValue)
            return patient.PatientId.Value;

        using var connection = _connectionFactory.CreateConnection();

        var phoneHash = _piiCryptoService.HashForLookup(patient.Phone);
        var emailHash = _piiCryptoService.HashForLookup(patient.Email);

        int? existingId = null;

        if (!string.IsNullOrWhiteSpace(patient.ExternalPatientRef))
        {
            existingId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT PATIENT_ID FROM HGB_PATIENTS WHERE EXTERNAL_PATIENT_REF = :ExternalPatientRef AND IS_DELETED = 0",
                new { patient.ExternalPatientRef });
        }

        if (!existingId.HasValue && !string.IsNullOrWhiteSpace(phoneHash))
        {
            existingId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT PATIENT_ID FROM HGB_PATIENTS WHERE PHONE_HASH = :PhoneHash AND IS_DELETED = 0",
                new { PhoneHash = phoneHash });
        }

        var encryptedPhone = _piiCryptoService.Encrypt(patient.Phone);
        var encryptedEmail = _piiCryptoService.Encrypt(patient.Email);

        if (existingId.HasValue)
        {
            var updateSql = @"
                UPDATE HGB_PATIENTS
                SET FULL_NAME = COALESCE(:FullName, FULL_NAME),
                    PHONE = CASE WHEN :PhoneEnc IS NOT NULL THEN NULL ELSE PHONE END,
                    PHONE_ENC = COALESCE(:PhoneEnc, PHONE_ENC),
                    PHONE_HASH = COALESCE(:PhoneHash, PHONE_HASH),
                    EMAIL = CASE WHEN :EmailEnc IS NOT NULL THEN NULL ELSE EMAIL END,
                    EMAIL_ENC = COALESCE(:EmailEnc, EMAIL_ENC),
                    EMAIL_HASH = COALESCE(:EmailHash, EMAIL_HASH),
                    PREFERRED_LANGUAGE = :PreferredLanguage,
                    ALLOW_CONTACT = :AllowContact,
                    UPDATED_AT = SYSTIMESTAMP
                WHERE PATIENT_ID = :PatientId";

            await connection.ExecuteAsync(updateSql, new
            {
                PatientId = existingId.Value,
                patient.FullName,
                PhoneEnc = encryptedPhone,
                PhoneHash = phoneHash,
                EmailEnc = encryptedEmail,
                EmailHash = emailHash,
                patient.PreferredLanguage,
                AllowContact = patient.AllowContact ? 1 : 0
            });

            return existingId.Value;
        }

        var insertSql = @"
            INSERT INTO HGB_PATIENTS
            (EXTERNAL_PATIENT_REF, FULL_NAME, PHONE, PHONE_ENC, PHONE_HASH,
             EMAIL, EMAIL_ENC, EMAIL_HASH, PREFERRED_LANGUAGE, ALLOW_CONTACT, IS_DELETED)
            VALUES
            (:ExternalPatientRef, :FullName, :Phone, :PhoneEnc, :PhoneHash,
             :Email, :EmailEnc, :EmailHash, :PreferredLanguage, :AllowContact, 0)
            RETURNING PATIENT_ID INTO :PatientId";

        var parameters = new DynamicParameters();
        parameters.Add("ExternalPatientRef", patient.ExternalPatientRef);
        parameters.Add("FullName", patient.FullName);
        parameters.Add("Phone", null);
        parameters.Add("PhoneEnc", encryptedPhone);
        parameters.Add("PhoneHash", phoneHash);
        parameters.Add("Email", null);
        parameters.Add("EmailEnc", encryptedEmail);
        parameters.Add("EmailHash", emailHash);
        parameters.Add("PreferredLanguage", patient.PreferredLanguage);
        parameters.Add("AllowContact", patient.AllowContact ? 1 : 0);
        parameters.Add("PatientId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(insertSql, parameters);
        return parameters.Get<int>("PatientId");
    }

    public class LookupItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class DepartmentLookup
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public int HospitalId { get; set; }
    }

    public async Task<List<LookupItem>> GetPatientLookupAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT PATIENT_ID as Id, COALESCE(FULL_NAME, TO_NCHAR(PSEUDONYM_CODE)) as Name
            FROM HGB_PATIENTS
            WHERE IS_DELETED = 0
            ORDER BY FULL_NAME";

        var results = await connection.QueryAsync<LookupItem>(sql);
        return results.ToList();
    }

    public async Task<List<DepartmentLookup>> GetDepartmentLookupAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                d.DEPARTMENT_ID as Id,
                d.DEPARTMENT_NAME as Name,
                d.BRANCH_ID as BranchId,
                b.HOSPITAL_ID as HospitalId
            FROM HGB_DEPARTMENTS d
            JOIN HGB_BRANCHES b ON d.BRANCH_ID = b.BRANCH_ID
            WHERE d.STATUS = 'ACTIVE'
            ORDER BY d.DEPARTMENT_NAME";

        var results = await connection.QueryAsync<DepartmentLookup>(sql);
        return results.ToList();
    }

    public async Task<List<LookupItem>> GetDoctorLookupAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT DOCTOR_ID as Id, FULL_NAME as Name
            FROM HGB_DOCTORS
            WHERE STATUS = 'ACTIVE'
            ORDER BY FULL_NAME";

        var results = await connection.QueryAsync<LookupItem>(sql);
        return results.ToList();
    }

    public async Task<List<LookupItem>> GetServiceLookupAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT SERVICE_ID as Id, SERVICE_NAME as Name
            FROM HGB_SERVICES
            WHERE STATUS = 'ACTIVE'
            ORDER BY SERVICE_NAME";

        var results = await connection.QueryAsync<LookupItem>(sql);
        return results.ToList();
    }
}

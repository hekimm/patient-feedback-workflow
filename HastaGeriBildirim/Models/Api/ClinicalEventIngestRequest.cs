namespace HastaGeriBildirim.Models.Api;

public class ClinicalEventIngestRequest
{
    public string? SourceSystem { get; set; }
    public string? ExternalEventRef { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int? PatientId { get; set; }
    public PatientPayload? Patient { get; set; }
    public int? HospitalId { get; set; }
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int? DoctorId { get; set; }
    public int? ServiceId { get; set; }
    public DateTime? EventTime { get; set; }
    public bool IsSensitive { get; set; }
    public string? SensitivityReason { get; set; }
    public bool ProcessImmediately { get; set; }
}

public class PatientPayload
{
    public int? PatientId { get; set; }
    public string? ExternalPatientRef { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PreferredLanguage { get; set; }
    public bool? AllowContact { get; set; }
}

public class ClinicalEventIngestResponse
{
    public int EventId { get; set; }
    public string Status { get; set; } = "RECEIVED";
}


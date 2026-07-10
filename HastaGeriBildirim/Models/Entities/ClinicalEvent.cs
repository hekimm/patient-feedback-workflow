namespace HastaGeriBildirim.Models.Entities;

public class ClinicalEvent
{
    public int EventId { get; set; }
    public string? ExternalEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int? PatientId { get; set; }
    public int? HospitalId { get; set; }
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int? DoctorId { get; set; }
    public int? ServiceId { get; set; }
    public DateTime EventDate { get; set; }
    public bool IsSensitiveCase { get; set; }
    public bool IsFrequencyCapped { get; set; }
    public string Status { get; set; } = "RECEIVED";
    public DateTime CreatedAt { get; set; }
}

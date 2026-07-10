namespace HastaGeriBildirim.Models.Entities;

public class ServiceRecoveryCase
{
    public int CaseId { get; set; }
    public int AlertId { get; set; }
    public int ResponseId { get; set; }
    public int? PatientId { get; set; }
    public int? DepartmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int? AssignedTo { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosureNote { get; set; }
    public DateTime CreatedAt { get; set; }
}

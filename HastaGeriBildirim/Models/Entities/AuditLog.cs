namespace HastaGeriBildirim.Models.Entities;

public class AuditLog
{
    public int LogId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public int? PerformedBy { get; set; }
    public int? PatientId { get; set; }
    public string? ChangeDescription { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

namespace HastaGeriBildirim.Models.Entities;

public class Alert
{
    public int AlertId { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? ResponseId { get; set; }
    public int? ClinicalEventId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? AssignedTo { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

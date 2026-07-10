namespace HastaGeriBildirim.Models.Entities;

public class DataSubjectRequest
{
    public int DsrId { get; set; }
    public int? PatientId { get; set; }
    public string? PatientName { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string RequestStatus { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? RequestedByNote { get; set; }
    public string? HandledByName { get; set; }
    public string? ResolutionNote { get; set; }
}

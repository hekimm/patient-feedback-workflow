namespace HastaGeriBildirim.Models.Entities;

public class IntegrationLog
{
    public int LogId { get; set; }
    public int SystemId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public int? HttpStatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

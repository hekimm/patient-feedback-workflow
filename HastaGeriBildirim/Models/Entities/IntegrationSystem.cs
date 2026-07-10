namespace HastaGeriBildirim.Models.Entities;

public class IntegrationSystem
{
    public int IntegrationSystemId { get; set; }
    public string SystemCode { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public bool IsEnabled { get; set; }
    public string? AuthType { get; set; }
    public DateTime CreatedAt { get; set; }
}

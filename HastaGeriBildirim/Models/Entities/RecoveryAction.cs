namespace HastaGeriBildirim.Models.Entities;

public class RecoveryAction
{
    public int ActionId { get; set; }
    public int CaseId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int PerformedBy { get; set; }
    public string? ActionNote { get; set; }
    public DateTime CreatedAt { get; set; }
}

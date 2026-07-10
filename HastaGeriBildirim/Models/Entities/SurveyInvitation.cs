namespace HastaGeriBildirim.Models.Entities;

public class SurveyInvitation
{
    public int InvitationId { get; set; }
    public int ClinicalEventId { get; set; }
    public int TemplateId { get; set; }
    public int? PatientId { get; set; }
    public int ChannelId { get; set; }
    public string InvitationToken { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
    public bool IsDelivered { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

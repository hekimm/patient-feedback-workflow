namespace HastaGeriBildirim.Models.Entities;

public class DeliveryAttempt
{
    public int DeliveryAttemptId { get; set; }
    public int InvitationId { get; set; }
    public int ChannelId { get; set; }
    public string? ChannelName { get; set; }
    public int AttemptNo { get; set; }
    public string DeliveryStatus { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

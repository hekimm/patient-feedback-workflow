namespace HastaGeriBildirim.Models.ViewModels;

public class InvitationListViewModel
{
    public List<InvitationSummary> Invitations { get; set; } = new();
    public string? StatusFilter { get; set; }
}

public class InvitationSummary
{
    public int InvitationId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class InvitationDetailViewModel
{
    public InvitationSummary Invitation { get; set; } = new();
    public List<Entities.DeliveryAttempt> DeliveryAttempts { get; set; } = new();

    public string? FreshSurveyLink { get; set; }
    public string? FreshQrSvg { get; set; }
}

namespace HastaGeriBildirim.Services.Integrations;

public sealed record IntegrationSendRequest(
    int InvitationId,
    string ChannelCode,
    string? RecipientPhone,
    string Message,
    string SurveyLink,
    string LanguageCode,
    bool IsReminder);

public sealed record IntegrationSendResult(
    bool Success,
    int? HttpStatusCode,
    string? ProviderMessageId,
    string? ErrorMessage,
    string? ResponsePayload);


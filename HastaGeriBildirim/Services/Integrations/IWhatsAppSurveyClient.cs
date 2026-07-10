namespace HastaGeriBildirim.Services.Integrations;

public interface IWhatsAppSurveyClient : ISurveyChannelClient
{
    Task<IntegrationSendResult> SendTextMessageAsync(
        string recipientPhone,
        string message,
        CancellationToken cancellationToken = default);
}

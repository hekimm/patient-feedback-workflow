namespace HastaGeriBildirim.Services.Integrations;

public interface ISurveyChannelClient
{
    string ChannelCode { get; }

    string IntegrationSystemCode { get; }

    Task<IntegrationSendResult> SendSurveyInvitationAsync(
        IntegrationSendRequest request,
        CancellationToken cancellationToken = default);
}

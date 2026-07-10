using HastaGeriBildirim.Models.Api;
using HastaGeriBildirim.Services;
using Microsoft.AspNetCore.Mvc;

namespace HastaGeriBildirim.Controllers.Api;

[ApiController]
[Route("api/survey-invitations")]
public class SurveyInvitationsApiController : ControllerBase
{
    private readonly IClinicalEventIngestionService _ingestionService;
    private readonly SurveyDispatchService _dispatchService;
    private readonly WebhookSecurityService _webhookSecurityService;

    public SurveyInvitationsApiController(
        IClinicalEventIngestionService ingestionService,
        SurveyDispatchService dispatchService,
        WebhookSecurityService webhookSecurityService)
    {
        _ingestionService = ingestionService;
        _dispatchService = dispatchService;
        _webhookSecurityService = webhookSecurityService;
    }

    [HttpPost]
    public async Task<ActionResult<ClinicalEventIngestResponse>> Create(
        ClinicalEventIngestRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _webhookSecurityService.VerifyHgbAsync(Request, "PROBEL_HBYS_INVITATION"))
            return Unauthorized();

        request.ProcessImmediately = true;

        var eventId = await _ingestionService.IngestAsync(request, cancellationToken);
        await _dispatchService.RunAllAsync();

        return Accepted(new ClinicalEventIngestResponse
        {
            EventId = eventId,
            Status = "INVITATION_REQUESTED"
        });
    }

}

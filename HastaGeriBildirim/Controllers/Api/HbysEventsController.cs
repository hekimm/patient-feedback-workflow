using HastaGeriBildirim.Models.Api;
using HastaGeriBildirim.Services;
using Microsoft.AspNetCore.Mvc;

namespace HastaGeriBildirim.Controllers.Api;

[ApiController]
[Route("api/hbys/events")]
public class HbysEventsController : ControllerBase
{
    private readonly IClinicalEventIngestionService _ingestionService;
    private readonly SurveyDispatchService _dispatchService;
    private readonly WebhookSecurityService _webhookSecurityService;

    public HbysEventsController(
        IClinicalEventIngestionService ingestionService,
        SurveyDispatchService dispatchService,
        WebhookSecurityService webhookSecurityService)
    {
        _ingestionService = ingestionService;
        _dispatchService = dispatchService;
        _webhookSecurityService = webhookSecurityService;
    }

    [HttpPost]
    public async Task<ActionResult<ClinicalEventIngestResponse>> Post(
        ClinicalEventIngestRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _webhookSecurityService.VerifyHgbAsync(Request, "PROBEL_HBYS"))
            return Unauthorized();

        var eventId = await _ingestionService.IngestAsync(request, cancellationToken);

        if (request.ProcessImmediately)
            await _dispatchService.RunAllAsync();

        return Accepted(new ClinicalEventIngestResponse
        {
            EventId = eventId,
            Status = request.ProcessImmediately ? "PROCESSED" : "RECEIVED"
        });
    }

}

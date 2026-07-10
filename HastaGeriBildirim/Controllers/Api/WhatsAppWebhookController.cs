using System.Text.Json;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Services;
using Microsoft.AspNetCore.Mvc;

namespace HastaGeriBildirim.Controllers.Api;

[ApiController]
[Route("api/whatsapp/webhook")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly DispatchRepository _dispatchRepository;
    private readonly IConfiguration _configuration;
    private readonly WebhookSecurityService _webhookSecurityService;
    private readonly WhatsAppChatSurveyService _whatsAppChatSurveyService;

    public WhatsAppWebhookController(
        DispatchRepository dispatchRepository,
        IConfiguration configuration,
        WebhookSecurityService webhookSecurityService,
        WhatsAppChatSurveyService whatsAppChatSurveyService)
    {
        _dispatchRepository = dispatchRepository;
        _configuration = configuration;
        _webhookSecurityService = webhookSecurityService;
        _whatsAppChatSurveyService = whatsAppChatSurveyService;
    }

    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var expected = _configuration["Integrations:WhatsApp:VerifyToken"] ??
                       Environment.GetEnvironmentVariable("WHATSAPP_VERIFY_TOKEN");

        if (!string.IsNullOrWhiteSpace(expected) &&
            mode == "subscribe" &&
            verifyToken == expected)
        {
            return Content(challenge ?? string.Empty, "text/plain");
        }

        return Unauthorized();
    }

    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        if (!await _webhookSecurityService.VerifyWhatsAppAsync(Request))
            return Unauthorized();

        var rawBody = await _webhookSecurityService.ReadRequestBodyAsync(Request);
        using var document = JsonDocument.Parse(rawBody);
        var processedMessages = await _whatsAppChatSurveyService.ProcessWebhookAsync(document.RootElement, HttpContext.RequestAborted);

        await _dispatchRepository.InsertIntegrationLogAsync(
            "WHATSAPP_BUSINESS",
            "INBOUND",
            "WEBHOOK",
            $"{{\"payloadBytes\":{rawBody.Length},\"processedMessages\":{processedMessages}}}",
            true,
            null);

        return Ok(new { status = "received", processedMessages });
    }
}

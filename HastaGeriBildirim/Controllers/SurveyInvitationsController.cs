using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Helpers;
using HastaGeriBildirim.Models.ViewModels;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Services;
using QRCoder;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("QUALITY_MANAGER")]
public class SurveyInvitationsController : BaseController
{
    private readonly DispatchRepository _dispatchRepository;
    private readonly SurveyDispatchService _dispatchService;

    public SurveyInvitationsController(
        DispatchRepository dispatchRepository,
        SurveyDispatchService dispatchService)
    {
        _dispatchRepository = dispatchRepository;
        _dispatchService = dispatchService;
    }

    public async Task<IActionResult> Index(string? status)
    {
        var model = new InvitationListViewModel
        {
            Invitations = await _dispatchRepository.GetInvitationListAsync(
                status,
                userId: HttpContext.GetUserId(),
                roleCode: HttpContext.GetRoleCode()),
            StatusFilter = status
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var invitation = await _dispatchRepository.GetInvitationSummaryAsync(
            id, HttpContext.GetUserId(), HttpContext.GetRoleCode());
        if (invitation == null)
            return NotFound();

        var model = new InvitationDetailViewModel
        {
            Invitation = invitation,
            DeliveryAttempts = await _dispatchRepository.GetDeliveryAttemptsAsync(id),
            FreshSurveyLink = TempData["FreshSurveyLink"] as string,
            FreshQrSvg = TempData["FreshQrSvg"] as string
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> RegenerateLink(int id)
    {
        var invitation = await _dispatchRepository.GetInvitationSummaryAsync(
            id, HttpContext.GetUserId(), HttpContext.GetRoleCode());
        if (invitation == null)
            return NotFound();

        if (invitation.Status == "COMPLETED")
        {
            TempData["Message"] = "Tamamlanmış davetin bağlantısı yenilenemez.";
            return RedirectToAction("Details", new { id });
        }

        var link = await _dispatchService.RegenerateInvitationLinkAsync(id, HttpContext.GetUserId()!.Value);
        TempData["FreshSurveyLink"] = link;
        TempData["FreshQrSvg"] = BuildQrSvg(link);

        return RedirectToAction("Details", new { id });
    }

    private static string BuildQrSvg(string link)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(link, QRCodeGenerator.ECCLevel.Q);
        var qr = new SvgQRCode(data);
        return qr.GetGraphic(5);
    }
}

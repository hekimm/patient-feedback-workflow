using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Helpers;
using HastaGeriBildirim.Repositories;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("QUALITY_MANAGER", "UNIT_MANAGER")]
public class AlertsController : BaseController
{
    private readonly AlertRepository _alertRepository;

    public AlertsController(AlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }

    public async Task<IActionResult> Index(string? alertType, string? severity, string? status)
    {
        var alerts = await _alertRepository.GetAlertsAsync(
            alertType, severity, status, HttpContext.GetUserId(), HttpContext.GetRoleCode());

        ViewBag.AlertType = alertType;
        ViewBag.Severity = severity;
        ViewBag.Status = status;

        return View(alerts);
    }

    [HttpPost]
    public async Task<IActionResult> Acknowledge(int id)
    {
        if (!await _alertRepository.CanAccessAlertAsync(id, HttpContext.GetUserId()!.Value, HttpContext.GetRoleCode()))
            return NotFound();

        await _alertRepository.AcknowledgeAlertAsync(id);
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Close(int id)
    {
        if (!await _alertRepository.CanAccessAlertAsync(id, HttpContext.GetUserId()!.Value, HttpContext.GetRoleCode()))
            return NotFound();

        await _alertRepository.CloseAlertAsync(id);
        return RedirectToAction("Index");
    }
}

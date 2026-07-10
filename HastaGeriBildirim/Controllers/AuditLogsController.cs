using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Helpers;
using HastaGeriBildirim.Repositories;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("SYS_ADMIN")]
public class AuditLogsController : BaseController
{
    private readonly AuditLogRepository _auditLogRepository;

    public AuditLogsController(AuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<IActionResult> Index(
        DateTime? startDate, 
        DateTime? endDate, 
        string? entityName, 
        string? actionCode)
    {
        if (!startDate.HasValue)
            startDate = DateTime.Now.AddDays(-7);
        
        if (!endDate.HasValue)
            endDate = DateTime.Now;

        var logs = await _auditLogRepository.GetAuditLogsAsync(
            startDate, endDate, entityName, actionCode, null);

        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EntityName = entityName;
        ViewBag.ActionCode = actionCode;

        return View(logs);
    }
}

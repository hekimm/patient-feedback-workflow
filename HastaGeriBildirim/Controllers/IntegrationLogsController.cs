using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Helpers;
using HastaGeriBildirim.Repositories;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("SYS_ADMIN")]
public class IntegrationLogsController : BaseController
{
    private readonly IntegrationLogRepository _integrationLogRepository;

    public IntegrationLogsController(IntegrationLogRepository integrationLogRepository)
    {
        _integrationLogRepository = integrationLogRepository;
    }

    public async Task<IActionResult> Index(int? systemId, string? direction, bool? isSuccess)
    {
        var logs = await _integrationLogRepository.GetIntegrationLogsAsync(
            systemId, direction, isSuccess, 100);

        ViewBag.SystemId = systemId;
        ViewBag.Direction = direction;
        ViewBag.IsSuccess = isSuccess;

        return View(logs);
    }
}

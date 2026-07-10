using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Helpers;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Services;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("SYS_ADMIN")]
public class ChannelsController : BaseController
{
    private readonly ChannelRepository _channelRepository;
    private readonly AuditService _auditService;

    public ChannelsController(
        ChannelRepository channelRepository,
        AuditService auditService)
    {
        _channelRepository = channelRepository;
        _auditService = auditService;
     }

    public async Task<IActionResult> Index()
    {
        var channels = await _channelRepository.GetAllChannelsAsync();
        return View(channels);
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id, bool enable)
    {
        await _channelRepository.SetChannelEnabledAsync(id, enable);
        await _auditService.AddLogAsync(
            "CHANNEL",
            id,
            enable ? "ENABLED" : "DISABLED",
            HttpContext.GetUserId(),
            null,
            "Kanal durumu değiştirildi",
            null);

        return RedirectToAction("Index");
    }
}

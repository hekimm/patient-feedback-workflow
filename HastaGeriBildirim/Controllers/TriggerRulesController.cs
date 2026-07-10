using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Helpers;
using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Services;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("SYS_ADMIN")]
public class TriggerRulesController : BaseController
{
    private readonly TriggerRuleRepository _triggerRuleRepository;
    private readonly ChannelRepository _channelRepository;
    private readonly AuditService _auditService;

    public TriggerRulesController(
        TriggerRuleRepository triggerRuleRepository,
        ChannelRepository channelRepository,
        AuditService auditService)
    {
        _triggerRuleRepository = triggerRuleRepository;
        _channelRepository = channelRepository;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var rules = await _triggerRuleRepository.GetAllRulesAsync();
        return View(rules);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var rule = await _triggerRuleRepository.GetRuleByIdAsync(id);
        if (rule == null)
            return NotFound();

        ViewBag.Channels = await _channelRepository.GetAllChannelsAsync();
        return View(rule);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(TriggerRule rule)
    {
        await _triggerRuleRepository.UpdateRuleAsync(rule);

        await _auditService.AddLogAsync(
            "TRIGGER_RULE", rule.TriggerRuleId, "UPDATED",
            HttpContext.GetUserId(), null,
            $"Tetikleme kuralı güncellendi. Olay: {rule.EventType}, Gecikme: {rule.DelayMinutes} dk", null);

        TempData["Message"] = "Tetikleme kuralı güncellendi.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id, bool enable)
    {
        await _triggerRuleRepository.SetRuleEnabledAsync(id, enable);

        await _auditService.AddLogAsync(
            "TRIGGER_RULE", id, enable ? "ENABLED" : "DISABLED",
            HttpContext.GetUserId(), null, "Tetikleme kuralı durumu değiştirildi", null);

        return RedirectToAction("Index");
    }
}

using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Services;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Models.ViewModels;
using HastaGeriBildirim.Helpers;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("QUALITY_MANAGER", "UNIT_MANAGER")]
public class ServiceRecoveryController : BaseController
{
    private readonly ServiceRecoveryService _recoveryService;
    private readonly ServiceRecoveryRepository _recoveryRepository;
    private readonly UserRepository _userRepository;

    public ServiceRecoveryController(
        ServiceRecoveryService recoveryService,
        ServiceRecoveryRepository recoveryRepository,
        UserRepository userRepository)
    {
        _recoveryService = recoveryService;
        _recoveryRepository = recoveryRepository;
        _userRepository = userRepository;
    }

    public async Task<IActionResult> Index()
    {
        var cases = await _recoveryRepository.GetOpenCasesAsync(HttpContext.GetUserId(), HttpContext.GetRoleCode());

        var model = new ServiceRecoveryListViewModel
        {
            Cases = cases,
            OpenCount = cases.Count(c => c.Status == "OPEN"),
            SlaWarningCount = cases.Count(c => c.IsSlaWarning),
            SlaBreachCount = cases.Count(c => c.IsSlaBreached)
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var caseDetail = await _recoveryRepository.GetCaseDetailAsync(id, HttpContext.GetUserId(), HttpContext.GetRoleCode());
        
        if (caseDetail == null)
            return NotFound();

        caseDetail.Actions = await _recoveryRepository.GetCaseActionsAsync(id);
        ViewBag.Users = await _userRepository.GetAllUsersAsync();

        return View(caseDetail);
    }

    [HttpGet]
    public IActionResult AddAction(int id)
    {
        var model = new AddActionViewModel { CaseId = id };
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> AddAction(AddActionViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = HttpContext.GetUserId()!.Value;
        if (await _recoveryRepository.GetCaseDetailAsync(model.CaseId, userId, HttpContext.GetRoleCode()) == null)
            return NotFound();

        await _recoveryService.AddActionAsync(model.CaseId, model.ActionNote, userId);

        return RedirectToAction("Details", new { id = model.CaseId });
    }

    [HttpPost]
    public async Task<IActionResult> Assign(int id, int userId)
    {
        var performedBy = HttpContext.GetUserId()!.Value;
        if (await _recoveryRepository.GetCaseDetailAsync(id, performedBy, HttpContext.GetRoleCode()) == null)
            return NotFound();

        await _recoveryService.AssignCaseAsync(id, userId, performedBy);

        return RedirectToAction("Details", new { id });
    }

    [HttpGet]
    public IActionResult Close(int id)
    {
        var model = new CloseCaseViewModel { CaseId = id };
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Close(CloseCaseViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = HttpContext.GetUserId()!.Value;
        if (await _recoveryRepository.GetCaseDetailAsync(model.CaseId, userId, HttpContext.GetRoleCode()) == null)
            return NotFound();

        await _recoveryService.CloseCaseAsync(model.CaseId, model.ClosureNote, userId);

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Escalate(int id)
    {
        var userId = HttpContext.GetUserId()!.Value;
        if (await _recoveryRepository.GetCaseDetailAsync(id, userId, HttpContext.GetRoleCode()) == null)
            return NotFound();

        await _recoveryService.EscalateCaseAsync(id, userId);

        return RedirectToAction("Details", new { id });
    }
}

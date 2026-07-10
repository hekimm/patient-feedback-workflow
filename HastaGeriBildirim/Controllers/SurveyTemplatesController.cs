using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Helpers;
using HastaGeriBildirim.Models.ViewModels;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Services;

namespace HastaGeriBildirim.Controllers;

[RoleAuthorize("QUALITY_MANAGER")]
public class SurveyTemplatesController : BaseController
{
    private readonly SurveyTemplateRepository _templateRepository;
    private readonly AuditService _auditService;

    public SurveyTemplatesController(
        SurveyTemplateRepository templateRepository,
        AuditService auditService)
    {
        _templateRepository = templateRepository;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var templates = await _templateRepository.GetAllTemplatesAsync();
        return View(templates);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string templateName, string? description)
    {
        var templateId = await _templateRepository.CreateTemplateAsync(
            templateName, description, HttpContext.GetUserId());

        await _auditService.AddLogAsync(
            "SURVEY_TEMPLATE",
            templateId,
            "CREATED",
            HttpContext.GetUserId(),
            null,
            $"Anket şablonu oluşturuldu: {templateName}",
            null);

        return RedirectToAction("Details", new { id = templateId });
    }

    public async Task<IActionResult> Details(int id, int? versionId)
    {
        var template = await _templateRepository.GetTemplateByIdAsync(id);
        
        if (template == null)
            return NotFound();

        var versions = await _templateRepository.GetVersionsAsync(id);
        var selectedVersion = versionId.HasValue
            ? versions.FirstOrDefault(v => v.VersionId == versionId.Value)
            : versions.FirstOrDefault();

        var model = new SurveyTemplateBuilderViewModel
        {
            Template = template,
            Versions = versions,
            SelectedVersion = selectedVersion
        };

        if (selectedVersion != null)
        {
            model.Questions = await _templateRepository.GetQuestionsForBuilderAsync(selectedVersion.VersionId);
            foreach (var question in model.Questions)
                model.OptionsByQuestion[question.QuestionId] = await _templateRepository.GetOptionsForBuilderAsync(question.QuestionId);

            model.BranchingRules = await _templateRepository.GetBranchingRulesForVersionAsync(selectedVersion.VersionId);
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateVersion(int templateId)
    {
        var versionId = await _templateRepository.CreateNewVersionAsync(templateId);
        await _auditService.AddLogAsync("SURVEY_TEMPLATE", templateId, "VERSION_CREATED",
            HttpContext.GetUserId(), null, $"Yeni şablon versiyonu oluşturuldu: {versionId}", null);

        return RedirectToAction("Details", new { id = templateId, versionId });
    }

    [HttpPost]
    public async Task<IActionResult> PublishVersion(int templateId, int versionId)
    {
        await _templateRepository.PublishVersionAsync(versionId);
        await _auditService.AddLogAsync("SURVEY_TEMPLATE_VERSION", versionId, "PUBLISHED",
            HttpContext.GetUserId(), null, "Anket şablon versiyonu yayınlandı", null);

        return RedirectToAction("Details", new { id = templateId, versionId });
    }

    [HttpPost]
    public async Task<IActionResult> AddQuestion(
        int templateId,
        int versionId,
        string questionType,
        string? metricType,
        string textTr,
        string? textEn,
        string? textAr,
        bool isRequired,
        bool isInitialQuestion,
        decimal? minValue,
        decimal? maxValue)
    {
        var count = await _templateRepository.CountQuestionsAsync(versionId);
        if (count >= 10)
        {
            TempData["Message"] = "Bir anket versiyonunda en fazla 10 soru olabilir.";
            return RedirectToAction("Details", new { id = templateId, versionId });
        }

        await _templateRepository.AddQuestionAsync(
            versionId, questionType, metricType, textTr, textEn, textAr,
            isRequired, isInitialQuestion, minValue, maxValue);

        return RedirectToAction("Details", new { id = templateId, versionId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteQuestion(int templateId, int versionId, int questionId)
    {
        await _templateRepository.DeleteQuestionAsync(questionId);
        return RedirectToAction("Details", new { id = templateId, versionId });
    }

    [HttpPost]
    public async Task<IActionResult> AddOption(
        int templateId,
        int versionId,
        int questionId,
        string optionText,
        decimal? numericValue)
    {
        await _templateRepository.AddOptionAsync(questionId, optionText, numericValue);
        return RedirectToAction("Details", new { id = templateId, versionId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteOption(int templateId, int versionId, int optionId)
    {
        await _templateRepository.DeleteOptionAsync(optionId);
        return RedirectToAction("Details", new { id = templateId, versionId });
    }

    [HttpPost]
    public async Task<IActionResult> AddBranchingRule(
        int templateId,
        int versionId,
        int sourceQuestionId,
        string operatorCode,
        decimal compareValue,
        int targetQuestionId)
    {
        await _templateRepository.AddBranchingRuleAsync(
            sourceQuestionId, operatorCode, compareValue, targetQuestionId);

        return RedirectToAction("Details", new { id = templateId, versionId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteBranchingRule(int templateId, int versionId, int branchingRuleId)
    {
        await _templateRepository.DeleteBranchingRuleAsync(branchingRuleId);
        return RedirectToAction("Details", new { id = templateId, versionId });
    }
}

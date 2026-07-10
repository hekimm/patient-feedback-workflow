using Microsoft.AspNetCore.Mvc;
using HastaGeriBildirim.Services;
using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Models.ViewModels;

namespace HastaGeriBildirim.Controllers;

public class SurveyController : Controller
{
    private readonly SurveyFlowService _surveyFlowService;

    public SurveyController(SurveyFlowService surveyFlowService)
    {
        _surveyFlowService = surveyFlowService;
    }

    [HttpGet]
    public async Task<IActionResult> Start(string token, string? lang, bool kiosk = false)
    {
        var (invitation, error) = await ValidateTokenAsync(token);
        if (error != null)
            return error;

        var language = SurveyTexts.NormalizeLang(lang);
        await _surveyFlowService.MarkOpenedAsync(invitation!.InvitationId);
        if (kiosk)
            HttpContext.Session.SetInt32(KioskKey(invitation.InvitationId), 1);

        var model = new SurveyStartViewModel
        {
            Token = token,
            TemplateName = SurveyTexts.Get(language, "welcome_title"),
            Lang = language
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Consent(string token, string? lang)
    {
        var (invitation, error) = await ValidateTokenAsync(token);
        if (error != null)
            return error;

        var language = SurveyTexts.NormalizeLang(lang);
        var consentText = await _surveyFlowService.GetConsentTextAsync(language);

        var model = new SurveyConsentViewModel
        {
            Token = token,
            ConsentText = consentText ?? "KVKK aydınlatma metni bulunamadı",
            Lang = language
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Consent(SurveyConsentViewModel model)
    {
        var (invitation, error) = await ValidateTokenAsync(model.Token);
        if (error != null)
            return error;

        var language = SurveyTexts.NormalizeLang(model.Lang);

        if (!model.IsConsentGiven)
            return View("ConsentRequired", language);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var responseId = await _surveyFlowService.SaveConsentAndStartResponseAsync(
            invitation!, model.IsConsentGiven, model.IsAnonymous, ipAddress, language);

        HttpContext.Session.SetInt32(ResponseKey(invitation!.InvitationId), responseId);
        HttpContext.Session.SetInt32(AnonymousKey(invitation.InvitationId), model.IsAnonymous ? 1 : 0);
        HttpContext.Session.SetString(LangKey(invitation.InvitationId), language);

        var firstQuestion = await _surveyFlowService.GetFirstQuestionAsync(invitation.TemplateId, language);
        if (firstQuestion == null)
            return RedirectToAction("Thanks", new
            {
                lang = language,
                kiosk = HttpContext.Session.GetInt32(KioskKey(invitation.InvitationId)) == 1
            });

        return RedirectToAction("Question", new { token = model.Token, questionId = firstQuestion.QuestionId });
    }

    [HttpGet]
    public async Task<IActionResult> Question(string token, int questionId)
    {
        var (invitation, error) = await ValidateTokenAsync(token);
        if (error != null)
            return error;

        var responseId = HttpContext.Session.GetInt32(ResponseKey(invitation!.InvitationId));
        if (responseId == null)
            return RedirectToAction("Consent", new { token });

        var language = HttpContext.Session.GetString(LangKey(invitation.InvitationId)) ?? "tr";

        var question = await _surveyFlowService.GetQuestionAsync(questionId, language);
        if (question == null || question.VersionId != invitation.TemplateId)
            return RedirectToAction("Thanks", new { lang = language });

        var options = await _surveyFlowService.GetOptionsAsync(questionId, language);
        var (current, total) = await _surveyFlowService.GetProgressAsync(
            invitation.TemplateId, responseId.Value, questionId);

        var model = new SurveyQuestionViewModel
        {
            Token = token,
            QuestionId = question.QuestionId,
            QuestionText = question.QuestionText,
            HelpText = question.HelpText,
            QuestionType = question.QuestionType,
            IsRequired = question.IsRequired,
            MinValue = (int)(question.MinValue ?? DefaultMinFor(question.QuestionType)),
            MaxValue = (int)(question.MaxValue ?? DefaultMaxFor(question.QuestionType)),
            Options = options.Select(o => new QuestionOption
            {
                OptionId = o.OptionId,
                OptionText = o.OptionText,
                NumericValue = o.NumericValue
            }).ToList(),
            CurrentQuestionNumber = current,
            TotalQuestions = total,
            Lang = language
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitAnswer(SurveyAnswerRequest request)
    {
        var (invitation, error) = await ValidateTokenAsync(request.Token);
        if (error != null)
            return error;

        var responseId = HttpContext.Session.GetInt32(ResponseKey(invitation!.InvitationId));
        if (responseId == null)
            return RedirectToAction("Consent", new { token = request.Token });

        var language = HttpContext.Session.GetString(LangKey(invitation.InvitationId)) ?? "tr";

        var hasAnswer = !request.IsSkipped &&
            (request.NumericValue.HasValue ||
             request.SelectedOptionId.HasValue ||
             !string.IsNullOrWhiteSpace(request.TextValue));

        if (hasAnswer)
        {
            await _surveyFlowService.SaveAnswerAsync(
                responseId.Value,
                request.QuestionId,
                request.SelectedOptionId,
                request.NumericValue,
                string.IsNullOrWhiteSpace(request.TextValue) ? null : request.TextValue.Trim());
        }

        var nextQuestionId = await _surveyFlowService.GetNextQuestionIdAsync(
            invitation.TemplateId,
            responseId.Value,
            request.QuestionId,
            hasAnswer ? request.NumericValue : null,
            hasAnswer ? request.SelectedOptionId : null);

        if (nextQuestionId.HasValue)
            return RedirectToAction("Question", new { token = request.Token, questionId = nextQuestionId.Value });

        var isAnonymous = HttpContext.Session.GetInt32(AnonymousKey(invitation.InvitationId)) == 1;
        await _surveyFlowService.FinalizeResponseAsync(invitation, responseId.Value, isAnonymous);

        HttpContext.Session.Remove(ResponseKey(invitation.InvitationId));
        HttpContext.Session.Remove(AnonymousKey(invitation.InvitationId));
        HttpContext.Session.Remove(LangKey(invitation.InvitationId));
        var kioskMode = HttpContext.Session.GetInt32(KioskKey(invitation.InvitationId)) == 1;
        HttpContext.Session.Remove(KioskKey(invitation.InvitationId));

        return RedirectToAction("Thanks", new { lang = language, kiosk = kioskMode });
    }

    [HttpGet]
    public IActionResult Thanks(string? lang, bool kiosk = false)
    {
        ViewBag.Lang = SurveyTexts.NormalizeLang(lang);
        ViewBag.KioskMode = kiosk;
        return View();
    }

    public IActionResult TokenError(string message)
    {
        ViewBag.ErrorMessage = message;
        return View();
    }

    public IActionResult ConsentRequired()
    {
        return View();
    }

    private static decimal DefaultMinFor(string questionType) => questionType switch
    {
        "NPS" => 0,
        _ => 1
    };

    private static decimal DefaultMaxFor(string questionType) => questionType switch
    {
        "NPS" => 10,
        "CSAT" => 10,
        "CES" => 7,
        "STAR" => 5,
        "SMILEY" => 5,
        _ => 5
    };

    private static string ResponseKey(int invitationId) => $"HGB_RESP_{invitationId}";
    private static string AnonymousKey(int invitationId) => $"HGB_ANON_{invitationId}";
    private static string LangKey(int invitationId) => $"HGB_LANG_{invitationId}";
    private static string KioskKey(int invitationId) => $"HGB_KIOSK_{invitationId}";

    private async Task<(SurveyInvitation? invitation, IActionResult? error)> ValidateTokenAsync(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            ViewBag.ErrorMessage = "Geçersiz davet bağlantısı";
            return (null, View("TokenError"));
        }

        var (isValid, invitation, errorMessage) = await _surveyFlowService.ValidateTokenAsync(token);
        if (!isValid)
        {
            ViewBag.ErrorMessage = errorMessage;
            return (null, View("TokenError"));
        }

        return (invitation, null);
    }
}

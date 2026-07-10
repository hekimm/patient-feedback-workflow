using System.Globalization;
using System.Text.Json;
using HastaGeriBildirim.Models.Entities;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Services.Integrations;

namespace HastaGeriBildirim.Services;

public class WhatsAppChatSurveyService
{
    private static readonly string[] ConsentWords = ["EVET", "ONAY", "KABUL", "ACCEPT", "YES"];

    private readonly DispatchRepository _dispatchRepository;
    private readonly SurveyRepository _surveyRepository;
    private readonly SurveyFlowService _surveyFlowService;
    private readonly IPiiCryptoService _piiCryptoService;
    private readonly IWhatsAppSurveyClient _whatsAppSurveyClient;
    private readonly ILogger<WhatsAppChatSurveyService> _logger;

    public WhatsAppChatSurveyService(
        DispatchRepository dispatchRepository,
        SurveyRepository surveyRepository,
        SurveyFlowService surveyFlowService,
        IPiiCryptoService piiCryptoService,
        IWhatsAppSurveyClient whatsAppSurveyClient,
        ILogger<WhatsAppChatSurveyService> logger)
    {
        _dispatchRepository = dispatchRepository;
        _surveyRepository = surveyRepository;
        _surveyFlowService = surveyFlowService;
        _piiCryptoService = piiCryptoService;
        _whatsAppSurveyClient = whatsAppSurveyClient;
        _logger = logger;
    }

    public async Task<int> ProcessWebhookAsync(JsonElement root, CancellationToken cancellationToken = default)
    {
        var processed = 0;

        foreach (var inbound in ExtractInboundMessages(root))
        {
            try
            {
                await ProcessMessageAsync(inbound.From, inbound.Text, cancellationToken);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WhatsApp inbound survey message could not be processed");
            }
        }

        return processed;
    }

    private async Task ProcessMessageAsync(string fromPhone, string text, CancellationToken cancellationToken)
    {
        var invitation = await _dispatchRepository.GetActiveWhatsAppInvitationByPhoneHashesAsync(
            BuildPhoneHashCandidates(fromPhone));

        if (invitation == null)
        {
            await _whatsAppSurveyClient.SendTextMessageAsync(
                fromPhone,
                "Aktif bir anket daveti bulunamadi veya davetin suresi doldu.",
                cancellationToken);
            return;
        }

        await _surveyFlowService.MarkOpenedAsync(invitation.InvitationId);

        var responseId = await _surveyRepository.GetResponseIdForInvitationAsync(invitation.InvitationId);
        if (!responseId.HasValue)
        {
            if (!IsConsentAccepted(text))
            {
                await SendConsentPromptAsync(fromPhone, cancellationToken);
                return;
            }

            responseId = await _surveyFlowService.SaveConsentAndStartResponseAsync(
                invitation,
                isConsentGiven: true,
                isAnonymous: false,
                ipAddress: null,
                languageCode: "tr");

            var firstQuestion = await _surveyFlowService.GetFirstQuestionAsync(invitation.TemplateId, "tr");
            await SendQuestionAsync(fromPhone, firstQuestion, cancellationToken);
            return;
        }

        var currentQuestion = await GetCurrentQuestionAsync(invitation.TemplateId, responseId.Value);
        if (currentQuestion == null)
        {
            await CompleteSurveyAsync(fromPhone, invitation, responseId.Value, cancellationToken);
            return;
        }

        var parsedAnswer = await ParseAnswerAsync(currentQuestion, text);
        if (!parsedAnswer.IsValid)
        {
            await _whatsAppSurveyClient.SendTextMessageAsync(
                fromPhone,
                parsedAnswer.ErrorMessage ?? "Yaniti anlayamadim. Lutfen tekrar deneyin.",
                cancellationToken);
            await SendQuestionAsync(fromPhone, currentQuestion, cancellationToken);
            return;
        }

        await _surveyFlowService.SaveAnswerAsync(
            responseId.Value,
            currentQuestion.QuestionId,
            parsedAnswer.SelectedOptionId,
            parsedAnswer.NumericValue,
            parsedAnswer.TextValue);

        var nextQuestionId = await _surveyFlowService.GetNextQuestionIdAsync(
            invitation.TemplateId,
            responseId.Value,
            currentQuestion.QuestionId,
            parsedAnswer.NumericValue,
            parsedAnswer.SelectedOptionId);

        if (!nextQuestionId.HasValue)
        {
            await CompleteSurveyAsync(fromPhone, invitation, responseId.Value, cancellationToken);
            return;
        }

        var nextQuestion = await _surveyFlowService.GetQuestionAsync(nextQuestionId.Value, "tr");
        await SendQuestionAsync(fromPhone, nextQuestion, cancellationToken);
    }

    private async Task<SurveyQuestion?> GetCurrentQuestionAsync(int templateVersionId, int responseId)
    {
        var answeredIds = await _surveyRepository.GetAnsweredQuestionIdsAsync(responseId);
        if (answeredIds.Count == 0)
            return await _surveyFlowService.GetFirstQuestionAsync(templateVersionId, "tr");

        var lastAnswer = await _surveyRepository.GetLastAnswerForResponseAsync(responseId);
        if (lastAnswer == null)
            return await _surveyFlowService.GetFirstQuestionAsync(templateVersionId, "tr");

        var nextQuestionId = await _surveyFlowService.GetNextQuestionIdAsync(
            templateVersionId,
            responseId,
            lastAnswer.QuestionId,
            lastAnswer.NumericValue,
            lastAnswer.SelectedOptionId);

        return nextQuestionId.HasValue
            ? await _surveyFlowService.GetQuestionAsync(nextQuestionId.Value, "tr")
            : null;
    }

    private async Task SendQuestionAsync(
        string toPhone,
        SurveyQuestion? question,
        CancellationToken cancellationToken)
    {
        if (question == null)
        {
            await _whatsAppSurveyClient.SendTextMessageAsync(
                toPhone,
                "Anket sorusu bulunamadi. Lutfen kurum ile iletisime gecin.",
                cancellationToken);
            return;
        }

        var message = question.QuestionText;
        var options = await _surveyFlowService.GetOptionsAsync(question.QuestionId, "tr");
        if (options.Count > 0)
        {
            message += "\n" + string.Join(
                "\n",
                options.Select(option => $"{option.OptionOrder}. {option.OptionText}"));
        }

        if (question.QuestionType is "NPS" or "CSAT" or "CES" or "SMILEY" or "STAR")
        {
            var min = question.MinValue ?? 1;
            var max = question.MaxValue ?? 5;
            message += $"\nLutfen {min:0}-{max:0} arasi bir sayi yazin.";
        }
        else if (options.Count > 0)
        {
            message += "\nLutfen secenek numarasini yazin.";
        }

        await _whatsAppSurveyClient.SendTextMessageAsync(toPhone, message, cancellationToken);
    }

    private async Task SendConsentPromptAsync(string toPhone, CancellationToken cancellationToken)
    {
        var consentText = await _surveyFlowService.GetConsentTextAsync("tr");
        var summary = string.IsNullOrWhiteSpace(consentText)
            ? "KVKK aydinlatma metnini kabul ediyorsaniz ankete WhatsApp uzerinden devam edebilirsiniz."
            : consentText.Length > 900 ? consentText[..900] + "..." : consentText;

        await _whatsAppSurveyClient.SendTextMessageAsync(
            toPhone,
            summary + "\n\nDevam etmek icin EVET yazin.",
            cancellationToken);
    }

    private async Task CompleteSurveyAsync(
        string toPhone,
        SurveyInvitation invitation,
        int responseId,
        CancellationToken cancellationToken)
    {
        await _surveyFlowService.FinalizeResponseAsync(invitation, responseId, isAnonymous: false);
        await _whatsAppSurveyClient.SendTextMessageAsync(
            toPhone,
            "Tesekkur ederiz. Anket yanitiniz kaydedildi.",
            cancellationToken);
    }

    private async Task<ParsedAnswer> ParseAnswerAsync(SurveyQuestion question, string text)
    {
        if (question.QuestionType == "FREE_TEXT")
        {
            if (string.IsNullOrWhiteSpace(text) && question.IsRequired)
                return ParsedAnswer.Invalid("Bu soru zorunlu. Lutfen yanitinizi yazin.");

            return ParsedAnswer.Valid(null, null, text.Trim());
        }

        var options = await _surveyFlowService.GetOptionsAsync(question.QuestionId, "tr");
        if (options.Count > 0)
        {
            var option = MatchOption(options, text);
            if (option == null)
                return ParsedAnswer.Invalid("Lutfen listedeki secenek numarasini veya metnini yazin.");

            return ParsedAnswer.Valid(option.OptionId, option.NumericValue, option.OptionText);
        }

        if (!TryParseDecimal(text, out var numericValue))
            return ParsedAnswer.Invalid("Lutfen sayisal bir yanit yazin.");

        var min = question.MinValue ?? (question.QuestionType == "NPS" ? 0 : 1);
        var max = question.MaxValue ?? (question.QuestionType == "NPS" ? 10 : 5);
        if (numericValue < min || numericValue > max)
            return ParsedAnswer.Invalid($"Lutfen {min:0}-{max:0} arasi bir sayi yazin.");

        return ParsedAnswer.Valid(null, numericValue, null);
    }

    private IEnumerable<string> BuildPhoneHashCandidates(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            phone,
            phone.Trim(),
            digits
        };

        if (!string.IsNullOrWhiteSpace(digits))
        {
            candidates.Add("+" + digits);
            if (digits.Length == 10)
            {
                candidates.Add("90" + digits);
                candidates.Add("+90" + digits);
            }
        }

        return candidates
            .Select(candidate => _piiCryptoService.HashForLookup(candidate))
            .Where(hash => !string.IsNullOrWhiteSpace(hash))!;
    }

    private static SurveyOption? MatchOption(List<SurveyOption> options, string text)
    {
        var normalized = text.Trim();
        if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            return options.FirstOrDefault(option => option.OptionOrder == index)
                ?? options.FirstOrDefault(option => option.NumericValue == index);
        }

        return options.FirstOrDefault(option =>
            string.Equals(option.OptionValue, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(option.OptionText, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        return decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out value)
            || decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsConsentAccepted(string text)
    {
        var normalized = text.Trim().ToUpperInvariant();
        return ConsentWords.Contains(normalized);
    }

    private static IEnumerable<InboundWhatsAppMessage> ExtractInboundMessages(JsonElement root)
    {
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value) ||
                    !value.TryGetProperty("messages", out var messages) ||
                    messages.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var message in messages.EnumerateArray())
                {
                    if (!message.TryGetProperty("from", out var fromElement))
                        continue;

                    var from = fromElement.GetString();
                    var text = ExtractMessageText(message);
                    if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(text))
                        yield return new InboundWhatsAppMessage(from, text);
                }
            }
        }
    }

    private static string? ExtractMessageText(JsonElement message)
    {
        if (message.TryGetProperty("text", out var text) &&
            text.TryGetProperty("body", out var body))
        {
            return body.GetString();
        }

        if (message.TryGetProperty("button", out var button) &&
            button.TryGetProperty("text", out var buttonText))
        {
            return buttonText.GetString();
        }

        if (message.TryGetProperty("interactive", out var interactive))
        {
            if (interactive.TryGetProperty("button_reply", out var buttonReply) &&
                buttonReply.TryGetProperty("title", out var buttonReplyTitle))
            {
                return buttonReplyTitle.GetString();
            }

            if (interactive.TryGetProperty("list_reply", out var listReply) &&
                listReply.TryGetProperty("title", out var listReplyTitle))
            {
                return listReplyTitle.GetString();
            }
        }

        return null;
    }

    private sealed record InboundWhatsAppMessage(string From, string Text);

    private sealed record ParsedAnswer(
        bool IsValid,
        int? SelectedOptionId,
        decimal? NumericValue,
        string? TextValue,
        string? ErrorMessage)
    {
        public static ParsedAnswer Valid(int? selectedOptionId, decimal? numericValue, string? textValue)
        {
            return new ParsedAnswer(true, selectedOptionId, numericValue, textValue, null);
        }

        public static ParsedAnswer Invalid(string errorMessage)
        {
            return new ParsedAnswer(false, null, null, null, errorMessage);
        }
    }
}

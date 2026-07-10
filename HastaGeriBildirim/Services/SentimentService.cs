using HastaGeriBildirim.Repositories;

namespace HastaGeriBildirim.Services;

public class SentimentService
{
    private const string ModelName = "HGB_TR_LEXICON";

    private static readonly Dictionary<string, string[]> ThemeKeywords = new()
    {
        ["WAIT_TIME"] = new[] { "bekle", "sıra", "sira", "kuyruk", "saat", "geç", "gec", "uzun süre", "uzun sure" },
        ["STAFF_ATTITUDE"] = new[] { "personel", "hemşire", "hemsire", "doktor", "kaba", "ilgisiz", "güler yüzlü", "guler yuzlu", "nazik", "kibar" },
        ["CLEANLINESS"] = new[] { "temiz", "kirli", "pis", "hijyen", "koku", "tuvalet" },
        ["COMMUNICATION"] = new[] { "bilgilendir", "açıkla", "acikla", "anlat", "iletişim", "iletisim", "bilgi ver" },
        ["TECHNICAL"] = new[] { "sistem", "cihaz", "randevu", "kayıt", "kayit", "uygulama", "arıza", "ariza" }
    };

    private readonly SentimentRepository _sentimentRepository;
    private readonly ISentimentAnalyzer _sentimentAnalyzer;

    public SentimentService(
        SentimentRepository sentimentRepository,
        ISentimentAnalyzer sentimentAnalyzer)
    {
        _sentimentRepository = sentimentRepository;
        _sentimentAnalyzer = sentimentAnalyzer;
    }

    public async Task AnalyzeResponseAsync(int responseId)
    {
        var freeTextAnswers = await _sentimentRepository.GetFreeTextAnswersAsync(responseId);
        if (freeTextAnswers.Count == 0)
            return;

        decimal totalScore = 0;
        var analyzedCount = 0;

        foreach (var answer in freeTextAnswers)
        {
            var (label, score) = Classify(answer.TextValue);
            totalScore += score;
            analyzedCount++;

            await _sentimentRepository.InsertSentimentResultAsync(
                responseId, answer.AnswerId, ModelName, label, score);

            await MatchThemesAsync(responseId, answer.TextValue);
        }

        var averageScore = Math.Round(totalScore / analyzedCount, 4);
        var overallLabel = LabelFromScore(averageScore);

        await _sentimentRepository.UpdateResponseSentimentAsync(responseId, overallLabel, averageScore);
    }

    public (string label, decimal score) Classify(string text)
    {
        var result = _sentimentAnalyzer.Analyze(text);
        return (result.Label, result.Score);
    }

    private static string LabelFromScore(decimal score)
    {
        if (score >= 0.2m) return "POSITIVE";
        if (score <= -0.2m) return "NEGATIVE";
        return "NEUTRAL";
    }

    private async Task MatchThemesAsync(int responseId, string text)
    {
        var lowered = text.ToLowerInvariant();
        var themes = await _sentimentRepository.GetActiveThemesAsync();

        foreach (var theme in themes)
        {
            if (!ThemeKeywords.TryGetValue(theme.ThemeCode, out var keywords))
                continue;

            var hits = keywords.Count(keyword => lowered.Contains(keyword));
            if (hits == 0)
                continue;

            var confidence = Math.Min(1m, Math.Round((decimal)hits / 3, 4));
            await _sentimentRepository.InsertThemeMatchAsync(responseId, theme.ThemeCategoryId, confidence);
        }
    }
}

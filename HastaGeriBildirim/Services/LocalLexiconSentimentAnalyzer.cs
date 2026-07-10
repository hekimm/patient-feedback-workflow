namespace HastaGeriBildirim.Services;

public class LocalLexiconSentimentAnalyzer : ISentimentAnalyzer
{
    private const decimal PositiveThreshold = 0.2m;
    private const decimal NegativeThreshold = -0.2m;

    private static readonly string[] PositiveWords =
    {
        "teşekkür", "tesekkur", "memnun", "güler yüzlü", "guler yuzlu", "ilgili", "harika",
        "mükemmel", "mukemmel", "iyi", "başarılı", "basarili", "hızlı", "hizli", "temiz",
        "kibar", "nazik", "profesyonel", "güzel", "guzel", "yardımcı", "yardimci", "titiz"
    };

    private static readonly string[] NegativeWords =
    {
        "kötü", "kotu", "berbat", "yavaş", "yavas", "bekledim", "bekletildim", "kaba",
        "ilgisiz", "kirli", "pis", "sorun", "şikayet", "sikayet", "geç", "gec", "uzun",
        "memnun değilim", "memnun degilim", "rezalet", "saygısız", "saygisiz", "kalabalık",
        "kalabalik", "gürültü", "gurultu", "iptal", "hata", "yanlış", "yanlis"
    };

    public SentimentAnalysisResult Analyze(string text)
    {
        var lowered = text.ToLowerInvariant();

        var positiveHits = PositiveWords.Count(word => lowered.Contains(word));
        var negativeHits = NegativeWords.Count(word => lowered.Contains(word));

        if (positiveHits + negativeHits == 0)
            return new SentimentAnalysisResult("NEUTRAL", 0);

        var score = Math.Round(
            (decimal)(positiveHits - negativeHits) / (positiveHits + negativeHits), 4);

        return new SentimentAnalysisResult(LabelFromScore(score), score);
    }

    private static string LabelFromScore(decimal score)
    {
        if (score >= PositiveThreshold) return "POSITIVE";
        if (score <= NegativeThreshold) return "NEGATIVE";
        return "NEUTRAL";
    }
}


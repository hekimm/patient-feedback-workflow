namespace HastaGeriBildirim.Services;

public sealed record SentimentAnalysisResult(string Label, decimal Score);

public interface ISentimentAnalyzer
{
    SentimentAnalysisResult Analyze(string text);
}


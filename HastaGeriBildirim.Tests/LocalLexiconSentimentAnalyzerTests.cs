using HastaGeriBildirim.Services;

namespace HastaGeriBildirim.Tests;

public class LocalLexiconSentimentAnalyzerTests
{
    [Theory]
    [InlineData("Personel çok ilgili ve güler yüzlüydü.", "POSITIVE")]
    [InlineData("Çok bekledim ve hizmet kötüydü.", "NEGATIVE")]
    [InlineData("Muayene tamamlandı.", "NEUTRAL")]
    public void Analyze_ClassifiesTurkishFeedback(string text, string expectedLabel)
    {
        var analyzer = new LocalLexiconSentimentAnalyzer();

        var result = analyzer.Analyze(text);

        Assert.Equal(expectedLabel, result.Label);
    }
}


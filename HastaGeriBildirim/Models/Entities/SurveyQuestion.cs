namespace HastaGeriBildirim.Models.Entities;

public class SurveyQuestion
{
    public int QuestionId { get; set; }
    public int VersionId { get; set; }
    public string QuestionCode { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string? MetricType { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? HelpText { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
    public bool IsInitialQuestion { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

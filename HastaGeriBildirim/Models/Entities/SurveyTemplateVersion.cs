namespace HastaGeriBildirim.Models.Entities;

public class SurveyTemplateVersion
{
    public int VersionId { get; set; }
    public int TemplateId { get; set; }
    public int VersionNo { get; set; }
    public string? VersionLabel { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int QuestionCount { get; set; }
}

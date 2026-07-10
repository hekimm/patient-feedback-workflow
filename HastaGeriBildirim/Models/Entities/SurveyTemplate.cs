namespace HastaGeriBildirim.Models.Entities;

public class SurveyTemplate
{
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

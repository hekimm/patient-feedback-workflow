namespace HastaGeriBildirim.Models.Entities;

public class SurveyAnswer
{
    public int AnswerId { get; set; }
    public int ResponseId { get; set; }
    public int QuestionId { get; set; }
    public int? SelectedOptionId { get; set; }
    public decimal? NumericValue { get; set; }
    public string? TextValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

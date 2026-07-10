namespace HastaGeriBildirim.Models.Entities;

public class SurveyOption
{
    public int OptionId { get; set; }
    public int QuestionId { get; set; }
    public int OptionOrder { get; set; }
    public string OptionValue { get; set; } = string.Empty;
    public string OptionText { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }
}

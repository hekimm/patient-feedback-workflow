namespace HastaGeriBildirim.Models.Entities;

public class BranchingRule
{
    public int BranchingRuleId { get; set; }
    public int SourceQuestionId { get; set; }
    public string? SourceQuestionCode { get; set; }
    public string OperatorCode { get; set; } = string.Empty;
    public decimal? CompareNumericValue { get; set; }
    public int? CompareOptionId { get; set; }
    public int TargetQuestionId { get; set; }
    public string? TargetQuestionCode { get; set; }
    public int RuleOrder { get; set; }
    public bool IsActive { get; set; }
}

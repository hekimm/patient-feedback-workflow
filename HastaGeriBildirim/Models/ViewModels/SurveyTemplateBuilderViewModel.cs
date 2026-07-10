using HastaGeriBildirim.Models.Entities;

namespace HastaGeriBildirim.Models.ViewModels;

public class SurveyTemplateBuilderViewModel
{
    public SurveyTemplate Template { get; set; } = new();
    public List<SurveyTemplateVersion> Versions { get; set; } = new();
    public SurveyTemplateVersion? SelectedVersion { get; set; }
    public List<SurveyQuestion> Questions { get; set; } = new();
    public Dictionary<int, List<SurveyOption>> OptionsByQuestion { get; set; } = new();
    public List<BranchingRule> BranchingRules { get; set; } = new();
}


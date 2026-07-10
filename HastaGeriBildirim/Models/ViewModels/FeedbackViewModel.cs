namespace HastaGeriBildirim.Models.ViewModels;

public class FeedbackListViewModel
{
    public List<FeedbackSummary> Feedbacks { get; set; } = new();
    public FeedbackFilter Filter { get; set; } = new();
}

public class FeedbackSummary
{
    public int ResponseId { get; set; }
    public DateTime CompletedAt { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public decimal? NpsScore { get; set; }
    public decimal? CsatScore { get; set; }
    public bool IsNegative { get; set; }
    public string? SentimentLabel { get; set; }
}

public class FeedbackFilter
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? DepartmentId { get; set; }
    public int? DoctorId { get; set; }
    public bool? IsNegative { get; set; }
}

public class FeedbackDetailViewModel
{
    public int ResponseId { get; set; }
    public DateTime CompletedAt { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public decimal? NpsScore { get; set; }
    public decimal? CsatScore { get; set; }
    public bool IsNegative { get; set; }
    public string? SentimentLabel { get; set; }
    public decimal? SentimentScore { get; set; }
    public List<AnswerDetail> Answers { get; set; } = new();
    public List<string> Themes { get; set; } = new();
    public int? RelatedCaseId { get; set; }
}

public class AnswerDetail
{
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }
    public string? TextValue { get; set; }
    public string? OptionText { get; set; }
}

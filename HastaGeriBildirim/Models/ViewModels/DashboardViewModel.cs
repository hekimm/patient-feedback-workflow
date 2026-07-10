namespace HastaGeriBildirim.Models.ViewModels;

public class DashboardViewModel
{
    public int TotalResponses { get; set; }
    public decimal AverageOverallScore { get; set; }
    public decimal AverageCsat { get; set; }
    public decimal NpsScore { get; set; }
    public decimal NegativePercentage { get; set; }
    public decimal? AverageCes { get; set; }
    public int InvitationsSent { get; set; }
    public decimal ResponseRate { get; set; }
    public string TrendPeriod { get; set; } = "DAY";
    public List<DepartmentSummary> DepartmentSummaries { get; set; } = new();
    public List<DoctorSummary> DoctorSummaries { get; set; } = new();
    public List<TrendDataPoint> TrendData { get; set; } = new();
    public List<KpiComparison> KpiComparisons { get; set; } = new();
    public List<SentimentSlice> SentimentDistribution { get; set; } = new();
}

public class DepartmentSummary
{
    public string DepartmentName { get; set; } = string.Empty;
    public int ResponseCount { get; set; }
    public decimal AverageScore { get; set; }
    public int NegativeCount { get; set; }
}

public class DoctorSummary
{
    public string DoctorName { get; set; } = string.Empty;
    public int ResponseCount { get; set; }
    public decimal AverageScore { get; set; }
}

public class TrendDataPoint
{
    public DateTime Date { get; set; }
    public decimal AverageScore { get; set; }
    public int ResponseCount { get; set; }
    public decimal? NpsValue { get; set; }
}

public class KpiComparison
{
    public string KpiCode { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal? Deviation => ActualValue.HasValue ? ActualValue.Value - TargetValue : null;
}

public class SentimentSlice
{
    public string Label { get; set; } = string.Empty;
    public int ResponseCount { get; set; }
}

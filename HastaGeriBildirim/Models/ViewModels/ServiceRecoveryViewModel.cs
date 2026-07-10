namespace HastaGeriBildirim.Models.ViewModels;

public class ServiceRecoveryListViewModel
{
    public List<ServiceRecoverySummary> Cases { get; set; } = new();
    public int OpenCount { get; set; }
    public int SlaWarningCount { get; set; }
    public int SlaBreachCount { get; set; }
}

public class ServiceRecoverySummary
{
    public int CaseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public int HoursRemaining { get; set; }
    public bool IsSlaWarning { get; set; }
    public bool IsSlaBreached { get; set; }
}

public class ServiceRecoveryDetailViewModel
{
    public int CaseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public string? PatientComment { get; set; }
    public string? SentimentLabel { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosureNote { get; set; }
    public List<RecoveryActionItem> Actions { get; set; } = new();
    public bool IsSlaBreached { get; set; }
}

public class RecoveryActionItem
{
    public string ActionType { get; set; } = string.Empty;
    public string PerformedByName { get; set; } = string.Empty;
    public string? ActionNote { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AddActionViewModel
{
    public int CaseId { get; set; }
    public string ActionNote { get; set; } = string.Empty;
}

public class CloseCaseViewModel
{
    public int CaseId { get; set; }
    public string ClosureNote { get; set; } = string.Empty;
}
